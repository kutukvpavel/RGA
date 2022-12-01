using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using LLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace _3DSpectrumVisualizer
{
    public class Program
    {

        public static event EventHandler RepositoriesParsed;
        public static event EventHandler RepositoriesInitialized;

        public static List<DataRepositoryBase> Repositories { get; } = new List<DataRepositoryBase>();
        public static object UpdateSynchronizingObject { get; } = new object();
        public static Configuration Config { get; private set; }

        private static readonly L Log = new L();

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
            Configuration.LogException += LogException;
            if (args.Length == 0) args = new string[] { Environment.CurrentDirectory };
            string singleRepo = args.Length == 1 ? args[0] : null;
            Config = Configuration.Load(singleRepo);
            InitStaticSettings();
            var mainWindow = new MainWindow();
            if (app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lt)
            {
                lt.MainWindow = mainWindow;
            }
            RepositoriesInitialized += mainWindow.RepoInitCallback;
            RepositoriesParsed += mainWindow.RepoLoadedCallback;
            var initTask = new Task(() =>
            {
                for (int i = 0; i < args.Length; i++)
                {
                    DataRepositoryBase dr = DataRepositoryFactory.CreateRepository(args[i]);
                    dr.Filter = Config.RepositoryFileFilter;
                    dr.UpdateSynchronizingObject = Program.UpdateSynchronizingObject;
                    dr.GasRegionColor = Config.GasRegionColor;
                    dr.LogarithmicIntensity = (Config.UseLogIntensity.Length > i) ?
                            Config.UseLogIntensity[i] : Config.UseLogIntensity[0];
                    dr.UVRegionPaint.Color = (Config.UVRegionColors.Length > i) ? 
                        Config.UVRegionColors[i] : Config.UVRegionColors[0];
                    dr.TemperaturePaint.Color = (Config.TemperatureProfileColors.Length > i) ? 
                        Config.TemperatureProfileColors[i] : Config.TemperatureProfileColors[0];
                    dr.SensorColors = Config.SensorColors.Select(x => 
                        new SkiaSharp.SKPaint() { 
                            Color = x, 
                            StrokeWidth = dr.TemperaturePaint.StrokeWidth,
                            IsAntialias = dr.TemperaturePaint.IsAntialias,
                            Style = dr.TemperaturePaint.Style
                        }).ToArray();
                    if (Config.ColorSchemes.Count > i)
                    {
                        dr.PaintStroke.Color = Config.ColorSchemes[i][0].Color;
                        dr.PaintWideStroke.Color = Config.ColorSchemes[i][0].Color;
                        dr.ColorScheme = Config.ColorSchemes[i];
                    }
                    dr.Initialize();
                    Repositories.Add(dr);
                }
                RepositoriesInitialized?.Invoke(null, null);
                foreach (var item in Repositories)
                {
                    item.DataAdded += mainWindow.InvalidateSpectrum;
                    item.LoadData();
                    item.Enabled = true;
                }
                RepositoriesParsed?.Invoke(null, null);
            });
            mainWindow.Opened += (s, e) => initTask.Start();
            app.Run(mainWindow);
            CollectSettings();
            Config.Save(singleRepo);
        }

        private static void InitStaticSettings()
        {
            DataRepositoryBase.AMURoundingDigits = Config.AMURoundingDigits;
            DataRepositoryBase.FallbackColor = Config.FallbackColor;
            DataRepositoryBase.GasFileName = Config.GasFileName;
            DataRepositoryBase.InfoSplitter = Config.InfoSplitter;
            DataRepositoryBase.InfoSubfolder = Config.InfoSubfolder;
            DataRepositoryBase.LightGradient = Config.LightGradient;
            DataRepositoryBase.TemperatureFileName = Config.TemperatureFileName;
            DataRepositoryBase.UVFileName = Config.UVFileName;
            DataRepositoryBase.SensorFileName = Config.SensorFileName;
            DataRepositoryBase.LightGradient[1] = DataRepositoryBase.LightGradient[1].WithAlpha(Config.LastLightSliderPosition);
            DataRepositoryBase.UseHorizontalGradient = Config.UseHorizontalGradient;
            DataRepositoryBase.ColorPositionSliderPrecision = Config.ColorPositionSliderPrecision;
            MainWindow.PositionValueConverter = new RootValueConverter(Config.GradientPositionSliderLawPower);
            Skia3DSpectrum.FastModeDepth = Config.FastModeDepth;
            Skia3DSpectrum.ScalingLowerLimit = Config.ZScalingLowerLimit;
            Skia3DSpectrum.IntensityLabelFormat = Config.IntensityLabelFormat;
            SkiaCustomControl.EnableCaching = Config.EnableRenderCaching;
            SkiaSectionPlot.IntensityLabelFormat = Config.IntensityLabelFormat;
        }

        private static void CollectSettings()
        {
            Config.UseHorizontalGradient = DataRepositoryBase.UseHorizontalGradient;
            Config.LastLightSliderPosition = DataRepositoryBase.LightGradient[1].Alpha;
            Config.UseLogIntensity = Repositories.Select(x => x.LogarithmicIntensity).ToArray();
            Config.FastModeDepth = Skia3DSpectrum.FastModeDepth;
            Config.ZScalingLowerLimit = Skia3DSpectrum.ScalingLowerLimit;
        }

        public static void LogException(object s, Exception e)
        {
            Log.Error($"Exception from object '{s?.GetType()?.FullName ?? "null/static"}': {e}");
        }

        public static void LogInfo(object s, string i)
        {
            Log.Info($"Info from object '{s?.GetType()?.FullName ?? "null/static"}': {i}");
        }

        public static void LogMemoryFootprint()
        {
            Log.Debug(
                $"Memory usage: managed = {GC.GetTotalMemory(false)}, total = {Process.GetCurrentProcess().VirtualMemorySize64}"
                );
        }
    }
}