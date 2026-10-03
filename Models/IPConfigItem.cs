// ==========================================
// ФАЙЛ 1: Models/IPConfigItem.cs
// ==========================================
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

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

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        private bool _isPinging;
        public bool IsPinging
        {
            get => _isPinging;
            set => SetProperty(ref _isPinging, value);
        }

        // --- НОВЫЕ СВОЙСТВА ДЛЯ РЕАЛИЗАЦИИ ТРЕБОВАНИЙ ---

        // Хранит старый IP-адрес для удаления старой привязки при изменении
        public string OldIPAddress { get; set; } = string.Empty;

        private string _statusColor = "#808080"; // Дефолтный цвет (синий)
        public string StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        private bool _isActivePinging; // Флаг, запущен ли циклический/активный пинг
        public bool IsActivePinging
        {
            get => _isActivePinging;
            set => SetProperty(ref _isActivePinging, value);
        }
    }
}


