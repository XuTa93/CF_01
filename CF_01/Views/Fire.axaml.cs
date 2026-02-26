using Avalonia;
using Avalonia.Controls;
using CF_01.ViewModels;
using System.ComponentModel;

namespace CF_01.Views;

public partial class Fire : UserControl
{
    private FireViewModel? _fireViewModel;
    private ThermometerViewModel? _thermoVm;
    private PropertyChangedEventHandler? _firePropertyHandler;
    private PropertyChangedEventHandler? _thermoPropertyHandler;

    public Fire()
    {
        InitializeComponent();
        _fireViewModel = new FireViewModel();
        DataContext = _fireViewModel;

        _thermoVm = ThermometerControl.ViewModel;

        // Sync nhiệt độ từ FireViewModel sang ThermometerControl
        _firePropertyHandler = (s, e) =>
        {
            if (e.PropertyName == nameof(FireViewModel.Temperature))
            {
                ThermometerControl.SetTemperature(_fireViewModel.Temperature);
            }
        };
        _fireViewModel.PropertyChanged += _firePropertyHandler;

        // Sync ngưỡng từ RangeSlider (ThermometerViewModel) sang FireViewModel
        if (_thermoVm != null)
        {
            _fireViewModel.LowTempThreshold = _thermoVm.LowerTemperature;
            _fireViewModel.HighTempThreshold = _thermoVm.UpperTemperature;

            _thermoPropertyHandler = (s, e) =>
            {
                if (e.PropertyName == nameof(ThermometerViewModel.LowerTemperature))
                    _fireViewModel.LowTempThreshold = _thermoVm.LowerTemperature;
                else if (e.PropertyName == nameof(ThermometerViewModel.UpperTemperature))
                    _fireViewModel.HighTempThreshold = _thermoVm.UpperTemperature;
            };
            _thermoVm.PropertyChanged += _thermoPropertyHandler;
        }
    }

    public FireViewModel? ViewModel => DataContext as FireViewModel;

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_fireViewModel != null && _firePropertyHandler != null)
            _fireViewModel.PropertyChanged -= _firePropertyHandler;

        if (_thermoVm != null && _thermoPropertyHandler != null)
            _thermoVm.PropertyChanged -= _thermoPropertyHandler;

        _fireViewModel?.Dispose();

        base.OnDetachedFromVisualTree(e);
    }
}