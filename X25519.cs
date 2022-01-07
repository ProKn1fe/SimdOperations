﻿//
// Original code taked from https://github.com/HirbodBehnam/X25519-CSharp under BSD 3-Clause "New" or "Revised" License

using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace SimdOperations
{
    /// <summary>
    /// A friendly front-end for you developers to generate keys and calculate agreements
    /// </summary>
    public static class X25519KeyAgreement
    {
#if !NET6_0_OR_GREATER
        private static readonly RNGCryptoServiceProvider RNGCryptoServiceProvider = new();
#endif

        /// <summary>
        /// Uses RNG Random to generate a key pair
        /// </summary>
        /// <returns>A random key pair</returns>
        public static X25519KeyPair GenerateKeyPair()
        {
            // at first generate the private key
            var key = new X25519KeyPair
            {
                PrivateKey = new byte[32]
            };
#if NET6_0_OR_GREATER
            key.PrivateKey = RandomNumberGenerator.GetBytes(32);
#else
            RNGCryptoServiceProvider.GetBytes(key.PrivateKey);
#endif
            // as defined in https://cr.yp.to/ecdh.html do these operation to finalize the private key
            key.PrivateKey[0] &= 248;
            key.PrivateKey[31] &= 127;
            key.PrivateKey[31] |= 64;
            // compute the public key
            key.PublicKey = X25519.ScalarMultiplication(key.PrivateKey, X25519.Basepoint);
            return key;
        }

        /// <summary>
        /// Generates a full key pair (Public and private) given the private key
        /// </summary>
        /// <param name="privateKey">The private key to generate public key from</param>
        /// <returns>A full key pair</returns>
        public static X25519KeyPair GenerateKeyFromPrivateKey(byte[] privateKey)
        {
            var key = new X25519KeyPair
            {
                PrivateKey = privateKey
            };
            key.PublicKey = X25519.ScalarMultiplication(key.PrivateKey, X25519.Basepoint);
            return key;
        }
        /// <summary>
        /// Generate a shared secret with the other users public key and your private key
        /// </summary>
        /// <param name="myPrivateKey">Your private key</param>
        /// <param name="otherPublicKey">The public key of the other user</param>
        /// <returns>A shared secret</returns>
        public static byte[] Agreement(byte[] myPrivateKey, byte[] otherPublicKey)
        {
            return X25519.ScalarMultiplication(myPrivateKey, otherPublicKey);
        }
    }

    /// <summary>
    /// Private an public keys for Curve25519
    /// </summary>
    public class X25519KeyPair
    {
        /// <summary>
        /// The private key that you have to keep secret
        /// </summary>
        public byte[] PrivateKey;
        /// <summary>
        /// The public key that you have to share with users
        /// </summary>
        public byte[] PublicKey;
    }

    public static class X25519
    {
#if NETCOREAPP || NETSTANDARD
        /// <summary>
        /// The base point that is x = 9
        /// </summary>
        public static readonly byte[] Basepoint = { 9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        /// <summary>
        /// An inner function to calculate scalar * point
        /// </summary>
        /// <param name="input"></param>
        /// <param name="baseIn"></param>
        private static byte[] ScalarMult(byte[] input, byte[] baseIn)
        {
            Span<byte> e = stackalloc byte[32];
            Span<long> carry = stackalloc long[10];
            Span<long> h = stackalloc long[10];

            input.CopyTo(e); //copy(e[:], input[:])
            e[0] &= 248;
            e[31] &= 127;
            e[31] |= 64;

            Span<int> x1 = stackalloc int[10];
            Span<int> x2 = stackalloc int[10];
            Span<int> z2 = stackalloc int[10];
            Span<int> x3 = stackalloc int[10];
            Span<int> z3 = stackalloc int[10];
            Span<int> tmp0 = stackalloc int[10];
            Span<int> tmp1 = stackalloc int[10];
            // feFromBytes(&x1, base)
            Create(baseIn, carry, h, x1); //SECOND NUMBER
                                          //feOne(&x2)
            One(x2);
            //feCopy(&x3, &x1)
            Copy(x1, x3);
            //feOne(&z3)
            One(z3);

            int swap = 0;
            for (int pos = 254; pos >= 0; pos--)
            {
                byte b = Convert.ToByte(e[pos / 8] >> (pos & 7));
                b &= 1;
                swap ^= b;
                CSwap(x2, x3, swap);
                CSwap(z2, z3, swap);
                swap = b;

                Minus(x3, z3, tmp0); //feSub(&tmp0, &x3, &z3)
                Minus(x2, z2, tmp1); //feSub(&tmp1, &x2, &z2)
                Plus(x2, z2, x2); //feAdd(&x2, &x2, &z2)
                Plus(x3, z3, z2); //feAdd(&z2, &x3, &z3)
                Multiply(tmp0, x2, carry, h, z3);
                Multiply(z2, tmp1, carry, h, z2);
                Square(tmp1, carry, h, tmp0);
                Square(x2, carry, h, tmp1);
                Plus(z3, z2, x3); //feAdd(&x3, &z3, &z2)
                Minus(z3, z2, z2); //feSub(&z2, &z3, &z2)
                Multiply(tmp1, tmp0, carry, h, x2);
                Minus(tmp1, tmp0, tmp1); //feSub(&tmp1, &tmp1, &tmp0)
                Square(z2, carry, h, z2);
                Mul121666(tmp1, carry, h, z3);
                Square(x3, carry, h, x3);
                Plus(tmp0, z3, tmp0); //feAdd(&tmp0, &tmp0, &z3)
                Multiply(x1, z2, carry, h, z3);
                Multiply(tmp1, tmp0, carry, h, z2);
            }

            CSwap(x2, x3, swap);
            CSwap(z2, z3, swap);

            Invert(z2, carry, h, z2);
            Multiply(x2, z2, carry, h, x2);
            return ToBytes(x2);
        }
        /// <summary>
        /// <para>
        /// X25519 returns the result of the scalar multiplication (scalar * point),
        /// according to RFC 7748, Section 5. scalar, point and the return value are
        /// slices of 32 bytes.
        /// </para>
        /// <para>
        /// If point is Basepoint (but not if it's a different slice with the same
        /// contents) a precomputed implementation might be used for performance.
        /// </para>
        /// </summary>
        public static byte[] ScalarMultiplication(byte[] scalar, byte[] point)
        {
            if (scalar.Length != 32)
                throw new ArgumentException("Length of scalar must be 32", nameof(scalar));
            if (point.Length != 32)
                throw new ArgumentException("Length of point must be 32", nameof(point));
            byte[] zero = new byte[32];
            byte[] result = ScalarMult(scalar, point);
            // here I tried to make something like subtle.ConstantTimeCompare
            if (result.Length != zero.Length)
                throw new Exception("This should not happen. Because result is always 32 bytes");

            byte v = 0;
            for (int i = 0; i < result.Length; i++)
                v = (byte)(v | (zero[i] ^ result[i]));
            if ((int)(((uint)(v ^ 0) - 1) >> 31) == 1) // no clue if these functions are ok or not
                throw new Exception("bad input point: low order point");
            return result;
        }

        private static void Minus(Span<int> a, Span<int> b, Span<int> output)
        {
            for (var i = 0; i < 10; ++i)
                output[i] = a[i] - b[i];
        }

        private static void Plus(Span<int> a, Span<int> b, Span<int> output)
        {
            for (var i = 0; i < 10; ++i)
                output[i] = a[i] + b[i];
        }

        /// <summary>
        /// Generate field element from byte array
        /// </summary>
        /// <remarks>
        /// This is identical to feFromBytes in Golang
        /// </remarks>
        /// <param name="src"></param>
        private static void Create(Span<byte> src, Span<long> carry, Span<long> h, Span<int> output)
        {
            h[0] = Load4(src, 0);
            h[1] = Load3(src, 4) << 6;
            h[2] = Load3(src, 7) << 5;
            h[3] = Load3(src, 10) << 3;
            h[4] = Load3(src, 13) << 2;
            h[5] = Load4(src, 16);
            h[6] = Load3(src, 20) << 7;
            h[7] = Load3(src, 23) << 5;
            h[8] = Load3(src, 26) << 4;
            h[9] = (Load3(src, 29) & 0x7fffff) << 2;

            carry[9] = (h[9] + (1 << 24)) >> 25;
            h[0] += carry[9] * 19;
            h[9] -= carry[9] << 25;
            carry[1] = (h[1] + (1 << 24)) >> 25;
            h[2] += carry[1];
            h[1] -= carry[1] << 25;
            carry[3] = (h[3] + (1 << 24)) >> 25;
            h[4] += carry[3];
            h[3] -= carry[3] << 25;
            carry[5] = (h[5] + (1 << 24)) >> 25;
            h[6] += carry[5];
            h[5] -= carry[5] << 25;
            carry[7] = (h[7] + (1 << 24)) >> 25;
            h[8] += carry[7];
            h[7] -= carry[7] << 25;

            carry[0] = (h[0] + (1 << 25)) >> 26;
            h[1] += carry[0];
            h[0] -= carry[0] << 26;
            carry[2] = (h[2] + (1 << 25)) >> 26;
            h[3] += carry[2];
            h[2] -= carry[2] << 26;
            carry[4] = (h[4] + (1 << 25)) >> 26;
            h[5] += carry[4];
            h[4] -= carry[4] << 26;
            carry[6] = (h[6] + (1 << 25)) >> 26;
            h[7] += carry[6];
            h[6] -= carry[6] << 26;
            carry[8] = (h[8] + (1 << 25)) >> 26;
            h[9] += carry[8];
            h[8] -= carry[8] << 26;

            output[0] = (int)h[0];
            output[1] = (int)h[1];
            output[2] = (int)h[2];
            output[3] = (int)h[3];
            output[4] = (int)h[4];
            output[5] = (int)h[5];
            output[6] = (int)h[6];
            output[7] = (int)h[7];
            output[8] = (int)h[8];
            output[9] = (int)h[9];
        }

        /// <summary>
        /// Converts to a 32 byte array
        /// </summary>
        /// <remarks>
        /// <para>
        /// Identical to feToBytes in Golang
        ///   feToBytes marshals h to s.
        /// Preconditions:
        /// |h| bounded by 1.1*2^25,1.1*2^24,1.1*2^25,1.1*2^24,etc.
        /// </para>
        /// <para>
        /// Write p=2^255-19; q=floor(h/p).
        /// Basic claim: q = floor(2^(-255)(h + 19 2^(-25)h9 + 2^(-1))).
        /// </para>
        /// <para>
        /// Proof:
        /// Have |h|&lt;=p so |q|&lt;=1 so |19^2 2^(-255) q|&lt;1/4.
        /// Also have |h-2^230 h9|&lt;2^230 so |19 2^(-255)(h-2^230 h9)|&lt;1/4.
        /// </para>
        /// <para>
        /// Write y=2^(-1)-19^2 2^(-255)q-19 2^(-255)(h-2^230 h9).
        /// Then 0&lt;y&lt;1.
        /// </para>
        /// <para>
        /// Write r=h-pq.
        /// Have 0&lt;=r&lt;=p-1=2^255-20.
        /// Thus 0&lt;=r+19(2^-255)r&lt;r+19(2^-255)2^255&lt;=2^255-1.
        /// </para>
        /// <para>
        /// Write x=r+19(2^-255)r+y.
        /// Then 0&lt;x&lt;2^255 so floor(2^(-255)x) = 0 so floor(q+2^(-255)x) = q.
        /// </para>
        /// <para>
        /// Have q+2^(-255)x = 2^(-255)(h + 19 2^(-25) h9 + 2^(-1))
        /// so floor(2^(-255)(h + 19 2^(-25) h9 + 2^(-1))) = q.
        /// </para>
        /// </remarks>
        private static byte[] ToBytes(Span<int> input)
        {
            var carry = new int[10];

            int q = ((19 * input[9]) + (1 << 24)) >> 25;
            q = (input[0] + q) >> 26;
            q = (input[1] + q) >> 25;
            q = (input[2] + q) >> 26;
            q = (input[3] + q) >> 25;
            q = (input[4] + q) >> 26;
            q = (input[5] + q) >> 25;
            q = (input[6] + q) >> 26;
            q = (input[7] + q) >> 25;
            q = (input[8] + q) >> 26;
            q = (input[9] + q) >> 25;

            // Goal: Output h-(2^255-19)q, which is between 0 and 2^255-20.
            input[0] += 19 * q;
            // Goal: Output h-2^255 q, which is between 0 and 2^255-20.

            carry[0] = input[0] >> 26;
            input[1] += carry[0];
            input[0] -= carry[0] << 26;
            carry[1] = input[1] >> 25;
            input[2] += carry[1];
            input[1] -= carry[1] << 25;
            carry[2] = input[2] >> 26;
            input[3] += carry[2];
            input[2] -= carry[2] << 26;
            carry[3] = input[3] >> 25;
            input[4] += carry[3];
            input[3] -= carry[3] << 25;
            carry[4] = input[4] >> 26;
            input[5] += carry[4];
            input[4] -= carry[4] << 26;
            carry[5] = input[5] >> 25;
            input[6] += carry[5];
            input[5] -= carry[5] << 25;
            carry[6] = input[6] >> 26;
            input[7] += carry[6];
            input[6] -= carry[6] << 26;
            carry[7] = input[7] >> 25;
            input[8] += carry[7];
            input[7] -= carry[7] << 25;
            carry[8] = input[8] >> 26;
            input[9] += carry[8];
            input[8] -= carry[8] << 26;
            carry[9] = input[9] >> 25;
            input[9] -= carry[9] << 25;
            // h10 = carry9

            // Goal: Output h[0]+...+2^255 h10-2^255 q, which is between 0 and 2^255-20.
            // Have h[0]+...+2^230 h[9] between 0 and 2^255-1;
            // evidently 2^255 h10-2^255 q = 0.
            // Goal: Output h[0]+...+2^230 h[9].

            var s = new byte[32];
            s[0] = (byte)(input[0] >> 0);
            s[1] = (byte)(input[0] >> 8);
            s[2] = (byte)(input[0] >> 16);
            s[3] = (byte)((input[0] >> 24) | (input[1] << 2));
            s[4] = (byte)(input[1] >> 6);
            s[5] = (byte)(input[1] >> 14);
            s[6] = (byte)((input[1] >> 22) | (input[2] << 3));
            s[7] = (byte)(input[2] >> 5);
            s[8] = (byte)(input[2] >> 13);
            s[9] = (byte)((input[2] >> 21) | (input[3] << 5));
            s[10] = (byte)(input[3] >> 3);
            s[11] = (byte)(input[3] >> 11);
            s[12] = (byte)((input[3] >> 19) | (input[4] << 6));
            s[13] = (byte)(input[4] >> 2);
            s[14] = (byte)(input[4] >> 10);
            s[15] = (byte)(input[4] >> 18);
            s[16] = (byte)(input[5] >> 0);
            s[17] = (byte)(input[5] >> 8);
            s[18] = (byte)(input[5] >> 16);
            s[19] = (byte)((input[5] >> 24) | (input[6] << 1));
            s[20] = (byte)(input[6] >> 7);
            s[21] = (byte)(input[6] >> 15);
            s[22] = (byte)((input[6] >> 23) | (input[7] << 3));
            s[23] = (byte)(input[7] >> 5);
            s[24] = (byte)(input[7] >> 13);
            s[25] = (byte)((input[7] >> 21) | (input[8] << 4));
            s[26] = (byte)(input[8] >> 4);
            s[27] = (byte)(input[8] >> 12);
            s[28] = (byte)((input[8] >> 20) | (input[9] << 6));
            s[29] = (byte)(input[9] >> 2);
            s[30] = (byte)(input[9] >> 10);
            s[31] = (byte)(input[9] >> 18);
            return s;
        }

        /// <summary>
        /// Calculates this * g
        /// </summary>
        /// <param name="g"></param>
        /// <remarks>
        /// <para>Can overlap h with f or g.</para>
        /// <para>
        /// Preconditions:
        /// |f| bounded by 1.1*2^26,1.1*2^25,1.1*2^26,1.1*2^25,etc.
        /// |g| bounded by 1.1*2^26,1.1*2^25,1.1*2^26,1.1*2^25,etc.
        /// </para>
        /// <para>
        /// Postconditions:
        /// |h| bounded by 1.1*2^25,1.1*2^24,1.1*2^25,1.1*2^24,etc.
        /// </para>
        /// <para>Notes on implementation strategy:</para>
        /// <para>
        /// Using schoolbook multiplication.
        /// Karatsuba would save a little in some cost models.
        /// </para>
        /// <para>
        /// Most multiplications by 2 and 19 are 32-bit precomputations;
        /// cheaper than 64-bit postcomputations.
        /// </para>
        /// <para>
        /// There is one remaining multiplication by 19 in the carry chain;
        /// one *19 precomputation can be merged into this,
        /// but the resulting data flow is considerably less clean.
        /// </para>
        /// <para>
        /// There are 12 carries below.
        /// 10 of them are 2-way parallelizable and vectorizable.
        /// Can get away with 11 carries, but then data flow is much deeper.
        /// </para>
        /// <para>With tighter constraints on inputs can squeeze carries into int32.</para>
        /// </remarks>
        private static void Multiply(Span<int> input, Span<int> g, Span<long> carry, Span<long> h, Span<int> output)
        {
            int f0 = input[0];
            int f1 = input[1];
            int f2 = input[2];
            int f3 = input[3];
            int f4 = input[4];
            int f5 = input[5];
            int f6 = input[6];
            int f7 = input[7];
            int f8 = input[8];
            int f9 = input[9];
            int g0 = g[0];
            int g1 = g[1];
            int g2 = g[2];
            int g3 = g[3];
            int g4 = g[4];
            int g5 = g[5];
            int g6 = g[6];
            int g7 = g[7];
            int g8 = g[8];
            int g9 = g[9];
            int g1_19 = 19 * g1; // 1.4*2^29
            int g2_19 = 19 * g2; // 1.4*2^30; still ok
            int g3_19 = 19 * g3;
            int g4_19 = 19 * g4;
            int g5_19 = 19 * g5;
            int g6_19 = 19 * g6;
            int g7_19 = 19 * g7;
            int g8_19 = 19 * g8;
            int g9_19 = 19 * g9;
            int f1_2 = 2 * f1;
            int f3_2 = 2 * f3;
            int f5_2 = 2 * f5;
            int f7_2 = 2 * f7;
            int f9_2 = 2 * f9;
            long f0g0 = (long)f0 * g0;
            long f0g1 = (long)f0 * (long)g1;
            long f0g2 = (long)f0 * (long)g2;
            long f0g3 = (long)f0 * (long)g3;
            long f0g4 = (long)f0 * (long)g4;
            long f0g5 = (long)f0 * (long)g5;
            long f0g6 = (long)f0 * (long)g6;
            long f0g7 = (long)f0 * (long)g7;
            long f0g8 = (long)f0 * (long)g8;
            long f0g9 = (long)f0 * (long)g9;
            long f1g0 = (long)f1 * (long)g0;
            long f1g1_2 = (long)f1_2 * (long)g1;
            long f1g2 = (long)f1 * (long)g2;
            long f1g3_2 = (long)f1_2 * (long)g3;
            long f1g4 = (long)f1 * (long)g4;
            long f1g5_2 = (long)f1_2 * (long)g5;
            long f1g6 = (long)f1 * (long)g6;
            long f1g7_2 = (long)f1_2 * (long)g7;
            long f1g8 = (long)f1 * (long)g8;
            long f1g9_38 = (long)f1_2 * (long)g9_19;
            long f2g0 = (long)f2 * (long)g0;
            long f2g1 = (long)f2 * (long)g1;
            long f2g2 = (long)f2 * (long)g2;
            long f2g3 = (long)f2 * (long)g3;
            long f2g4 = (long)f2 * (long)g4;
            long f2g5 = (long)f2 * (long)g5;
            long f2g6 = (long)f2 * (long)g6;
            long f2g7 = (long)f2 * (long)g7;
            long f2g8_19 = (long)f2 * (long)g8_19;
            long f2g9_19 = (long)f2 * (long)g9_19;
            long f3g0 = (long)f3 * (long)g0;
            long f3g1_2 = (long)f3_2 * (long)g1;
            long f3g2 = (long)f3 * (long)g2;
            long f3g3_2 = (long)f3_2 * (long)g3;
            long f3g4 = (long)f3 * (long)g4;
            long f3g5_2 = (long)f3_2 * (long)g5;
            long f3g6 = (long)f3 * (long)g6;
            long f3g7_38 = (long)f3_2 * (long)g7_19;
            long f3g8_19 = (long)f3 * (long)g8_19;
            long f3g9_38 = (long)f3_2 * (long)g9_19;
            long f4g0 = (long)f4 * (long)g0;
            long f4g1 = (long)f4 * (long)g1;
            long f4g2 = (long)f4 * (long)g2;
            long f4g3 = (long)f4 * (long)g3;
            long f4g4 = (long)f4 * (long)g4;
            long f4g5 = (long)f4 * (long)g5;
            long f4g6_19 = (long)f4 * (long)g6_19;
            long f4g7_19 = (long)f4 * (long)g7_19;
            long f4g8_19 = (long)f4 * (long)g8_19;
            long f4g9_19 = (long)f4 * (long)g9_19;
            long f5g0 = (long)f5 * (long)g0;
            long f5g1_2 = (long)f5_2 * (long)g1;
            long f5g2 = (long)f5 * (long)g2;
            long f5g3_2 = (long)f5_2 * (long)g3;
            long f5g4 = (long)f5 * (long)g4;
            long f5g5_38 = (long)f5_2 * (long)g5_19;
            long f5g6_19 = (long)f5 * (long)g6_19;
            long f5g7_38 = (long)f5_2 * (long)g7_19;
            long f5g8_19 = (long)f5 * (long)g8_19;
            long f5g9_38 = (long)f5_2 * (long)g9_19;
            long f6g0 = (long)f6 * (long)g0;
            long f6g1 = (long)f6 * (long)g1;
            long f6g2 = (long)f6 * (long)g2;
            long f6g3 = (long)f6 * (long)g3;
            long f6g4_19 = (long)f6 * (long)g4_19;
            long f6g5_19 = (long)f6 * (long)g5_19;
            long f6g6_19 = (long)f6 * (long)g6_19;
            long f6g7_19 = (long)f6 * (long)g7_19;
            long f6g8_19 = (long)f6 * (long)g8_19;
            long f6g9_19 = (long)f6 * (long)g9_19;
            long f7g0 = (long)f7 * (long)g0;
            long f7g1_2 = (long)f7_2 * (long)g1;
            long f7g2 = (long)f7 * (long)g2;
            long f7g3_38 = (long)f7_2 * (long)g3_19;
            long f7g4_19 = (long)f7 * (long)g4_19;
            long f7g5_38 = (long)f7_2 * (long)g5_19;
            long f7g6_19 = (long)f7 * (long)g6_19;
            long f7g7_38 = (long)f7_2 * (long)g7_19;
            long f7g8_19 = (long)f7 * (long)g8_19;
            long f7g9_38 = (long)f7_2 * (long)g9_19;
            long f8g0 = (long)f8 * (long)g0;
            long f8g1 = (long)f8 * (long)g1;
            long f8g2_19 = (long)f8 * (long)g2_19;
            long f8g3_19 = (long)f8 * (long)g3_19;
            long f8g4_19 = (long)f8 * (long)g4_19;
            long f8g5_19 = (long)f8 * (long)g5_19;
            long f8g6_19 = (long)f8 * (long)g6_19;
            long f8g7_19 = (long)f8 * (long)g7_19;
            long f8g8_19 = (long)f8 * (long)g8_19;
            long f8g9_19 = (long)f8 * (long)g9_19;
            long f9g0 = (long)f9 * (long)g0;
            long f9g1_38 = (long)f9_2 * (long)g1_19;
            long f9g2_19 = (long)f9 * (long)g2_19;
            long f9g3_38 = (long)f9_2 * (long)g3_19;
            long f9g4_19 = (long)f9 * (long)g4_19;
            long f9g5_38 = (long)f9_2 * (long)g5_19;
            long f9g6_19 = (long)f9 * (long)g6_19;
            long f9g7_38 = (long)f9_2 * (long)g7_19;
            long f9g8_19 = (long)f9 * (long)g8_19;
            long f9g9_38 = (long)f9_2 * (long)g9_19;
            h[0] = f0g0 + f1g9_38 + f2g8_19 + f3g7_38 + f4g6_19 + f5g5_38 + f6g4_19 + f7g3_38 + f8g2_19 + f9g1_38;
            h[1] = f0g1 + f1g0 + f2g9_19 + f3g8_19 + f4g7_19 + f5g6_19 + f6g5_19 + f7g4_19 + f8g3_19 + f9g2_19;
            h[2] = f0g2 + f1g1_2 + f2g0 + f3g9_38 + f4g8_19 + f5g7_38 + f6g6_19 + f7g5_38 + f8g4_19 + f9g3_38;
            h[3] = f0g3 + f1g2 + f2g1 + f3g0 + f4g9_19 + f5g8_19 + f6g7_19 + f7g6_19 + f8g5_19 + f9g4_19;
            h[4] = f0g4 + f1g3_2 + f2g2 + f3g1_2 + f4g0 + f5g9_38 + f6g8_19 + f7g7_38 + f8g6_19 + f9g5_38;
            h[5] = f0g5 + f1g4 + f2g3 + f3g2 + f4g1 + f5g0 + f6g9_19 + f7g8_19 + f8g7_19 + f9g6_19;
            h[6] = f0g6 + f1g5_2 + f2g4 + f3g3_2 + f4g2 + f5g1_2 + f6g0 + f7g9_38 + f8g8_19 + f9g7_38;
            h[7] = f0g7 + f1g6 + f2g5 + f3g4 + f4g3 + f5g2 + f6g1 + f7g0 + f8g9_19 + f9g8_19;
            h[8] = f0g8 + f1g7_2 + f2g6 + f3g5_2 + f4g4 + f5g3_2 + f6g2 + f7g1_2 + f8g0 + f9g9_38;
            h[9] = f0g9 + f1g8 + f2g7 + f3g6 + f4g5 + f5g4 + f6g3 + f7g2 + f8g1 + f9g0;

            // |h0| <= (1.1*1.1*2^52*(1+19+19+19+19)+1.1*1.1*2^50*(38+38+38+38+38))
            //   i.e. |h0| <= 1.2*2^59; narrower ranges for h2, h4, h6, h8
            // |h1| <= (1.1*1.1*2^51*(1+1+19+19+19+19+19+19+19+19))
            //   i.e. |h1| <= 1.5*2^58; narrower ranges for h3, h5, h7, h9

            carry[0] = (h[0] + (1 << 25)) >> 26;
            h[1] += carry[0];
            h[0] -= carry[0] << 26;
            carry[4] = (h[4] + (1 << 25)) >> 26;
            h[5] += carry[4];
            h[4] -= carry[4] << 26;
            // |h0| <= 2^25
            // |h4| <= 2^25
            // |h1| <= 1.51*2^58
            // |h5| <= 1.51*2^58

            carry[1] = (h[1] + (1 << 24)) >> 25;
            h[2] += carry[1];
            h[1] -= carry[1] << 25;
            carry[5] = (h[5] + (1 << 24)) >> 25;
            h[6] += carry[5];
            h[5] -= carry[5] << 25;
            // |h1| <= 2^24; from now on fits into int32
            // |h5| <= 2^24; from now on fits into int32
            // |h2| <= 1.21*2^59
            // |h6| <= 1.21*2^59

            carry[2] = (h[2] + (1 << 25)) >> 26;
            h[3] += carry[2];
            h[2] -= carry[2] << 26;
            carry[6] = (h[6] + (1 << 25)) >> 26;
            h[7] += carry[6];
            h[6] -= carry[6] << 26;
            // |h2| <= 2^25; from now on fits into int32 unchanged
            // |h6| <= 2^25; from now on fits into int32 unchanged
            // |h3| <= 1.51*2^58
            // |h7| <= 1.51*2^58

            carry[3] = (h[3] + (1 << 24)) >> 25;
            h[4] += carry[3];
            h[3] -= carry[3] << 25;
            carry[7] = (h[7] + (1 << 24)) >> 25;
            h[8] += carry[7];
            h[7] -= carry[7] << 25;
            // |h3| <= 2^24; from now on fits into int32 unchanged
            // |h7| <= 2^24; from now on fits into int32 unchanged
            // |h4| <= 1.52*2^33
            // |h8| <= 1.52*2^33

            carry[4] = (h[4] + (1 << 25)) >> 26;
            h[5] += carry[4];
            h[4] -= carry[4] << 26;
            carry[8] = (h[8] + (1 << 25)) >> 26;
            h[9] += carry[8];
            h[8] -= carry[8] << 26;
            // |h4| <= 2^25; from now on fits into int32 unchanged
            // |h8| <= 2^25; from now on fits into int32 unchanged
            // |h5| <= 1.01*2^24
            // |h9| <= 1.51*2^58

            carry[9] = (h[9] + (1 << 24)) >> 25;
            h[0] += carry[9] * 19;
            h[9] -= carry[9] << 25;
            // |h9| <= 2^24; from now on fits into int32 unchanged
            // |h0| <= 1.8*2^37

            carry[0] = (h[0] + (1 << 25)) >> 26;
            h[1] += carry[0];
            h[0] -= carry[0] << 26;
            // |h0| <= 2^25; from now on fits into int32 unchanged
            // |h1| <= 1.01*2^24

            output[0] = (int)h[0];
            output[1] = (int)h[1];
            output[2] = (int)h[2];
            output[3] = (int)h[3];
            output[4] = (int)h[4];
            output[5] = (int)h[5];
            output[6] = (int)h[6];
            output[7] = (int)h[7];
            output[8] = (int)h[8];
            output[9] = (int)h[9];
        }

        /// <summary>
        /// Calculates f*f. Can overlap h with f.
        /// </summary>
        private static void Square(Span<int> input, Span<long> carry, Span<long> h, Span<int> output)
        {
            int f0 = input[0];
            int f1 = input[1];
            int f2 = input[2];
            int f3 = input[3];
            int f4 = input[4];
            int f5 = input[5];
            int f6 = input[6];
            int f7 = input[7];
            int f8 = input[8];
            int f9 = input[9];
            int f0_2 = 2 * f0;
            int f1_2 = 2 * f1;
            int f2_2 = 2 * f2;
            int f3_2 = 2 * f3;
            int f4_2 = 2 * f4;
            int f5_2 = 2 * f5;
            int f6_2 = 2 * f6;
            int f7_2 = 2 * f7;
            int f5_38 = 38 * f5; // 1.31*2^30
            int f6_19 = 19 * f6; // 1.31*2^30
            int f7_38 = 38 * f7; // 1.31*2^30
            int f8_19 = 19 * f8; // 1.31*2^30
            int f9_38 = 38 * f9; // 1.31*2^30
            long f0f0 = (long)f0 * (long)f0;
            long f0f1_2 = (long)f0_2 * (long)f1;
            long f0f2_2 = (long)f0_2 * (long)f2;
            long f0f3_2 = (long)f0_2 * (long)f3;
            long f0f4_2 = (long)f0_2 * (long)f4;
            long f0f5_2 = (long)f0_2 * (long)f5;
            long f0f6_2 = (long)f0_2 * (long)f6;
            long f0f7_2 = (long)f0_2 * (long)f7;
            long f0f8_2 = (long)f0_2 * (long)f8;
            long f0f9_2 = (long)f0_2 * (long)f9;
            long f1f1_2 = (long)f1_2 * (long)f1;
            long f1f2_2 = (long)f1_2 * (long)f2;
            long f1f3_4 = (long)f1_2 * (long)f3_2;
            long f1f4_2 = (long)f1_2 * (long)f4;
            long f1f5_4 = (long)f1_2 * (long)f5_2;
            long f1f6_2 = (long)f1_2 * (long)f6;
            long f1f7_4 = (long)f1_2 * (long)f7_2;
            long f1f8_2 = (long)f1_2 * (long)f8;
            long f1f9_76 = (long)f1_2 * (long)f9_38;
            long f2f2 = (long)f2 * (long)f2;
            long f2f3_2 = (long)f2_2 * (long)f3;
            long f2f4_2 = (long)f2_2 * (long)f4;
            long f2f5_2 = (long)f2_2 * (long)f5;
            long f2f6_2 = (long)f2_2 * (long)f6;
            long f2f7_2 = (long)f2_2 * (long)f7;
            long f2f8_38 = (long)f2_2 * (long)f8_19;
            long f2f9_38 = (long)f2 * (long)f9_38;
            long f3f3_2 = (long)f3_2 * (long)f3;
            long f3f4_2 = (long)f3_2 * (long)f4;
            long f3f5_4 = (long)f3_2 * (long)f5_2;
            long f3f6_2 = (long)f3_2 * (long)f6;
            long f3f7_76 = (long)f3_2 * (long)f7_38;
            long f3f8_38 = (long)f3_2 * (long)f8_19;
            long f3f9_76 = (long)f3_2 * (long)f9_38;
            long f4f4 = (long)f4 * (long)f4;
            long f4f5_2 = (long)f4_2 * (long)f5;
            long f4f6_38 = (long)f4_2 * (long)f6_19;
            long f4f7_38 = (long)f4 * (long)f7_38;
            long f4f8_38 = (long)f4_2 * (long)f8_19;
            long f4f9_38 = (long)f4 * (long)f9_38;
            long f5f5_38 = (long)f5 * (long)f5_38;
            long f5f6_38 = (long)f5_2 * (long)f6_19;
            long f5f7_76 = (long)f5_2 * (long)f7_38;
            long f5f8_38 = (long)f5_2 * (long)f8_19;
            long f5f9_76 = (long)f5_2 * (long)f9_38;
            long f6f6_19 = (long)f6 * (long)f6_19;
            long f6f7_38 = (long)f6 * (long)f7_38;
            long f6f8_38 = (long)f6_2 * (long)f8_19;
            long f6f9_38 = (long)f6 * (long)f9_38;
            long f7f7_38 = (long)f7 * (long)f7_38;
            long f7f8_38 = (long)f7_2 * (long)f8_19;
            long f7f9_76 = (long)f7_2 * (long)f9_38;
            long f8f8_19 = (long)f8 * (long)f8_19;
            long f8f9_38 = (long)f8 * (long)f9_38;
            long f9f9_38 = (long)f9 * (long)f9_38;
            h[0] = f0f0 + f1f9_76 + f2f8_38 + f3f7_76 + f4f6_38 + f5f5_38;
            h[1] = f0f1_2 + f2f9_38 + f3f8_38 + f4f7_38 + f5f6_38;
            h[2] = f0f2_2 + f1f1_2 + f3f9_76 + f4f8_38 + f5f7_76 + f6f6_19;
            h[3] = f0f3_2 + f1f2_2 + f4f9_38 + f5f8_38 + f6f7_38;
            h[4] = f0f4_2 + f1f3_4 + f2f2 + f5f9_76 + f6f8_38 + f7f7_38;
            h[5] = f0f5_2 + f1f4_2 + f2f3_2 + f6f9_38 + f7f8_38;
            h[6] = f0f6_2 + f1f5_4 + f2f4_2 + f3f3_2 + f7f9_76 + f8f8_19;
            h[7] = f0f7_2 + f1f6_2 + f2f5_2 + f3f4_2 + f8f9_38;
            h[8] = f0f8_2 + f1f7_4 + f2f6_2 + f3f5_4 + f4f4 + f9f9_38;
            h[9] = f0f9_2 + f1f8_2 + f2f7_2 + f3f6_2 + f4f5_2;

            carry[0] = (h[0] + (1 << 25)) >> 26;
            h[1] += carry[0];
            h[0] -= carry[0] << 26;
            carry[4] = (h[4] + (1 << 25)) >> 26;
            h[5] += carry[4];
            h[4] -= carry[4] << 26;

            carry[1] = (h[1] + (1 << 24)) >> 25;
            h[2] += carry[1];
            h[1] -= carry[1] << 25;
            carry[5] = (h[5] + (1 << 24)) >> 25;
            h[6] += carry[5];
            h[5] -= carry[5] << 25;

            carry[2] = (h[2] + (1 << 25)) >> 26;
            h[3] += carry[2];
            h[2] -= carry[2] << 26;
            carry[6] = (h[6] + (1 << 25)) >> 26;
            h[7] += carry[6];
            h[6] -= carry[6] << 26;

            carry[3] = (h[3] + (1 << 24)) >> 25;
            h[4] += carry[3];
            h[3] -= carry[3] << 25;
            carry[7] = (h[7] + (1 << 24)) >> 25;
            h[8] += carry[7];
            h[7] -= carry[7] << 25;

            carry[4] = (h[4] + (1 << 25)) >> 26;
            h[5] += carry[4];
            h[4] -= carry[4] << 26;
            carry[8] = (h[8] + (1 << 25)) >> 26;
            h[9] += carry[8];
            h[8] -= carry[8] << 26;

            carry[9] = (h[9] + (1 << 24)) >> 25;
            h[0] += carry[9] * 19;
            h[9] -= carry[9] << 25;

            carry[0] = (h[0] + (1 << 25)) >> 26;
            h[1] += carry[0];
            h[0] -= carry[0] << 26;

            output[0] = (int)h[0];
            output[1] = (int)h[1];
            output[2] = (int)h[2];
            output[3] = (int)h[3];
            output[4] = (int)h[4];
            output[5] = (int)h[5];
            output[6] = (int)h[6];
            output[7] = (int)h[7];
            output[8] = (int)h[8];
            output[9] = (int)h[9];
        }

        /// <summary>
        /// Calculates h = f * 121666. Can overlap h with f; I have no clue why this is a thing
        /// </summary>
        /// <remarks>
        /// <para>
        ///   Preconditions:
        /// |f| bounded by 1.1*2^26,1.1*2^25,1.1*2^26,1.1*2^25,etc.
        /// </para>
        /// <para>
        /// Postconditions:
        /// |h| bounded by 1.1*2^25,1.1*2^24,1.1*2^25,1.1*2^24,etc.
        /// </para>
        /// </remarks>
        private static void Mul121666(Span<int> input, Span<long> carry, Span<long> h, Span<int> output)
        {
            h[0] = (long)input[0] * 121666;
            h[1] = (long)input[1] * 121666;
            h[2] = (long)input[2] * 121666;
            h[3] = (long)input[3] * 121666;
            h[4] = (long)input[4] * 121666;
            h[5] = (long)input[5] * 121666;
            h[6] = (long)input[6] * 121666;
            h[7] = (long)input[7] * 121666;
            h[8] = (long)input[8] * 121666;
            h[9] = (long)input[9] * 121666;

            carry[9] = (h[9] + (1 << 24)) >> 25;
            h[0] += carry[9] * 19;
            h[9] -= carry[9] << 25;
            carry[1] = (h[1] + (1 << 24)) >> 25;
            h[2] += carry[1];
            h[1] -= carry[1] << 25;
            carry[3] = (h[3] + (1 << 24)) >> 25;
            h[4] += carry[3];
            h[3] -= carry[3] << 25;
            carry[5] = (h[5] + (1 << 24)) >> 25;
            h[6] += carry[5];
            h[5] -= carry[5] << 25;
            carry[7] = (h[7] + (1 << 24)) >> 25;
            h[8] += carry[7];
            h[7] -= carry[7] << 25;

            carry[0] = (h[0] + (1 << 25)) >> 26;
            h[1] += carry[0];
            h[0] -= carry[0] << 26;
            carry[2] = (h[2] + (1 << 25)) >> 26;
            h[3] += carry[2];
            h[2] -= carry[2] << 26;
            carry[4] = (h[4] + (1 << 25)) >> 26;
            h[5] += carry[4];
            h[4] -= carry[4] << 26;
            carry[6] = (h[6] + (1 << 25)) >> 26;
            h[7] += carry[6];
            h[6] -= carry[6] << 26;
            carry[8] = (h[8] + (1 << 25)) >> 26;
            h[9] += carry[8];
            h[8] -= carry[8] << 26;

            output[0] = (int)h[0];
            output[1] = (int)h[1];
            output[2] = (int)h[2];
            output[3] = (int)h[3];
            output[4] = (int)h[4];
            output[5] = (int)h[5];
            output[6] = (int)h[6];
            output[7] = (int)h[7];
            output[8] = (int)h[8];
            output[9] = (int)h[9];
        }

        /// <summary>
        /// Calculates this ^(-1)
        /// </summary>
        private static void Invert(Span<int> input, Span<long> carry, Span<long> h, Span<int> output)
        {
            Span<int> t0 = stackalloc int[10];
            Span<int> t1 = stackalloc int[10];
            Span<int> t2 = stackalloc int[10];
            Span<int> t3 = stackalloc int[10];
            int i;

            Square(input, carry, h, t0);
            for (i = 1; i < 1; i++) // why is this a thing?
            {
                Square(t0, carry, h, t0);
            }

            Square(t0, carry, h, t1);
            for (i = 1; i < 2; i++)
            {
                Square(t1, carry, h, t1);
            }

            Multiply(input, t1, carry, h, t1);
            Multiply(t0, t1, carry, h, t0);
            Square(t0, carry, h, t2);
            for (i = 1; i < 1; i++)
            { // what the fuck
                Square(t2, carry, h, t2);
            }

            Multiply(t1, t2, carry, h, t1);
            Square(t1, carry, h, t2);
            for (i = 1; i < 5; i++)
            {
                Square(t2, carry, h, t2);
            }

            Multiply(t2, t1, carry, h, t1);
            Square(t1, carry, h, t2);
            for (i = 1; i < 10; i++)
            {
                Square(t2, carry, h, t2);
            }

            Multiply(t2, t1, carry, h, t2);
            Square(t2, carry, h, t3);
            for (i = 1; i < 20; i++)
            {
                Square(t3, carry, h, t3);
            }

            Multiply(t3, t2, carry, h, t2);
            Square(t2, carry, h, t2);
            for (i = 1; i < 10; i++)
            {
                Square(t2, carry, h, t2);
            }

            Multiply(t2, t1, carry, h, t1);
            Square(t1, carry, h, t2);
            for (i = 1; i < 50; i++)
            {
                Square(t2, carry, h, t2);
            }

            Multiply(t2, t1, carry, h, t2);
            Square(t2, carry, h, t3);
            for (i = 1; i < 100; i++)
            {
                Square(t3, carry, h, t3);
            }

            Multiply(t3, t2, carry, h, t2);
            Square(t2, carry, h, t2);
            for (i = 1; i < 50; i++)
            {
                Square(t2, carry, h, t2);
            }

            Multiply(t2, t1, carry, h, t1);
            Square(t1, carry, h, t1);
            for (i = 1; i < 5; i++)
            {
                Square(t1, carry, h, t1);
            }

            Multiply(t1, t0, carry, h, output);
        }

        /// <summary>
        /// Sets all values of field to zero except that Element[0] = 1
        /// </summary>
        private static void One(Span<int> input)
        {
            for (int i = 1; i < 10; i++)
                input[i] = 0;
            input[0] = 1;
        }

        /// <summary>
        /// Replaces (f,g) with (g,f) if b == 1; replaces (f,g) with (f,g) if b == 0.
        /// </summary>
        /// <param name="f">f</param>
        /// <param name="g">g</param>
        /// <param name="b"></param>
        private static void CSwap(Span<int> f, Span<int> g, int b)
        {
            b = -b;
            for (int i = 0; i < 10; i++)
            {
                int t = b & (f[i] ^ g[i]);
                f[i] ^= t;
                g[i] ^= t;
            }
        }
        /// <summary>
        /// Copies one FieldElement to another FieldElement
        /// </summary>
        /// <param name="dst">Where the src must be copied to</param>
        /// <param name="src">What to copy</param>
        private static void Copy(Span<int> input, Span<int> output)
        {
            for (int i = 0; i < 10; i++)
                output[i] = input[i];
        }

        /// <summary>
        /// load3 reads a 24-bit, little-endian value from in
        /// </summary>
        /// <param name="bytes">Input</param>
        private static long Load3(Span<byte> bytes, int pos)
        {
            long r = bytes[pos];
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
            r |= bytes[pos + 1] << 8;
            r |= bytes[pos + 2] << 16;
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand
            return r;
        }

        /// <summary>
        /// load4 reads a 32-bit, little-endian value from in.
        /// </summary>
        /// <param name="bytes"></param>
        private static long Load4(Span<byte> bytes, int pos)
        {
            // notice this is acted like an UINT NOT LONG
            return (bytes[pos]) | (uint)bytes[pos + 1] << 8 | (uint)bytes[pos + 2] << 16 | (uint)bytes[pos + 3] << 24;
        }
    }
}

#else
        /// <summary>
        /// The base point that is x = 9
        /// </summary>
        public static readonly byte[] Basepoint = { 9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        /// <summary>
        /// An inner function to calculate scalar * point
        /// </summary>
        /// <param name="input"></param>
        /// <param name="baseIn"></param>
        private static byte[] ScalarMult(byte[] input, byte[] baseIn)
        {
            var e = new byte[32];

            Array.Copy(input, e, 32); //copy(e[:], input[:])
            e[0] &= 248;
            e[31] &= 127;
            e[31] |= 64;

            FieldElement x1, x2, z2, x3, z3, tmp0, tmp1;
            z2 = new FieldElement();
            // feFromBytes(&x1, base)
            x1 = new FieldElement(baseIn); //SECOND NUMBER
                                           //feOne(&x2)
            x2 = new FieldElement();
            x2.One();
            //feCopy(&x3, &x1)
            x3 = new FieldElement();
            FieldElement.Copy(ref x3, x1);
            //feOne(&z3)
            z3 = new FieldElement();
            z3.One();

            int swap = 0;
            for (int pos = 254; pos >= 0; pos--)
            {
                byte b = Convert.ToByte(e[pos / 8] >> (pos & 7));
                b &= 1;
                swap ^= (int)(b);
                FieldElement.CSwap(ref x2, ref x3, swap);
                FieldElement.CSwap(ref z2, ref z3, swap);
                swap = (int)(b);

                tmp0 = x3 - z3; //feSub(&tmp0, &x3, &z3)
                tmp1 = x2 - z2; //feSub(&tmp1, &x2, &z2)
                x2 += z2; //feAdd(&x2, &x2, &z2)
                z2 = x3 + z3; //feAdd(&z2, &x3, &z3)
                z3 = tmp0.Multiply(x2);
                z2 = z2.Multiply(tmp1);
                tmp0 = tmp1.Square();
                tmp1 = x2.Square();
                x3 = z3 + z2; //feAdd(&x3, &z3, &z2)
                z2 = z3 - z2; //feSub(&z2, &z3, &z2)
                x2 = tmp1.Multiply(tmp0);
                tmp1 -= tmp0;//feSub(&tmp1, &tmp1, &tmp0)
                z2 = z2.Square();
                z3 = tmp1.Mul121666();
                x3 = x3.Square();
                tmp0 += z3; //feAdd(&tmp0, &tmp0, &z3)
                z3 = x1.Multiply(z2);
                z2 = tmp1.Multiply(tmp0);
            }

            FieldElement.CSwap(ref x2, ref x3, swap);
            FieldElement.CSwap(ref z2, ref z3, swap);

            z2 = z2.Invert();
            x2 = x2.Multiply(z2);
            return x2.ToBytes();
        }
        /// <summary>
        /// <para>
        /// X25519 returns the result of the scalar multiplication (scalar * point),
        /// according to RFC 7748, Section 5. scalar, point and the return value are
        /// slices of 32 bytes.
        /// </para>
        /// <para>
        /// If point is Basepoint (but not if it's a different slice with the same
        /// contents) a precomputed implementation might be used for performance.
        /// </para>
        /// </summary>
        public static byte[] ScalarMultiplication(byte[] scalar, byte[] point)
        {
            if (scalar.Length != 32)
                throw new ArgumentException("Length of scalar must be 32", nameof(scalar));
            if (point.Length != 32)
                throw new ArgumentException("Length of point must be 32", nameof(point));
            byte[] zero = new byte[32];
            byte[] result = ScalarMult(scalar, point);
            // here I tried to make something like subtle.ConstantTimeCompare
            if (result.Length != zero.Length)
                throw new Exception("This should not happen. Because result is always 32 bytes");

            byte v = 0;
            for (int i = 0; i < result.Length; i++)
                v = (byte)(v | (zero[i] ^ result[i]));
            if ((int)(((uint)(v ^ 0) - 1) >> 31) == 1) // no clue if these functions are ok or not
                throw new Exception("bad input point: low order point");
            return result;
        }
    }

    /// <summary>
    /// fieldElement represents an element of the field GF(2^255 - 19). An element
    /// t, entries t[0]...t[9], represents the integer t[0]+2^26 t[1]+2^51 t[2]+2^77
    /// t[3]+2^102 t[4]+...+2^230 t[9]. Bounds on each t[i] vary depending on
    /// context.
    /// </summary>
    internal class FieldElement
    {
        /// <summary>
        /// In Golang, field element type is just only [10]int array
        /// </summary>
        private readonly int[] _elements = new int[10];

        /// <summary>
        /// Generate field with empty elements
        /// </summary>
        public FieldElement()
        {
        }

        /// <summary>
        /// Generate field element from byte array
        /// </summary>
        /// <remarks>
        /// This is identical to feFromBytes in Golang
        /// </remarks>
        /// <param name="src"></param>
        public FieldElement(byte[] src)
        {
            long h0 = Load4(SubArray(src, 0, 4));
            long h1 = Load3(SubArray(src, 4, 3)) << 6;
            long h2 = Load3(SubArray(src, 7, 3)) << 5;
            long h3 = Load3(SubArray(src, 10, 3)) << 3;
            long h4 = Load3(SubArray(src, 13, 3)) << 2;
            long h5 = Load4(SubArray(src, 16, 4));
            long h6 = Load3(SubArray(src, 20, 3)) << 7;
            long h7 = Load3(SubArray(src, 23, 3)) << 5;
            long h8 = Load3(SubArray(src, 26, 3)) << 4;
            long h9 = (Load3(SubArray(src, 29, 3)) & 0x7fffff) << 2;

            var carry = new long[10];
            carry[9] = (h9 + (1 << 24)) >> 25;
            h0 += carry[9] * 19;
            h9 -= carry[9] << 25;
            carry[1] = (h1 + (1 << 24)) >> 25;
            h2 += carry[1];
            h1 -= carry[1] << 25;
            carry[3] = (h3 + (1 << 24)) >> 25;
            h4 += carry[3];
            h3 -= carry[3] << 25;
            carry[5] = (h5 + (1 << 24)) >> 25;
            h6 += carry[5];
            h5 -= carry[5] << 25;
            carry[7] = (h7 + (1 << 24)) >> 25;
            h8 += carry[7];
            h7 -= carry[7] << 25;

            carry[0] = (h0 + (1 << 25)) >> 26;
            h1 += carry[0];
            h0 -= carry[0] << 26;
            carry[2] = (h2 + (1 << 25)) >> 26;
            h3 += carry[2];
            h2 -= carry[2] << 26;
            carry[4] = (h4 + (1 << 25)) >> 26;
            h5 += carry[4];
            h4 -= carry[4] << 26;
            carry[6] = (h6 + (1 << 25)) >> 26;
            h7 += carry[6];
            h6 -= carry[6] << 26;
            carry[8] = (h8 + (1 << 25)) >> 26;
            h9 += carry[8];
            h8 -= carry[8] << 26;

            _elements[0] = (int)(h0);
            _elements[1] = (int)(h1);
            _elements[2] = (int)(h2);
            _elements[3] = (int)(h3);
            _elements[4] = (int)(h4);
            _elements[5] = (int)(h5);
            _elements[6] = (int)(h6);
            _elements[7] = (int)(h7);
            _elements[8] = (int)(h8);
            _elements[9] = (int)(h9);
        }

        /// <summary>
        /// Directly overwrites elements in field with given array
        /// </summary>
        /// <param name="src"></param>
        public void SetElementsDirect(int[] src)
        {
            for (int i = 0; i < 10; i++)
                _elements[i] = src[i];
        }

        /// <summary>
        /// Converts to a 32 byte array
        /// </summary>
        /// <remarks>
        /// <para>
        /// Identical to feToBytes in Golang
        ///   feToBytes marshals h to s.
        /// Preconditions:
        /// |h| bounded by 1.1*2^25,1.1*2^24,1.1*2^25,1.1*2^24,etc.
        /// </para>
        /// <para>
        /// Write p=2^255-19; q=floor(h/p).
        /// Basic claim: q = floor(2^(-255)(h + 19 2^(-25)h9 + 2^(-1))).
        /// </para>
        /// <para>
        /// Proof:
        /// Have |h|&lt;=p so |q|&lt;=1 so |19^2 2^(-255) q|&lt;1/4.
        /// Also have |h-2^230 h9|&lt;2^230 so |19 2^(-255)(h-2^230 h9)|&lt;1/4.
        /// </para>
        /// <para>
        /// Write y=2^(-1)-19^2 2^(-255)q-19 2^(-255)(h-2^230 h9).
        /// Then 0&lt;y&lt;1.
        /// </para>
        /// <para>
        /// Write r=h-pq.
        /// Have 0&lt;=r&lt;=p-1=2^255-20.
        /// Thus 0&lt;=r+19(2^-255)r&lt;r+19(2^-255)2^255&lt;=2^255-1.
        /// </para>
        /// <para>
        /// Write x=r+19(2^-255)r+y.
        /// Then 0&lt;x&lt;2^255 so floor(2^(-255)x) = 0 so floor(q+2^(-255)x) = q.
        /// </para>
        /// <para>
        /// Have q+2^(-255)x = 2^(-255)(h + 19 2^(-25) h9 + 2^(-1))
        /// so floor(2^(-255)(h + 19 2^(-25) h9 + 2^(-1))) = q.
        /// </para>
        /// </remarks>
        public byte[] ToBytes()
        {
            var carry = new int[10];

            int q = ((19 * _elements[9]) + (1 << 24)) >> 25;
            q = (_elements[0] + q) >> 26;
            q = (_elements[1] + q) >> 25;
            q = (_elements[2] + q) >> 26;
            q = (_elements[3] + q) >> 25;
            q = (_elements[4] + q) >> 26;
            q = (_elements[5] + q) >> 25;
            q = (_elements[6] + q) >> 26;
            q = (_elements[7] + q) >> 25;
            q = (_elements[8] + q) >> 26;
            q = (_elements[9] + q) >> 25;

            // Goal: Output h-(2^255-19)q, which is between 0 and 2^255-20.
            _elements[0] += 19 * q;
            // Goal: Output h-2^255 q, which is between 0 and 2^255-20.

            carry[0] = _elements[0] >> 26;
            _elements[1] += carry[0];
            _elements[0] -= carry[0] << 26;
            carry[1] = _elements[1] >> 25;
            _elements[2] += carry[1];
            _elements[1] -= carry[1] << 25;
            carry[2] = _elements[2] >> 26;
            _elements[3] += carry[2];
            _elements[2] -= carry[2] << 26;
            carry[3] = _elements[3] >> 25;
            _elements[4] += carry[3];
            _elements[3] -= carry[3] << 25;
            carry[4] = _elements[4] >> 26;
            _elements[5] += carry[4];
            _elements[4] -= carry[4] << 26;
            carry[5] = _elements[5] >> 25;
            _elements[6] += carry[5];
            _elements[5] -= carry[5] << 25;
            carry[6] = _elements[6] >> 26;
            _elements[7] += carry[6];
            _elements[6] -= carry[6] << 26;
            carry[7] = _elements[7] >> 25;
            _elements[8] += carry[7];
            _elements[7] -= carry[7] << 25;
            carry[8] = _elements[8] >> 26;
            _elements[9] += carry[8];
            _elements[8] -= carry[8] << 26;
            carry[9] = _elements[9] >> 25;
            _elements[9] -= carry[9] << 25;
            // h10 = carry9

            // Goal: Output h[0]+...+2^255 h10-2^255 q, which is between 0 and 2^255-20.
            // Have h[0]+...+2^230 h[9] between 0 and 2^255-1;
            // evidently 2^255 h10-2^255 q = 0.
            // Goal: Output h[0]+...+2^230 h[9].

            var s = new byte[32];
            s[0] = (byte)(_elements[0] >> 0);
            s[1] = (byte)(_elements[0] >> 8);
            s[2] = (byte)(_elements[0] >> 16);
            s[3] = (byte)((_elements[0] >> 24) | (_elements[1] << 2));
            s[4] = (byte)(_elements[1] >> 6);
            s[5] = (byte)(_elements[1] >> 14);
            s[6] = (byte)((_elements[1] >> 22) | (_elements[2] << 3));
            s[7] = (byte)(_elements[2] >> 5);
            s[8] = (byte)(_elements[2] >> 13);
            s[9] = (byte)((_elements[2] >> 21) | (_elements[3] << 5));
            s[10] = (byte)(_elements[3] >> 3);
            s[11] = (byte)(_elements[3] >> 11);
            s[12] = (byte)((_elements[3] >> 19) | (_elements[4] << 6));
            s[13] = (byte)(_elements[4] >> 2);
            s[14] = (byte)(_elements[4] >> 10);
            s[15] = (byte)(_elements[4] >> 18);
            s[16] = (byte)(_elements[5] >> 0);
            s[17] = (byte)(_elements[5] >> 8);
            s[18] = (byte)(_elements[5] >> 16);
            s[19] = (byte)((_elements[5] >> 24) | (_elements[6] << 1));
            s[20] = (byte)(_elements[6] >> 7);
            s[21] = (byte)(_elements[6] >> 15);
            s[22] = (byte)((_elements[6] >> 23) | (_elements[7] << 3));
            s[23] = (byte)(_elements[7] >> 5);
            s[24] = (byte)(_elements[7] >> 13);
            s[25] = (byte)((_elements[7] >> 21) | (_elements[8] << 4));
            s[26] = (byte)(_elements[8] >> 4);
            s[27] = (byte)(_elements[8] >> 12);
            s[28] = (byte)((_elements[8] >> 20) | (_elements[9] << 6));
            s[29] = (byte)(_elements[9] >> 2);
            s[30] = (byte)(_elements[9] >> 10);
            s[31] = (byte)(_elements[9] >> 18);
            return s;
        }

        /// <summary>
        /// Calculates this * g
        /// </summary>
        /// <param name="g"></param>
        /// <remarks>
        /// <para>Can overlap h with f or g.</para>
        /// <para>
        /// Preconditions:
        /// |f| bounded by 1.1*2^26,1.1*2^25,1.1*2^26,1.1*2^25,etc.
        /// |g| bounded by 1.1*2^26,1.1*2^25,1.1*2^26,1.1*2^25,etc.
        /// </para>
        /// <para>
        /// Postconditions:
        /// |h| bounded by 1.1*2^25,1.1*2^24,1.1*2^25,1.1*2^24,etc.
        /// </para>
        /// <para>Notes on implementation strategy:</para>
        /// <para>
        /// Using schoolbook multiplication.
        /// Karatsuba would save a little in some cost models.
        /// </para>
        /// <para>
        /// Most multiplications by 2 and 19 are 32-bit precomputations;
        /// cheaper than 64-bit postcomputations.
        /// </para>
        /// <para>
        /// There is one remaining multiplication by 19 in the carry chain;
        /// one *19 precomputation can be merged into this,
        /// but the resulting data flow is considerably less clean.
        /// </para>
        /// <para>
        /// There are 12 carries below.
        /// 10 of them are 2-way parallelizable and vectorizable.
        /// Can get away with 11 carries, but then data flow is much deeper.
        /// </para>
        /// <para>With tighter constraints on inputs can squeeze carries into int32.</para>
        /// </remarks>
        public FieldElement Multiply(FieldElement g)
        {
            int f0 = _elements[0];
            int f1 = _elements[1];
            int f2 = _elements[2];
            int f3 = _elements[3];
            int f4 = _elements[4];
            int f5 = _elements[5];
            int f6 = _elements[6];
            int f7 = _elements[7];
            int f8 = _elements[8];
            int f9 = _elements[9];
            int g0 = g[0];
            int g1 = g[1];
            int g2 = g[2];
            int g3 = g[3];
            int g4 = g[4];
            int g5 = g[5];
            int g6 = g[6];
            int g7 = g[7];
            int g8 = g[8];
            int g9 = g[9];
            int g1_19 = 19 * g1; // 1.4*2^29
            int g2_19 = 19 * g2; // 1.4*2^30; still ok
            int g3_19 = 19 * g3;
            int g4_19 = 19 * g4;
            int g5_19 = 19 * g5;
            int g6_19 = 19 * g6;
            int g7_19 = 19 * g7;
            int g8_19 = 19 * g8;
            int g9_19 = 19 * g9;
            int f1_2 = 2 * f1;
            int f3_2 = 2 * f3;
            int f5_2 = 2 * f5;
            int f7_2 = 2 * f7;
            int f9_2 = 2 * f9;
            long f0g0 = (long)f0 * g0;
            long f0g1 = (long)(f0) * (long)(g1);
            long f0g2 = (long)(f0) * (long)(g2);
            long f0g3 = (long)(f0) * (long)(g3);
            long f0g4 = (long)(f0) * (long)(g4);
            long f0g5 = (long)(f0) * (long)(g5);
            long f0g6 = (long)(f0) * (long)(g6);
            long f0g7 = (long)(f0) * (long)(g7);
            long f0g8 = (long)(f0) * (long)(g8);
            long f0g9 = (long)(f0) * (long)(g9);
            long f1g0 = (long)(f1) * (long)(g0);
            long f1g1_2 = (long)(f1_2) * (long)(g1);
            long f1g2 = (long)(f1) * (long)(g2);
            long f1g3_2 = (long)(f1_2) * (long)(g3);
            long f1g4 = (long)(f1) * (long)(g4);
            long f1g5_2 = (long)(f1_2) * (long)(g5);
            long f1g6 = (long)(f1) * (long)(g6);
            long f1g7_2 = (long)(f1_2) * (long)(g7);
            long f1g8 = (long)(f1) * (long)(g8);
            long f1g9_38 = (long)(f1_2) * (long)(g9_19);
            long f2g0 = (long)(f2) * (long)(g0);
            long f2g1 = (long)(f2) * (long)(g1);
            long f2g2 = (long)(f2) * (long)(g2);
            long f2g3 = (long)(f2) * (long)(g3);
            long f2g4 = (long)(f2) * (long)(g4);
            long f2g5 = (long)(f2) * (long)(g5);
            long f2g6 = (long)(f2) * (long)(g6);
            long f2g7 = (long)(f2) * (long)(g7);
            long f2g8_19 = (long)(f2) * (long)(g8_19);
            long f2g9_19 = (long)(f2) * (long)(g9_19);
            long f3g0 = (long)(f3) * (long)(g0);
            long f3g1_2 = (long)(f3_2) * (long)(g1);
            long f3g2 = (long)(f3) * (long)(g2);
            long f3g3_2 = (long)(f3_2) * (long)(g3);
            long f3g4 = (long)(f3) * (long)(g4);
            long f3g5_2 = (long)(f3_2) * (long)(g5);
            long f3g6 = (long)(f3) * (long)(g6);
            long f3g7_38 = (long)(f3_2) * (long)(g7_19);
            long f3g8_19 = (long)(f3) * (long)(g8_19);
            long f3g9_38 = (long)(f3_2) * (long)(g9_19);
            long f4g0 = (long)(f4) * (long)(g0);
            long f4g1 = (long)(f4) * (long)(g1);
            long f4g2 = (long)(f4) * (long)(g2);
            long f4g3 = (long)(f4) * (long)(g3);
            long f4g4 = (long)(f4) * (long)(g4);
            long f4g5 = (long)(f4) * (long)(g5);
            long f4g6_19 = (long)(f4) * (long)(g6_19);
            long f4g7_19 = (long)(f4) * (long)(g7_19);
            long f4g8_19 = (long)(f4) * (long)(g8_19);
            long f4g9_19 = (long)(f4) * (long)(g9_19);
            long f5g0 = (long)(f5) * (long)(g0);
            long f5g1_2 = (long)(f5_2) * (long)(g1);
            long f5g2 = (long)(f5) * (long)(g2);
            long f5g3_2 = (long)(f5_2) * (long)(g3);
            long f5g4 = (long)(f5) * (long)(g4);
            long f5g5_38 = (long)(f5_2) * (long)(g5_19);
            long f5g6_19 = (long)(f5) * (long)(g6_19);
            long f5g7_38 = (long)(f5_2) * (long)(g7_19);
            long f5g8_19 = (long)(f5) * (long)(g8_19);
            long f5g9_38 = (long)(f5_2) * (long)(g9_19);
            long f6g0 = (long)(f6) * (long)(g0);
            long f6g1 = (long)(f6) * (long)(g1);
            long f6g2 = (long)(f6) * (long)(g2);
            long f6g3 = (long)(f6) * (long)(g3);
            long f6g4_19 = (long)(f6) * (long)(g4_19);
            long f6g5_19 = (long)(f6) * (long)(g5_19);
            long f6g6_19 = (long)(f6) * (long)(g6_19);
            long f6g7_19 = (long)(f6) * (long)(g7_19);
            long f6g8_19 = (long)(f6) * (long)(g8_19);
            long f6g9_19 = (long)(f6) * (long)(g9_19);
            long f7g0 = (long)(f7) * (long)(g0);
            long f7g1_2 = (long)(f7_2) * (long)(g1);
            long f7g2 = (long)(f7) * (long)(g2);
            long f7g3_38 = (long)(f7_2) * (long)(g3_19);
            long f7g4_19 = (long)(f7) * (long)(g4_19);
            long f7g5_38 = (long)(f7_2) * (long)(g5_19);
            long f7g6_19 = (long)(f7) * (long)(g6_19);
            long f7g7_38 = (long)(f7_2) * (long)(g7_19);
            long f7g8_19 = (long)(f7) * (long)(g8_19);
            long f7g9_38 = (long)(f7_2) * (long)(g9_19);
            long f8g0 = (long)(f8) * (long)(g0);
            long f8g1 = (long)(f8) * (long)(g1);
            long f8g2_19 = (long)(f8) * (long)(g2_19);
            long f8g3_19 = (long)(f8) * (long)(g3_19);
            long f8g4_19 = (long)(f8) * (long)(g4_19);
            long f8g5_19 = (long)(f8) * (long)(g5_19);
            long f8g6_19 = (long)(f8) * (long)(g6_19);
            long f8g7_19 = (long)(f8) * (long)(g7_19);
            long f8g8_19 = (long)(f8) * (long)(g8_19);
            long f8g9_19 = (long)(f8) * (long)(g9_19);
            long f9g0 = (long)(f9) * (long)(g0);
            long f9g1_38 = (long)(f9_2) * (long)(g1_19);
            long f9g2_19 = (long)(f9) * (long)(g2_19);
            long f9g3_38 = (long)(f9_2) * (long)(g3_19);
            long f9g4_19 = (long)(f9) * (long)(g4_19);
            long f9g5_38 = (long)(f9_2) * (long)(g5_19);
            long f9g6_19 = (long)(f9) * (long)(g6_19);
            long f9g7_38 = (long)(f9_2) * (long)(g7_19);
            long f9g8_19 = (long)(f9) * (long)(g8_19);
            long f9g9_38 = (long)(f9_2) * (long)(g9_19);
            long h0 = f0g0 + f1g9_38 + f2g8_19 + f3g7_38 + f4g6_19 + f5g5_38 + f6g4_19 + f7g3_38 + f8g2_19 + f9g1_38;
            long h1 = f0g1 + f1g0 + f2g9_19 + f3g8_19 + f4g7_19 + f5g6_19 + f6g5_19 + f7g4_19 + f8g3_19 + f9g2_19;
            long h2 = f0g2 + f1g1_2 + f2g0 + f3g9_38 + f4g8_19 + f5g7_38 + f6g6_19 + f7g5_38 + f8g4_19 + f9g3_38;
            long h3 = f0g3 + f1g2 + f2g1 + f3g0 + f4g9_19 + f5g8_19 + f6g7_19 + f7g6_19 + f8g5_19 + f9g4_19;
            long h4 = f0g4 + f1g3_2 + f2g2 + f3g1_2 + f4g0 + f5g9_38 + f6g8_19 + f7g7_38 + f8g6_19 + f9g5_38;
            long h5 = f0g5 + f1g4 + f2g3 + f3g2 + f4g1 + f5g0 + f6g9_19 + f7g8_19 + f8g7_19 + f9g6_19;
            long h6 = f0g6 + f1g5_2 + f2g4 + f3g3_2 + f4g2 + f5g1_2 + f6g0 + f7g9_38 + f8g8_19 + f9g7_38;
            long h7 = f0g7 + f1g6 + f2g5 + f3g4 + f4g3 + f5g2 + f6g1 + f7g0 + f8g9_19 + f9g8_19;
            long h8 = f0g8 + f1g7_2 + f2g6 + f3g5_2 + f4g4 + f5g3_2 + f6g2 + f7g1_2 + f8g0 + f9g9_38;
            long h9 = f0g9 + f1g8 + f2g7 + f3g6 + f4g5 + f5g4 + f6g3 + f7g2 + f8g1 + f9g0;
            var carry = new long[10];

            // |h0| <= (1.1*1.1*2^52*(1+19+19+19+19)+1.1*1.1*2^50*(38+38+38+38+38))
            //   i.e. |h0| <= 1.2*2^59; narrower ranges for h2, h4, h6, h8
            // |h1| <= (1.1*1.1*2^51*(1+1+19+19+19+19+19+19+19+19))
            //   i.e. |h1| <= 1.5*2^58; narrower ranges for h3, h5, h7, h9

            carry[0] = (h0 + (1 << 25)) >> 26;
            h1 += carry[0];
            h0 -= carry[0] << 26;
            carry[4] = (h4 + (1 << 25)) >> 26;
            h5 += carry[4];
            h4 -= carry[4] << 26;
            // |h0| <= 2^25
            // |h4| <= 2^25
            // |h1| <= 1.51*2^58
            // |h5| <= 1.51*2^58

            carry[1] = (h1 + (1 << 24)) >> 25;
            h2 += carry[1];
            h1 -= carry[1] << 25;
            carry[5] = (h5 + (1 << 24)) >> 25;
            h6 += carry[5];
            h5 -= carry[5] << 25;
            // |h1| <= 2^24; from now on fits into int32
            // |h5| <= 2^24; from now on fits into int32
            // |h2| <= 1.21*2^59
            // |h6| <= 1.21*2^59

            carry[2] = (h2 + (1 << 25)) >> 26;
            h3 += carry[2];
            h2 -= carry[2] << 26;
            carry[6] = (h6 + (1 << 25)) >> 26;
            h7 += carry[6];
            h6 -= carry[6] << 26;
            // |h2| <= 2^25; from now on fits into int32 unchanged
            // |h6| <= 2^25; from now on fits into int32 unchanged
            // |h3| <= 1.51*2^58
            // |h7| <= 1.51*2^58

            carry[3] = (h3 + (1 << 24)) >> 25;
            h4 += carry[3];
            h3 -= carry[3] << 25;
            carry[7] = (h7 + (1 << 24)) >> 25;
            h8 += carry[7];
            h7 -= carry[7] << 25;
            // |h3| <= 2^24; from now on fits into int32 unchanged
            // |h7| <= 2^24; from now on fits into int32 unchanged
            // |h4| <= 1.52*2^33
            // |h8| <= 1.52*2^33

            carry[4] = (h4 + (1 << 25)) >> 26;
            h5 += carry[4];
            h4 -= carry[4] << 26;
            carry[8] = (h8 + (1 << 25)) >> 26;
            h9 += carry[8];
            h8 -= carry[8] << 26;
            // |h4| <= 2^25; from now on fits into int32 unchanged
            // |h8| <= 2^25; from now on fits into int32 unchanged
            // |h5| <= 1.01*2^24
            // |h9| <= 1.51*2^58

            carry[9] = (h9 + (1 << 24)) >> 25;
            h0 += carry[9] * 19;
            h9 -= carry[9] << 25;
            // |h9| <= 2^24; from now on fits into int32 unchanged
            // |h0| <= 1.8*2^37

            carry[0] = (h0 + (1 << 25)) >> 26;
            h1 += carry[0];
            h0 -= carry[0] << 26;
            // |h0| <= 2^25; from now on fits into int32 unchanged
            // |h1| <= 1.01*2^24

            int[] h = new int[10];
            h[0] = (int)(h0);
            h[1] = (int)(h1);
            h[2] = (int)(h2);
            h[3] = (int)(h3);
            h[4] = (int)(h4);
            h[5] = (int)(h5);
            h[6] = (int)(h6);
            h[7] = (int)(h7);
            h[8] = (int)(h8);
            h[9] = (int)(h9);

            var final = new FieldElement();
            final.SetElementsDirect(h);
            return final;
        }
        /// <summary>
        /// Calculates f*f. Can overlap h with f.
        /// </summary>
        public FieldElement Square()
        {
            int f0 = _elements[0];
            int f1 = _elements[1];
            int f2 = _elements[2];
            int f3 = _elements[3];
            int f4 = _elements[4];
            int f5 = _elements[5];
            int f6 = _elements[6];
            int f7 = _elements[7];
            int f8 = _elements[8];
            int f9 = _elements[9];
            int f0_2 = 2 * f0;
            int f1_2 = 2 * f1;
            int f2_2 = 2 * f2;
            int f3_2 = 2 * f3;
            int f4_2 = 2 * f4;
            int f5_2 = 2 * f5;
            int f6_2 = 2 * f6;
            int f7_2 = 2 * f7;
            int f5_38 = 38 * f5; // 1.31*2^30
            int f6_19 = 19 * f6; // 1.31*2^30
            int f7_38 = 38 * f7; // 1.31*2^30
            int f8_19 = 19 * f8; // 1.31*2^30
            int f9_38 = 38 * f9; // 1.31*2^30
            long f0f0 = (long)(f0) * (long)(f0);
            long f0f1_2 = (long)(f0_2) * (long)(f1);
            long f0f2_2 = (long)(f0_2) * (long)(f2);
            long f0f3_2 = (long)(f0_2) * (long)(f3);
            long f0f4_2 = (long)(f0_2) * (long)(f4);
            long f0f5_2 = (long)(f0_2) * (long)(f5);
            long f0f6_2 = (long)(f0_2) * (long)(f6);
            long f0f7_2 = (long)(f0_2) * (long)(f7);
            long f0f8_2 = (long)(f0_2) * (long)(f8);
            long f0f9_2 = (long)(f0_2) * (long)(f9);
            long f1f1_2 = (long)(f1_2) * (long)(f1);
            long f1f2_2 = (long)(f1_2) * (long)(f2);
            long f1f3_4 = (long)(f1_2) * (long)(f3_2);
            long f1f4_2 = (long)(f1_2) * (long)(f4);
            long f1f5_4 = (long)(f1_2) * (long)(f5_2);
            long f1f6_2 = (long)(f1_2) * (long)(f6);
            long f1f7_4 = (long)(f1_2) * (long)(f7_2);
            long f1f8_2 = (long)(f1_2) * (long)(f8);
            long f1f9_76 = (long)(f1_2) * (long)(f9_38);
            long f2f2 = (long)(f2) * (long)(f2);
            long f2f3_2 = (long)(f2_2) * (long)(f3);
            long f2f4_2 = (long)(f2_2) * (long)(f4);
            long f2f5_2 = (long)(f2_2) * (long)(f5);
            long f2f6_2 = (long)(f2_2) * (long)(f6);
            long f2f7_2 = (long)(f2_2) * (long)(f7);
            long f2f8_38 = (long)(f2_2) * (long)(f8_19);
            long f2f9_38 = (long)(f2) * (long)(f9_38);
            long f3f3_2 = (long)(f3_2) * (long)(f3);
            long f3f4_2 = (long)(f3_2) * (long)(f4);
            long f3f5_4 = (long)(f3_2) * (long)(f5_2);
            long f3f6_2 = (long)(f3_2) * (long)(f6);
            long f3f7_76 = (long)(f3_2) * (long)(f7_38);
            long f3f8_38 = (long)(f3_2) * (long)(f8_19);
            long f3f9_76 = (long)(f3_2) * (long)(f9_38);
            long f4f4 = (long)(f4) * (long)(f4);
            long f4f5_2 = (long)(f4_2) * (long)(f5);
            long f4f6_38 = (long)(f4_2) * (long)(f6_19);
            long f4f7_38 = (long)(f4) * (long)(f7_38);
            long f4f8_38 = (long)(f4_2) * (long)(f8_19);
            long f4f9_38 = (long)(f4) * (long)(f9_38);
            long f5f5_38 = (long)(f5) * (long)(f5_38);
            long f5f6_38 = (long)(f5_2) * (long)(f6_19);
            long f5f7_76 = (long)(f5_2) * (long)(f7_38);
            long f5f8_38 = (long)(f5_2) * (long)(f8_19);
            long f5f9_76 = (long)(f5_2) * (long)(f9_38);
            long f6f6_19 = (long)(f6) * (long)(f6_19);
            long f6f7_38 = (long)(f6) * (long)(f7_38);
            long f6f8_38 = (long)(f6_2) * (long)(f8_19);
            long f6f9_38 = (long)(f6) * (long)(f9_38);
            long f7f7_38 = (long)(f7) * (long)(f7_38);
            long f7f8_38 = (long)(f7_2) * (long)(f8_19);
            long f7f9_76 = (long)(f7_2) * (long)(f9_38);
            long f8f8_19 = (long)(f8) * (long)(f8_19);
            long f8f9_38 = (long)(f8) * (long)(f9_38);
            long f9f9_38 = (long)(f9) * (long)(f9_38);
            long h0 = f0f0 + f1f9_76 + f2f8_38 + f3f7_76 + f4f6_38 + f5f5_38;
            long h1 = f0f1_2 + f2f9_38 + f3f8_38 + f4f7_38 + f5f6_38;
            long h2 = f0f2_2 + f1f1_2 + f3f9_76 + f4f8_38 + f5f7_76 + f6f6_19;
            long h3 = f0f3_2 + f1f2_2 + f4f9_38 + f5f8_38 + f6f7_38;
            long h4 = f0f4_2 + f1f3_4 + f2f2 + f5f9_76 + f6f8_38 + f7f7_38;
            long h5 = f0f5_2 + f1f4_2 + f2f3_2 + f6f9_38 + f7f8_38;
            long h6 = f0f6_2 + f1f5_4 + f2f4_2 + f3f3_2 + f7f9_76 + f8f8_19;
            long h7 = f0f7_2 + f1f6_2 + f2f5_2 + f3f4_2 + f8f9_38;
            long h8 = f0f8_2 + f1f7_4 + f2f6_2 + f3f5_4 + f4f4 + f9f9_38;
            long h9 = f0f9_2 + f1f8_2 + f2f7_2 + f3f6_2 + f4f5_2;
            var carry = new long[10];

            carry[0] = (h0 + (1 << 25)) >> 26;
            h1 += carry[0];
            h0 -= carry[0] << 26;
            carry[4] = (h4 + (1 << 25)) >> 26;
            h5 += carry[4];
            h4 -= carry[4] << 26;

            carry[1] = (h1 + (1 << 24)) >> 25;
            h2 += carry[1];
            h1 -= carry[1] << 25;
            carry[5] = (h5 + (1 << 24)) >> 25;
            h6 += carry[5];
            h5 -= carry[5] << 25;

            carry[2] = (h2 + (1 << 25)) >> 26;
            h3 += carry[2];
            h2 -= carry[2] << 26;
            carry[6] = (h6 + (1 << 25)) >> 26;
            h7 += carry[6];
            h6 -= carry[6] << 26;

            carry[3] = (h3 + (1 << 24)) >> 25;
            h4 += carry[3];
            h3 -= carry[3] << 25;
            carry[7] = (h7 + (1 << 24)) >> 25;
            h8 += carry[7];
            h7 -= carry[7] << 25;

            carry[4] = (h4 + (1 << 25)) >> 26;
            h5 += carry[4];
            h4 -= carry[4] << 26;
            carry[8] = (h8 + (1 << 25)) >> 26;
            h9 += carry[8];
            h8 -= carry[8] << 26;

            carry[9] = (h9 + (1 << 24)) >> 25;
            h0 += carry[9] * 19;
            h9 -= carry[9] << 25;

            carry[0] = (h0 + (1 << 25)) >> 26;
            h1 += carry[0];
            h0 -= carry[0] << 26;

            var final = new FieldElement();
            var h = new int[10];
            h[0] = (int)(h0);
            h[1] = (int)(h1);
            h[2] = (int)(h2);
            h[3] = (int)(h3);
            h[4] = (int)(h4);
            h[5] = (int)(h5);
            h[6] = (int)(h6);
            h[7] = (int)(h7);
            h[8] = (int)(h8);
            h[9] = (int)(h9);
            final.SetElementsDirect(h);
            return final;
        }
        /// <summary>
        /// Calculates h = f * 121666. Can overlap h with f; I have no clue why this is a thing
        /// </summary>
        /// <remarks>
        /// <para>
        ///   Preconditions:
        /// |f| bounded by 1.1*2^26,1.1*2^25,1.1*2^26,1.1*2^25,etc.
        /// </para>
        /// <para>
        /// Postconditions:
        /// |h| bounded by 1.1*2^25,1.1*2^24,1.1*2^25,1.1*2^24,etc.
        /// </para>
        /// </remarks>
        public FieldElement Mul121666()
        {
            long h0 = (long)(_elements[0]) * 121666;
            long h1 = (long)(_elements[1]) * 121666;
            long h2 = (long)(_elements[2]) * 121666;
            long h3 = (long)(_elements[3]) * 121666;
            long h4 = (long)(_elements[4]) * 121666;
            long h5 = (long)(_elements[5]) * 121666;
            long h6 = (long)(_elements[6]) * 121666;
            long h7 = (long)(_elements[7]) * 121666;
            long h8 = (long)(_elements[8]) * 121666;
            long h9 = (long)(_elements[9]) * 121666;
            var carry = new long[10];

            carry[9] = (h9 + (1 << 24)) >> 25;
            h0 += carry[9] * 19;
            h9 -= carry[9] << 25;
            carry[1] = (h1 + (1 << 24)) >> 25;
            h2 += carry[1];
            h1 -= carry[1] << 25;
            carry[3] = (h3 + (1 << 24)) >> 25;
            h4 += carry[3];
            h3 -= carry[3] << 25;
            carry[5] = (h5 + (1 << 24)) >> 25;
            h6 += carry[5];
            h5 -= carry[5] << 25;
            carry[7] = (h7 + (1 << 24)) >> 25;
            h8 += carry[7];
            h7 -= carry[7] << 25;

            carry[0] = (h0 + (1 << 25)) >> 26;
            h1 += carry[0];
            h0 -= carry[0] << 26;
            carry[2] = (h2 + (1 << 25)) >> 26;
            h3 += carry[2];
            h2 -= carry[2] << 26;
            carry[4] = (h4 + (1 << 25)) >> 26;
            h5 += carry[4];
            h4 -= carry[4] << 26;
            carry[6] = (h6 + (1 << 25)) >> 26;
            h7 += carry[6];
            h6 -= carry[6] << 26;
            carry[8] = (h8 + (1 << 25)) >> 26;
            h9 += carry[8];
            h8 -= carry[8] << 26;

            var final = new FieldElement();
            var h = new int[10];
            h[0] = (int)(h0);
            h[1] = (int)(h1);
            h[2] = (int)(h2);
            h[3] = (int)(h3);
            h[4] = (int)(h4);
            h[5] = (int)(h5);
            h[6] = (int)(h6);
            h[7] = (int)(h7);
            h[8] = (int)(h8);
            h[9] = (int)(h9);
            final.SetElementsDirect(h);
            return final;
        }
        /// <summary>
        /// Calculates this ^(-1)
        /// </summary>
        ///
        ///
        public FieldElement Invert()
        {
            FieldElement t0, t1, t2, t3;
            int i;

            t0 = Square();
            for (i = 1; i < 1; i++) // why is this a thing?
            {
                t0 = t0.Square();
            }

            t1 = t0.Square();
            for (i = 1; i < 2; i++)
            {
                t1 = t1.Square();
            }

            t1 = Multiply(t1);
            t0 = t0.Multiply(t1);
            t2 = t0.Square();
            for (i = 1; i < 1; i++)
            { // what the fuck
                t2 = t2.Square();
            }

            t1 = t1.Multiply(t2);
            t2 = t1.Square();
            for (i = 1; i < 5; i++)
            {
                t2 = t2.Square();
            }

            t1 = t2.Multiply(t1);
            t2 = t1.Square();
            for (i = 1; i < 10; i++)
            {
                t2 = t2.Square();
            }

            t2 = t2.Multiply(t1);
            t3 = t2.Square();
            for (i = 1; i < 20; i++)
            {
                t3 = t3.Square();
            }

            t2 = t3.Multiply(t2);
            t2 = t2.Square();
            for (i = 1; i < 10; i++)
            {
                t2 = t2.Square();
            }

            t1 = t2.Multiply(t1);
            t2 = t1.Square();
            for (i = 1; i < 50; i++)
            {
                t2 = t2.Square();
            }

            t2 = t2.Multiply(t1);
            t3 = t2.Square();
            for (i = 1; i < 100; i++)
            {
                t3 = t3.Square();
            }

            t2 = t3.Multiply(t2);
            t2 = t2.Square();
            for (i = 1; i < 50; i++)
            {
                t2 = t2.Square();
            }

            t1 = t2.Multiply(t1);
            t1 = t1.Square();
            for (i = 1; i < 5; i++)
            {
                t1 = t1.Square();
            }

            return t1.Multiply(t0);
        }

        /// <summary>
        /// Directly gets or sets the _elements
        /// </summary>
        /// <param name="i">The index. Note that the max value is always 10</param>
        public int this[int i]
        {
            get => _elements[i];
            set => _elements[i] = value;
        }
        /// <summary>
        /// Sets all values of field to zero except that Element[0] = 1
        /// </summary>
        public void One()
        {
            for (int i = 1; i < 10; i++)
                _elements[i] = 0;
            _elements[0] = 1;
        }

        public static FieldElement operator +(FieldElement f1, FieldElement f2)
        {
            var res = new FieldElement();
            for (int i = 0; i < 10; i++)
                res[i] = f1[i] + f2[i];
            return res;
        }

        public static FieldElement operator -(FieldElement f1, FieldElement f2)
        {
            var res = new FieldElement();
            for (int i = 0; i < 10; i++)
                res[i] = f1[i] - f2[i];
            return res;
        }

        /// <summary>
        /// Replaces (f,g) with (g,f) if b == 1; replaces (f,g) with (f,g) if b == 0.
        /// </summary>
        /// <param name="f">f</param>
        /// <param name="g">g</param>
        /// <param name="b"></param>
        public static void CSwap(ref FieldElement f, ref FieldElement g, int b)
        {
            b = -b;
            for (int i = 0; i < 10; i++)
            {
                int t = b & (f[i] ^ g[i]);
                f[i] ^= t;
                g[i] ^= t;
            }
        }
        /// <summary>
        /// Copies one FieldElement to another FieldElement
        /// </summary>
        /// <param name="dst">Where the src must be copied to</param>
        /// <param name="src">What to copy</param>
        public static void Copy(ref FieldElement dst, FieldElement src)
        {
            for (int i = 0; i < 10; i++)
                dst[i] = src[i];
        }

        /// <summary>
        /// load3 reads a 24-bit, little-endian value from in
        /// </summary>
        /// <param name="bytes">Input</param>
#if NET45_OR_GREATER || NETCOREAPP || NETSTANDARD
        private static long Load3(IReadOnlyList<byte> bytes)
#else
        private static long Load3(byte[] bytes)
#endif
        {
            long r = bytes[0];
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
            r |= bytes[1] << 8;
            r |= bytes[2] << 16;
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand
            return r;
        }

        /// <summary>
        /// load4 reads a 32-bit, little-endian value from in.
        /// </summary>
        /// <param name="bytes"></param>
#if NET45_OR_GREATER || NETCOREAPP || NETSTANDARD
        private static long Load4(IReadOnlyList<byte> bytes)
#else
        private static long Load4(byte[] bytes)
#endif
        {
            // notice this is acted like an UINT NOT LONG
            return (bytes[0]) | (uint)(bytes[1]) << 8 | (uint)(bytes[2]) << 16 | (uint)(bytes[3]) << 24;
        }
        /// <summary>
        /// Generates a slice of array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data">Data to get slice from</param>
        /// <param name="index">The start index</param>
        /// <param name="length">The length of sub array</param>
        private static T[] SubArray<T>(T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }
    }
}
#endif