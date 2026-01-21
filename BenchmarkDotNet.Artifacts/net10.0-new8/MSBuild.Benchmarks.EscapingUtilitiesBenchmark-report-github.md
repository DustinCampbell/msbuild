```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2


```
| Method                                       | Mean       | Error     | StdDev    | Gen0   | Allocated |
|--------------------------------------------- |-----------:|----------:|----------:|-------:|----------:|
| Escape_SimpleString                          |  11.546 ns | 0.0410 ns | 0.0384 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 127.976 ns | 0.5681 ns | 0.5314 ns | 0.0751 |     472 B |
| Unescape_SimpleString                        |   6.305 ns | 0.0391 ns | 0.0366 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 183.757 ns | 0.9309 ns | 0.8252 ns | 0.0648 |     408 B |
