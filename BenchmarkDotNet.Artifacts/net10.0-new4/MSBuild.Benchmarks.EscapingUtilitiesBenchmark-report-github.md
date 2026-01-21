```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2


```
| Method                                       | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------------------------- |----------:|---------:|---------:|-------:|----------:|
| Escape_SimpleString                          |  15.95 ns | 0.048 ns | 0.042 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 129.64 ns | 1.908 ns | 1.691 ns | 0.0751 |     472 B |
| Unescape_SimpleString                        |  12.13 ns | 0.063 ns | 0.059 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 222.69 ns | 1.258 ns | 1.115 ns | 0.0648 |     408 B |
