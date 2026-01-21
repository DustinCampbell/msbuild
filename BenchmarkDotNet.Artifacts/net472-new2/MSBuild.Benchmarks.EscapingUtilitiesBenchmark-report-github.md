```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT
  DefaultJob : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT


```
| Method                                       | Mean     | Error   | StdDev  | Gen0   | Allocated |
|--------------------------------------------- |---------:|--------:|--------:|-------:|----------:|
| Escape_SimpleString                          | 323.5 ns | 1.02 ns | 0.95 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 496.1 ns | 2.48 ns | 2.20 ns | 0.0877 |     465 B |
| Unescape_SimpleString                        | 144.5 ns | 0.34 ns | 0.32 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 520.3 ns | 1.63 ns | 1.53 ns | 0.0763 |     401 B |
