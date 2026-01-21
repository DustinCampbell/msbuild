```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT
  DefaultJob : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT


```
| Method                                       | Mean     | Error   | StdDev  | Gen0   | Allocated |
|--------------------------------------------- |---------:|--------:|--------:|-------:|----------:|
| Escape_SimpleString                          | 446.5 ns | 0.64 ns | 0.57 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 869.8 ns | 3.06 ns | 2.56 ns | 0.0877 |     465 B |
| Unescape_SimpleString                        | 145.4 ns | 0.58 ns | 0.55 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 518.4 ns | 2.45 ns | 2.17 ns | 0.0763 |     401 B |
