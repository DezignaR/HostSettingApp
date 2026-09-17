using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using WpfApp1.Models;


namespace WpfApp1.ViewModels
{
    // ИСПРАВЛЕНО: Класс обязательно должен быть public
    public class MainViewModel : BindableBase
    {
        private bool _isWidgetMode;
        public bool IsWidgetMode
        {
            get => _isWidgetMode;
            set
            {
                _isWidgetMode = value;
                RaisePropertyChanged(nameof(IsWidgetMode));
            }
        }

        private readonly Host _host = new Host();
        public ReadOnlyObservableCollection<IPConfigItem> PublicHost => _host.PublicHost;
        public ReadOnlyObservableCollection<string> NetworkAdapters => _host.NetAdapters;

        // ИСПРАВЛЕНО: Типизация команд строго под вызовы из XAML
        public DelegateCommand PrepareAddCommand { get; }
        public DelegateCommand<IPConfigItem> AddCommand { get; }
        public DelegateCommand<IPConfigItem> RemoveCommand { get; }
        public DelegateCommand<IPConfigItem> PingHost { get; }

        private string? _selectedAdapter;
        public string? SelectedAdapter
        {
            get => _selectedAdapter;
            set
            {
                _selectedAdapter = value;
                RaisePropertyChanged(nameof(SelectedAdapter));

                if (!string.IsNullOrEmpty(_selectedAdapter))
                {
                    _host.ReadIpAddressFromAdapter(_selectedAdapter);
                }
            }
        }

        private IPConfigItem? _selectedHost;
        public IPConfigItem? SelectedHost
        {
            get => _selectedHost;
            set
            {
                _selectedHost = value;
                RaisePropertyChanged(nameof(SelectedHost));
            }
        }

        public MainViewModel()
        {
            _host.PropertyChanged += (s, e) => { RaisePropertyChanged(e.PropertyName); };
            _host.ReadNetAdapters();

            if (NetworkAdapters.Count > 0)
            {
                SelectedAdapter = NetworkAdapters[0];
            }

            // ИСПРАВЛЕНО: Типизированное удаление элемента из контекстного меню
            RemoveCommand = new DelegateCommand<IPConfigItem>(selectedItem =>
            {
                var itemToRemove = selectedItem ?? SelectedHost;
                if (itemToRemove != null && !string.IsNullOrEmpty(SelectedAdapter) && !string.IsNullOrEmpty(itemToRemove.IPAddress))
                {
                    _host.RemoveHost(SelectedAdapter, itemToRemove.IPAddress);
                    SelectedHost = null;
                }
            });

            PingHost = new DelegateCommand<IPConfigItem>(async selectedItem =>
            {
                var itemToPing = selectedItem ?? SelectedHost;
                if (itemToPing != null)
                {
                    string result = await Host.PingAddressAsync(itemToPing.IPAddress);
                    System.Windows.MessageBox.Show(result, $"Результат проверки: {itemToPing.IPAddress}",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show("Сначала выберите IP-адрес для проверки!", "Внимание",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            });

            PrepareAddCommand = new DelegateCommand(() =>
            {
                if (string.IsNullOrEmpty(SelectedAdapter))
                {
                    System.Windows.MessageBox.Show("Сначала выберите сетевой адаптер!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newItem = new IPConfigItem
                {
                    IPAddress = "0.0.0.0",
                    SubnetMask = "255.255.255.0",
                    IsEditing = true
                };

                _host.AddItemDirect(newItem);
                SelectedHost = newItem;
            });

            AddCommand = new DelegateCommand<IPConfigItem>(item =>
            {
                if (item != null && !string.IsNullOrEmpty(SelectedAdapter))
                {
                    if (string.IsNullOrEmpty(item.IPAddress) || string.IsNullOrEmpty(item.SubnetMask))
                    {
                        System.Windows.MessageBox.Show("Поля IP и Маски не могут быть пустыми!", "Ошибка");
                        return;
                    }

                    _host.AddHost(SelectedAdapter, item.IPAddress, item.SubnetMask);
                    item.IsEditing = false;
                    _host.ReadIpAddressFromAdapter(SelectedAdapter);
                }
            });
        }
    }
}
