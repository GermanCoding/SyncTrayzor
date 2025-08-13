#nullable enable
using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;

namespace SyncTrayzor.Services.Metering;

public static class NetworkUtils
{
    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetBestInterfaceEx(IntPtr sockaddr, out uint interfaceIndex);

    private const ushort AF_INET = 2; // IPv4
    private const ushort AF_INET6 = 23; // IPv6

    [StructLayout(LayoutKind.Sequential)]
    private struct SOCKADDR_IN
    {
        public ushort sin_family;
        public ushort sin_port;
        public uint sin_addr; // IPv4 address

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] sin_zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SOCKADDR_IN6
    {
        public ushort sin6_family;
        public ushort sin6_port;
        public uint sin6_flowinfo;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] sin6_addr;

        public uint sin6_scope_id;
    }

    public static uint GetBestInterfaceIndex(IPAddress destinationAddress)
    {
        if (destinationAddress == null)
            throw new ArgumentNullException(nameof(destinationAddress));

        IntPtr pSockAddr = IntPtr.Zero;

        try
        {
            if (destinationAddress.AddressFamily == AddressFamily.InterNetwork)
            {
                var sockaddr = new SOCKADDR_IN
                {
                    sin_family = AF_INET,
                    sin_port = 0,
                    sin_addr = BitConverter.ToUInt32(destinationAddress.GetAddressBytes(), 0),
                    sin_zero = new byte[8]
                };

                pSockAddr = Marshal.AllocHGlobal(Marshal.SizeOf<SOCKADDR_IN>());
                Marshal.StructureToPtr(sockaddr, pSockAddr, false);
            }
            else if (destinationAddress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var sockaddr = new SOCKADDR_IN6
                {
                    sin6_family = AF_INET6,
                    sin6_port = 0,
                    sin6_flowinfo = 0,
                    sin6_addr = destinationAddress.GetAddressBytes(),
                    sin6_scope_id = (uint)destinationAddress.ScopeId
                };

                pSockAddr = Marshal.AllocHGlobal(Marshal.SizeOf<SOCKADDR_IN6>());
                Marshal.StructureToPtr(sockaddr, pSockAddr, false);
            }
            else
            {
                throw new NotSupportedException("Only IPv4 and IPv6 are supported");
            }

            int result = GetBestInterfaceEx(pSockAddr, out uint interfaceIndex);
            if (result != 0)
            {
                throw new InvalidOperationException($"GetBestInterfaceEx failed with error {result}");
            }

            return interfaceIndex;
        }
        finally
        {
            if (pSockAddr != IntPtr.Zero)
                Marshal.FreeHGlobal(pSockAddr);
        }
    }

    public static NetworkInterface GetBestNetworkInterface(IPAddress remoteAddress)
    {
        var interfaceIndex = GetBestInterfaceIndex(remoteAddress);

        // Find a matching .NET interface object with the given index.
        foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            var ipProperties = networkInterface.GetIPProperties();
            try
            {
                switch (remoteAddress.AddressFamily)
                {
                    case AddressFamily.InterNetwork:
                        if (ipProperties.GetIPv4Properties().Index == interfaceIndex)
                        {
                            return networkInterface;
                        }

                        break;
                    case AddressFamily.InterNetworkV6:
                        if (ipProperties.GetIPv6Properties().Index == interfaceIndex)
                        {
                            return networkInterface;
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(remoteAddress));
                }
            }
            catch (NetworkInformationException)
            {
                // Interface does not support the IP address, ignore
            }
        }

        throw new InvalidOperationException($"Could not find best interface for {remoteAddress}.");
    }

    public static async Task<ConnectionProfile?> GetNetworkProfileForIpAddress(IPAddress remoteAddress)
    {
        var networkInterface = GetBestNetworkInterface(remoteAddress);

        var networkInterfaceGuid = Guid.Parse(networkInterface.Id);
        var adapters = NetworkInformation.GetConnectionProfiles()
            .Select(existingProfile => existingProfile.NetworkAdapter).Distinct();
        foreach (var networkAdapter in adapters)
        {
            if (networkAdapter?.NetworkAdapterId == networkInterfaceGuid)
            {
                return await Task
                    .Run(async () => await networkAdapter.GetConnectedProfileAsync().AsTask().ConfigureAwait(false))
                    .ConfigureAwait(false);
            }
        }

        // No "real" adapter found... This can happen for e.g., virtual interfaces
        return null;
    }
}