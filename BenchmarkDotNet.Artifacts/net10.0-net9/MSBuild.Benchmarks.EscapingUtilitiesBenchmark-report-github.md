```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2


```
| Method                                       | Mean       | Error     | StdDev    | Gen0   | Allocated |
|--------------------------------------------- |-----------:|----------:|----------:|-------:|----------:|
| Escape_SimpleString                          |  11.945 ns | 0.0947 ns | 0.0840 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 123.789 ns | 0.6018 ns | 0.5335 ns | 0.0751 |     472 B |
| Unescape_SimpleString                        |   5.983 ns | 0.0231 ns | 0.0205 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 186.300 ns | 1.1028 ns | 0.9776 ns | 0.0648 |     408 B |
