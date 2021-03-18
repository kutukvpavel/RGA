using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Collections.Generic;
using SkiaSharp;

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
                Repositories.Add(new DataRepository(args[i]) 
                {
                    Paint = new SKPaint() 
                    { 
                        Color = (i < ColorScheme.Count) ? ColorScheme[i] : DefaultColor,
                        StrokeWidth = 0.5f
                    },
                    Filter = "*.txt"
                });
            }
            foreach (var item in Repositories)
            {
                item.Enabled = true;
            }
            app.Run(new MainWindow());
        }

        public static SKColor DefaultColor = new SKColor(0, 0, 0);

        public static List<SKColor> ColorScheme { get; } = new List<SKColor>()
        {
            new SKColor(104, 179, 217),
            new SKColor(104, 217, 119)
        };

        public static List<DataRepository> Repositories { get; } = new List<DataRepository>();

        public static object UpdateSynchronizingObject { get; } = new object();
    }
}
