```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT
  DefaultJob : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT


```
| Method                                       | Mean     | Error   | StdDev  | Gen0   | Allocated |
|--------------------------------------------- |---------:|--------:|--------:|-------:|----------:|
| Escape_SimpleString                          | 168.2 ns | 0.75 ns | 0.66 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 354.3 ns | 1.42 ns | 1.26 ns | 0.0882 |     465 B |
| Unescape_SimpleString                        | 144.7 ns | 0.45 ns | 0.37 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 442.6 ns | 2.09 ns | 1.96 ns | 0.0763 |     401 B |
