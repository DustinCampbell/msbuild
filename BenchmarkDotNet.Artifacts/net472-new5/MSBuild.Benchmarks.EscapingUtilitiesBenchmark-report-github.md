```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT
  DefaultJob : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT


```
| Method                                       | Mean     | Error   | StdDev  | Gen0   | Allocated |
|--------------------------------------------- |---------:|--------:|--------:|-------:|----------:|
| Escape_SimpleString                          | 174.5 ns | 0.87 ns | 0.77 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 343.6 ns | 2.01 ns | 1.78 ns | 0.0882 |     465 B |
| Unescape_SimpleString                        | 144.7 ns | 0.41 ns | 0.34 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 619.9 ns | 2.89 ns | 2.57 ns | 0.0763 |     401 B |
