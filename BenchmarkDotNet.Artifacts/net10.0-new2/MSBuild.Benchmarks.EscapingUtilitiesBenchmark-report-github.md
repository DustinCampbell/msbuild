```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2


```
| Method                                       | Mean       | Error     | StdDev    | Gen0   | Allocated |
|--------------------------------------------- |-----------:|----------:|----------:|-------:|----------:|
| Escape_SimpleString                          |  14.573 ns | 0.0605 ns | 0.0566 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 128.003 ns | 1.1117 ns | 0.9855 ns | 0.0751 |     472 B |
| Unescape_SimpleString                        |   7.057 ns | 0.0278 ns | 0.0247 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 179.740 ns | 0.8127 ns | 0.6787 ns | 0.0648 |     408 B |
