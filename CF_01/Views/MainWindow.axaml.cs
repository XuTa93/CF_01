using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CF_01.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Code-behind
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MyThermometer.SetTemperature(30);
        }
    }
}