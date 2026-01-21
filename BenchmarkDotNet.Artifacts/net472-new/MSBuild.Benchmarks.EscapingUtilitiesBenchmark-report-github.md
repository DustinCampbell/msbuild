```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.28020.1371)
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT
  DefaultJob : .NET Framework 4.8.1 (4.8.9222.0), X86 LegacyJIT


```
| Method                                       | Mean     | Error   | StdDev  | Gen0   | Allocated |
|--------------------------------------------- |---------:|--------:|--------:|-------:|----------:|
| Escape_SimpleString                          | 367.7 ns | 1.88 ns | 1.76 ns |      - |         - |
| Escape_UnescapedString_WithSpecialCharacters | 549.4 ns | 2.11 ns | 1.87 ns | 0.0877 |     465 B |
| Unescape_SimpleString                        | 144.9 ns | 0.14 ns | 0.13 ns |      - |         - |
| Unescape_StringWithEscapeSequences           | 518.8 ns | 3.02 ns | 2.83 ns | 0.0763 |     401 B |
