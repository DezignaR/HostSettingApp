// ==========================================
// ФАЙЛ 4: ViewModels/MainViewModel.cs
// ==========================================
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WpfApp1.Models;

namespace WpfApp1.ViewModels
{
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

        // Команды
        public DelegateCommand RefreshCommand { get; }
        public DelegateCommand PrepareAddCommand { get; }
        public DelegateCommand<IPConfigItem> EditElementCommand { get; }
        public DelegateCommand<IPConfigItem> AddCommand { get; }
        public DelegateCommand<IPConfigItem> RemoveCommand { get; }

        // Команды контекстного меню Ping Host
        public DelegateCommand<IPConfigItem> StartPingCommand { get; }
        public DelegateCommand<IPConfigItem> StopPingCommand { get; }

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

        // Словарь для отслеживания запущенных тасок пинга по IP адресу
        private readonly Dictionary<string, CancellationTokenSource> _pingTokens = new Dictionary<string, CancellationTokenSource>();

        public MainViewModel()
        {
            _host.PropertyChanged += (s, e) => { RaisePropertyChanged(e.PropertyName); };
            _host.ReadNetAdapters();

            if (NetworkAdapters.Count > 0)
            {
                SelectedAdapter = NetworkAdapters[0];
            }

            // Логика обновления адаптеров и IP
            RefreshCommand = new DelegateCommand(() =>
            {
                string? currentAdapter = SelectedAdapter;
                _host.ReadNetAdapters();
                if (!string.IsNullOrEmpty(currentAdapter) && NetworkAdapters.Contains(currentAdapter))
                {
                    SelectedAdapter = currentAdapter;
                }
                else if (NetworkAdapters.Count > 0)
                {
                    SelectedAdapter = NetworkAdapters[0];
                }
            });


            // Подготовка к добавлению
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
                    IsEditing = true,
                    IsPinging = SelectedAdapter == "Ping Host"
                };

                _host.AddItemDirect(newItem);
                SelectedHost = newItem;
            });

            // Переключение в режим редактирования существующего элемента
            EditElementCommand = new DelegateCommand<IPConfigItem>(item =>
            {
                var target = item ?? SelectedHost;
                if (target != null)
                {
                    target.IsEditing = true;
                }
            });

            // Сохранение изменений (Добавление нового или обновление старого)
            AddCommand = new DelegateCommand<IPConfigItem>(item =>
            {
                if (item == null || string.IsNullOrEmpty(SelectedAdapter)) return;

                if (string.IsNullOrEmpty(item.IPAddress) || string.IsNullOrEmpty(item.SubnetMask))
                {
                    System.Windows.MessageBox.Show("Поля IP и Маски не могут быть пустыми!", "Ошибка");
                    return;
                }

                item.IsEditing = false;

                if (SelectedAdapter == "Ping Host")
                {
                    // Логика для Ping Host: сохраняем в JSON
                    item.OldIPAddress = item.IPAddress;
                    _host.SavePingHostsToJson();
                }
                else
                {
                    // Логика для реального адаптера
                    if (string.IsNullOrEmpty(item.OldIPAddress) || item.OldIPAddress == "0.0.0.0")
                    {
                        // Абсолютно новый адрес
                        _host.AddHost(SelectedAdapter, item.IPAddress, item.SubnetMask);
                    }
                    else
                    {
                        // Изменение уже добавленного адреса
                        _host.UpdateHostIP(SelectedAdapter, item.OldIPAddress, item.IPAddress, item.SubnetMask);
                    }
                }
            });

            // Удаление
            RemoveCommand = new DelegateCommand<IPConfigItem>(selectedItem =>
            {
                var itemToRemove = selectedItem ?? SelectedHost;
                if (itemToRemove != null && !string.IsNullOrEmpty(SelectedAdapter))
                {
                    if (SelectedAdapter == "Ping Host")
                    {
                        // Останавливаем пинг перед удалением, если он работал
                        StopPingLogic(itemToRemove);
                        _host.ReadIpAddressFromAdapter("Ping Host"); // Перезагрузит список без этого элемента
                        _host.SavePingHostsToJson();
                    }
                    else if (!string.IsNullOrEmpty(itemToRemove.IPAddress))
                    {
                        _host.RemoveHost(SelectedAdapter, itemToRemove.IPAddress);
                    }
                    SelectedHost = null;
                }
            });

            // Включение циклического Ping запроса
            StartPingCommand = new DelegateCommand<IPConfigItem>(item =>
            {
                var target = item ?? SelectedHost;
                if (target == null || string.IsNullOrEmpty(target.IPAddress)) return;

                if (_pingTokens.ContainsKey(target.IPAddress)) return; // Уже пингуется

                var cts = new CancellationTokenSource();
                _pingTokens[target.IPAddress] = cts;
                target.IsActivePinging = true;

                // Запуск бесконечной фоновой задачи пинга
                Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        bool isAlive = await Host.PingAddressAsync(target.IPAddress);

                        // Меняем цвет в UI потоке
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            target.StatusColor = isAlive ? "LightGreen" : "Red";
                        });

                        try
                        {
                            await Task.Delay(2000, cts.Token); // Опрос каждые 2 секунды
                        }
                        catch (TaskCanceledException) { break; }
                    }
                }, cts.Token);
            });

            // Выключение Ping запроса
            StopPingCommand = new DelegateCommand<IPConfigItem>(item =>
            {
                var target = item ?? SelectedHost;
                if (target != null)
                {
                    StopPingLogic(target);
                }
            });
        }

        // Этот метод вызывается из MainWindow.xaml.cs при выходе, чтобы остановить пинги
        public void StopAllPings()
        {
            foreach (var token in _pingTokens.Values)
            {
                token.Cancel();
            }
            _pingTokens.Clear();
        }
        private void StopPingLogic(IPConfigItem target)
        {
            if (!string.IsNullOrEmpty(target.IPAddress) && _pingTokens.TryGetValue(target.IPAddress, out var cts))
            {
                cts.Cancel();
                _pingTokens.Remove(target.IPAddress);
            }
            target.IsActivePinging = false;
            target.StatusColor = "LightBlue"; // Сброс цвета к дефолтному
        }
    }
}
