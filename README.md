Remarks:
* All Parallel operations run as AsParallel().WithDegreeOfParallelism(4)

TODO:
* Simd version of X25519 cryptography
* Min and max with IEnumerable<T>
* Something with avx?

Changelog:
* 09.01.2021 - Adding SumSse42Vector128 and SumAvx2Vector256 benchmarks.

#### SumOperation

TODO: Why linq sum so slow?

``` ini
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19044.1415 (21H2)
Intel Core i7-4910MQ CPU 2.90GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.200-preview.21617.4
  [Host]     : .NET 6.0.1 (6.0.121.56705), X64 RyuJIT
  DefaultJob : .NET 6.0.1 (6.0.121.56705), X64 RyuJIT
```

|            Method |        Mean |     Error |    StdDev | Allocated |
|------------------ |------------:|----------:|----------:|----------:|
|            SumFor |   306.33 us |  2.769 us |  2.590 us |         - |
|        SumForeach |   203.02 us |  1.266 us |  1.122 us |         - |
|           SumLinq | 2,145.04 us | 40.648 us | 38.022 us |      34 B |
|       SumParallel |   857.53 us | 16.753 us | 27.990 us |   3,752 B |
|           SumSimd |    74.20 us |  0.660 us |  0.617 us |         - |
| SumSse42Vector128 |   114.05 us |  1.569 us |  1.467 us |         - |
|  SumAvx2Vector256 |    67.42 us |  1.270 us |  1.247 us |         - |

#### CompareOperation

``` ini
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19044.1415 (21H2)
Intel Core i7-4910MQ CPU 2.90GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.200-preview.21617.4
  [Host]     : .NET 6.0.1 (6.0.121.56705), X64 RyuJIT
  DefaultJob : .NET 6.0.1 (6.0.121.56705), X64 RyuJIT
```

|                              Method |         Mean |      Error |       StdDev |  Gen 0 |  Gen 1 | Allocated |
|------------------------------------ |-------------:|-----------:|-------------:|-------:|-------:|----------:|
|                   CompareForSuccess | 42,874.55 us | 383.164 us |   319.960 us |      - |      - |      57 B |
|                      CompareForFail | 43,178.77 us | 848.900 us | 1,010.555 us |      - |      - |      40 B |
|         CompareSequenceEqualSuccess | 33,829.86 us | 268.495 us |   298.432 us |      - |      - |      34 B |
|            CompareSequenceEqualFail | 33,327.22 us | 226.438 us |   200.731 us |      - |      - |      32 B |
| CompareSequenceEqualParallelSuccess |     12.81 us |   0.247 us |     0.294 us | 6.6833 |      - |  27,009 B |
|    CompareSequenceEqualParallelFail |     13.19 us |   0.260 us |     0.405 us | 6.7596 | 0.0305 |  27,245 B |
|                  CompareSimdSuccess | 38,048.60 us | 217.637 us |   169.917 us |      - |      - |      34 B |
|                     CompareSimdFail | 37,401.66 us | 165.255 us |   146.494 us |      - |      - |      34 B |

#### MinOperation

TODO: It run fast only with arrays[], IEnumerable<T> performance are slow.

``` ini
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19044.1415 (21H2)
Intel Core i7-4910MQ CPU 2.90GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.200-preview.21617.4
  [Host]     : .NET 6.0.1 (6.0.121.56705), X64 RyuJIT
  DefaultJob : .NET 6.0.1 (6.0.121.56705), X64 RyuJIT
```

|          Method |      Mean |    Error |   StdDev | Allocated |
|---------------- |----------:|---------:|---------:|----------:|
|         LinqMin | 308.17 ms | 2.851 ms | 2.381 ms |     640 B |
| ParallelLinqMin |  70.38 ms | 1.745 ms | 5.062 ms |   3,912 B |
|         SimdMin |  20.75 ms | 0.088 ms | 0.082 ms |      16 B |

#### MaxOperation

``` ini
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19044.1415 (21H2)
Intel Core i7-4910MQ CPU 2.90GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.200-preview.21617.4
  [Host]     : .NET 6.0.1 (6.0.121.56705), X64 RyuJIT
  DefaultJob : .NET 6.0.1 (6.0.121.56705), X64 RyuJIT
```

|          Method |      Mean |    Error |    StdDev |    Median | Allocated |
|---------------- |----------:|---------:|----------:|----------:|----------:|
|         LinqMax | 345.30 ms | 6.687 ms |  7.433 ms | 342.50 ms |     752 B |
| ParallelLinqMax |  74.43 ms | 3.701 ms | 10.738 ms |  70.77 ms |   3,933 B |
|         SimdMax |  20.72 ms | 0.132 ms |  0.117 ms |  20.74 ms |      16 B |




