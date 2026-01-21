```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT
  DefaultJob : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT


```
| Method                                       | Mean     | Error   | StdDev  | Gen0   | Allocated |
|--------------------------------------------- |---------:|--------:|--------:|-------:|----------:|
| Escape_SimpleString                          | 172.2 ns | 0.74 ns | 0.69 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 339.1 ns | 1.45 ns | 1.28 ns | 0.0882 |     465 B |
| Unescape_SimpleString                        | 144.7 ns | 0.78 ns | 0.73 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 621.5 ns | 3.20 ns | 3.00 ns | 0.0763 |     401 B |
