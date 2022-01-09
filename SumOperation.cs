using System;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

using BenchmarkDotNet.Attributes;

namespace SimdOperations
{
    [MemoryDiagnoser]
    public class SumOperation
    {
        private readonly int[] Array;
        private const int Cnt = 4096 * 128;

        public SumOperation()
        {
            Array = new int[Cnt];
            for (var a = 0; a < Cnt; ++a)
                Array[a] = Random.Shared.Next(0, 1000);
        }

        [Benchmark]
        public void SumFor()
        {
            var result = 0;
            for (var a = 0; a < Cnt; ++a)
                result += Array[a];
        }

        [Benchmark]
        public void SumForeach()
        {
            var result = 0;
            foreach (var a in Array)
                result += a;
        }

        [Benchmark]
        public void SumLinq()
        {
            _ = Array.Sum();
        }

        [Benchmark]
        public void SumParallel()
        {
            _ = Array.AsParallel().WithDegreeOfParallelism(4).Sum();
        }

        [Benchmark]
        public void SumSimd()
        {
            var result = 0;
            var vectorCount = Vector<int>.Count;
            var vector = new Vector<int>(Array, 0);

            for (var a = vectorCount; a < Array.Length; a += vectorCount)
            {
                var plusVector = new Vector<int>(Array, a);
                vector = Vector.Add(plusVector, vector);
            }
            for (var a = 0; a < vectorCount; ++a)
                result += vector[a];
        }

        [Benchmark]
        public void SumSse42Vector128()
        {
            var result = 0;
            var vectorCount = Vector128<int>.Count;
            Vector128<int> vector;

            unsafe
            {
                fixed (int* ptr = &Array[0])
                    vector = Sse2.LoadVector128(ptr);
                for (var a = vectorCount; a < Array.Length; a += vectorCount)
                {
                    fixed (int *ptr = &Array[a])
                    {
                        var plusVector = Sse2.LoadVector128(ptr);
                        vector = Sse2.Add(plusVector, vector);
                    }
                }
            }

            for (var a = 0; a < vectorCount; ++a)
                result += vector.GetElement(a);
        }

        [Benchmark]
        public void SumAvx2Vector256()
        {
            var result = 0;
            var vectorCount = Vector256<int>.Count;
            Vector256<int> vector;

            unsafe
            {
                fixed (int* ptr = &Array[0])
                    vector = Avx.LoadVector256(ptr);
                for (var a = vectorCount; a < Array.Length; a += vectorCount)
                {
                    fixed (int* ptr = &Array[a])
                    {
                        var plusVector = Avx.LoadVector256(ptr);
                        vector = Avx2.Add(plusVector, vector);
                    }
                }
            }

            for (var a = 0; a < vectorCount; ++a)
                result += vector.GetElement(a);
        }
    }
}
