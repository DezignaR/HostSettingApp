using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WpfApp1.Models;

namespace WpfApp1.Services
{
    public class FileStorageService : IStorageService
    {
        private readonly string _jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ping_hosts.json");

        public async Task<List<IPConfigItem>> LoadPingHostsAsync()
        {
            if (!File.Exists(_jsonPath))
            {
                await File.WriteAllTextAsync(_jsonPath, "[]", Encoding.UTF8);
                return new List<IPConfigItem>();
            }
            try
            {
                string json = await File.ReadAllTextAsync(_jsonPath, Encoding.UTF8);
                var items = JsonSerializer.Deserialize<List<IPConfigItem>>(json);
                return items ?? new List<IPConfigItem>();
            }
            catch
            {
                return new List<IPConfigItem>();
            }
        }

        public async Task SavePingHostsAsync(IEnumerable<IPConfigItem> items)
        {
            try
            {
                var listToSave = new List<object>();
                foreach (var item in items)
                {
                    if (item.IsPinging)
                    {
                        listToSave.Add(new { item.IPAddress, item.SubnetMask });
                    }
                }
                string json = JsonSerializer.Serialize(listToSave, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_jsonPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка сохранения JSON: {ex.Message}");
            }
        }
    }
}
