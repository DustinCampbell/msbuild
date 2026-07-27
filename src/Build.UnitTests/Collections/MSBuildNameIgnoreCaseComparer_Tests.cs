// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Text;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Collections
{
    /// <summary>
    /// Tests for MSBuildNameIgnoreCaseComparer
    /// </summary>
    public class MSBuildNameIgnoreCaseComparer_Tests
    {
        /// <summary>
        /// Verify default comparer works on the whole string
        /// </summary>
        [Fact]
        public void DefaultEquals()
        {
            Assert.True(MSBuildNameIgnoreCaseComparer.Default.Equals("FOO", "foo"));
            Assert.False(MSBuildNameIgnoreCaseComparer.Default.Equals("FOO", " FOO"));
            Assert.False(MSBuildNameIgnoreCaseComparer.Default.Equals("FOOA", "FOOB"));
            Assert.False(MSBuildNameIgnoreCaseComparer.Default.Equals("AFOO", "BFOO"));
            Assert.False(MSBuildNameIgnoreCaseComparer.Default.Equals("FOO", "FOO "));
            Assert.False(MSBuildNameIgnoreCaseComparer.Default.Equals("a", "b"));
            Assert.True(MSBuildNameIgnoreCaseComparer.Default.Equals("", ""));
            Assert.False(MSBuildNameIgnoreCaseComparer.Default.Equals("x", null));
            Assert.False(MSBuildNameIgnoreCaseComparer.Default.Equals(null, "x"));
            Assert.True(MSBuildNameIgnoreCaseComparer.Default.Equals((string)null, (string)null));
        }

        /// <summary>
        /// Compare real expressions
        /// </summary>
        [Fact]
        public void MatchProperty()
        {
            MSBuildNameIgnoreCaseComparer comparer = MSBuildNameIgnoreCaseComparer.Default;
            PropertyDictionary<ProjectPropertyInstance> dictionary = new PropertyDictionary<ProjectPropertyInstance>(comparer);

            ProjectPropertyInstance p = ProjectPropertyInstance.Create("foo", "bar");

            dictionary.Set(p);

            string s = "$(foo)";
            ProjectPropertyInstance value = dictionary.GetProperty(s, 2, 4);

            Assert.True(Object.ReferenceEquals(p, value)); // "Should have returned the same object as was inserted"

            Assert.Equal(MSBuildNameIgnoreCaseComparer.Default.GetHashCode("foo"), comparer.GetHashCode(s, 2, 3));
        }

        /// <summary>
        /// Null
        /// </summary>
        [Fact]
        public void Null1()
        {
            Assert.False(MSBuildNameIgnoreCaseComparer.Default.Equals("x", null));
        }

        /// <summary>
        /// Null
        /// </summary>
        [Fact]
        public void Null2()
        {
            Assert.False(MSBuildNameIgnoreCaseComparer.Default.Equals(null, "x"));
        }

        /// <summary>
        /// Invalid start
        /// </summary>
        [Fact]
        public void InvalidValue2()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                MSBuildNameIgnoreCaseComparer.Default.Equals("x", "y", -1, 0);
            });
        }
        /// <summary>
        /// Invalid small end
        /// </summary>
        [Fact]
        public void InvalidValue4()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                MSBuildNameIgnoreCaseComparer.Default.Equals("x", "y", 0, -1);
            });
        }
        /// <summary>
        /// Invalid large end
        /// </summary>
        [Fact]
        public void InvalidValue5()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                MSBuildNameIgnoreCaseComparer.Default.Equals("x", "y", 0, 2);
            });
        }
        /// <summary>
        /// End past the end of other string
        /// </summary>
        [Fact]
        public void EqualsEndPastEnd1()
        {
            Assert.True(MSBuildNameIgnoreCaseComparer.Default.Equals("bbb", "abbbaaa", 1, 3));
        }

        /// <summary>
        /// Same values means one char
        /// </summary>
        [Fact]
        public void EqualsSameStartEnd1()
        {
            Assert.True(MSBuildNameIgnoreCaseComparer.Default.Equals("A", "babbbb", 1, 1));
        }

        /// <summary>
        /// Same values means one char
        /// </summary>
        [Fact]
        public void EqualsSameStartEnd2()
        {
            Assert.True(MSBuildNameIgnoreCaseComparer.Default.Equals("b", "aabaa", 2, 1));
        }

        /// <summary>
        /// Same values means one char
        /// </summary>
        [Fact]
        public void EqualsSameStartEnd3()
        {
            Assert.True(MSBuildNameIgnoreCaseComparer.Default.Equals("a", "ab", 0, 1));
        }

        /// <summary>
        /// Start at 0
        /// </summary>
        [Fact]
        public void EqualsStartZero()
        {
            Assert.True(MSBuildNameIgnoreCaseComparer.Default.Equals("aab", "aabaa", 0, 3));
        }

        /// <summary>
        /// Default get hash code
        /// </summary>
        [Fact]
        public void DefaultGetHashcode()
        {
            Assert.True(0 == MSBuildNameIgnoreCaseComparer.Default.GetHashCode((string)null));

            MSBuildNameIgnoreCaseComparer.Default.GetHashCode(""); // doesn't throw
            Assert.Equal(MSBuildNameIgnoreCaseComparer.Default.GetHashCode("aBc"), MSBuildNameIgnoreCaseComparer.Default.GetHashCode("AbC"));
        }

        /// <summary>
        /// Indexed get hashcode
        /// </summary>
        [Fact]
        public void IndexedGetHashcode1()
        {
            MSBuildNameIgnoreCaseComparer comparer = MSBuildNameIgnoreCaseComparer.Default;

            comparer.GetHashCode(""); // does not crash

            Assert.True(0 == comparer.GetHashCode((string)null));
            Assert.Equal(comparer.GetHashCode("aBc"), comparer.GetHashCode("AbC"));
            Assert.Equal(comparer.GetHashCode("xyz", 0, 1), comparer.GetHashCode("x"));
        }

        /// <summary>
        /// Indexed get hashcode
        /// </summary>
        [Fact]
        public void IndexedGetHashcode2()
        {
            MSBuildNameIgnoreCaseComparer comparer = MSBuildNameIgnoreCaseComparer.Default;

            Assert.Equal(comparer.GetHashCode("xyz", 1, 2), comparer.GetHashCode("YZ"));
        }

        /// <summary>
        /// Indexed get hashcode
        /// </summary>
        [Fact]
        public void IndexedGetHashcode3()
        {
            MSBuildNameIgnoreCaseComparer comparer = MSBuildNameIgnoreCaseComparer.Default;

            Assert.Equal(comparer.GetHashCode("abcd", 0, 3), comparer.GetHashCode("abc"));
        }

        /// <summary>
        /// The StringSegment Equals overload must produce the same result as the (buffer, start, length)
        /// triple it encapsulates, since a segment view of a name is meant to replace that triple.
        /// </summary>
        [Fact]
        public void EqualsStringSegmentMatchesTriple()
        {
            MSBuildNameIgnoreCaseComparer comparer = MSBuildNameIgnoreCaseComparer.Default;

            // "$(foo)" -> the inner "foo" is offset 2, length 3.
            StringSegment fooSegment = new StringSegment("$(foo)", 2, 3);

            comparer.Equals("foo", fooSegment).ShouldBeTrue();
            comparer.Equals("FOO", fooSegment).ShouldBeTrue();
            comparer.Equals("food", fooSegment).ShouldBeFalse();
            comparer.Equals("fo", fooSegment).ShouldBeFalse();

            // Equivalent to the triple overload.
            comparer.Equals("foo", fooSegment).ShouldBe(comparer.Equals("foo", "$(foo)", 2, 3));
        }

        /// <summary>
        /// The StringSegment GetHashCode overload must produce the same hash as the (buffer, start, length)
        /// triple and the same hash as the whole equivalent string. This is the correctness crux: a name
        /// stored as a whole string must be retrievable by a segment view of a larger buffer.
        /// </summary>
        [Fact]
        public void GetHashCodeStringSegmentMatchesTriple()
        {
            MSBuildNameIgnoreCaseComparer comparer = MSBuildNameIgnoreCaseComparer.Default;

            StringSegment fooSegment = new StringSegment("$(foo)", 2, 3);

            comparer.GetHashCode(fooSegment).ShouldBe(comparer.GetHashCode("$(foo)", 2, 3));
            comparer.GetHashCode(fooSegment).ShouldBe(comparer.GetHashCode("foo"));

            // Case-insensitivity is preserved through the segment overload.
            comparer.GetHashCode(new StringSegment("FOO")).ShouldBe(comparer.GetHashCode("foo"));

            // A default (null) segment hashes to 0, matching the null-string convention.
            comparer.GetHashCode(default(StringSegment)).ShouldBe(0);
        }

        /// <summary>
        /// End-to-end proof that a property stored by whole-string key can be retrieved by a
        /// StringSegment view of a larger buffer, using the same dictionary and comparer. If the
        /// segment hash did not match the stored-string hash, this lookup would miss.
        /// </summary>
        [Fact]
        public void PropertyDictionaryGetPropertyByStringSegment()
        {
            MSBuildNameIgnoreCaseComparer comparer = MSBuildNameIgnoreCaseComparer.Default;
            PropertyDictionary<ProjectPropertyInstance> dictionary = new PropertyDictionary<ProjectPropertyInstance>(comparer);

            ProjectPropertyInstance p = ProjectPropertyInstance.Create("foo", "bar");
            dictionary.Set(p);

            // "$(foo)" -> inner name "foo" is offset 2, length 3.
            StringSegment nameSegment = new StringSegment("$(foo)", 2, 3);
            ProjectPropertyInstance value = dictionary.GetProperty(nameSegment);

            value.ShouldBeSameAs(p);

            // A segment that does not match any stored key returns null.
            dictionary.GetProperty(new StringSegment("$(bar)", 2, 3)).ShouldBeNull();
        }
    }
}
