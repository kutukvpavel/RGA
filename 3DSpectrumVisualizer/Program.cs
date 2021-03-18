using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Collections.Generic;
using SkiaSharp;
using System.Linq;

namespace _3DSpectrumVisualizer
{
    public class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        public static void Main(string[] args) => BuildAvaloniaApp().Start(AppMain, args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();

        public static void AppMain(Application app, string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var dr = new DataRepository(args[i])
                {
                    Paint = new SKPaint()
                    {
                        Color = (ColorSchemes.Count > i) ? ColorSchemes[i][0] : DefaultColor,
                        StrokeWidth = 0.1f,
                        Style = SKPaintStyle.Fill
                    },
                    Filter = "*.txt"
                };
                if (ColorSchemes.Count > i) dr.ColorScheme = ColorSchemes[i];
                Repositories.Add(dr);
            }
            foreach (var item in Repositories)
            {
                item.Enabled = true;
            }
            app.Run(new MainWindow());
        }

        public static SKColor DefaultColor = new SKColor(0, 0, 0);

        public static List<SKColor[]> ColorSchemes { get; } = new List<SKColor[]>()
        {
            new SKColor[] 
            { 
                SKColor.Parse("#FB47FF"),
                SKColor.Parse("#E8D845"),
                SKColor.Parse("#4FDCD3")
            },
            new SKColor[] 
            {
                SKColor.Parse("#4590E8"),
                SKColor.Parse("#4ACC3F"), 
                SKColor.Parse("#CC3F41") 
            }
        };

        public static List<DataRepository> Repositories { get; } = new List<DataRepository>();

        public static object UpdateSynchronizingObject { get; } = new object();
    }
}
