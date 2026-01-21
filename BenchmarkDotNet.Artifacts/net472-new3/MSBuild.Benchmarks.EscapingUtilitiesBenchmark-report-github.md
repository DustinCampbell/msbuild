```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT
  DefaultJob : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT


```
| Method                                       | Mean     | Error   | StdDev  | Gen0   | Allocated |
|--------------------------------------------- |---------:|--------:|--------:|-------:|----------:|
| Escape_SimpleString                          | 333.6 ns | 0.84 ns | 0.74 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 489.5 ns | 1.55 ns | 1.30 ns | 0.0877 |     465 B |
| Unescape_SimpleString                        | 202.7 ns | 0.57 ns | 0.54 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 476.3 ns | 1.06 ns | 0.89 ns | 0.0763 |     401 B |
