// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class RegexHandler
    {
        public WellKnownFunctionResult TryInvokeConstructor(object?[] args)
            => args switch
            {
                [string pattern] => Invoked(new Regex(pattern)),
                [string pattern, var options] when ArgumentParser.TryConvertToEnum(options, out RegexOptions convertedOptions)
                    => Invoked(new Regex(pattern, convertedOptions)),
                [string pattern, var options, TimeSpan timeout] when ArgumentParser.TryConvertToEnum(options, out RegexOptions convertedOptions)
                    => Invoked(new Regex(pattern, convertedOptions, timeout)),
                _ => NotHandled,
            };

        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
            => methodName.Length switch
            {
                5 when methodName.Equals(nameof(Regex.Match), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeStaticMatch(args),
                5 when methodName.Equals(nameof(Regex.Split), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeStaticSplit(args),
                6 when methodName.Equals(nameof(Regex.Escape), StringComparison.OrdinalIgnoreCase)
                    => args is [string value] ? Invoked(Regex.Escape(value)) : NotHandled,
                7 when methodName.Equals(nameof(Regex.IsMatch), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeStaticIsMatch(args),
                7 when methodName.Equals(nameof(Regex.Matches), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeStaticMatches(args),
                7 when methodName.Equals(nameof(Regex.Replace), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeReplace(args),
                8 when methodName.Equals(nameof(Regex.Unescape), StringComparison.OrdinalIgnoreCase)
                    => args is [string value] ? Invoked(Regex.Unescape(value)) : NotHandled,

                _ => NotHandled,
            };

        public WellKnownFunctionResult TryInvokeInstance(string methodName, object instance, object?[] args)
            => instance switch
            {
                Regex regex => TryInvokeRegexInstance(methodName, regex, args),
                GroupCollection groups => TryInvokeGroupCollection(methodName, groups, args),
                CaptureCollection captures => TryInvokeCaptureCollection(methodName, captures, args),
                Match match => TryInvokeMatch(methodName, match, args),
                MatchCollection matches => TryInvokeMatchCollection(methodName, matches, args),
                Group group => TryInvokeGroup(methodName, group, args),
                Capture capture => TryInvokeCapture(methodName, capture, args),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeCapture(string methodName, Capture capture, object?[] args)
        {
            if (args is not [])
            {
                return NotHandled;
            }

            if (methodName.Equals(nameof(Capture.Index), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(capture.Index);
            }

            if (methodName.Equals(nameof(Capture.Length), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(capture.Length);
            }

            if (methodName.Equals(nameof(Capture.Value), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(capture.Value);
            }

            if (methodName.Equals(nameof(Capture.ToString), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(capture.ToString());
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeCaptureCollection(string methodName, CaptureCollection captures, object?[] args)
        {
            if (methodName.Equals(nameof(CaptureCollection.Count), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(captures.Count) : NotHandled;
            }

            if (methodName.Equals("get_Item", StringComparison.OrdinalIgnoreCase))
            {
                return args is [var index] && ArgumentParser.TryConvertToInt(index, out int convertedIndex)
                    ? Invoked(captures[convertedIndex])
                    : NotHandled;
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeGroup(string methodName, Group group, object?[] args)
        {
            if (methodName.Equals(nameof(Group.Captures), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(group.Captures) : NotHandled;
            }

            if (methodName.Equals(nameof(Group.Success), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(group.Success) : NotHandled;
            }

            return TryInvokeCapture(methodName, group, args);
        }

        private static WellKnownFunctionResult TryInvokeGroupCollection(string methodName, GroupCollection groups, object?[] args)
        {
            if (methodName.Equals(nameof(GroupCollection.Count), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(groups.Count) : NotHandled;
            }

            if (methodName.Equals("get_Item", StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [var index] when ArgumentParser.TryConvertToInt(index, out int convertedIndex) => Invoked(groups[convertedIndex]),
                    [string name] => Invoked(groups[name]),
                    _ => NotHandled,
                };
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeMatch(string methodName, Match match, object?[] args)
        {
            if (methodName.Equals(nameof(Match.Empty), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(Match.Empty) : NotHandled;
            }

            if (methodName.Equals(nameof(Match.Groups), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(match.Groups) : NotHandled;
            }

            if (methodName.Equals(nameof(Match.NextMatch), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(match.NextMatch()) : NotHandled;
            }

            if (methodName.Equals(nameof(Match.Result), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string replacement] ? Invoked(match.Result(replacement)) : NotHandled;
            }

            return TryInvokeGroup(methodName, match, args);
        }

        private static WellKnownFunctionResult TryInvokeMatchCollection(string methodName, MatchCollection matches, object?[] args)
        {
            if (methodName.Equals(nameof(MatchCollection.Count), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(matches.Count) : NotHandled;
            }

            if (methodName.Equals("get_Item", StringComparison.OrdinalIgnoreCase))
            {
                return args is [var index] && ArgumentParser.TryConvertToInt(index, out int convertedIndex)
                    ? Invoked(matches[convertedIndex])
                    : NotHandled;
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeRegexInstance(string methodName, Regex regex, object?[] args)
        {
            if (methodName.Equals(nameof(Regex.IsMatch), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [string input] => Invoked(regex.IsMatch(input)),
                    [string input, var startAt] when ArgumentParser.TryConvertToInt(startAt, out int convertedStartAt)
                        => Invoked(regex.IsMatch(input, convertedStartAt)),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(Regex.Match), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [string input] => Invoked(regex.Match(input)),
                    [string input, var startAt] when ArgumentParser.TryConvertToInt(startAt, out int convertedStartAt)
                        => Invoked(regex.Match(input, convertedStartAt)),
                    [string input, var beginning, var length]
                        when ArgumentParser.TryConvertToInt(beginning, out int convertedBeginning) &&
                             ArgumentParser.TryConvertToInt(length, out int convertedLength)
                        => Invoked(regex.Match(input, convertedBeginning, convertedLength)),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(Regex.Matches), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [string input] => Invoked(regex.Matches(input)),
                    [string input, var startAt] when ArgumentParser.TryConvertToInt(startAt, out int convertedStartAt)
                        => Invoked(regex.Matches(input, convertedStartAt)),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(Regex.Replace), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [string input, string replacement] => Invoked(regex.Replace(input, replacement)),
                    [string input, string replacement, var count] when ArgumentParser.TryConvertToInt(count, out int convertedCount)
                        => Invoked(regex.Replace(input, replacement, convertedCount)),
                    [string input, string replacement, var count, var startAt]
                        when ArgumentParser.TryConvertToInt(count, out int convertedCount) &&
                             ArgumentParser.TryConvertToInt(startAt, out int convertedStartAt)
                        => Invoked(regex.Replace(input, replacement, convertedCount, convertedStartAt)),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(Regex.Split), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [string input] => Invoked(regex.Split(input)),
                    [string input, var count] when ArgumentParser.TryConvertToInt(count, out int convertedCount)
                        => Invoked(regex.Split(input, convertedCount)),
                    [string input, var count, var startAt]
                        when ArgumentParser.TryConvertToInt(count, out int convertedCount) &&
                             ArgumentParser.TryConvertToInt(startAt, out int convertedStartAt)
                        => Invoked(regex.Split(input, convertedCount, convertedStartAt)),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(Regex.GetGroupNames), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(regex.GetGroupNames()) : NotHandled;
            }

            if (methodName.Equals(nameof(Regex.GetGroupNumbers), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(regex.GetGroupNumbers()) : NotHandled;
            }

            if (methodName.Equals(nameof(Regex.GroupNameFromNumber), StringComparison.OrdinalIgnoreCase))
            {
                return args is [var number] && ArgumentParser.TryConvertToInt(number, out int convertedNumber)
                    ? Invoked(regex.GroupNameFromNumber(convertedNumber))
                    : NotHandled;
            }

            if (methodName.Equals(nameof(Regex.GroupNumberFromName), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string name] ? Invoked(regex.GroupNumberFromName(name)) : NotHandled;
            }

            if (methodName.Equals(nameof(Regex.MatchTimeout), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(regex.MatchTimeout) : NotHandled;
            }

            if (methodName.Equals(nameof(Regex.Options), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(regex.Options) : NotHandled;
            }

            if (methodName.Equals(nameof(Regex.RightToLeft), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(regex.RightToLeft) : NotHandled;
            }

            if (methodName.Equals(nameof(Regex.ToString), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(regex.ToString()) : NotHandled;
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeStaticIsMatch(object?[] args)
            => args switch
            {
                [string input, string pattern] => Invoked(Regex.IsMatch(input, pattern)),
                [string input, string pattern, var options] when ArgumentParser.TryConvertToEnum(options, out RegexOptions convertedOptions)
                    => Invoked(Regex.IsMatch(input, pattern, convertedOptions)),
                [string input, string pattern, var options, TimeSpan timeout]
                    when ArgumentParser.TryConvertToEnum(options, out RegexOptions convertedOptions)
                    => Invoked(Regex.IsMatch(input, pattern, convertedOptions, timeout)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeStaticMatch(object?[] args)
            => args switch
            {
                [string input, string pattern] => Invoked(Regex.Match(input, pattern)),
                [string input, string pattern, var options] when ArgumentParser.TryConvertToEnum(options, out RegexOptions convertedOptions)
                    => Invoked(Regex.Match(input, pattern, convertedOptions)),
                [string input, string pattern, var options, TimeSpan timeout]
                    when ArgumentParser.TryConvertToEnum(options, out RegexOptions convertedOptions)
                    => Invoked(Regex.Match(input, pattern, convertedOptions, timeout)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeStaticMatches(object?[] args)
            => args switch
            {
                [string input, string pattern] => Invoked(Regex.Matches(input, pattern)),
                [string input, string pattern, var options] when ArgumentParser.TryConvertToEnum(options, out RegexOptions convertedOptions)
                    => Invoked(Regex.Matches(input, pattern, convertedOptions)),
                [string input, string pattern, var options, TimeSpan timeout]
                    when ArgumentParser.TryConvertToEnum(options, out RegexOptions convertedOptions)
                    => Invoked(Regex.Matches(input, pattern, convertedOptions, timeout)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeReplace(object?[] args)
            => args switch
            {
                [string input, string pattern, string replacement] => Invoked(Regex.Replace(input, pattern, replacement)),
                [string input, string pattern, string replacement, var options]
                    when ArgumentParser.TryConvertToEnum(options, out RegexOptions convertedOptions)
                    => Invoked(Regex.Replace(input, pattern, replacement, convertedOptions)),
                [string input, string pattern, string replacement, var options, TimeSpan timeout]
                    when ArgumentParser.TryConvertToEnum(options, out RegexOptions convertedOptions)
                    => Invoked(Regex.Replace(input, pattern, replacement, convertedOptions, timeout)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeStaticSplit(object?[] args)
            => args switch
            {
                [string input, string pattern] => Invoked(Regex.Split(input, pattern)),
                [string input, string pattern, var options] when ArgumentParser.TryConvertToEnum(options, out RegexOptions convertedOptions)
                    => Invoked(Regex.Split(input, pattern, convertedOptions)),
                [string input, string pattern, var options, TimeSpan timeout]
                    when ArgumentParser.TryConvertToEnum(options, out RegexOptions convertedOptions)
                    => Invoked(Regex.Split(input, pattern, convertedOptions, timeout)),
                _ => NotHandled,
            };
    }
}
