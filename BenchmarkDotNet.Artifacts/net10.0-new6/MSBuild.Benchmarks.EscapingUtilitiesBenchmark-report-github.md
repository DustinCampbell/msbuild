```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2


```
| Method                                       | Mean       | Error     | StdDev    | Gen0   | Allocated |
|--------------------------------------------- |-----------:|----------:|----------:|-------:|----------:|
| Escape_SimpleString                          |  12.381 ns | 0.0230 ns | 0.0204 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 122.665 ns | 0.8584 ns | 0.8030 ns | 0.0751 |     472 B |
| Unescape_SimpleString                        |   5.913 ns | 0.0643 ns | 0.0602 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 230.765 ns | 1.4216 ns | 1.2602 ns | 0.0648 |     408 B |
