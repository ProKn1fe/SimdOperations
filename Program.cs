using System;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

#if RELEASE
using BenchmarkDotNet.Running;
#endif

namespace SimdOperations
{
    public class Program
    {
        public static void Main()
        {
#if RELEASE
            BenchmarkRunner.Run<SumOperation>();
            //BenchmarkRunner.Run<CompareOperation>();
            //BenchmarkRunner.Run<MinOperation>();
            //BenchmarkRunner.Run<MaxOperation>();
#else
            Console.WriteLine($"Vector<T>: {Vector.IsHardwareAccelerated}");
            Console.WriteLine($"Aes: {Aes.IsSupported}");
            Console.WriteLine($"Avx: {Avx.IsSupported}");
            Console.WriteLine($"Avx2: {Avx2.IsSupported}");
            Console.WriteLine($"Bmi1: {Bmi1.IsSupported}");
            Console.WriteLine($"Bmi1.X64: {Bmi1.X64.IsSupported}");
            Console.WriteLine($"Bmi2: {Bmi2.IsSupported}");
            Console.WriteLine($"Bmi2.X64: {Bmi2.X64.IsSupported}");
            Console.WriteLine($"Fma: {Fma.IsSupported}");
            Console.WriteLine($"Lzcnt: {Lzcnt.IsSupported}");
            Console.WriteLine($"Lzcnt.X64: {Lzcnt.X64.IsSupported}");

            var a = new int[] { 10, 20, 30, 40, 50, 60, 70, 80 };
            var span = new ReadOnlySpan<int>(a);
            var ctor = new Vector<int>(span);
            Console.WriteLine(span[5]);
            a[5] = 1000;
            Console.WriteLine(span[5]);
            var vc = Vector256<int>.Count;

            //var res = new MaxOperation().SimdMax(new int[] { 10, 32, 14, 55, 43, 76, 42, 27, 2, 31, 24, 11, 44, 12, 58, 64 });
            var sum = new SumOperation();
            sum.SumSse42Vector128();
            sum.SumAvx2Vector256();
            Console.ReadLine();
#endif
        }
    }
}