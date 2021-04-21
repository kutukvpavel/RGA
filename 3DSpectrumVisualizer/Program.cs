using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SkiaSharp;
using System.Linq;

namespace _3DSpectrumVisualizer
{
    public class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        public static void Main(string[] args) => BuildAvaloniaApp()
            .With(new Win32PlatformOptions() { AllowEglInitialization = true })
            .Start(AppMain, args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();

        public static void AppMain(Application app, string[] args)
        {
            string filter = "*.csv";
            if (args[0].StartsWith("-f"))
            {
                filter = args[0].Split(':')[1];
                args = args.Skip(1).ToArray();
            }
            for (int i = 0; i < args.Length; i++)
            {
                var dr = new DataRepository(args[i]) 
                { 
                    Filter = filter, 
                    UpdateSynchronizingObject = Program.UpdateSynchronizingObject 
                };
                if (ColorSchemes.Count > i)
                {
                    dr.PaintFill.Color = ColorSchemes[i][0];
                    dr.PaintStroke.Color = ColorSchemes[i][0];
                    dr.ColorScheme = ColorSchemes[i];
                }
                Repositories.Add(dr);
            }
            foreach (var item in Repositories)
            {
                item.Enabled = true;
            }
            var mainWindow = new MainWindow();
            app.Run(mainWindow);
        }

        public static ColorScheme ColorSchemes { get; } = new ColorScheme()
        {
            new ObservableCollection<SKColor>()
            {
                SKColor.Parse("#551B23D4"),
                SKColor.Parse("#8627C624"),
                SKColor.Parse("#8627C624"),
                SKColor.Parse("#7FCB2325"),
                SKColor.Parse("#7FCB2325")
            },
            new ObservableCollection<SKColor>()
            {
                SKColor.Parse("#84CB32CE"),
                SKColor.Parse("#8DD1C12C"),
                SKColor.Parse("#8DD1C12C"),
                SKColor.Parse("#882EC6BC"),
                SKColor.Parse("#882EC6BC")
            }
        };

        public static List<DataRepository> Repositories { get; } = new List<DataRepository>();

        public static object UpdateSynchronizingObject { get; } = new object();

        //public static GnuPlot GnuPlotInstance { get; } = new GnuPlot(@"C:\gnuplot_new\bin");
    }
}
