using System;
using System.Linq;
using System.Numerics;

using BenchmarkDotNet.Attributes;

namespace SimdOperations
{
    [MemoryDiagnoser]
    public class SumOperation
    {
        private readonly int[] Array;
        private const int Cnt = 4096;

        public SumOperation()
        {
            Array = new int[Cnt];
            for (var a = 0; a < Cnt; ++a)
                Array[a] = Random.Shared.Next(0, 10000);
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
            var result = Array.Sum();
        }

        [Benchmark]
        public void SumParallel()
        {
            var result = Array.AsParallel().WithDegreeOfParallelism(4).Sum();
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
    }
}
