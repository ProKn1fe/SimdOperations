using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

namespace SimdOperations
{
    [MemoryDiagnoser]
    public class MaxOperation
    {
        private readonly int[] Array;
        private const int Cnt = 8192 * 8192;

        public MaxOperation()
        {
            Array = new int[Cnt];
            for (var a = 0; a < Cnt; ++a)
                Array[a] = Random.Shared.Next();
        }

        [Benchmark]
        public void LinqMax()
        {
            var _ = Array.Max();
        }

        [Benchmark]
        public void ParallelLinqMax()
        {
            var _ = Array.AsParallel().WithDegreeOfParallelism(4).Max();
        }

        [Benchmark]
        public void SimdMax()
        {
            var _ = SimdMax(Array);
        }

        public T SimdMax<T>(T[] source) where T : struct
        {
            var vectorCount = Vector<T>.Count;
            var count = source.Length;
            if (count >= vectorCount)
            {
                var maxVector = new Vector<T>(source, 0);
                for (var a = vectorCount; a < count; a += vectorCount)
                {
                    var v = new Vector<T>(source, a);
                    maxVector = Vector.Max(v, maxVector);
                }
                T max = maxVector[0];
                for (var a = 1; a < vectorCount; ++a)
                {
                    if (!ScalarLessThan(maxVector[a], max))
                        max = maxVector[a];
                }
                return max;
            }
            else
            {
                return source.Max();
            }
        }

        // I'm found this inside Vector<T> sources from System.Numerics.Vector`1.ScalarLessThan
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ScalarLessThan<T>(T left, T right)
        {
            if (typeof(T) == typeof(byte))
            {
                return (byte)(object)left < (byte)(object)right;
            }
            if (typeof(T) == typeof(sbyte))
            {
                return (sbyte)(object)left < (sbyte)(object)right;
            }
            if (typeof(T) == typeof(ushort))
            {
                return (ushort)(object)left < (ushort)(object)right;
            }
            if (typeof(T) == typeof(short))
            {
                return (short)(object)left < (short)(object)right;
            }
            if (typeof(T) == typeof(uint))
            {
                return (uint)(object)left < (uint)(object)right;
            }
            if (typeof(T) == typeof(int))
            {
                return (int)(object)left < (int)(object)right;
            }
            if (typeof(T) == typeof(ulong))
            {
                return (ulong)(object)left < (ulong)(object)right;
            }
            if (typeof(T) == typeof(long))
            {
                return (long)(object)left < (long)(object)right;
            }
            if (typeof(T) == typeof(float))
            {
                return (float)(object)left < (float)(object)right;
            }
            if (typeof(T) == typeof(double))
            {
                return (double)(object)left < (double)(object)right;
            }
            if (typeof(T) == typeof(UIntPtr))
            {
                return (UIntPtr)(object)left < (nuint)(UIntPtr)(object)right;
            }
            if (typeof(T) == typeof(IntPtr))
            {
                return (IntPtr)(object)left < (nint)(IntPtr)(object)right;
            }
            throw new NotSupportedException("This type is not supported");
        }
    }
}
