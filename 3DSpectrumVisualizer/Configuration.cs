using Newtonsoft.Json;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace _3DSpectrumVisualizer
{
    public class Configuration
    {
        public static event EventHandler<Exception> LogException;
        public static JsonSerializerSettings SerializerSettings { get; set; } = new JsonSerializerSettings()
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Converters = new JsonConverter[] { new SKColorJsonConverter(), new SKColorDictionaryJsonConverter<string>() }
        };

        public const string SerializationFileName = "settings";
        public const string JsonExtension = ".json";

        public ColorScheme ColorSchemes { get; set; } = new ColorScheme()
        {
            new GradientColorObservableCollection()
            {
                new GradientColor(SKColor.Parse("#C50D17F4"), 0),
                new GradientColor(SKColor.Parse("#C013E90F"), 0.5f),
                new GradientColor(SKColor.Parse("#C5EF0F12"), 1)
            },
            new GradientColorObservableCollection()
            {
                new GradientColor(SKColor.Parse("#C3E511E9"), 0),
                new GradientColor(SKColor.Parse("#C8F1DC0F"), 0.5f),
                new GradientColor(SKColor.Parse("#D40DECDD"), 1)
            }
        };

        [JsonConverter(typeof(SKColorDictionaryJsonConverter<string>))]
        public Dictionary<string, SKColor> GasRegionColor { get; set; } = new Dictionary<string, SKColor>()
        {
            { "NO2", SKColor.Parse("#2EFF6200") },
            { "O2", SKColor.Parse("#3300FF0B") },
            { "He", SKColor.Parse("#36FAFF00") },
            { "CO2", SKColor.Parse("#2E00EFFF") }
        };

        [JsonProperty(ItemConverterType = typeof(SKColorJsonConverter))]
        public SKColor[] SensorColors { get; set; } = new SKColor[]
        {
            SKColor.Parse("#1B1B1B"),
            SKColor.Parse("#D900FF"),
            SKColor.Parse("#FF9200"),
            SKColor.Parse("#1E6FFF")
        };

        [JsonProperty(ItemConverterType = typeof(SKColorJsonConverter))]
        public SKColor[] UVRegionColors { get; set; } = new SKColor[] { SKColor.Parse("#38B088FF") };

        [JsonProperty(ItemConverterType = typeof(SKColorJsonConverter))]
        public SKColor[] TemperatureProfileColors { get; set; } = new SKColor[] { SKColor.Parse("#F20B15F7") };

        [JsonConverter(typeof(SKColorJsonConverter))]
        public SKColor FallbackColor { get; set; } = SKColor.Parse("#F20B15F7");

        [JsonProperty(ItemConverterType = typeof(SKColorJsonConverter))]
        public SKColor[] LightGradient { get; set; } = new SKColor[]
        {
            SKColor.Parse("#00FAF4F4"),
            SKColor.Parse("#8F7B7B7B")
        };

        [JsonConverter(typeof(SKColorJsonConverter))]
        public SKColor SpectraBackground { get; set; } = SKColor.Parse("#0E0D0D");

        public string InfoSplitter { get; set; } = " | ";
        public string InfoSubfolder { get; set; } = "info";
        public string TemperatureFileName { get; set; } = "Temp.txt";
        public string UVFileName { get; set; } = "UV.txt";
        public string GasFileName { get; set; } = "Gas.txt";
        public string SensorFileName { get; set; } = "Sensor{0}.txt";
        public double GradientPositionSliderLawPower { get; set; } = 2.5;
        public int AMURoundingDigits { get; set; } = 1;
        public string RepositoryFileFilter { get; set; } = "*.csv";
        public float[] Last3DCoords { get; set; } = new float[] { 10, 10, 15, 0, 45, 4, 0.01f, 0.1f };
        public float LastAMUSection { get; set; } = 18;
        public byte LastLightSliderPosition { get; set; } = 128;
        public bool[] UseLogIntensity { get; set; } = new bool[] { false };
        public bool UseHorizontalGradient { get; set; } = false;
        public float LastTimeAxisInterval { get; set; } = 2.5f;
        public string LastExportDir { get; set; }
        public string ExportYFormat { get; set; } = "E4";
        public string ExportXFormat { get; set; } = "F1";
        public string ExportUVTrueString { get; set; } = "1";
        public string ExportUVFalseString { get; set; } = "0";
        public bool AutoupdateXScale { get; set; } = false;
        public float ColorPositionSliderPrecision { get; set; } = 0.005f;
        public bool ShowTemperatureProfile { get; set; } = true;
        public bool ShowGasRegions { get; set; } = true;
        public bool FastMode { get; set; } = false;
        public float FastModeDepth { get; set; } = -3.5f;
        public float ZScalingLowerLimit { get; set; } = 0.001f;
        public bool EnableRenderCaching { get; set; } = true;
        public string IntensityLabelFormat { get; set; } = "0.#E+0";
        public bool LeftPanelVisible { get; set; } = true;
        public bool EnableExportExtremumSearch { get; set; } = true;
        public bool[] RenderSensorProfiles { get; set; } = new bool[0];

        public void Save(string priorityFolder = null)
        {
            Serialize(this, SerializationFileName, SerializerSettings, priorityFolder);
        }

        public static Configuration Load(string priorityFolder = null)
        {
            return Deserialize(SerializationFileName, new Configuration(), SerializerSettings, priorityFolder);
        }

        #region Serialization
        public static void Serialize<T>(T obj, string name, JsonSerializerSettings settings, string priorityFolder = null)
        {
            try
            {
                string fn = name + JsonExtension;
                string res = JsonConvert.SerializeObject(obj, Formatting.Indented, settings);
                if (priorityFolder != null)
                {
                    if (Path.GetExtension(priorityFolder) == ZipHelpers.ZipFileExtension)
                    {
                        ZipHelpers.WriteConfigurationFile(priorityFolder, res, fn);
                    }
                    else
                    {
                        File.WriteAllText(Path.Combine(priorityFolder, fn), res);
                    }
                }
                File.WriteAllText(Path.Combine(Environment.CurrentDirectory, fn), res);
            }
            catch (Exception ex)
            {
                LogException?.Invoke(null, ex);
            }
        }

        public static T Deserialize<T>(string name, T def, JsonSerializerSettings settings, string priorityFolder = null)
        {
            try
            {
                var fn = name + JsonExtension;
                string p = null;
                string json = null;
                if (Path.GetExtension(priorityFolder) == ZipHelpers.ZipFileExtension)
                {
                    json = ZipHelpers.ReadConfigurationFile(priorityFolder, fn);
                }
                else
                {
                    if (priorityFolder != null) p = Path.Combine(priorityFolder, fn);
                    if (!File.Exists(p)) p = Path.Combine(Environment.CurrentDirectory, fn);
                    if (File.Exists(p)) json = File.ReadAllText(p);
                }
                if (json == null) return def;
                object o = JsonConvert.DeserializeObject(json, typeof(T), settings);
                if (o == null) throw new JsonSerializationException("Deserialization result is null.");
                return (T)o;
            }
            catch (Exception ex)
            {
                LogException?.Invoke(null, ex);
            }
            return def;
        }
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

    public class SKColorDictionaryJsonConverter<TKey> : JsonConverter<Dictionary<TKey, SKColor>>
    {
        public override Dictionary<TKey, SKColor> ReadJson(JsonReader reader, Type objectType, Dictionary<TKey, SKColor> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return JsonConvert.DeserializeObject<Dictionary<TKey, string>>((string)reader.Value)
                .ToDictionary(x => x.Key, x => SKColor.Parse(x.Value));
        }

        public override void WriteJson(JsonWriter writer, Dictionary<TKey, SKColor> value, JsonSerializer serializer)
        {
            writer.WriteValue(JsonConvert.SerializeObject(value.ToDictionary(x => x.Key, x => x.Value.ToString())));
        }
    }

    #endregion
}
