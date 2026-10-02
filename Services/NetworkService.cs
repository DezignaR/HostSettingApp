using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using WpfApp1.Models;

namespace WpfApp1.Services
{
    public class NetworkService : INetworkService
    {
        public async Task<List<string>> GetNetAdaptersAsync()
        {
            var adapters = new List<string>();
            var psCommand = "Get-NetAdapter | Where-Object {$_.Status -eq 'Up'} | Select-Object -ExpandProperty Name";
            using (var process = CreatePowerShellProcess(psCommand, false))
            {
                if (process == null || !process.Start()) return adapters;
                using (var reader = process.StandardOutput)
                {
                    while (!reader.EndOfStream)
                    {
                        string line = await reader.ReadLineAsync();
                        if (!string.IsNullOrWhiteSpace(line)) adapters.Add(line.Trim());
                    }
                }
                await process.WaitForExitAsync();
            }
            return adapters;
        }

        public async Task<List<IPConfigItem>> GetIpAddressesFromAdapterAsync(string adapterName)
        {
            var ipList = new List<IPConfigItem>();
            string escapedName = adapterName.Replace("'", "''");
            string psCommand = $"Get-NetIPAddress -InterfaceAlias '{escapedName}' -AddressFamily IPv4 | ForEach-Object {{ $bits = $_.PrefixLength; $bytes = [BitConverter]::GetBytes(([uint32]::MaxValue -shl (32 - $bits)) -band [uint32]::MaxValue); [Array]::Reverse($bytes); $mask = ([IPAddress]$bytes).IPAddressToString; [PSCustomObject]@{{ IP = $_.IPAddress; Mask = $mask; Family = $_.AddressFamily }} }} | ConvertTo-Csv -NoTypeInformation";
            using (var process = CreatePowerShellProcess(psCommand, false))
            {
                if (process == null || !process.Start()) return ipList;
                using (var reader = process.StandardOutput)
                {
                    await reader.ReadLineAsync();
                    while (!reader.EndOfStream)
                    {
                        string line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        string[] parts = line.Replace("\"", "").Split(',');
                        if (parts.Length >= 3)
                        {
                            ipList.Add(new IPConfigItem { IPAddress = parts[0].Trim(), SubnetMask = parts[1].Trim(), AddressFamily = parts[2].Trim() });
                        }
                    }
                }
                await process.WaitForExitAsync();
            }
            return ipList;
        }

        public async Task AddAdditionalIPAsync(string adapterName, string extraIp, string subnetMask)
        {
            string cleanAdapter = adapterName.Replace("'", "''").Trim();
            string cleanIp = extraIp.Trim();
            int prefixLength = ConvertMaskToPrefix(subnetMask);
            string psCommand = $"New-NetIPAddress -InterfaceAlias '{cleanAdapter}' -IPAddress '{cleanIp}' -PrefixLength {prefixLength} -SkipAsSource $true";
            using (var process = CreatePowerShellProcess(psCommand, true))
            {
                if (process != null && process.Start()) await process.WaitForExitAsync();
            }
        }

        public async Task RemoveAdditionalIPAsync(string adapterName, string ipAddress)
        {
            string cleanAdapter = adapterName.Replace("'", "''").Trim();
            string cleanIp = ipAddress.Trim();
            string psCommand = $"Remove-NetIPAddress -InterfaceAlias '{cleanAdapter}' -IPAddress '{cleanIp}' -Confirm:$false";
            using (var process = CreatePowerShellProcess(psCommand, true))
            {
                if (process != null && process.Start()) await process.WaitForExitAsync();
            }
        }

        public async Task<bool> PingAddressAsync(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress)) return false;
            try
            {
                using (var pingSender = new Ping())
                {
                    var reply = await pingSender.SendPingAsync(ipAddress, 1500);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch { return false; }
        }

        private Process CreatePowerShellProcess(string command, bool runAsAdmin)
        {
            string fullCommand = $"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; {command}";
            var psi = new ProcessStartInfo { FileName = "powershell.exe", Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{fullCommand}\"", UseShellExecute = runAsAdmin, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            if (runAsAdmin) psi.Verb = "runas";
            else { psi.RedirectStandardOutput = true; psi.StandardOutputEncoding = Encoding.UTF8; }
            return new Process { StartInfo = psi };
        }

        private static int ConvertMaskToPrefix(string subnetMask)
        {
            try
            {
                byte[] bytes = System.Net.IPAddress.Parse(subnetMask.Trim()).GetAddressBytes();
                int prefix = 0;
                foreach (byte b in bytes) { byte val = b; while (val > 0) { if ((val & 1) == 1) prefix++; val >>= 1; } }
                return prefix;
            }
            catch { return 24; }
        }
    }
}
