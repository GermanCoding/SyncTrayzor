﻿using NLog;
using System;
using System.Management;

namespace SyncTrayzor.Services
{
    public class GraphicsCardDetector
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private bool? _isIntelXe;
        public bool IsIntelXe
        {
            get
            {
                if (_isIntelXe == null)
                    _isIntelXe = GetIsIntelXe();
                return _isIntelXe.Value;
            }
        }

        private static bool GetIsIntelXe()
        {
            // ManagedObjectEnumerator.MoveNext can throw a COMException if WMI isn't started.
            // We also want to ignore other potential problems. If we hit something, assume they're not
            // using Intel Xe Graphics.
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                foreach (ManagementObject obj in searcher.Get())
                {
                    if (obj["CurrentBitsPerPixel"] != null && obj["CurrentHorizontalResolution"] != null)
                    {
                        string name = obj["Name"]?.ToString();
                        if (name.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            name.IndexOf(" Xe ", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            logger.Info($"Graphics card: {name}");
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }
    }
}
