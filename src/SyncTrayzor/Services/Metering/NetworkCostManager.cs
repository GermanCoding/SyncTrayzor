using System;
using System.Net;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;
using NLog;

namespace SyncTrayzor.Services.Metering
{
    public interface INetworkCostManager
    {
        bool IsSupported { get; }

        event EventHandler NetworksChanged;

        Task<bool> IsConnectionMetered(IPAddress address);
    }

    public class NetworkCostManager : INetworkCostManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public bool IsSupported => true;

        public event EventHandler NetworksChanged;

        public NetworkCostManager()
        {
            NetworkInformation.NetworkStatusChanged += NetworkStatusChanged;
        }

        public async Task<bool> IsConnectionMetered(IPAddress address)
        {
            var tcs = new TaskCompletionSource<bool>();
            await Task.Run(async () =>
            {
                try
                {
                    var metered = await IsConnectionMeteredUnsafe(address);
                    tcs.SetResult(metered);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });
            return await tcs.Task;
        }

        private async Task<bool> IsConnectionMeteredUnsafe(IPAddress address)
        {
            try
            {
                var profile = await NetworkUtils.GetNetworkProfileForIpAddress(address);
                if (profile == null)
                {
                    return false;
                }

                var cost = profile.GetConnectionCost();
                // A network is "metered" if it is not unrestricted or unknown:
                return cost.NetworkCostType != NetworkCostType.Unrestricted &&
                       cost.NetworkCostType != NetworkCostType.Unknown;
            }
            catch (Exception exception)
            {
                Logger.Warn(exception, $"Unable to retrieve network profile for IP {address}");
                return false;
            }
        }

        private void NetworkStatusChanged(object sender)
        {
            NetworksChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}