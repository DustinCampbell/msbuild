// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation.Expander;

internal readonly struct ArithmeticArguments
{
    private readonly long _long0;
    private readonly long _long1;
    private readonly double _double0;
    private readonly double _double1;
    private readonly bool _isDouble;

    internal ArithmeticArguments(long arg0, long arg1)
    {
        _long0 = arg0;
        _long1 = arg1;
        _double0 = default;
        _double1 = default;
        _isDouble = false;
    }

    internal ArithmeticArguments(double arg0, double arg1)
    {
        _long0 = default;
        _long1 = default;
        _double0 = arg0;
        _double1 = arg1;
        _isDouble = true;
    }

    public bool TryGetLongs(out long arg0, out long arg1)
    {
        arg0 = _long0;
        arg1 = _long1;
        return !_isDouble;
    }

    public bool TryGetDoubles(out double arg0, out double arg1)
    {
        arg0 = _double0;
        arg1 = _double1;
        return _isDouble;
    }
}
