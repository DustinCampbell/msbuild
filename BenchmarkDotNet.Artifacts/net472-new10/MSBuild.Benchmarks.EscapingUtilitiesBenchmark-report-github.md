```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT
  DefaultJob : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT


```
| Method                                       | Mean     | Error   | StdDev  | Gen0   | Allocated |
|--------------------------------------------- |---------:|--------:|--------:|-------:|----------:|
| Escape_SimpleString                          | 168.1 ns | 0.65 ns | 0.61 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 351.9 ns | 1.24 ns | 1.04 ns | 0.0882 |     465 B |
| Unescape_SimpleString                        | 145.2 ns | 0.69 ns | 0.65 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 452.8 ns | 1.91 ns | 1.69 ns | 0.0763 |     401 B |
