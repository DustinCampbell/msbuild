```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2


```
| Method                                       | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------------------------- |----------:|---------:|---------:|-------:|----------:|
| Escape_SimpleString                          |  15.88 ns | 0.062 ns | 0.055 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 126.39 ns | 0.781 ns | 0.693 ns | 0.0751 |     472 B |
| Unescape_SimpleString                        |  12.23 ns | 0.041 ns | 0.036 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 215.62 ns | 2.689 ns | 2.384 ns | 0.0648 |     408 B |
