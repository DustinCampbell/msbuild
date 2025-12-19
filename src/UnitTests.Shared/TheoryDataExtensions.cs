// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xunit;

public static class TheoryDataExtensions
{
    public static void Add<T1, T2>(this TheoryData<T1, T2> data, (T1, T2) item)
        => data.Add(item.Item1, item.Item2);

    public static void Add<T1, T2, T3>(this TheoryData<T1, T2, T3> data, (T1, T2, T3) item)
        => data.Add(item.Item1, item.Item2, item.Item3);

    public static void Add<T1, T2, T3, T4>(this TheoryData<T1, T2, T3, T4> data, (T1, T2, T3, T4) item)
        => data.Add(item.Item1, item.Item2, item.Item3, item.Item4);

    public static void Add<T1, T2, T3, T4, T5>(this TheoryData<T1, T2, T3, T4, T5> data, (T1, T2, T3, T4, T5) item)
        => data.Add(item.Item1, item.Item2, item.Item3, item.Item4, item.Item5);

    public static void Add<T1, T2, T3, T4, T5, T6>(this TheoryData<T1, T2, T3, T4, T5, T6> data, (T1, T2, T3, T4, T5, T6) item)
        => data.Add(item.Item1, item.Item2, item.Item3, item.Item4, item.Item5, item.Item6);

    public static void Add<T1, T2, T3, T4, T5, T6, T7>(this TheoryData<T1, T2, T3, T4, T5, T6, T7> data, (T1, T2, T3, T4, T5, T6, T7) item)
        => data.Add(item.Item1, item.Item2, item.Item3, item.Item4, item.Item5, item.Item6, item.Item7);

    public static void Add<T1, T2, T3, T4, T5, T6, T7, T8>(this TheoryData<T1, T2, T3, T4, T5, T6, T7, T8> data, (T1, T2, T3, T4, T5, T6, T7, T8) item)
        => data.Add(item.Item1, item.Item2, item.Item3, item.Item4, item.Item5, item.Item6, item.Item7, item.Item8);

    public static void Add<T1, T2, T3, T4, T5, T6, T7, T8, T9>(this TheoryData<T1, T2, T3, T4, T5, T6, T7, T8, T9> data, (T1, T2, T3, T4, T5, T6, T7, T8, T9) item)
        => data.Add(item.Item1, item.Item2, item.Item3, item.Item4, item.Item5, item.Item6, item.Item7, item.Item8, item.Item9);

    public static void Add<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this TheoryData<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> data, (T1, T2, T3, T4, T5, T6, T7, T8, T9, T10) item)
        => data.Add(item.Item1, item.Item2, item.Item3, item.Item4, item.Item5, item.Item6, item.Item7, item.Item8, item.Item9, item.Item10);
}
