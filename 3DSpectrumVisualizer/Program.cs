using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using LLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace _3DSpectrumVisualizer
{
    public class Program
    {

        public static event EventHandler RepositoriesParsed;
        public static event EventHandler RepositoriesInitialized;

        public static List<DataRepository> Repositories { get; } = new List<DataRepository>();
        public static object UpdateSynchronizingObject { get; } = new object();
        public static Configuration Config { get; private set; }

        private static L Log = new L();

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
                    var dr = new DataRepository(args[i])
                    {
                        Filter = Config.RepositoryFileFilter,
                        UpdateSynchronizingObject = Program.UpdateSynchronizingObject,
                        GasRegionColor = Config.GasRegionColor,
                        LogarithmicIntensity = (Config.UseLogIntensity.Length > i) ? 
                            Config.UseLogIntensity[i] : Config.UseLogIntensity[0]
                    };
                    dr.UVRegionPaint.Color = (Config.UVRegionColors.Length > i) ? 
                        Config.UVRegionColors[i] : Config.UVRegionColors[0];
                    dr.TemperaturePaint.Color = (Config.TemperatureProfileColors.Length > i) ? 
                        Config.TemperatureProfileColors[i] : Config.TemperatureProfileColors[0];
                    if (Config.ColorSchemes.Count > i)
                    {
                        dr.PaintFill.Color = Config.ColorSchemes[i][0].Color;
                        dr.PaintStroke.Color = Config.ColorSchemes[i][0].Color;
                        dr.ColorScheme = Config.ColorSchemes[i];
                    }
                    dr.InitializeInfoPathes();
                    Repositories.Add(dr);
                }
                RepositoriesInitialized?.Invoke(null, null);
                foreach (var item in Repositories)
                {
                    item.DataAdded += mainWindow.InvalidateSpectrum;
                    item.UpdateData();
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
            DataRepository.AMURoundingDigits = Config.AMURoundingDigits;
            DataRepository.FallbackColor = Config.FallbackColor;
            DataRepository.GasFileName = Config.GasFileName;
            DataRepository.InfoSplitter = Config.InfoSplitter;
            DataRepository.InfoSubfolder = Config.InfoSubfolder;
            DataRepository.LightGradient = Config.LightGradient;
            DataRepository.TemperatureFileName = Config.TemperatureFileName;
            DataRepository.UVFileName = Config.UVFileName;
            DataRepository.LightGradient[1] = DataRepository.LightGradient[1].WithAlpha(Config.LastLightSliderPosition);
            DataRepository.UseHorizontalGradient = Config.UseHorizontalGradient;
            DataRepository.ColorPositionSliderPrecision = Config.ColorPositionSliderPrecision;
            MainWindow.PositionValueConverter = new RootValueConverter(Config.GradientPositionSliderLawPower);
        }

        private static void CollectSettings()
        {
            Config.UseHorizontalGradient = DataRepository.UseHorizontalGradient;
            Config.LastLightSliderPosition = DataRepository.LightGradient[1].Alpha;
            Config.UseLogIntensity = Repositories.Select(x => x.LogarithmicIntensity).ToArray();
        }

        public static void LogException(object s, Exception e)
        {
            Log.Error($"Exception from object '{s?.GetType()?.FullName ?? "null/static"}': {e}");
        }

        public static void LogInfo(object s, string i)
        {
            Log.Info($"Info from object '{s?.GetType()?.FullName ?? "null/static"}': {i}");
        }
    }
}