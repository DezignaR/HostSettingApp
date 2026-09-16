using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
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

        public void AddItemDirect(IPConfigItem item) => _host.Add(item);

        public void RemoveHost(string adapterName, string ipAddress)
        {
            RemoveAdditionalIP(adapterName, ipAddress);
            ReadIpAddressFromAdapter(adapterName);
        }

        public void ReadNetAdapters() {
            _netAdapters.Clear();
            List<string> adapters = GetNetAdaptersFromPowerShell();
            foreach (var adapter in adapters)
            {
                _netAdapters.Add(adapter);
            }
        }

        public void ReadIpAddressFromAdapter(string adapterName) { 
            _host.Clear();
            List<IPConfigItem> ipAdresses = GetIpAddressesFromAdapter(adapterName);
            foreach (var ipAd in ipAdresses) {
                _host.Add(ipAd);
            }
        }

        private static int ConvertMaskToPrefix(string subnetMask)
        {
            try
            {
                // Преобразуем строку в IPAddress и берем байты
                byte[] bytes = System.Net.IPAddress.Parse(subnetMask.Trim()).GetAddressBytes();

                int prefix = 0;
                foreach (byte b in bytes)
                {
                    // Считаем количество выставленных бит в каждом байте
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
                // Значение по умолчанию (например, /24 для 255.255.255.0), если маска введена неверно
                return 24;
            }
        }
        private void AddAdditionalIP(string adapterName, string extraIp, string subnetMask)
        {

            string cleanAdapter = adapterName.Replace("'", "''").Trim();
            string cleanIp = extraIp.Trim();

            // 1. Конвертируем маску (например, "255.255.255.0") в префикс (например, 24)
            int prefixLength = ConvertMaskToPrefix(subnetMask);

            // 2. Формируем команду, передавая СТРОГО число в -PrefixLength
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
                    if (process?.ExitCode != 0)
                    {
                        Debug.WriteLine($"PowerShell завершился с ошибкой. Код: {process?.ExitCode}");
                    }
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

            // PowerShell команда: удаляет IP с адаптера без вывода окна подтверждения
            string command = $"Remove-NetIPAddress -InterfaceAlias '{cleanAdapter}' " +
                             $"-IPAddress '{cleanIp}' -Confirm:$false";

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c powershell.exe -NoProfile -WindowStyle Hidden -Command \"{command}\"",
                UseShellExecute = true,
                Verb = "runas", // Требуются права администратора
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };

            try
            {
                using (Process? process = Process.Start(psi))
                {
                    process?.WaitForExit();
                    if (process?.ExitCode != 0)
                    {
                        Debug.WriteLine($"Ошибка удаления IP через PowerShell. Код: {process?.ExitCode}");
                    }
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
                // Команда выбирает только имена активных (Up) физических и Wi-Fi адаптеров
                Arguments = "-NoProfile -Command \"Get-NetAdapter | Where-Object {$_.Status -eq 'Up'} | Select-Object -ExpandProperty Name\"",
                UseShellExecute = false,
                RedirectStandardOutput = true, // Перенаправляем вывод консоли в код
                CreateNoWindow = true,

                StandardOutputEncoding = System.Text.Encoding.GetEncoding(866)
            };

            try
            {
                using (Process? process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        // Построчно читаем то, что вывела команда PowerShell
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

            // PowerShell скрипт: берет IP-адреса, вычисляет маску из PrefixLength для IPv4
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
                        process.StandardOutput.ReadLine(); // Пропускаем заголовок CSV

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

        public static async Task<string> PingAddressAsync(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return "Неверный IP-адрес.";
            }

            try
            {
                using (Ping pingSender = new Ping())
                {
                    // Отправляем асинхронный пинг (таймаут 2000 мс)
                    PingReply reply = await pingSender.SendPingAsync(ipAddress, 2000);

                    if (reply.Status == IPStatus.Success)
                    {
                        return $"Ответ от {ipAddress}: время={reply.RoundtripTime}мс";
                    }
                    else
                    {
                        return $"Хост {ipAddress} недоступен. Статус: {reply.Status}";
                    }
                }
            }
            catch (PingException ex)
            {
                // Перехватывает ошибки вроде отсутствия сети или неверного формата IP
                return $"Ошибка ping: {ex.InnerException?.Message ?? ex.Message}";
            }
            catch (Exception ex)
            {
                return $"Непредвиденная ошибка: {ex.Message}";
            }
        }

    }
}
