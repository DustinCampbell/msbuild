// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Text;

using Shouldly;

using Xunit;
using ParseArgs = Microsoft.Build.Evaluation.Expander.FunctionArguments;

namespace Microsoft.Build.Engine.UnitTests.Evaluation
{
    public class ExpanderFunction_Tests
    {
        /* Tests for TryConvertToInt */

        [Fact]
        public void TryConvertToIntGivenNull()
        {
            ParseArgs.TryConvertToInt(null, out int actual).ShouldBeFalse();
            actual.ShouldBe(0);
        }

        [Fact]
        public void TryConvertToIntGivenDouble()
        {
            const double value = 10.0;
            ParseArgs.TryConvertToInt(value, out int actual).ShouldBeTrue();
            actual.ShouldBe(10);
        }

        [Fact]
        public void TryConvertToIntGivenLong()
        {
            const long value = 10;
            ParseArgs.TryConvertToInt(value, out int actual).ShouldBeTrue();
            actual.ShouldBe(10);
        }

        [Fact]
        public void TryConvertToIntGivenInt()
        {
            const int value = 10;
            ParseArgs.TryConvertToInt(value, out int actual).ShouldBeTrue();
            actual.ShouldBe(10);
        }

        [Fact]
        public void TryConvertToIntGivenString()
        {
            const string value = "10";
            ParseArgs.TryConvertToInt(value, out int actual).ShouldBeTrue();
            actual.ShouldBe(10);
        }

        [Fact]
        public void TryConvertToIntGivenDoubleWithIntMinValue()
        {
            const int expected = int.MinValue;
            const double value = expected;
            ParseArgs.TryConvertToInt(value, out int actual).ShouldBeTrue();
            actual.ShouldBe(expected);
        }

        [Fact]
        public void TryConvertToIntGivenDoubleWithIntMaxValue()
        {
            const int expected = int.MaxValue;
            const double value = expected;
            ParseArgs.TryConvertToInt(value, out int actual).ShouldBeTrue();
            actual.ShouldBe(expected);
        }

        [Fact]
        public void TryConvertToIntGivenDoubleWithLessThanIntMinValue()
        {
            const double value = int.MinValue - 1.0;
            ParseArgs.TryConvertToInt(value, out int actual).ShouldBeFalse();
            actual.ShouldBe(0);
        }

        [Fact]
        public void TryConvertToIntGivenDoubleWithGreaterThanIntMaxValue()
        {
            const double value = int.MaxValue + 1.0;
            ParseArgs.TryConvertToInt(value, out int actual).ShouldBeFalse();
            actual.ShouldBe(0);
        }

        [Fact]
        public void TryConvertToIntGivenLongWithGreaterThanIntMaxValue()
        {
            const long value = int.MaxValue + 1L;
            ParseArgs.TryConvertToInt(value, out int actual).ShouldBeFalse();
            actual.ShouldBe(0);
        }

        /* Tests for TryConvertToLong */

        [Fact]
        public void TryConvertToLongGivenNull()
        {
            ParseArgs.TryConvertToLong(null, out long actual).ShouldBeFalse();
            actual.ShouldBe(0);
        }

        [Fact]
        public void TryConvertToLongGivenDouble()
        {
            const double value = 10.0;
            ParseArgs.TryConvertToLong(value, out long actual).ShouldBeTrue();
            actual.ShouldBe(10);
        }

        [Fact]
        public void TryConvertToLongGivenLong()
        {
            const long value = 10;
            ParseArgs.TryConvertToLong(value, out long actual).ShouldBeTrue();
            actual.ShouldBe(10);
        }

        [Fact]
        public void TryConvertToLongGivenInt()
        {
            const int value = 10;
            ParseArgs.TryConvertToLong(value, out long actual).ShouldBeTrue();
            actual.ShouldBe(10);
        }

        [Fact]
        public void TryConvertToLongGivenString()
        {
            const string value = "10";
            ParseArgs.TryConvertToLong(value, out long actual).ShouldBeTrue();
            actual.ShouldBe(10);
        }

        [Fact]
        public void TryConvertToLongGivenDoubleWithLongMinValue()
        {
            const long expected = long.MinValue;
            const double value = expected;
            ParseArgs.TryConvertToLong(value, out long actual).ShouldBeTrue();
            actual.ShouldBe(expected);
        }

        [Fact]
        public void TryConvertToLongGivenDoubleWithLongMaxValueShouldNotThrow()
        {
            // An OverflowException should not be thrown from TryConvertToLong().
            // Convert.ToInt64(double) has a defect and will throw an OverflowException
            // for values >= (long.MaxValue - 511) and <= long.MaxValue.
            _ = Should.NotThrow(() => ParseArgs.TryConvertToLong((double)long.MaxValue, out _));
        }

        [WindowsFullFrameworkOnlyFact]
        public void TryConvertToLongGivenDoubleWithLongMaxValueFramework()
        {
            const long longMaxValue = long.MaxValue;
            bool result = ParseArgs.TryConvertToLong((double)longMaxValue, out long actual);

            // Because of loss of precision, long.MaxValue will not 'round trip' from long to double to long.
            result.ShouldBeFalse();
            actual.ShouldBe(0);
        }

        [DotNetOnlyFact]
        public void TryConvertToLongGivenDoubleWithLongMaxValueDotNet()
        {
            const long longMaxValue = long.MaxValue;
            bool result = ParseArgs.TryConvertToLong((double)longMaxValue, out long actual);

            // Testing on macOS 12 on Apple Silicon M1 Pro produces different result.
            result.ShouldBeTrue();
            actual.ShouldBe(longMaxValue);
        }

