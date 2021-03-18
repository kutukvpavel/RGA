using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace _3DSpectrumVisualizer
{
    public class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private CustomSkiaPage Canvas;

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            Canvas = this.FindControl<CustomSkiaPage>("Canvas");
        }

        private void OnRenderChecked(object sender, RoutedEventArgs e)
        {
            Canvas.InvalidateVisual();
        }
    }
}
