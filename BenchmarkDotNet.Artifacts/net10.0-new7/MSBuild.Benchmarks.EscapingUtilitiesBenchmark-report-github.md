```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2


```
| Method                                       | Mean       | Error     | StdDev    | Gen0   | Allocated |
|--------------------------------------------- |-----------:|----------:|----------:|-------:|----------:|
| Escape_SimpleString                          |  11.910 ns | 0.0249 ns | 0.0195 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 123.893 ns | 0.9630 ns | 0.7518 ns | 0.0751 |     472 B |
| Unescape_SimpleString                        |   6.358 ns | 0.0523 ns | 0.0464 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 330.867 ns | 1.2844 ns | 1.1386 ns | 0.0648 |     408 B |