        [Fact]
        public void TryConvertToLongGivenDoubleWithVeryLargeLongValue()
        {
            // Because of loss of precision, veryLargeLong will not 'round trip' but within TryConvertToLong
            // the double to long conversion will pass the tolerance test. Return will be true and veryLargeLong != expected.
            const long veryLargeLong = long.MaxValue - 512;
            const double value = veryLargeLong;
            const long expected = 9223372036854774784L;
            ParseArgs.TryConvertToLong(value, out long actual).ShouldBeTrue();
            actual.ShouldBe(expected);
        }

        [Fact]
        public void TryConvertToLongGivenDoubleWithLessThanLongMinValue()
        {
            const double value = -92233720368547758081D;
            ParseArgs.TryConvertToLong(value, out long actual).ShouldBeFalse();
            actual.ShouldBe(0);
        }

        [Fact]
        public void TryConvertToLongGivenDoubleWithGreaterThanLongMaxValue()
        {
            const double value = (double)long.MaxValue + long.MaxValue;
            ParseArgs.TryConvertToLong(value, out long actual).ShouldBeFalse();
            actual.ShouldBe(0);
        }

        /* Tests for TryConvertToDouble */

        [Fact]
        public void TryConvertToDoubleGivenNull()
        {
            ParseArgs.TryConvertToDouble(null, out double actual).ShouldBeFalse();
            actual.ShouldBe(0);
        }

        [Fact]
        public void TryConvertToDoubleGivenDouble()
        {
            const double value = 10.0;
            ParseArgs.TryConvertToDouble(value, out double actual).ShouldBeTrue();
            actual.ShouldBe(10.0);
        }

        [Fact]
        public void TryConvertToDoubleGivenLong()
        {
            const long value = 10;
            ParseArgs.TryConvertToDouble(value, out double actual).ShouldBeTrue();
            actual.ShouldBe(10.0);
        }

        [Fact]
        public void TryConvertToDoubleGivenInt()
        {
            const int value = 10;
            ParseArgs.TryConvertToDouble(value, out double actual).ShouldBeTrue();
            actual.ShouldBe(10.0);
        }

        [Fact]
        public void TryConvertToDoubleGivenString()
        {
            const string value = "10";
            ParseArgs.TryConvertToDouble(value, out double actual).ShouldBeTrue();
            actual.ShouldBe(10.0);
        }

        [Fact]
        public void TryConvertToDoubleGivenStringAndLocale()
        {
            const string value = "1,2";

            Thread currentThread = Thread.CurrentThread;
            CultureInfo originalCulture = currentThread.CurrentCulture;

            try
            {
                // English South Africa locale uses ',' as decimal separator.
                // The invariant culture should be used and "1,2" should be 12.0 not 1.2.
                var cultureEnglishSouthAfrica = CultureInfo.CreateSpecificCulture("en-ZA");
                currentThread.CurrentCulture = cultureEnglishSouthAfrica;
                ParseArgs.TryConvertToDouble(value, out double actual).ShouldBeTrue();
                actual.ShouldBe(12.0);
            }
            finally
            {
                // Restore CultureInfo.
                currentThread.CurrentCulture = originalCulture;
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.DefaultThreadCurrentCulture = originalCulture;
            }
        }

        [Fact]
        public void FunctionArgumentsReadRawArgumentsWithoutMaterializing()
        {
            ParseArgs arguments = new(["first", "second"]);

            arguments.TryGetArgs(out string? first, out string? second).ShouldBeTrue();
            first.ShouldBe("first");
            second.ShouldBe("second");
            arguments.IsMaterialized.ShouldBeFalse();
        }

        [Fact]
        public void FunctionArgumentsMaterializeOnDemand()
        {
            ParseArgs arguments = new(["first", "second"]);
            var materializer = new TrackingMaterializer(index => $"expanded-{index}");

            arguments.ConfigureMaterialization(materializer, materializeOnAccess: true);

            arguments.IsMaterialized.ShouldBeFalse();
            arguments[1].ShouldBe("expanded-1");
            materializer.Indices.ShouldBe([1]);
            arguments.IsMaterialized.ShouldBeFalse();

            arguments[0].ShouldBe("expanded-0");
            materializer.Indices.ShouldBe([1, 0]);
            arguments.IsMaterialized.ShouldBeTrue();
        }

        [Fact]
        public void FunctionArgumentsMaterializeAllArgumentsOnce()
        {
            ParseArgs arguments = new(["first", "second"]);
            var materializer = new TrackingMaterializer(index => $"expanded-{index}");

            arguments.ConfigureMaterialization(materializer, materializeOnAccess: false);

            arguments.MaterializeAll().ShouldBe(["expanded-0", "expanded-1"]);
            arguments.MaterializeAll().ShouldBe(["expanded-0", "expanded-1"]);
            materializer.Indices.ShouldBe([0, 1]);
            arguments.IsMaterialized.ShouldBeTrue();
        }

        private sealed class TrackingMaterializer : IFunctionArgumentMaterializer
        {
            private readonly Func<int, object?> _materialize;

            internal TrackingMaterializer(Func<int, object?> materialize)
            {
                _materialize = materialize;
            }

            internal List<int> Indices { get; } = [];

            public object? Materialize(StringSegment source, int index)
            {
                Indices.Add(index);
                return _materialize(index);
            }
        }
    }
}
