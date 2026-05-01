using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SyncTrayzor.Utils;
using Xunit;

namespace SyncTrayzor.Tests
{
    public class ChecksumFileUtilitiesTests
    {
        private static MemoryStream ToStream(string text) =>
            new(Encoding.UTF8.GetBytes(text));

        private static MemoryStream ToStream(byte[] bytes) => new(bytes);

        // ── WriteChecksumToFile ──────────────────────────────────────────────

        [Fact]
        public void WriteChecksumToFile_ProducesValidChecksumLine()
        {
            var content = Encoding.UTF8.GetBytes("hello world");
            using var fileStream = ToStream(content);
            using var checksumStream = new MemoryStream();

            using var sha256 = SHA256.Create();
            ChecksumFileUtilities.WriteChecksumToFile(sha256, checksumStream, "test.txt", fileStream);

            checksumStream.Position = 0;
            var line = new StreamReader(checksumStream, Encoding.ASCII).ReadLine();

            // Expected: "<hex>  test.txt"
            Assert.NotNull(line);
            var parts = line!.Split("  ", 2);
            Assert.Equal(2, parts.Length);
            Assert.Equal("test.txt", parts[1].Trim());
            Assert.Equal(64, parts[0].Length); // SHA-256 hex is 64 chars
        }

        // ── ValidateChecksum – positive case ────────────────────────────────

        [Fact]
        public void ValidateChecksum_ReturnsTrueForMatchingContent()
        {
            var content = Encoding.UTF8.GetBytes("hello world");
            var checksumLine = ComputeChecksumLine(content, "test.txt");

            using var checksumStream = ToStream(checksumLine);
            using var fileStream = ToStream(content);

            using var sha256 = SHA256.Create();
            Assert.True(ChecksumFileUtilities.ValidateChecksum(sha256, checksumStream, "test.txt", fileStream));
        }

        // ── ValidateChecksum – negative case ────────────────────────────────

        [Fact]
        public void ValidateChecksum_ReturnsFalseForModifiedContent()
        {
            var originalContent = Encoding.UTF8.GetBytes("hello world");
            var modifiedContent = Encoding.UTF8.GetBytes("hello WORLD");
            var checksumLine = ComputeChecksumLine(originalContent, "test.txt");

            using var checksumStream = ToStream(checksumLine);
            using var fileStream = ToStream(modifiedContent);

            using var sha256 = SHA256.Create();
            Assert.False(ChecksumFileUtilities.ValidateChecksum(sha256, checksumStream, "test.txt", fileStream));
        }

        // ── ValidateChecksum – multi-entry checksum file ─────────────────────

        [Fact]
        public void ValidateChecksum_FindsCorrectEntryInMultiLineFile()
        {
            var content = Encoding.UTF8.GetBytes("data");
            var otherContent = Encoding.UTF8.GetBytes("other");

            var lines = ComputeChecksumLine(otherContent, "other.txt")
                      + ComputeChecksumLine(content, "data.txt");

            using var checksumStream = ToStream(lines);
            using var fileStream = ToStream(content);

            using var sha256 = SHA256.Create();
            Assert.True(ChecksumFileUtilities.ValidateChecksum(sha256, checksumStream, "data.txt", fileStream));
        }

        // ── ValidateChecksum – missing filename ──────────────────────────────

        [Fact]
        public void ValidateChecksum_ThrowsWhenFilenameNotFound()
        {
            var content = Encoding.UTF8.GetBytes("hello");
            var checksumLine = ComputeChecksumLine(content, "other.txt");

            using var checksumStream = ToStream(checksumLine);
            using var fileStream = ToStream(content);

            using var sha256 = SHA256.Create();
            Assert.Throws<ArgumentException>(() =>
                ChecksumFileUtilities.ValidateChecksum(sha256, checksumStream, "missing.txt", fileStream));
        }

        // ── Round-trip: WriteChecksumToFile then ValidateChecksum ────────────

        [Fact]
        public void RoundTrip_WriteAndValidate()
        {
            var content = Encoding.UTF8.GetBytes("round-trip test content");
            using var fileStreamWrite = ToStream(content);
            using var checksumStream = new MemoryStream();

            using var sha256Write = SHA256.Create();
            ChecksumFileUtilities.WriteChecksumToFile(sha256Write, checksumStream, "file.bin", fileStreamWrite);

            checksumStream.Position = 0;
            using var fileStreamValidate = ToStream(content);
            using var sha256Validate = SHA256.Create();
            Assert.True(ChecksumFileUtilities.ValidateChecksum(sha256Validate, checksumStream, "file.bin", fileStreamValidate));
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string ComputeChecksumLine(byte[] content, string filename)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(content);
            var hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            return $"{hex}  {filename}\n";
        }
    }
}
