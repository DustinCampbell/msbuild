// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Threading;

using Microsoft.Build.Evaluation.Expander;

using Shouldly;

using Xunit;
using Coercion = Microsoft.Build.Evaluation.Expander.FunctionArgumentCoercion;

namespace Microsoft.Build.Engine.UnitTests.Evaluation
{
    public class FunctionArgumentCoercion_Tests
    {
        /* Tests for TryCoerceToInt32 */

        [Fact]
        public void TryCoerceToInt32GivenNull()
        {
            Coercion.TryCoerce(null, out int actual).ShouldBeFalse();
            actual.ShouldBe(0);
        }

        [Fact]
        public void TryCoerceToInt32GivenDouble()
        {
            const double value = 10.0;
            Coercion.TryCoerce(value, out int actual).ShouldBeTrue();
            actual.ShouldBe(10);
        }

        [Fact]
        public void TryCoerceToInt32GivenLong()
        {
            const long value = 10;
            Coercion.TryCoerce(value, out int actual).ShouldBeTrue();
            actual.ShouldBe(10);
        }

        [Fact]
        public void TryCoerceToInt32GivenInt()
        {
            const int value = 10;
            Coercion.TryCoerce(value, out int actual).ShouldBeTrue();
            actual.ShouldBe(10);
        }

        [Fact]
        public void TryCoerceToInt32GivenString()
        {
            const string value = "10";
            Coercion.TryCoerce(value, out int actual).ShouldBeTrue();
            actual.ShouldBe(10);
        }

        [Fact]
        public void TryCoerceToInt32GivenDoubleWithIntMinValue()
        {
            const int expected = int.MinValue;
            const double value = expected;
            Coercion.TryCoerce(value, out int actual).ShouldBeTrue();
            actual.ShouldBe(expected);
        }

        [Fact]
        public void TryCoerceToInt32GivenDoubleWithIntMaxValue()
        {
            const int expected = int.MaxValue;
            const double value = expected;
            Coercion.TryCoerce(value, out int actual).ShouldBeTrue();
            actual.ShouldBe(expected);
        }

        [Fact]
        public void TryCoerceToInt32GivenDoubleWithLessThanIntMinValue()
        {
            const double value = int.MinValue - 1.0;
            Coercion.TryCoerce(value, out int actual).ShouldBeFalse();
            actual.ShouldBe(0);
        }

        [Fact]
        public void TryCoerceToInt32GivenDoubleWithGreaterThanIntMaxValue()
        {
            const double value = int.MaxValue + 1.0;
            Coercion.TryCoerce(value, out int actual).ShouldBeFalse();
            actual.ShouldBe(0);
        }

        [Fact]
        public void TryCoerceToInt32GivenLongWithGreaterThanIntMaxValue()
        {
            const long value = int.MaxValue + 1L;
            Coercion.TryCoerce(value, out int actual).ShouldBeFalse();
            actual.ShouldBe(0);
        }

        /* Tests for TryCoerceToInt64 */

        [Fact]
        public void TryCoerceToInt64GivenNull()
        {
            Coercion.TryCoerce(null, out long actual).ShouldBeFalse();
            actual.ShouldBe(0);
        }

        [Fact]
        public void TryCoerceToInt64GivenDouble()
        {
            const double value = 10.0;
            Coercion.TryCoerce(value, out long actual).ShouldBeTrue();
            actual.ShouldBe(10);
        }

        [Fact]
        public void TryCoerceToInt64GivenLong()
        {
            const long value = 10;
            Coercion.TryCoerce(value, out long actual).ShouldBeTrue();
            actual.ShouldBe(10);
        }

        [Fact]
        public void TryCoerceToInt64GivenInt()
        {
            const int value = 10;
            Coercion.TryCoerce(value, out long actual).ShouldBeTrue();
            actual.ShouldBe(10);
        }

        [Fact]
        public void TryCoerceToInt64GivenString()
        {
            const string value = "10";
            Coercion.TryCoerce(value, out long actual).ShouldBeTrue();
            actual.ShouldBe(10);
        }

        [Fact]
        public void TryCoerceToInt64GivenDoubleWithLongMinValue()
        {
            const long expected = long.MinValue;
            const double value = expected;
            Coercion.TryCoerce(value, out long actual).ShouldBeTrue();
            actual.ShouldBe(expected);
        }

        [Fact]
        public void TryCoerceToInt64GivenDoubleWithLongMaxValueShouldNotThrow()
        {
            // An OverflowException should not be thrown from TryCoerceToInt64().
            // Convert.ToInt64(double) has a defect and will throw an OverflowException
            // for values >= (long.MaxValue - 511) and <= long.MaxValue.
            _ = Should.NotThrow(() => Coercion.TryCoerce((double)long.MaxValue, out long _));
        }

        [WindowsFullFrameworkOnlyFact]
        public void TryCoerceToInt64GivenDoubleWithLongMaxValueFramework()
        {
            const long longMaxValue = long.MaxValue;
            bool result = Coercion.TryCoerce((double)longMaxValue, out long actual);

            // Because of loss of precision, long.MaxValue will not 'round trip' from long to double to long.
            result.ShouldBeFalse();
            actual.ShouldBe(0);
        }

