﻿using NLog;
using SyncTrayzor.Utils;
using System;
using System.IO;
using System.Security.Cryptography;

namespace SyncTrayzor.Services.UpdateManagement
{
    public interface IInstallerCertificateVerifier
    {
        bool VerifySha512sum(string filePath, out Stream cleartext);
        bool VerifyUpdate(string filePath, Stream sha512sumFile, string originalFileName);
    }

    public class InstallerCertificateVerifier : IInstallerCertificateVerifier
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private const string certificateName = "SyncTrayzor.Resources.synctrayzor_releases_cert.asc";

        private readonly IAssemblyProvider assemblyProvider;
        private readonly IFilesystemProvider filesystemProvider;

        public InstallerCertificateVerifier(IAssemblyProvider assemblyProvider, IFilesystemProvider filesystemProvider)
        {
            this.assemblyProvider = assemblyProvider;
            this.filesystemProvider = filesystemProvider;
        }

        private Stream LoadCertificate()
        {
            return assemblyProvider.GetManifestResourceStream(certificateName);
        }

        public bool VerifySha512sum(string filePath, out Stream cleartext)
        {
            using var file = filesystemProvider.OpenRead(filePath);
            using var certificate = LoadCertificate();
            return PgpClearsignUtilities.ReadAndVerifyFile(file, certificate, out cleartext);
        }

        public bool VerifyUpdate(string filePath, Stream sha512sumFile, string originalFileName)
        {
            using var hashAlgorithm = SHA512.Create();
            using var file = filesystemProvider.OpenRead(filePath);
            try
            {
                return ChecksumFileUtilities.ValidateChecksum(hashAlgorithm, sha512sumFile, originalFileName, file);
            }
            catch (ArgumentException)
            {
                logger.Warn("Could not find checksum for file {0}", originalFileName);
                return false;
            }
        }
    }
}
