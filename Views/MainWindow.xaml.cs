using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using WpfApp1.Models;
using WpfApp1.ViewModels;

namespace WpfApp1.Views
{
    public partial class MainWindow : Window
    {
        private NotifyIcon _notifyIcon = null!;
        private bool _isMinimizedToWidget = false;
        private double _originalWidth;
        private double _originalHeight;

        public MainWindow()
        {
            InitializeComponent();
            InitTrayIcon();
            _originalWidth = this.Width;
            _originalHeight = this.Height;
        }

        private void InitTrayIcon()
        {
            _notifyIcon = new NotifyIcon { Icon = SystemIcons.Information, Text = "IP Address Manager" };
            _notifyIcon.DoubleClick += (s, e) => RestoreWindow();
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Открыть", null, (s, e) => RestoreWindow());
            contextMenu.Items.Add("Выход", null, (s, e) => ShutdownApplication());
            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void Button_Click_Minimize(object sender, RoutedEventArgs e) => MinimizeToWidget();

        private void MinimizeToWidget()
        {
            _isMinimizedToWidget = true; _notifyIcon.Visible = true;
            this.ShowInTaskbar = false; this.Width = 25; this.Height = 17; this.Topmost = true;
            this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2; this.Top = 0;
            if (this.DataContext is MainViewModel vm) vm.IsWidgetMode = true;
        }

        private void RestoreWindow()
        {
            if (!_isMinimizedToWidget) return;
            _isMinimizedToWidget = false; _notifyIcon.Visible = true;
            this.Width = _originalWidth; this.Height = _originalHeight; this.Topmost = false;
            this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2; this.Top = 0;
            if (this.DataContext is MainViewModel vm) vm.IsWidgetMode = false;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) { if (_isMinimizedToWidget) RestoreWindow(); else this.DragMove(); }
        }

        private void Button_Click_Close(object sender, RoutedEventArgs e) => MinimizeToWidget();

   

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && textBox.DataContext is IPConfigItem currentItem)
            {
                if (currentItem.IsEditing)
                {
                    if (this.DataContext is MainViewModel viewModel)
                    {
                        textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();

                        if (viewModel.AddCommand.CanExecute(currentItem))
                        {
                            viewModel.AddCommand.Execute(currentItem);
                        }
                    }
                }
            }
        }

        private void TextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (sender is System.Windows.Controls.TextBox textBox && textBox.DataContext is IPConfigItem currentItem)
                {
                    if (this.DataContext is MainViewModel viewModel)
                    {
                        textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();

                        if (viewModel.AddCommand.CanExecute(currentItem))
                        {
                            viewModel.AddCommand.Execute(currentItem);
                        }
                    }
                }
            }
        }

        private void ShutdownApplication()
        {
            if (this.DataContext is MainViewModel vm) vm.StopAllPings();
            _notifyIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e) { _notifyIcon.Dispose(); base.OnClosed(e); }
    }
}
