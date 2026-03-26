using System.Linq;
using SyncTrayzor.Utils;
using Xunit;

namespace SyncTrayzor.Tests
{
    public class StringExtensionsTests
    {
        // ── TrimStart(string prefix) ────────────────────────────────────────

        [Fact]
        public void TrimStart_RemovesPrefixWhenPresent()
        {
            Assert.Equal("world", "helloworld".TrimStart("hello"));
        }

        [Fact]
        public void TrimStart_ReturnsOriginalWhenPrefixAbsent()
        {
            Assert.Equal("world", "world".TrimStart("hello"));
        }

        [Fact]
        public void TrimStart_EmptyPrefixReturnsOriginal()
        {
            Assert.Equal("hello", "hello".TrimStart(""));
        }

        [Fact]
        public void TrimStart_OnlyRemovesLeadingOccurrence()
        {
            // "abab" starts with "ab", so the result should be "ab" (second occurrence)
            Assert.Equal("ab", "abab".TrimStart("ab"));
        }

        // ── TrimMatchingQuotes(char quote) ─────────────────────────────────

        [Fact]
        public void TrimMatchingQuotes_RemovesWrappingDoubleQuotes()
        {
            Assert.Equal("hello world", "\"hello world\"".TrimMatchingQuotes('"'));
        }

        [Fact]
        public void TrimMatchingQuotes_ReturnsOriginalWhenNotWrapped()
        {
            Assert.Equal("hello", "hello".TrimMatchingQuotes('"'));
        }

        [Fact]
        public void TrimMatchingQuotes_ReturnsOriginalWhenOnlyLeadingQuote()
        {
            Assert.Equal("\"hello", "\"hello".TrimMatchingQuotes('"'));
        }

        [Fact]
        public void TrimMatchingQuotes_ReturnsOriginalWhenOnlyTrailingQuote()
        {
            Assert.Equal("hello\"", "hello\"".TrimMatchingQuotes('"'));
        }

        [Fact]
        public void TrimMatchingQuotes_ReturnsEmptyStringForTwoQuotes()
        {
            Assert.Equal("", "\"\"".TrimMatchingQuotes('"'));
        }

        // ── SplitCommandLine ────────────────────────────────────────────────

        [Fact]
        public void SplitCommandLine_SplitsSimpleArgs()
        {
            var result = StringExtensions.SplitCommandLine("foo bar baz").ToList();
            Assert.Equal(new[] { "foo", "bar", "baz" }, result);
        }

        [Fact]
        public void SplitCommandLine_KeepsQuotedSpaces()
        {
            var result = StringExtensions.SplitCommandLine("\"hello world\" foo").ToList();
            Assert.Equal(new[] { "hello world", "foo" }, result);
        }

        [Fact]
        public void SplitCommandLine_EmptyStringReturnsNoElements()
        {
            var result = StringExtensions.SplitCommandLine("").ToList();
            Assert.Empty(result);
        }

        [Fact]
        public void SplitCommandLine_SingleArgReturnsOneElement()
        {
            var result = StringExtensions.SplitCommandLine("only").ToList();
            Assert.Equal(new[] { "only" }, result);
        }

        [Fact]
        public void SplitCommandLine_SkipsExtraSpaces()
        {
            var result = StringExtensions.SplitCommandLine("foo  bar").ToList();
            // extra space creates an empty token which is filtered out
            Assert.Equal(new[] { "foo", "bar" }, result);
        }

        // ── JoinCommandLine ─────────────────────────────────────────────────

        [Fact]
        public void JoinCommandLine_SimpleArgsNoSpaces()
        {
            Assert.Equal("foo bar", StringExtensions.JoinCommandLine(new[] { "foo", "bar" }));
        }

        [Fact]
        public void JoinCommandLine_QuotesArgWithSpace()
        {
            Assert.Equal("\"hello world\" foo", StringExtensions.JoinCommandLine(new[] { "hello world", "foo" }));
        }

        [Fact]
        public void JoinCommandLine_EscapesEmbeddedQuotes()
        {
            // An arg that itself contains a double-quote should be quoted and the inner quote escaped
            Assert.Equal("\"say \\\"hi\\\"\"", StringExtensions.JoinCommandLine(new[] { "say \"hi\"" }));
        }

        [Fact]
        public void JoinCommandLine_EmptyInputReturnsEmptyString()
        {
            Assert.Equal("", StringExtensions.JoinCommandLine(new string[] { }));
        }

        // ── Round-trip: SplitCommandLine ∘ JoinCommandLine ─────────────────

        [Fact]
        public void RoundTrip_SplitThenJoin()
        {
            var original = new[] { "path to/file", "simple", "--flag=value" };
            var joined = StringExtensions.JoinCommandLine(original);
            var split = StringExtensions.SplitCommandLine(joined).ToArray();
            Assert.Equal(original, split);
        }
    }
}
