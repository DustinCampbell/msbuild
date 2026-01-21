```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2


```
| Method                                       | Mean       | Error     | StdDev    | Gen0   | Allocated |
|--------------------------------------------- |-----------:|----------:|----------:|-------:|----------:|
| Escape_SimpleString                          |  18.353 ns | 0.0892 ns | 0.0791 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 191.396 ns | 1.3603 ns | 1.2724 ns | 0.0751 |     472 B |
| Unescape_SimpleString                        |   9.952 ns | 0.0632 ns | 0.0591 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 175.143 ns | 1.0740 ns | 1.0046 ns | 0.0648 |     408 B |
