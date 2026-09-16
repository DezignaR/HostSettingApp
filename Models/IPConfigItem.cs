using System;
using System.Collections.Generic;
using System.Text;

namespace WpfApp1.Models
{
    public class IPConfigItem : BindableBase
    {
        private string _ipAddress = string.Empty;
        public string IPAddress
        {
            get => _ipAddress;
            set => SetProperty(ref _ipAddress, value);
        }

        private string _subnetMask = string.Empty;
        public string SubnetMask
        {
            get => _subnetMask;
            set => SetProperty(ref _subnetMask, value);
        }

        public string AddressFamily { get; set; } = "IPv4";

        // ИСПРАВЛЕНО: Флаг редактирования (true для новой пустой строки)
        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }
    }
}
