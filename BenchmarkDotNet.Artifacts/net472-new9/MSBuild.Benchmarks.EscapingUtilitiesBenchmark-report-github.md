```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT
  DefaultJob : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT


```
| Method                                       | Mean     | Error   | StdDev  | Gen0   | Allocated |
|--------------------------------------------- |---------:|--------:|--------:|-------:|----------:|
| Escape_SimpleString                          | 168.6 ns | 0.60 ns | 0.56 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 353.5 ns | 1.70 ns | 1.59 ns | 0.0882 |     465 B |
| Unescape_SimpleString                        | 145.0 ns | 0.44 ns | 0.39 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 600.0 ns | 1.43 ns | 1.34 ns | 0.0763 |     401 B |
