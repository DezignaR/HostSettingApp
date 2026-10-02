using System.Collections.Generic;
using System.Threading.Tasks;
using WpfApp1.Models;

namespace WpfApp1.Services
{
    public interface INetworkService
    {
        Task<List<string>> GetNetAdaptersAsync();
        Task<List<IPConfigItem>> GetIpAddressesFromAdapterAsync(string adapterName);
        Task AddAdditionalIPAsync(string adapterName, string extraIp, string subnetMask);
        Task RemoveAdditionalIPAsync(string adapterName, string ipAddress);
        Task<bool> PingAddressAsync(string ipAddress);
    }
}
