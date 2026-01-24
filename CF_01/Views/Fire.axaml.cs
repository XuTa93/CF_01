using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree; // <-- Add this using directive
using CF_01.ViewModels;

namespace CF_01.Views;

public partial class Fire : UserControl
{
    public Fire()
    {
        InitializeComponent();
        var fireViewModel = new FireViewModel();
        DataContext = fireViewModel;

        // Sync nhiệt độ từ FireViewModel sang ThermometerControl
        fireViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(FireViewModel.Temperature))
            {
                ThermometerControl.SetTemperature(fireViewModel.Temperature);
            }
        };
    }

    // Property để access ViewModel từ bên ngoài
    public FireViewModel? ViewModel => DataContext as FireViewModel;

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        (DataContext as FireViewModel)?.Dispose();
    }
}