using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfApp1.Models;
using WpfApp1.ViewModels;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            
        }

        private void Button_Click_Close(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Button_Click_Minimize(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void FillOut(object sender, MouseButtonEventArgs e)
        {
            this.maskhost.Text = "255.255.255.0";
        }

        private void TextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Если нажата клавиша Enter
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                // Получаем TextBox, в котором произошло нажатие
                if (sender is TextBox textBox && textBox.DataContext is IPConfigItem currentItem)
                {
                    // Из DataContext всего окна достаем нашу ViewModel
                    if (this.DataContext is WpfApp1.ViewModels.MainViewModel viewModel)
                    {
                        // Принудительно обновляем привязку текста (чтобы зафиксировать последние введенные символы)
                        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

                        // Вызываем команду добавления и передаем ей наш текущий IPConfigItem
                        if (viewModel.AddCommand.CanExecute(currentItem))
                        {
                            viewModel.AddCommand.Execute(currentItem);
                        }
                    }
                }
            }
        }
    }
}