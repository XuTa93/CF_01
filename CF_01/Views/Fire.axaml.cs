using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CF_01.ViewModels;

namespace CF_01.Views;

public partial class Fire : UserControl
{
    public Fire()
    {
        InitializeComponent();
        var fire = new FireViewModel();
        fire.Temperature = 25.0; // Giá trị nhiệt độ ban đầu
        DataContext = fire;
    }

    // Property để access ViewModel từ bên ngoài
    public FireViewModel? ViewModel => DataContext as FireViewModel;
}