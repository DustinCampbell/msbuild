```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT
  DefaultJob : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT


```
| Method                                       | Mean     | Error   | StdDev  | Gen0   | Allocated |
|--------------------------------------------- |---------:|--------:|--------:|-------:|----------:|
| Escape_SimpleString                          | 266.6 ns | 1.05 ns | 0.98 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 408.7 ns | 1.42 ns | 1.33 ns | 0.0882 |     465 B |
| Unescape_SimpleString                        | 194.7 ns | 0.76 ns | 0.71 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 457.9 ns | 1.63 ns | 1.52 ns | 0.0763 |     401 B |
