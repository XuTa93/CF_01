using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using CF_01.ViewModels;

namespace CF_01.Views;

public partial class Thermometer : UserControl
{
    public Thermometer()
    {
        InitializeComponent();
        DataContext = new ThermometerViewModel();
        DrawTickMarks();
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

    private void DrawTickMarks()
    {
        const double minTemp = 30.0;
        const double maxTemp = 120.0;
        const double canvasHeight = 650.0;
        const double topPadding = 25.0;
        const double bottomPadding = 35.0;
        const double usableHeight = canvasHeight - topPadding - bottomPadding;
        const double tickX = 66.0; // Right edge of mercury column

        for (int temp = (int)minTemp; temp <= (int)maxTemp; temp++)
        {
            double percentage = (temp - minTemp) / (maxTemp - minTemp);
            double y = topPadding + (1 - percentage) * usableHeight;

            bool isMajor = temp % 10 == 0;
            bool isMedium = temp % 5 == 0 && !isMajor;

            double tickWidth;
            double tickHeight;
            Color tickColor;

            if (isMajor)
            {
                tickWidth = 25;
                tickHeight = 2.5;
                tickColor = GetTickColor(temp);
            }
            else if (isMedium)
            {
                tickWidth = 15;
                tickHeight = 1.5;
                tickColor = Color.Parse("#CCCCCC");
            }
            else
            {
                // Minor tick every 1°C
                tickWidth = 8;
                tickHeight = 1;
                tickColor = Color.Parse("#999999");
            }

            // Draw tick line
            var tick = new Rectangle
            {
                Width = tickWidth,
                Height = tickHeight,
                Fill = new SolidColorBrush(tickColor),
                RadiusX = 0.5,
                RadiusY = 0.5
            };
            Canvas.SetLeft(tick, tickX);
            Canvas.SetTop(tick, y - tickHeight / 2);
            TickCanvas.Children.Add(tick);

            // Draw label for major ticks
            if (isMajor)
            {
                var label = new TextBlock
                {
                    Text = $"{temp}°",
                    Foreground = new SolidColorBrush(tickColor),
                    FontSize = (temp == (int)minTemp || temp == (int)maxTemp) ? 16 : 13,
                    FontWeight = (temp == (int)minTemp || temp == (int)maxTemp)
                        ? FontWeight.Bold
                        : FontWeight.SemiBold
                };
                Canvas.SetLeft(label, tickX + tickWidth + 4);
                Canvas.SetTop(label, y - 10);
                TickCanvas.Children.Add(label);
            }
        }
    }

    private static Color GetTickColor(int temp) => temp switch
    {
        >= 100 => Color.Parse("#FF4444"),
        >= 90  => Color.Parse("#FF5544"),
        >= 80  => Color.Parse("#FF6655"),
        >= 70  => Color.Parse("#FF8866"),
        >= 60  => Color.Parse("#FFAA66"),
        >= 50  => Color.Parse("#FFCC66"),
        >= 40  => Color.Parse("#FFDD66"),
        >= 30  => Color.Parse("#FFEE77"),
        >= 20  => Color.Parse("#BBFF66"),
        _      => Color.Parse("#66AAFF")
    };
}