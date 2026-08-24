// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Text;
using ParseArgs = Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    ///  Retains source-backed function arguments and evaluates individual values only when requested.
    /// </summary>
    /// <remarks>
    ///  The first two evaluated values are retained inline. A parallel object array is allocated only
    ///  for larger argument sets or when reflection requires all values to be materialized.
    /// </remarks>
    private struct FunctionArguments
    {
        private static readonly object s_nullValue = new();

        private readonly StringSegment[] _expressions;
        private readonly IPropertyProvider<P> _properties;
        private readonly ExpanderOptions _options;
        private readonly IElementLocation _elementLocation;
        private readonly PropertiesUseTracker _propertiesUseTracker;
        private readonly IFileSystem _fileSystem;
        private readonly Type _receiverType;
        private readonly StringSegment _methodName;

        private object? _firstValue;
        private object? _secondValue;
        private object[]? _values;
        private byte _evaluatedMask;
        private bool _allEvaluated;

        /// <summary>
        ///  Initializes lazily evaluated function arguments.
        /// </summary>
        /// <param name="expressions">The source-backed argument expressions.</param>
        /// <param name="properties">The property provider used to expand arguments.</param>
        /// <param name="options">The expansion options.</param>
        /// <param name="elementLocation">The expression location used for errors.</param>
        /// <param name="propertiesUseTracker">The property-use tracker.</param>
        /// <param name="fileSystem">The file system used during expansion.</param>
        /// <param name="receiverType">The function receiver type.</param>
        /// <param name="methodName">The source-backed method name.</param>
        public FunctionArguments(
            StringSegment[] expressions,
            IPropertyProvider<P> properties,
            ExpanderOptions options,
            IElementLocation elementLocation,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem,
            Type receiverType,
            StringSegment methodName)
        {
            _expressions = expressions;
            _properties = properties;
            _options = options;
            _elementLocation = elementLocation;
            _propertiesUseTracker = propertiesUseTracker;
            _fileSystem = fileSystem;
            _receiverType = receiverType;
            _methodName = methodName;
            _firstValue = null;
            _secondValue = null;
            _values = null;
            _evaluatedMask = 0;
            _allEvaluated = false;
        }

        /// <summary>
        ///  Gets the number of arguments.
        /// </summary>
        public readonly int Length
            => _values is { } values && values.Length > _expressions.Length
                ? values.Length
                : _expressions.Length;

        /// <summary>
        ///  Gets or replaces an evaluated argument.
        /// </summary>
        /// <param name="index">The zero-based argument index.</param>
        public object? this[int index]
        {
            get => GetObject(index);
            set => SetObject(index, value);
        }

        /// <summary>
        ///  Appends an engine-supplied argument.
        /// </summary>
        /// <param name="value">The argument value to append.</param>
        public void Append(object? value)
        {
            object[] values = ToObjectArray();
            Array.Resize(ref values, values.Length + 1);
            values[^1] = value!;
            _values = values;
            _allEvaluated = true;
        }

        /// <summary>
        ///  Evaluates every argument and returns the shared object array used for reflection.
        /// </summary>
        /// <returns>The fully evaluated argument array.</returns>
        public object[] ToObjectArray()
        {
            object[] values = EnsureValues(_expressions.Length);
            if (!_allEvaluated)
            {
                for (int i = 0; i < _expressions.Length; i++)
                {
                    if (values[i] is null)
                    {
                        EvaluateAndCache(i);
                    }
                }

                for (int i = 0; i < values.Length; i++)
                {
                    if (ReferenceEquals(values[i], s_nullValue))
                    {
                        values[i] = null!;
                    }
                }

                _allEvaluated = true;
            }

            return values;
        }

        /// <summary>
        ///  Returns the values evaluated before an exception without evaluating additional arguments.
        /// </summary>
        /// <returns>A diagnostic snapshot of the arguments evaluated so far.</returns>
        public readonly object[] GetEvaluatedValuesForError()
        {
            object[] result = new object[Length];
            if (_values is null)
            {
                if ((_evaluatedMask & 1) != 0)
                {
                    result[0] = ReferenceEquals(_firstValue, s_nullValue) ? null! : _firstValue!;
                }

                if ((_evaluatedMask & 2) != 0)
                {
                    result[1] = ReferenceEquals(_secondValue, s_nullValue) ? null! : _secondValue!;
                }

                return result;
            }

            for (int i = 0; i < _values.Length; i++)
            {
                object? value = _values[i];
                result[i] = ReferenceEquals(value, s_nullValue) ? null! : value!;
            }

            return result;
        }

        /// <summary>
        ///  Tries to retrieve two string arguments.
        /// </summary>
        public bool TryGetArgs(out string? arg0, out string? arg1)
        {
            arg0 = null;
            arg1 = null;

            if (Length != 2 ||
                this[0] is not string value0 ||
                this[1] is not string value1)
            {
                return false;
            }

            arg0 = value0;
            arg1 = value1;
            return true;
        }

        /// <summary>
        ///  Tries to retrieve three string arguments.
        /// </summary>
        public bool TryGetArgs(out string? arg0, out string? arg1, out string? arg2)
        {
            arg0 = null;
            arg1 = null;
            arg2 = null;

            if (Length != 3 ||
                this[0] is not string value0 ||
                this[1] is not string value1 ||
                this[2] is not string value2)
            {
                return false;
            }

            arg0 = value0;
            arg1 = value1;
            arg2 = value2;
            return true;
        }

        /// <summary>
        ///  Tries to retrieve four string arguments.
        /// </summary>
        public bool TryGetArgs(out string? arg0, out string? arg1, out string? arg2, out string? arg3)
        {
            arg0 = null;
            arg1 = null;
            arg2 = null;
            arg3 = null;

            if (Length != 4 ||
                this[0] is not string value0 ||
                this[1] is not string value1 ||
                this[2] is not string value2 ||
                this[3] is not string value3)
            {
                return false;
            }

            arg0 = value0;
            arg1 = value1;
            arg2 = value2;
            arg3 = value3;
            return true;
        }

        /// <summary>
        ///  Tries to retrieve one string followed by two integer arguments.
        /// </summary>
        public bool TryGetArgs(out string? arg0, out int arg1, out int arg2)
        {
            arg0 = null;
            arg1 = 0;
            arg2 = 0;

            if (Length != 3 || this[0] is not string value0)
            {
                return false;
            }

            arg0 = value0;
            return TryGetInt32(1, out arg1) && TryGetInt32(2, out arg2);
        }

        /// <summary>
        ///  Tries to retrieve one integer argument.
        /// </summary>
        public bool TryGetArg(out int arg0)
        {
            if (Length != 1)
            {
                arg0 = 0;
                return false;
            }

            return TryGetInt32(0, out arg0);
        }

        /// <summary>
        ///  Tries to retrieve one version argument.
        /// </summary>
        public bool TryGetArg(out Version? arg0)
        {
            if (Length != 1)
            {
                arg0 = null;
                return false;
            }

            return ParseArgs.TryConvertToVersion(this[0], out arg0);
        }

        /// <summary>
        ///  Tries to retrieve one string argument.
        /// </summary>
        public bool TryGetArg(out string? arg0)
        {
            if (Length != 1)
            {
                arg0 = null;
                return false;
            }

            arg0 = this[0] as string;
            return arg0 is not null;
        }

        /// <summary>
        ///  Tries to retrieve a string and a string-comparison argument.
        /// </summary>
        public bool TryGetArgs(out string? arg0, out StringComparison arg1)
        {
            arg0 = null;
            arg1 = default;

            if (Length != 2 || this[0] is not string value0 || this[1] is not string comparisonTypeName)
            {
                return false;
            }

            // Reject enums represented as integers. C# requires a cast, which MSBuild expressions do not support.
            if (int.TryParse(comparisonTypeName, out _))
            {
                return false;
            }

            if (comparisonTypeName.IndexOf('.') >= 0)
            {
                comparisonTypeName = comparisonTypeName
                    .Replace("System.StringComparison.", string.Empty)
                    .Replace("StringComparison.", string.Empty);
            }

            if (!Enum.TryParse(comparisonTypeName, out arg1))
            {
                return false;
            }

            arg0 = value0;
            return true;
        }

        /// <summary>
        ///  Tries to retrieve one integer argument.
        /// </summary>
        public bool TryGetArgs(out int arg0)
            => TryGetArg(out arg0);

        /// <summary>
        ///  Tries to retrieve two integer arguments.
        /// </summary>
        public bool TryGetArgs(out int arg0, out int arg1)
        {
            arg0 = 0;
            arg1 = 0;

            return Length == 2 &&
                TryGetInt32(0, out arg0) &&
                TryGetInt32(1, out arg1);
        }

        /// <summary>
        ///  Tries to retrieve two double-precision arguments.
        /// </summary>
        public bool TryGetArgs(out double arg0, out double arg1)
        {
            arg0 = 0;
            arg1 = 0;

            return Length == 2 &&
                TryGetDouble(0, out arg0) &&
                TryGetDouble(1, out arg1);
        }

        /// <summary>
        ///  Tries to retrieve an integer and a string argument.
        /// </summary>
        public bool TryGetArgs(out int arg0, out string? arg1)
        {
            arg0 = 0;
            arg1 = null;

            if (Length != 2)
            {
                return false;
            }

            object? value = this[1];
            arg1 = value as string;
            if (arg1 is null && value is char character)
            {
                arg1 = character.ToString();
            }

            return arg1 is not null && TryGetInt32(0, out arg0);
        }

        /// <summary>
        ///  Tries to retrieve a string and an integer argument.
        /// </summary>
        public bool TryGetArgs(out string? arg0, out int arg1)
        {
            arg0 = null;
            arg1 = 0;

            if (Length != 2 || this[0] is not string value0)
            {
                return false;
            }

            arg0 = value0;
            return TryGetInt32(1, out arg1);
        }

        /// <summary>
        ///  Determines whether an argument can be represented as a floating-point value.
        /// </summary>
        public bool IsFloatingPointRepresentation(int index)
        {
            if (TryGetCachedValue(index, out object? value))
            {
                return ParseArgs.IsFloatingPointRepresentation(value);
            }

            StringSegment expression = _expressions[index];
#if NET
            return double.TryParse(
                expression.AsSpan(),
                NumberStyles.Number | NumberStyles.Float,
                CultureInfo.InvariantCulture.NumberFormat,
                out _);
#else
            return long.TryParse(expression, out _) ||
                ParseArgs.IsFloatingPointRepresentation(GetObject(index));
#endif
        }

        /// <summary>
        ///  Tries integer arithmetic first, then floating-point arithmetic.
        /// </summary>
        public bool TryExecuteArithmeticOverload(
            Func<long, long, long> integerOperation,
            Func<double, double, double> realOperation,
            out object? resultValue)
        {
            resultValue = null;
            if (Length != 2)
            {
                return false;
            }

            if (TryGetInt64(0, out long argLong0) && TryGetInt64(1, out long argLong1))
            {
                resultValue = integerOperation(argLong0, argLong1);
                return true;
            }

            if (TryGetDouble(0, out double argDouble0) && TryGetDouble(1, out double argDouble1))
            {
                resultValue = realOperation(argDouble0, argDouble1);
                return true;
            }

            return false;
        }

        private object? GetObject(int index)
        {
            if (TryGetCachedValue(index, out object? value))
            {
                return value;
            }

            return EvaluateAndCache(index);
        }

        private void SetObject(int index, object? value)
        {
            CacheValue(index, value);
        }

        private object? EvaluateAndCache(int index)
        {
            StringSegment expression = _expressions[index];
            object? value;
            if (!expression.HasValue)
            {
                value = null;
            }
            else if (ExpressionShredder.IndexOfPropertyMarker(expression) < 0)
            {
                value = expression.Value;
            }
            else
            {
                value = PropertyExpander.ExpandPropertiesLeaveTypedAndEscaped(
                    expression,
                    _properties,
                    _options,
                    _elementLocation,
                    _propertiesUseTracker,
                    _fileSystem);
            }

            if (value is string stringValue)
            {
                if (_receiverType == typeof(System.IO.File) ||
                    _receiverType == typeof(System.IO.Directory) ||
                    _receiverType == typeof(System.IO.Path))
                {
                    stringValue = FileUtilities.FixFilePath(stringValue);
                }

                stringValue = EscapingUtilities.UnescapeAll(stringValue);

                if ((_receiverType == typeof(System.IO.File) || _receiverType == typeof(System.IO.Directory)) &&
                    IsFileOrDirectoryPathArgument(_methodName, index))
                {
                    AbsolutePath? resolved = FileUtilities.MakeFullPathFromThreadWorkingDirectory(stringValue);
                    if (resolved.HasValue)
                    {
                        stringValue = resolved.GetValueOrDefault();
                    }
                }

                value = stringValue;
            }

            CacheValue(index, value);
            return value;
        }

        private readonly bool TryGetCachedValue(int index, out object? value)
        {
            if (_values is null)
            {
                if (index == 0 && (_evaluatedMask & 1) != 0)
                {
                    value = ReferenceEquals(_firstValue, s_nullValue) ? null : _firstValue;
                    return true;
                }

                if (index == 1 && (_evaluatedMask & 2) != 0)
                {
                    value = ReferenceEquals(_secondValue, s_nullValue) ? null : _secondValue;
                    return true;
                }

                value = null;
                return false;
            }

            object? cachedValue = _values[index];
            if (cachedValue is null && !_allEvaluated)
            {
                value = null;
                return false;
            }

            value = ReferenceEquals(cachedValue, s_nullValue) ? null : cachedValue;
            return true;
        }

        private object[] EnsureValues(int length)
        {
            if (_values is null)
            {
                _values = new object[length];
                if ((_evaluatedMask & 1) != 0)
                {
                    _values[0] = _firstValue!;
                }

                if ((_evaluatedMask & 2) != 0)
                {
                    _values[1] = _secondValue!;
                }
            }

            return _values;
        }

        private void CacheValue(int index, object? value)
        {
            object cachedValue = value ?? s_nullValue;
            if (_values is not null)
            {
                _values[index] = _allEvaluated ? value! : cachedValue;
            }
            else if (index == 0)
            {
                _firstValue = cachedValue;
                _evaluatedMask |= 1;
            }
            else if (index == 1)
            {
                _secondValue = cachedValue;
                _evaluatedMask |= 2;
            }
            else
            {
                EnsureValues(_expressions.Length)[index] = cachedValue;
            }
        }

        private bool TryGetInt32(int index, out int value)
        {
            if (TryGetCachedValue(index, out object? cachedValue))
            {
                return ParseArgs.TryConvertToInt(cachedValue, out value);
            }

            StringSegment expression = _expressions[index];
            if (int.TryParse(expression, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            return ParseArgs.TryConvertToInt(GetObject(index), out value);
        }

        private bool TryGetInt64(int index, out long value)
        {
            if (TryGetCachedValue(index, out object? cachedValue))
            {
                return ParseArgs.TryConvertToLong(cachedValue, out value);
            }

            StringSegment expression = _expressions[index];
            if (long.TryParse(expression, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            return ParseArgs.TryConvertToLong(GetObject(index), out value);
        }

        private bool TryGetDouble(int index, out double value)
        {
            if (TryGetCachedValue(index, out object? cachedValue))
            {
                return ParseArgs.TryConvertToDouble(cachedValue, out value);
            }

            StringSegment expression = _expressions[index];
            if (long.TryParse(expression, out long integer))
            {
                value = integer;
                return true;
            }

#if NET
            if (double.TryParse(
                    expression.AsSpan(),
                    NumberStyles.Number | NumberStyles.Float,
                    CultureInfo.InvariantCulture.NumberFormat,
                    out value))
            {
                return true;
            }
#endif

            return ParseArgs.TryConvertToDouble(GetObject(index), out value);
        }

        private static bool IsFileOrDirectoryPathArgument(StringSegment methodName, int argIndex)
        {
            if (argIndex == 0)
            {
                return true;
            }

            if (argIndex == 1)
            {
                return methodName.Equals("Copy", StringComparison.OrdinalIgnoreCase)
                    || methodName.Equals("Move", StringComparison.OrdinalIgnoreCase)
                    || methodName.Equals("Replace", StringComparison.OrdinalIgnoreCase);
            }

            return argIndex == 2 && methodName.Equals("Replace", StringComparison.OrdinalIgnoreCase);
        }
    }
}
