```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2


```
| Method                                       | Mean       | Error     | StdDev    | Gen0   | Allocated |
|--------------------------------------------- |-----------:|----------:|----------:|-------:|----------:|
| Escape_SimpleString                          |   9.314 ns | 0.1712 ns | 0.1602 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 121.000 ns | 1.0599 ns | 0.9915 ns | 0.0751 |     472 B |
| Unescape_SimpleString                        |   6.289 ns | 0.0379 ns | 0.0336 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 231.058 ns | 3.2760 ns | 2.5577 ns | 0.0648 |     408 B |
