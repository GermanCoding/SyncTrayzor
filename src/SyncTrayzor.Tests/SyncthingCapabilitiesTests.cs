using System;
using SyncTrayzor.Syncthing;
using Xunit;

namespace SyncTrayzor.Tests
{
    public class SyncthingCapabilitiesTests
    {
        // ── SupportsDebugFacilities ──────────────────────────────────────────

        [Fact]
        public void SupportsDebugFacilities_FalseBeforeIntroducedVersion()
        {
            var caps = new SyncthingCapabilities { SyncthingVersion = new Version(0, 11, 99) };
            Assert.False(caps.SupportsDebugFacilities);
        }

        [Fact]
        public void SupportsDebugFacilities_TrueAtIntroducedVersion()
        {
            var caps = new SyncthingCapabilities { SyncthingVersion = new Version(0, 12, 0) };
            Assert.True(caps.SupportsDebugFacilities);
        }

        [Fact]
        public void SupportsDebugFacilities_TrueAfterIntroducedVersion()
        {
            var caps = new SyncthingCapabilities { SyncthingVersion = new Version(1, 0, 0) };
            Assert.True(caps.SupportsDebugFacilities);
        }

        // ── SupportsDevicePauseResume ────────────────────────────────────────

        [Fact]
        public void SupportsDevicePauseResume_FalseBeforeIntroducedVersion()
        {
            var caps = new SyncthingCapabilities { SyncthingVersion = new Version(0, 11, 0) };
            Assert.False(caps.SupportsDevicePauseResume);
        }

        [Fact]
        public void SupportsDevicePauseResume_TrueAtIntroducedVersion()
        {
            var caps = new SyncthingCapabilities { SyncthingVersion = new Version(0, 12, 0) };
            Assert.True(caps.SupportsDevicePauseResume);
        }

        [Fact]
        public void SupportsDevicePauseResume_TrueAfterIntroducedVersion()
        {
            var caps = new SyncthingCapabilities { SyncthingVersion = new Version(2, 0, 0) };
            Assert.True(caps.SupportsDevicePauseResume);
        }

        // ── Default version (0.0.0) ──────────────────────────────────────────

        [Fact]
        public void DefaultVersion_NoCapabilitiesSupported()
        {
            var caps = new SyncthingCapabilities(); // default version is 0.0.0
            Assert.False(caps.SupportsDebugFacilities);
            Assert.False(caps.SupportsDevicePauseResume);
        }
    }
}
