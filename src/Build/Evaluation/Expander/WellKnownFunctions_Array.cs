// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class ArrayHandler
    {
        public WellKnownFunctionResult TryInvokeInstance(string methodName, Array array, object?[] args)
            => methodName.Length switch
            {
                4 when methodName.Equals(nameof(Array.Rank), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(array.Rank) : NotHandled,
                5 when methodName.Equals(nameof(Array.Clone), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(array.Clone()) : NotHandled,
                6 when methodName.Equals(nameof(Array.Length), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(array.Length) : NotHandled,
                6 when methodName.Equals(nameof(Array.CopyTo), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeCopyTo(array, args),
                6 when methodName.Equals(nameof(Array.Equals), StringComparison.OrdinalIgnoreCase)
                    => args.Length == 1 ? Invoked(array.Equals(args[0])) : NotHandled,
                8 when methodName.Equals(nameof(Array.GetValue), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetValue(array, args),
                8 when methodName.Equals(nameof(Array.SetValue), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeSetValue(array, args),
                8 when methodName.Equals(nameof(Array.ToString), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(array.ToString()) : NotHandled,
                8 when methodName.Equals(nameof(Array.SyncRoot), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(array.SyncRoot) : NotHandled,
                9 when methodName.Equals(nameof(Array.GetLength), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetLength(array, args),
                10 when methodName.Equals(nameof(Array.Initialize), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeInitialize(array, args),
                10 when methodName.Equals(nameof(Array.LongLength), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(array.LongLength) : NotHandled,
                10 when methodName.Equals(nameof(Array.IsReadOnly), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(array.IsReadOnly) : NotHandled,
                11 when methodName.Equals(nameof(Array.IsFixedSize), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(array.IsFixedSize) : NotHandled,
                11 when methodName.Equals(nameof(Array.GetHashCode), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(array.GetHashCode()) : NotHandled,
                13 when methodName.Equals(nameof(Array.GetLowerBound), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetLowerBound(array, args),
                13 when methodName.Equals(nameof(Array.GetLongLength), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetLongLength(array, args),
                13 when methodName.Equals(nameof(Array.GetUpperBound), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetUpperBound(array, args),
                13 when methodName.Equals(nameof(Array.GetEnumerator), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(array.GetEnumerator()) : NotHandled,
                14 when methodName.Equals(nameof(Array.IsSynchronized), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(array.IsSynchronized) : NotHandled,

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeCopyTo(Array array, object?[] args)
        {
            if (args is [Array longDestination, long index])
            {
                array.CopyTo(longDestination, index);
                return Invoked(null);
            }

            if (args is [Array intDestination, var indexValue] && ArgumentParser.TryConvertToInt(indexValue, out int convertedIndex))
            {
                array.CopyTo(intDestination, convertedIndex);
                return Invoked(null);
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeGetLength(Array array, object?[] args)
            => args is [var dimension] && ArgumentParser.TryConvertToInt(dimension, out int convertedDimension)
                ? Invoked(array.GetLength(convertedDimension))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetLongLength(Array array, object?[] args)
            => args is [var dimension] && ArgumentParser.TryConvertToInt(dimension, out int convertedDimension)
                ? Invoked(array.GetLongLength(convertedDimension))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetLowerBound(Array array, object?[] args)
            => args is [var dimension] && ArgumentParser.TryConvertToInt(dimension, out int convertedDimension)
                ? Invoked(array.GetLowerBound(convertedDimension))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetUpperBound(Array array, object?[] args)
            => args is [var dimension] && ArgumentParser.TryConvertToInt(dimension, out int convertedDimension)
                ? Invoked(array.GetUpperBound(convertedDimension))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetValue(Array array, object?[] args)
        {
            if (args is [int[] intIndices])
            {
                return Invoked(array.GetValue(intIndices));
            }

            if (args is [long[] longIndices])
            {
                return Invoked(array.GetValue(longIndices));
            }

            return args switch
            {
                [long index] => Invoked(array.GetValue(index)),
                [long index1, long index2] => Invoked(array.GetValue(index1, index2)),
                [long index1, long index2, long index3] => Invoked(array.GetValue(index1, index2, index3)),
                [var index1, var index2]
                    when ContainsLongIndex(args, 0) &&
                         ArgumentParser.TryConvertToLong(index1, out long convertedIndex1) &&
                         ArgumentParser.TryConvertToLong(index2, out long convertedIndex2)
                    => Invoked(array.GetValue(convertedIndex1, convertedIndex2)),
                [var index1, var index2, var index3]
                    when ContainsLongIndex(args, 0) &&
                         ArgumentParser.TryConvertToLong(index1, out long convertedIndex1) &&
                         ArgumentParser.TryConvertToLong(index2, out long convertedIndex2) &&
                         ArgumentParser.TryConvertToLong(index3, out long convertedIndex3)
                    => Invoked(array.GetValue(convertedIndex1, convertedIndex2, convertedIndex3)),
                [var index] when ArgumentParser.TryConvertToInt(index, out int convertedIndex)
                    => Invoked(array.GetValue(convertedIndex)),
                [var index1, var index2]
                    when ArgumentParser.TryConvertToInt(index1, out int convertedIndex1) &&
                         ArgumentParser.TryConvertToInt(index2, out int convertedIndex2)
                    => Invoked(array.GetValue(convertedIndex1, convertedIndex2)),
                [var index1, var index2, var index3]
                    when ArgumentParser.TryConvertToInt(index1, out int convertedIndex1) &&
                         ArgumentParser.TryConvertToInt(index2, out int convertedIndex2) &&
                         ArgumentParser.TryConvertToInt(index3, out int convertedIndex3)
                    => Invoked(array.GetValue(convertedIndex1, convertedIndex2, convertedIndex3)),
                _ => NotHandled,
            };
        }

        private static WellKnownFunctionResult TryInvokeInitialize(Array array, object?[] args)
        {
            if (args is [])
            {
                array.Initialize();
                return Invoked(null);
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeSetValue(Array array, object?[] args)
        {
            if (args.Length < 2)
            {
                return NotHandled;
            }

            if (args is [var intIndicesValue, int[] intIndices])
            {
                array.SetValue(intIndicesValue, intIndices);
                return Invoked(null);
            }

            if (args is [var longIndicesValue, long[] longIndices])
            {
                array.SetValue(longIndicesValue, longIndices);
                return Invoked(null);
            }

            switch (args)
            {
                case [var elementValue, long index]:
                    array.SetValue(elementValue, index);
                    return Invoked(null);
                case [var elementValue, long index1, long index2]:
                    array.SetValue(elementValue, index1, index2);
                    return Invoked(null);
                case [var elementValue, long index1, long index2, long index3]:
                    array.SetValue(elementValue, index1, index2, index3);
                    return Invoked(null);
                case [var elementValue, var index1, var index2]
                    when ContainsLongIndex(args, 1) &&
                         ArgumentParser.TryConvertToLong(index1, out long convertedIndex1) &&
                         ArgumentParser.TryConvertToLong(index2, out long convertedIndex2):
                    array.SetValue(elementValue, convertedIndex1, convertedIndex2);
                    return Invoked(null);
                case [var elementValue, var index1, var index2, var index3]
                    when ContainsLongIndex(args, 1) &&
                         ArgumentParser.TryConvertToLong(index1, out long convertedIndex1) &&
                         ArgumentParser.TryConvertToLong(index2, out long convertedIndex2) &&
                         ArgumentParser.TryConvertToLong(index3, out long convertedIndex3):
                    array.SetValue(elementValue, convertedIndex1, convertedIndex2, convertedIndex3);
                    return Invoked(null);
                case [var elementValue, var index] when ArgumentParser.TryConvertToInt(index, out int convertedIndex):
                    array.SetValue(elementValue, convertedIndex);
                    return Invoked(null);
                case [var elementValue, var index1, var index2]
                    when ArgumentParser.TryConvertToInt(index1, out int convertedIndex1) &&
                         ArgumentParser.TryConvertToInt(index2, out int convertedIndex2):
                    array.SetValue(elementValue, convertedIndex1, convertedIndex2);
                    return Invoked(null);
                case [var elementValue, var index1, var index2, var index3]
                    when ArgumentParser.TryConvertToInt(index1, out int convertedIndex1) &&
                         ArgumentParser.TryConvertToInt(index2, out int convertedIndex2) &&
                         ArgumentParser.TryConvertToInt(index3, out int convertedIndex3):
                    array.SetValue(elementValue, convertedIndex1, convertedIndex2, convertedIndex3);
                    return Invoked(null);
                default:
                    return NotHandled;
            }
        }

        private static bool ContainsLongIndex(object?[] args, int startIndex)
        {
            for (int i = startIndex; i < args.Length; i++)
            {
                if (args[i] is long)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