        [DotNetOnlyFact]
        public void TryCoerceToInt64GivenDoubleWithLongMaxValueDotNet()
        {
            const long longMaxValue = long.MaxValue;
            bool result = Coercion.TryCoerce((double)longMaxValue, out long actual);

            // Testing on macOS 12 on Apple Silicon M1 Pro produces different result.
            result.ShouldBeTrue();
            actual.ShouldBe(longMaxValue);
        }

        [Fact]
        public void TryCoerceToInt64GivenDoubleWithVeryLargeLongValue()
        {
            // Because of loss of precision, veryLargeLong will not 'round trip' but within TryCoerceToInt64
            // the double to long conversion will pass the tolerance test. Return will be true and veryLargeLong != expected.
            const long veryLargeLong = long.MaxValue - 512;
            const double value = veryLargeLong;
            const long expected = 9223372036854774784L;
            Coercion.TryCoerce(value, out long actual).ShouldBeTrue();
            actual.ShouldBe(expected);
        }

        [Fact]
        public void TryCoerceToInt64GivenDoubleWithLessThanLongMinValue()
        {
            const double value = -92233720368547758081D;
            Coercion.TryCoerce(value, out long actual).ShouldBeFalse();
            actual.ShouldBe(0);
        }

        [Fact]
        public void TryCoerceToInt64GivenDoubleWithGreaterThanLongMaxValue()
        {
            const double value = (double)long.MaxValue + long.MaxValue;
            Coercion.TryCoerce(value, out long actual).ShouldBeFalse();
            actual.ShouldBe(0);
        }

        /* Tests for TryCoerceToDouble */

        [Fact]
        public void TryCoerceToDoubleGivenNull()
        {
            Coercion.TryCoerce(null, out double actual).ShouldBeFalse();
            actual.ShouldBe(0);
        }

        [Fact]
        public void TryCoerceToDoubleGivenDouble()
        {
            const double value = 10.0;
            Coercion.TryCoerce(value, out double actual).ShouldBeTrue();
            actual.ShouldBe(10.0);
        }

        [Fact]
        public void TryCoerceToDoubleGivenLong()
        {
            const long value = 10;
            Coercion.TryCoerce(value, out double actual).ShouldBeTrue();
            actual.ShouldBe(10.0);
        }

        [Fact]
        public void TryCoerceToDoubleGivenInt()
        {
            const int value = 10;
            Coercion.TryCoerce(value, out double actual).ShouldBeTrue();
            actual.ShouldBe(10.0);
        }

        [Fact]
        public void TryCoerceToDoubleGivenString()
        {
            const string value = "10";
            Coercion.TryCoerce(value, out double actual).ShouldBeTrue();
            actual.ShouldBe(10.0);
        }

        [Fact]
        public void TryCoerceToDoubleGivenStringAndLocale()
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
                Coercion.TryCoerce(value, out double actual).ShouldBeTrue();
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

        [Theory]
        [InlineData((sbyte)1)]
        [InlineData((byte)1)]
        [InlineData((short)1)]
        [InlineData((ushort)1)]
        [InlineData((char)1)]
        public void TryCoerceWidensPrimitiveToInt32(object value)
        {
            Coercion.TryCoerce(value, out int actual).ShouldBeTrue();
            actual.ShouldBe(1);
        }

        [Theory]
        [InlineData((sbyte)1)]
        [InlineData((byte)1)]
        [InlineData((short)1)]
        [InlineData((ushort)1)]
        [InlineData((char)1)]
        [InlineData(1)]
        [InlineData((uint)1)]
        public void TryCoerceWidensPrimitiveToInt64(object value)
        {
            Coercion.TryCoerce(value, out long actual).ShouldBeTrue();
            actual.ShouldBe(1);
        }

        [Theory]
        [InlineData((sbyte)1)]
        [InlineData((byte)1)]
        [InlineData((short)1)]
        [InlineData((ushort)1)]
        [InlineData((char)1)]
        [InlineData(1)]
        [InlineData((uint)1)]
        [InlineData(1L)]
        [InlineData((ulong)1)]
        [InlineData(1F)]
        public void TryCoerceWidensPrimitiveToDouble(object value)
        {
            Coercion.TryCoerce(value, out double actual).ShouldBeTrue();
            actual.ShouldBe(1);
        }

        [Fact]
        public void TryCoerceDoesNotApplyUnsupportedNumericNarrowing()
        {
            Coercion.TryCoerce((uint)1, out int _).ShouldBeFalse();
            Coercion.TryCoerce(1F, out int _).ShouldBeFalse();
            Coercion.TryCoerce(1D, out float _).ShouldBeFalse();
        }

        [Fact]
        public void TryCoerceOrDefaultAcceptsNullForValueTypes()
        {
            Coercion.TryCoerce(null, out int _).ShouldBeFalse();
            Coercion.TryCoerceOrDefault(null, out int value).ShouldBeTrue();
            value.ShouldBe(0);
        }
    }
}
