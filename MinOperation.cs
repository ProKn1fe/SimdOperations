using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

namespace SimdOperations
{
    [MemoryDiagnoser]
    public class MinOperation
    {
        private readonly int[] Array;
        private const int Cnt = 8192 * 8192;

        public MinOperation()
        {
            Array = new int[Cnt];
            for (var a = 0; a < Cnt; ++a)
                Array[a] = Random.Shared.Next();
        }

        [Benchmark]
        public void LinqMin()
        {
            var _ = Array.Min();
        }

        [Benchmark]
        public void ParallelLinqMin()
        {
            var _ = Array.AsParallel().WithDegreeOfParallelism(4).Min();
        }

        [Benchmark]
        public void SimdMin()
        {
            var _ = SimdMin(Array);
        }

        public T SimdMin<T>(T[] source) where T : struct
        {
            var vectorCount = Vector<T>.Count;
            var count = source.Length;
            if (count >= vectorCount)
            {
                var minVector = new Vector<T>(source, 0);
                for (var a = vectorCount; a < count; a += vectorCount)
                {
                    var v = new Vector<T>(source, a);
                    minVector = Vector.Min(v, minVector);
                }
                T min = minVector[0];
                for (var a = 1; a < vectorCount; ++a)
                {
                    if (ScalarLessThan(minVector[a], min))
                        min = minVector[a];
                }
                return min;
            }
            else
            {
                return source.Min();
            }
        }

        public T SimdMin<T>(IEnumerable<T> source) where T : struct
        {
            var vectorCount = Vector<T>.Count;
            var array = new T[vectorCount];
            var index = vectorCount;
            if (!source.TryGetNonEnumeratedCount(out int count))
                count = source.Count();
            using var enumerator = source.GetEnumerator();
            if (count >= vectorCount)
            {
                FillArray();
                var minVector = new Vector<T>(array);
                for (var a = vectorCount; a < count; a += vectorCount)
                {
                    FillArray();
                    var v = new Vector<T>(array);
                    minVector = Vector.Min(v, minVector);
                }
                T min = minVector[0];
                for (var a = 1; a < vectorCount; ++a)
                {
                    if (ScalarLessThan(minVector[a], min))
                        min = minVector[a];
                }
                return min;
            }
            else
            {
                return source.Min();
            }

            void FillArray()
            {
                var can = count - index;
                if (can > vectorCount)
                    can = vectorCount;
                for (var a = 0; a < can; ++a)
                {
                    enumerator.MoveNext();
                    array[a] = enumerator.Current;
                }
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

    public class PreloadEnumerator<T> : IEnumerator<T>
    {
        public T Current
        {
            get
            {
                var item = Preload[PreloadIndex];
                PreloadIndex++;
                if (PreloadIndex > PreloadSize)
                    PreloadIndex = 0;
                return item;
            }
        }

        object System.Collections.IEnumerator.Current => Current;

        private readonly IEnumerator<T> Enumerator;
        private readonly object Lock = new();
        private T[] Preload { get; }
        private int PreloadIndex { get; set; }
        private int PreloadSize { get; }

        public PreloadEnumerator(IEnumerator<T> enumerator, int preloadSize = 4096)
        {
            Enumerator = enumerator;
            Preload = new T[preloadSize];
            PreloadSize = preloadSize;

            for (var a = 0; a < preloadSize; ++a)
            {
                if (Enumerator.MoveNext())
                    Preload[a] = Enumerator.Current;
            }

            Task.Factory.StartNew(PreloadTask);
        }

        public void Dispose() { }

        public bool MoveNext()
        {
            return PreloadIndex < Preload.Length;
        }

        public void Reset()
        {
            Enumerator.Reset();
        }

        private async void PreloadTask()
        {
            while (true)
            {
                var need = false;
                var count = 0;
                lock (Lock)
                {
                    need = PreloadIndex > PreloadSize / 2;
                    count = PreloadSize - PreloadIndex;
                }
                if (need)
                {
                    for (var a = 0; a < count; ++a)
                    {
                        if (Enumerator.MoveNext())
                            Preload[a] = Enumerator.Current;
                    }
                }
                await Task.Delay(10);
            }
        }
    }
}
