using System;
using SyncTrayzor.Utils;
using Xunit;

namespace SyncTrayzor.Tests
{
    public class UriExtensionsTests
    {
        [Fact]
        public void NormalizeZeroHost_ReplacesZeroHostWith127()
        {
            var input = new Uri("http://0.0.0.0:8384/");
            var result = input.NormalizeZeroHost();
            Assert.Equal("127.0.0.1", result.Host);
        }

        [Fact]
        public void NormalizeZeroHost_PreservesPort()
        {
            var input = new Uri("http://0.0.0.0:8384/");
            var result = input.NormalizeZeroHost();
            Assert.Equal(8384, result.Port);
        }

        [Fact]
        public void NormalizeZeroHost_PreservesPath()
        {
            var input = new Uri("http://0.0.0.0:8384/some/path");
            var result = input.NormalizeZeroHost();
            Assert.Equal("/some/path", result.AbsolutePath);
        }

        [Fact]
        public void NormalizeZeroHost_LeavesNonZeroHostUnchanged()
        {
            var input = new Uri("http://192.168.1.1:8384/");
            var result = input.NormalizeZeroHost();
            Assert.Equal("192.168.1.1", result.Host);
        }

        [Fact]
        public void NormalizeZeroHost_LeavesLocalhostUnchanged()
        {
            var input = new Uri("http://localhost:8384/");
            var result = input.NormalizeZeroHost();
            Assert.Equal("localhost", result.Host);
        }
    }
}
