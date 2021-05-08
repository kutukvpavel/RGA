using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SkiaSharp;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

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
            ColorSchemes = Deserialize(ColorSerializationName, ColorSchemes, ColorSerializationConverter);
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
            Serialize(ColorSchemes, ColorSerializationName, ColorSerializationConverter);
        }

        public static void Serialize<T>(T obj, string name, JsonConverter converter = null)
        {
            try
            {
                var p = Path.Combine(Environment.CurrentDirectory, name + JsonExtension);
                File.WriteAllText(p, JsonConvert.SerializeObject(obj, converter));
            }
            catch (Exception)
            {
                
            }
        }

        public static T Deserialize<T>(string name, T def, JsonConverter converter = null)
        {
            try
            {
                var p = Path.Combine(Environment.CurrentDirectory, name + JsonExtension);
                if (File.Exists(p))
                {
                    object o;
                    if (converter == null)
                    {
                        o = JsonConvert.DeserializeObject(File.ReadAllText(p));
                    }
                    else
                    {
                        o = JsonConvert.DeserializeObject(File.ReadAllText(p), typeof(T), converter);
                    }
                    if (o == null) throw new JsonSerializationException();
                    return (T)o;
                }
            }
            catch (Exception ex)
            {

            }
            return def;
        }

        public const string ColorSerializationName = "colors";
        public const string JsonExtension = ".json";
        /*public static readonly JsonSerializerOptions SerializerOptions =
            new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };*/

        public static JsonConverter ColorSerializationConverter = new SKColorJsonConverter();

        public static ColorScheme ColorSchemes { get; private set; } = new ColorScheme()
        {
            new SKColorObservableCollection()
            {
                SKColor.Parse("#C50D17F4"),
                SKColor.Parse("#C013E90F"),
                SKColor.Parse("#C5EF0F12")
            },
            new SKColorObservableCollection()
            {
                SKColor.Parse("#C3E511E9"),
                SKColor.Parse("#C8F1DC0F"),
                SKColor.Parse("#D40DECDD")
            }
        };

        public static List<DataRepository> Repositories { get; } = new List<DataRepository>();

        public static object UpdateSynchronizingObject { get; } = new object();

        //public static GnuPlot GnuPlotInstance { get; } = new GnuPlot(@"C:\gnuplot_new\bin");
    }

    public class SKColorJsonConverter : JsonConverter<SKColor>
    {
        public override SKColor ReadJson(JsonReader reader, Type objectType, SKColor existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return SKColor.Parse((string)reader.Value);
        }

        public override void WriteJson(JsonWriter writer, SKColor value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }
    }
}
