
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

                        // Удаляем айтем из коллекции внутри Host
                        _host.RemovePingHost(itemToRemove);

                        // Сохраняем обновленную коллекцию (уже без этого айтема) в JSON
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

                // Безопасно выставляем флаг старта в UI-потоке
                target.IsActivePinging = true;

                // Запуск бесконечной фоновой задачи пинга
                Task.Run(async () =>
                {
                    // Фиксируем токен локально для потока
                    CancellationToken localToken = cts.Token;

                    while (!localToken.IsCancellationRequested)
                    {
                        try
                        {
                            // 1. Сбрасываем цвет в серый перед началом отправки пакета (в UI-потоке)
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                target.StatusColor = "#808080";
                            });

                            // 2. Выполняем сетевой запрос асинхронно в фоновом потоке
                            bool isAlive = await Host.PingAddressAsync(target.IPAddress);

                            // 3. Проверяем, не отменили ли задачу, пока шел пинг
                            if (localToken.IsCancellationRequested) break;

                            // 4. Обновляем результат в UI-потоке
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                target.StatusColor = isAlive ? "LightGreen" : "Red";
                            });

                            // Ожидаем 2 секунды перед следующим пингом
                            await Task.Delay(2000, localToken);
                        }
                        catch (TaskCanceledException)
                        {
                            break;
                        }
                        catch (ObjectDisposedException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Ошибка в потоке пинга: {ex.Message}");
                            break;
                        }
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
                    target.StatusColor = "#808080";
                }
            });



        }
        public void StopAllPings()
        {
            // Шаг 1: Сначала ТОЛЬКО отменяем все токены, чтобы фоновые потоки вышли из циклов
            foreach (var token in _pingTokens.Values)
            {
                try
                {
                    token.Cancel();
                }
                catch (ObjectDisposedException) { }
            }

            // Шаг 2: Даем микропаузу (несколько миллисекунд), чтобы потоки успели среагировать и завершиться
            // Но так как Dispose при закрытии критичен, мы можем вызвать его через безопасный блок try-catch,
            // либо полностью доверить очистку операционной системе, так как процесс всё равно уничтожается.
            // Самый надежный и не падающий вариант для OnExit:
            foreach (var token in _pingTokens.Values)
            {
                try
                {
                    token.Dispose();
                }
                catch { } // Подавляем любые ошибки деструктуризации при выходе
            }

            _pingTokens.Clear();
        }
        private void StopPingLogic(IPConfigItem target)
        {
            if (target == null) return;

            if (!string.IsNullOrEmpty(target.IPAddress) && _pingTokens.TryGetValue(target.IPAddress, out var cts))
            {
                try
                {
                    cts.Cancel();
                    // Перед Dispose убираем токен из словаря, чтобы фоновый поток 
                    // больше не мог получить к нему доступ через коллекцию
                    _pingTokens.Remove(target.IPAddress);

                    cts.Dispose();
                }
                catch (ObjectDisposedException) { }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при остановке пинга: {ex.Message}");
                }
            }

            target.IsActivePinging = false;
            target.StatusColor = "#808080";
        }
    }
        // Этот метод вызывается из MainWindow.xaml.cs при выходе, чтобы остановить пинги

    }
