// ==========================================
// ФАЙЛ 3: Models/Host.cs (ЧАСТЬ 1)
// ==========================================
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace WpfApp1.Models
{
    public class Host : BindableBase
    {
        private readonly ObservableCollection<IPConfigItem> _host = new ObservableCollection<IPConfigItem>();
        private readonly ObservableCollection<string> _netAdapters = new ObservableCollection<string>();
        public readonly ReadOnlyObservableCollection<IPConfigItem> PublicHost;
        public readonly ReadOnlyObservableCollection<string> NetAdapters;

        // Путь к файлу конфигурации пингов
        private readonly string _jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ping_hosts.json");

        public Host()
        {
            PublicHost = new ReadOnlyObservableCollection<IPConfigItem>(_host);
            NetAdapters = new ReadOnlyObservableCollection<string>(_netAdapters);
        }

        public void AddHost(string adapterName, string extraIp, string subnetPrefix)
        {
            AddAdditionalIP(adapterName, extraIp, subnetPrefix);
            ReadIpAddressFromAdapter(adapterName);
        }

        public void UpdateHostIP(string adapterName, string oldIp, string newIp, string subnetMask)
        {
            // Чтобы изменить IP в системе, нужно удалить старый и добавить новый
            if (!string.IsNullOrEmpty(oldIp) && oldIp != "0.0.0.0")
            {
                RemoveAdditionalIP(adapterName, oldIp);
            }
            AddAdditionalIP(adapterName, newIp, subnetMask);
            ReadIpAddressFromAdapter(adapterName);
        }

        public void AddItemDirect(IPConfigItem item) => _host.Add(item);

        public void RemoveHost(string adapterName, string ipAddress)
        {
            RemoveAdditionalIP(adapterName, ipAddress);
            ReadIpAddressFromAdapter(adapterName);
        }

        public void ReadNetAdapters()
        {
            _netAdapters.Clear();
            List<string> adapters = GetNetAdaptersFromPowerShell();
            foreach (var adapter in adapters)
            {
                _netAdapters.Add(adapter);
            }
            // Добавляем виртуальный элемент в список адаптеров
            _netAdapters.Add("Ping Host");
        }

        public void ReadIpAddressFromAdapter(string adapterName)
        {
            _host.Clear();
            if (adapterName == "Ping Host")
            {
                LoadPingHostsFromJson();
                return;
            }

            List<IPConfigItem> ipAdresses = GetIpAddressesFromAdapter(adapterName);
            foreach (var ipAd in ipAdresses)
            {
                // Запоминаем текущий IP как старый на случай редактирования
                ipAd.OldIPAddress = ipAd.IPAddress;
                _host.Add(ipAd);
            }
        }

        // --- РАБОТА С JSON ДЛЯ PING HOST ---

        public void LoadPingHostsFromJson()
        {
            _host.Clear();
            if (!File.Exists(_jsonPath))
            {
                // Если файла нет, создаем пустой массив
                File.WriteAllText(_jsonPath, "[]", Encoding.UTF8);
                return;
            }

            try
            {
                string json = File.ReadAllText(_jsonPath, Encoding.UTF8);
                var items = System.Text.Json.JsonSerializer.Deserialize<List<IPConfigItem>>(json);
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        item.IsPinging = true; // Помечаем, что это элемент для пинга
                        item.OldIPAddress = item.IPAddress;
                        _host.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка чтения JSON: {ex.Message}");
            }
        }

        public void SavePingHostsToJson()
        {
            try
            {
                var listToSave = new List<object>();
                foreach (var item in _host)
                {
                    if (item.IsPinging)
                    {
                        listToSave.Add(new { item.IPAddress, item.SubnetMask });
                    }
                }
                string json = System.Text.Json.JsonSerializer.Serialize(listToSave, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_jsonPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка записи JSON: {ex.Message}");
            }
        }
        // ==========================================
        // ФАЙЛ 3: Models/Host.cs (ЧАСТЬ 2)
        // ==========================================
        private static int ConvertMaskToPrefix(string subnetMask)
        {
            try
            {
                byte[] bytes = System.Net.IPAddress.Parse(subnetMask.Trim()).GetAddressBytes();
                int prefix = 0;
                foreach (byte b in bytes)
                {
                    byte val = b;
                    while (val > 0)
                    {
                        if ((val & 1) == 1) prefix++;
                        val >>= 1;
                    }
                }
                return prefix;
            }
            catch
            {
                return 24;
            }
        }

        private void AddAdditionalIP(string adapterName, string extraIp, string subnetMask)
        {
            string cleanAdapter = adapterName.Replace("'", "''").Trim();
            string cleanIp = extraIp.Trim();
            int prefixLength = ConvertMaskToPrefix(subnetMask);

            string command = $"New-NetIPAddress -InterfaceAlias '{cleanAdapter}' " +
                             $"-IPAddress '{cleanIp}' -PrefixLength {prefixLength} -SkipAsSource $true";

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{command}\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };

            try
            {
                using (Process? process = Process.Start(psi))
                {
                    process?.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка выполнения PowerShell: {ex.Message}");
            }
        }

        private void RemoveAdditionalIP(string adapterName, string ipAddress)
        {
            string cleanAdapter = adapterName.Replace("'", "''").Trim();
            string cleanIp = ipAddress.Trim();

            string command = $"Remove-NetIPAddress -InterfaceAlias '{cleanAdapter}' " +
                             $"-IPAddress '{cleanIp}' -Confirm:$false";

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c powershell.exe -NoProfile -WindowStyle Hidden -Command \"{command}\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };

            try
            {
                using (Process? process = Process.Start(psi))
                {
                    process?.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка выполнения PowerShell (удаление): {ex.Message}");
            }
        }

        private List<string> GetNetAdaptersFromPowerShell()
        {
            List<string> adapters = new List<string>();
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -Command \"Get-NetAdapter | Where-Object {$_.Status -eq 'Up'} | Select-Object -ExpandProperty Name\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.GetEncoding(866)
            };

            try
            {
                using (Process? process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        while (!process.StandardOutput.EndOfStream)
                        {
                            string? line = process.StandardOutput.ReadLine();
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                adapters.Add(line.Trim());
                            }
                        }
                        process.WaitForExit();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка получения адаптеров: {ex.Message}");
            }

            return adapters;
        }

        private List<IPConfigItem> GetIpAddressesFromAdapter(string adapterName)
        {
            List<IPConfigItem> ipList = new List<IPConfigItem>();
            string escapedName = adapterName.Replace("'", "''");

            string psCommand =
            $"Get-NetIPAddress -InterfaceAlias '{escapedName}' -AddressFamily IPv4 | ForEach-Object {{ " +
            $"  $bits = $_.PrefixLength; " +
            $"  $bytes = [BitConverter]::GetBytes(([uint32]::MaxValue -shl (32 - $bits)) -band [uint32]::MaxValue); " +
            $"  [Array]::Reverse($bytes); " +
            $"  $mask = ([IPAddress]$bytes).IPAddressToString; " +
            $"  [PSCustomObject]@{{ IP = $_.IPAddress; Mask = $mask; Family = $_.AddressFamily }} " +
            $"}} | ConvertTo-Csv -NoTypeInformation";

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{psCommand}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                Verb = "runas"
            };

            try
            {
                using (Process? process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        process.StandardOutput.ReadLine(); // Пропускаем заголовок

                        while (!process.StandardOutput.EndOfStream)
                        {
                            string? line = process.StandardOutput.ReadLine();
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            string[] parts = line.Replace("\"", "").Split(',');
                            if (parts.Length >= 3)
                            {
                                ipList.Add(new IPConfigItem
                                {
                                    IPAddress = parts[0].Trim(),
                                    SubnetMask = parts[1].Trim(),
                                    AddressFamily = parts[2].Trim()
                                });
                            }
                        }
                        process.WaitForExit();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка получения IP: {ex.Message}");
            }
            return ipList;
        }

        public static async Task<bool> PingAddressAsync(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress)) return false;
            try
            {
                using (Ping pingSender = new Ping())
                {
                    PingReply reply = await pingSender.SendPingAsync(ipAddress, 1500);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
