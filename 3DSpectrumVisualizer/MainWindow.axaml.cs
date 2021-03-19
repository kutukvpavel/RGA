using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

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

        private Skia3DSpectrum Spectrum3D;
        private Skia2DWaterfall Waterfall2D;
        private Label GLLabel;

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            Spectrum3D = this.FindControl<Skia3DSpectrum>("Spectrum3D");
            Waterfall2D = this.FindControl<Skia2DWaterfall>("Waterfall2D");
            GLLabel = this.FindControl<Label>("GLLabel");
        }

        private void OnRenderChecked(object sender, RoutedEventArgs e)
        {
            GLLabel.Background = SkiaCustomControl.OpenGLEnabled ? Brushes.Lime : Brushes.OrangeRed;
            Waterfall2D.SelectedRepositoryIndex = 0;
        }
    }
}
