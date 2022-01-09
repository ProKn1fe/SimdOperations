using System;
using System.Linq;
using System.Numerics;

using BenchmarkDotNet.Attributes;

namespace SimdOperations
{
    [MemoryDiagnoser]
    public class CompareOperation
    {
        private readonly int[] Array1;
        private readonly int[] Array2;
        private readonly int[] Array3;
        private const int Cnt = 8192 * 8192;

        public CompareOperation()
        {
            Array1 = new int[Cnt];
            Array2 = new int[Cnt];
            Array3 = new int[Cnt];
            for (var a = 0; a < Cnt; ++a)
            {
                var val = Random.Shared.Next();
                Array1[a] = val;
                Array2[a] = val;
                Array3[a] = val;
            }
            Array3[Cnt - 1] = -Array3[Cnt - 1];
        }

        [Benchmark]
        public void CompareForSuccess()
        {
            _ = CompareFor(Array1, Array2);
        }

        [Benchmark]
        public void CompareForFail()
        {
            _ = CompareFor(Array1, Array3);
        }

        [Benchmark]
        public void CompareSequenceEqualSuccess()
        {
            _ = CompareSequenceEqual(Array1, Array2);
        }

        [Benchmark]
        public void CompareSequenceEqualFail()
        {
            _ = CompareSequenceEqual(Array1, Array3);
        }

        [Benchmark]
        public void CompareSequenceEqualParallelSuccess()
        {
            _ = CompareSequenceEqualParallel(Array1, Array2);
        }

        [Benchmark]
        public void CompareSequenceEqualParallelFail()
        {
            _ = CompareSequenceEqualParallel(Array1, Array3);
        }

        [Benchmark]
        public void CompareSimdSuccess()
        {
            _ = CompareSimd(Array1, Array2);
        }

        [Benchmark]
        public void CompareSimdFail()
        {
            _ = CompareSimd(Array1, Array3);
        }

        private bool CompareFor(int[] array1, int[] array2)
        {
            for (var a = 0; a < Cnt; ++a)
            {
                if (array1[a] != array2[a])
                    return false;
            }
            return true;
        }

        private bool CompareSequenceEqual(int[] array1, int[] array2)
        {
            return Enumerable.SequenceEqual(array1, array2);
        }

        private bool CompareSequenceEqualParallel(int[] array1, int[] array2)
        {
            return array1.AsParallel().WithDegreeOfParallelism(4).SequenceEqual(array2.AsParallel());
        }

        private bool CompareSimd(int[] array1, int[] array2)
        {
            var vectorCount = Vector<int>.Count;
            var arrayLength = array1.Length;

            for (var a = 0; a < arrayLength; a += vectorCount)
            {
                var vector1 = new Vector<int>(array1, a);
                var vector2 = new Vector<int>(array2, a);
                if (!Vector.EqualsAll(vector1, vector2))
                    return false;
            }

            return true;
        }
    }
}
