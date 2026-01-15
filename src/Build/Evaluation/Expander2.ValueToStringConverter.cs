// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation;

internal partial class Expander2<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    private abstract class ValueToStringConverter
    {
        public static ValueToStringConverter Default => field ??= new DefaultConverter();

        public static ValueToStringConverter Legacy => field ??= new LegacyConverter();

        // Issue: https://github.com/dotnet/msbuild/issues/9757
        public static ValueToStringConverter GetConfigured()
            => ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_12) ? Default : Legacy;

        private sealed class DefaultConverter : ValueToStringConverter
        {
            protected override string FallbackConvert(object value)
               => System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

            protected override void FallbackAppend(object value, ref RefStringBuilder builder)
            {
                switch (value)
                {
                    case sbyte v:
                        builder.Append(v, CultureInfo.InvariantCulture);
                        break;

                    case byte v:
                        builder.Append(v, CultureInfo.InvariantCulture);
                        break;

                    case short v:
                        builder.Append(v, CultureInfo.InvariantCulture);
                        break;

                    case int v:
                        builder.Append(v, CultureInfo.InvariantCulture);
                        break;

                    case long v:
                        builder.Append(v, CultureInfo.InvariantCulture);
                        break;

                    case float v:
                        builder.Append(v, CultureInfo.InvariantCulture);
                        break;

                    case double v:
                        builder.Append(v, CultureInfo.InvariantCulture);
                        break;

                    case decimal v:
                        builder.Append(v, CultureInfo.InvariantCulture);
                        break;

                    case ushort v:
                        builder.Append(v, CultureInfo.InvariantCulture);
                        break;

                    case uint v:
                        builder.Append(v, CultureInfo.InvariantCulture);
                        break;

                    case ulong v:
                        builder.Append(v, CultureInfo.InvariantCulture);
                        break;

                    default:
                        builder.Append(System.Convert.ToString(value, CultureInfo.InvariantCulture));
                        break;
                }
            }
        }

        private sealed class LegacyConverter : ValueToStringConverter
        {
            protected override string FallbackConvert(object value)
                => value.ToString() ?? string.Empty;

            protected override void FallbackAppend(object value, ref RefStringBuilder builder)
            {
                switch (value)
                {
                    case sbyte v:
                        builder.Append(v);
                        break;

                    case byte v:
                        builder.Append(v);
                        break;

                    case short v:
                        builder.Append(v);
                        break;

                    case int v:
                        builder.Append(v);
                        break;

                    case long v:
                        builder.Append(v);
                        break;

                    case float v:
                        builder.Append(v);
                        break;

                    case double v:
                        builder.Append(v);
                        break;

                    case decimal v:
                        builder.Append(v);
                        break;

                    case ushort v:
                        builder.Append(v);
                        break;

                    case uint v:
                        builder.Append(v);
                        break;

                    case ulong v:
                        builder.Append(v);
                        break;

                    default:
                        builder.Append(value.ToString());
                        break;
                }
            }
        }

        public string Convert(object? value)
            => value switch
            {
                null => string.Empty,
                string s => s,
                char c => c.ToString(),

                IDictionary dictionary => dictionary.Count == 0
                    ? string.Empty
                    : string.Build(dictionary, AppendDictionary),

                IEnumerable enumerable => string.Build(enumerable, AppendEnumerable),

                _ => FallbackConvert(value)
            };

        protected abstract string FallbackConvert(object value);

        protected abstract void FallbackAppend(object value, ref RefStringBuilder builder);

        private void AppendValue(object? value, ref RefStringBuilder builder)
        {
            switch (value)
            {
                case null:
                    break;

                case string s:
                    builder.AppendEscaped(s);
                    break;

                case char ch:
                    builder.AppendEscaped(ch);
                    break;

                case IDictionary dictionary:
                    AppendDictionary(dictionary, ref builder);
                    break;

                case IEnumerable enumerable:
                    AppendEnumerable(enumerable, ref builder);
                    break;

                default:
                    FallbackAppend(value, ref builder);
                    break;
            }
        }

        private void AppendDictionary(
            IDictionary dictionary,
            ref RefStringBuilder builder)
        {
            if (dictionary.Count == 0)
            {
                return;
            }

            // If the return type is an IDictionary, we convert it to
            // a semi-colon delimited set of A=B pairs.
            // Key and Value are appended as escaped strings
            bool first = true;

            // Note: Use IDictionaryEnumerator directly to avoid boxing DictionaryEntry.
            IDictionaryEnumerator enumerator = dictionary.GetEnumerator();

            while (enumerator.MoveNext())
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    builder.Append(';');
                }

                // convert and escape each key and value in the dictionary entry
                AppendValue(enumerator.Key, ref builder);
                builder.Append('=');
                AppendValue(enumerator.Value, ref builder);
            }
        }

        private void AppendEnumerable(IEnumerable enumerable, ref RefStringBuilder builder)
        {
            bool first = true;

            // If the return is an IEnumerable, we convert it to
            // a semi-colon delimited set of elements, each of which
            // is converted to an escaped strings.
            foreach (object element in enumerable)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    builder.Append(';');
                }

                // we need to convert and escape each element of the array
                AppendValue(element, ref builder);
            }
        }
    }
}
