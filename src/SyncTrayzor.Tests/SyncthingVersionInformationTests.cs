using System;
using SyncTrayzor.Syncthing;
using Xunit;

namespace SyncTrayzor.Tests
{
    public class SyncthingVersionInformationTests
    {
        [Theory]
        [InlineData("v1.23.4",  "1.23.4")]
        [InlineData("v0.14.28", "0.14.28")]
        [InlineData("v0.12.0",  "0.12.0")]
        public void ParsedVersion_ExtractsVersionFromShortVersionString(string shortVersion, string expectedVersion)
        {
            var info = new SyncthingVersionInformation(shortVersion, "");
            Assert.Equal(Version.Parse(expectedVersion), info.ParsedVersion);
        }

        [Fact]
        public void ParsedVersion_HandlesLongVersionString()
        {
            // syncthing sometimes reports "syncthing v1.2.3 (go1.21 linux-amd64) ..."
            var info = new SyncthingVersionInformation("syncthing v1.2.3", "syncthing v1.2.3 (go1.21)");
            Assert.Equal(new Version(1, 2, 3), info.ParsedVersion);
        }

        [Fact]
        public void ParsedVersion_DefaultsToZeroWhenNoVersionFound()
        {
            var info = new SyncthingVersionInformation("no-version-here", "");
            Assert.Equal(new Version(0, 0, 0), info.ParsedVersion);
        }

        [Fact]
        public void ParsedVersion_DefaultsToZeroForEmptyString()
        {
            var info = new SyncthingVersionInformation("", "");
            Assert.Equal(new Version(0, 0, 0), info.ParsedVersion);
        }

        [Fact]
        public void ShortVersion_StoredAsProvided()
        {
            var info = new SyncthingVersionInformation("v1.0.0", "long version");
            Assert.Equal("v1.0.0", info.ShortVersion);
        }

        [Fact]
        public void LongVersion_StoredAsProvided()
        {
            var info = new SyncthingVersionInformation("v1.0.0", "long version");
            Assert.Equal("long version", info.LongVersion);
        }
    }
}
