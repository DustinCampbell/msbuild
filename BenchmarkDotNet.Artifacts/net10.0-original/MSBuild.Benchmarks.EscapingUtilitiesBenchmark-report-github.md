```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2


```
| Method                                       | Mean       | Error     | StdDev    | Gen0   | Allocated |
|--------------------------------------------- |-----------:|----------:|----------:|-------:|----------:|
| Escape_SimpleString                          |  25.590 ns | 0.0930 ns | 0.0824 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 252.071 ns | 1.1441 ns | 1.0702 ns | 0.0749 |     472 B |
| Unescape_SimpleString                        |   7.542 ns | 0.0874 ns | 0.0774 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 197.537 ns | 1.6612 ns | 1.4726 ns | 0.0648 |     408 B |
