using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using WpfApp1.Views;

namespace WpfApp1
{
    public partial class App : System.Windows.Application
    {
        // Уникальное имя Mutex для вашей системы
        private static Mutex? _mutex;
        private const string MutexName = "Global\\WpfApp1_IP_Manager_Unique_Mutex_Name";

        // Импорт функций WinAPI для уведомления первого экземпляра приложения
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        // Константа широковещательной рассылки сообщений всем окнам
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

        protected override void OnStartup(StartupEventArgs e)
        {
            // Проверяем, запущено ли уже приложение
            _mutex = new Mutex(true, MutexName, out bool isNewInstance);

            if (!isNewInstance)
            {
                // Если приложение уже запущено, регистрируем такое же сообщение
                int msg = RegisterWindowMessage(WpfApp1.Views.MainWindow.UniqueMessageName);

                // Отправляем сигнал первому экземпляру, чтобы он развернулся
                PostMessage(HWND_BROADCAST, msg, IntPtr.Zero, IntPtr.Zero);

                // Завершаем работу текущей (второй) копии
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Освобождаем Mutex при закрытии основного приложения
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}
