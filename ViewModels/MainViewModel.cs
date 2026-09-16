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
    class MainViewModel : BindableBase
    {
        readonly Host _host = new Host();
        public ReadOnlyObservableCollection<IPConfigItem> PublicHost => _host.PublicHost;

        public ReadOnlyObservableCollection<string> NetworkAdapters => _host.NetAdapters;
        //public ObservableCollection<string> NetworkAdapters { get; } = new ObservableCollection<string>();

        public DelegateCommand PrepareAddCommand { get; }
        public DelegateCommand<IPConfigItem> AddCommand { get; }


        private string? _newHostIp;
        public string? newHostIp
        {
            get => _newHostIp;
            set
            {
                _newHostIp = value;
                RaisePropertyChanged(nameof(newHostIp));
            }
        }
        private string? _newHostMask;
        public string? newHostMask
        {
            get => _newHostMask;
            set
            {
                _newHostMask = value;
                RaisePropertyChanged(nameof(newHostMask));
            }
        }

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
        public IPConfigItem? SelectedHost {
        get=> _selectedHost;
            set {
            _selectedHost = value;
                RaisePropertyChanged(nameof(SelectedHost));
                
               
            }
        }
      

        public MainViewModel() {
            _host.PropertyChanged += (s, e) => { RaisePropertyChanged(e.PropertyName); };
            _host.ReadNetAdapters();

            if (NetworkAdapters.Count > 0)
            {
                SelectedAdapter = NetworkAdapters[0];
            }

           /* AddCommand = new DelegateCommand(() =>
            {
                if(!string.IsNullOrEmpty(_selectedAdapter) && !string.IsNullOrEmpty(_newHostIp) && !string.IsNullOrEmpty(_newHostMask) ) {
                    
                    _host.AddHost(_selectedAdapter, _newHostIp, _newHostMask);
                }
                newHostIp = string.Empty;
                newHostMask = string.Empty;
            });  */


            RemoveCommand = new DelegateCommand(() => 
            {
                if (!string.IsNullOrEmpty(_selectedAdapter) && !string.IsNullOrEmpty(_selectedHost.IPAddress))
                { _host.RemoveHost(_selectedAdapter, _selectedHost.IPAddress); }
            });

            PingHost = new DelegateCommand<IPConfigItem>(async selectedItem =>
            {
                if (selectedItem != null)
                {
                    // 1. Показываем в статус-баре или MessageBox, что процесс пошел
                    // Чтобы окно MessageBox не блокировало поток, выведем результат только после завершения операции
                    string result = await Host.PingAddressAsync(selectedItem.IPAddress);

                    // 2. Выводим результат пользователю
                    MessageBox.Show(result, $"Результат проверки: {selectedItem.IPAddress}",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Сначала выберите IP-адрес для проверки!", "Внимание",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            });

            PrepareAddCommand = new DelegateCommand(() =>
            {
                if (string.IsNullOrEmpty(SelectedAdapter))
                {
                    MessageBox.Show("Сначала выберите сетевой адаптер!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Создаем черновик
                var newItem = new IPConfigItem
                {
                    IPAddress = "0.0.0.0",
                    SubnetMask = "255.255.255.0",
                    IsEditing = true
                };

                // Добавляем напрямую во внутреннюю коллекцию через метод модели Host
                // Для этого в класс Host добавьте метод: public void AddItemDirect(IPConfigItem item) => _host.Add(item);
                _host.AddItemDirect(newItem);

                // Автоматически выделяем созданный элемент
                SelectedHost = newItem;
            });

            // 2. Команда фиксации IP в Windows при нажатии Enter
            AddCommand = new DelegateCommand<IPConfigItem>(item =>
            {
                if (item != null && !string.IsNullOrEmpty(SelectedAdapter))
                {
                    if (string.IsNullOrEmpty(item.IPAddress) || string.IsNullOrEmpty(item.SubnetMask))
                    {
                        MessageBox.Show("Поля IP и Маски не могут быть пустыми!", "Ошибка");
                        return;
                    }

                    // Добавляем в ОС Windows через PowerShell
                    _host.AddHost(SelectedAdapter, item.IPAddress, item.SubnetMask);

                    // Выключаем режим редактирования, превращая строки в обычный текст
                    item.IsEditing = false;

                    // Перечитываем данные из системы для гарантии актуальности
                    _host.ReadIpAddressFromAdapter(SelectedAdapter);
                }
            });
        }

        //public DelegateCommand AddCommand { get; }
        public DelegateCommand RemoveCommand { get; }
        public DelegateCommand<IPConfigItem> PingHost { get; }



    }
}
