using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CF_01.ViewModels;

namespace CF_01.Views;

public partial class Thermometer : UserControl
{
    public Thermometer()
    {
        InitializeComponent();
        DataContext = new ThermometerViewModel();
    }

    // Property để access ViewModel từ bên ngoài
    public ThermometerViewModel? ViewModel => DataContext as ThermometerViewModel;
    
    // Method tiện ích để set nhiệt độ từ bên ngoài
    public void SetTemperature(double temperature)
    {
        if (ViewModel != null)
        {
            ViewModel.Temperature = temperature;
        }
    }

}