using SyncTrayzor.Utils;
using Xunit;

namespace SyncTrayzor.Tests
{
    public class FormatUtilsTests
    {
        // ── BytesToHuman ────────────────────────────────────────────────────

        [Theory]
        [InlineData(0,    0, "0B")]
        [InlineData(1,    0, "1B")]
        [InlineData(1023, 0, "1023B")]
        [InlineData(1024, 0, "1KiB")]
        [InlineData(1536, 0, "2KiB")]          // 1.5 KiB rounds to 2 with 0 decimal places
        [InlineData(1048576, 0, "1MiB")]        // 1 MiB
        [InlineData(1073741824, 0, "1GiB")]     // 1 GiB
        public void BytesToHuman_DefaultDecimalPlaces(double bytes, int decimalPlaces, string expected)
        {
            Assert.Equal(expected, FormatUtils.BytesToHuman(bytes, decimalPlaces));
        }

        [Theory]
        [InlineData(1536,    1, "1.5KiB")]      // exactly 1.5 KiB
        [InlineData(1024,    2, "1.00KiB")]
        [InlineData(1048576, 1, "1.0MiB")]
        public void BytesToHuman_WithDecimalPlaces(double bytes, int decimalPlaces, string expected)
        {
            Assert.Equal(expected, FormatUtils.BytesToHuman(bytes, decimalPlaces));
        }

        [Fact]
        public void BytesToHuman_ByteRangeNeverShowsDecimalPlaces()
        {
            // Bytes (order == 0) should never have decimal places regardless of the argument
            var result = FormatUtils.BytesToHuman(512, decimalPlaces: 3);
            Assert.Equal("512B", result);
        }

        [Fact]
        public void BytesToHuman_CapsAtGiB()
        {
            // Even a very large value should be expressed in GiB, not some higher unit
            var result = FormatUtils.BytesToHuman(1024.0 * 1024 * 1024 * 10, decimalPlaces: 0);
            Assert.EndsWith("GiB", result);
        }
    }
}
