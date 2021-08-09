using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CsvHelper.Configuration;
using Newtonsoft.Json;

namespace Acquisition
{
    public class Configuration
    {
        public string BackupSubfolderName { get; set; } = "backup";
        public string InfoSubfolderName { get; set; } = "info";
        public string FileNameFormat { get; set; } = "Scan_{0:yyyy-MM-dd_HH-mm-ss}.csv";
        public string TemperatureFileName { get; set; } = "Temp.txt";
        public string GasFileName { get; set; } = "Gas.txt";
        public string UVFileName { get; set; } = "UV.txt";
        public string InfoLineFormat { get; set; } = "{0} | {1}";
        public string TemperatureFormat { get; set; } = "F1";
        public string AMUFormat { get; set; } = "F2";
        public string IntensityFormat { get; set; } = "E4";
        public string PipeName { get; set; } = "LabPID_Profile_Broadcast";
        [JsonIgnore]
        public static string WorkingDirectory { get => Environment.CurrentDirectory; }
        [JsonIgnore]
        public static CsvConfiguration CsvConfig { get; } = new CsvConfiguration(CultureInfo.InvariantCulture);
        public string BackgroundFolderName { get; set; } = "background";
        public string BackgroundSearchPattern { get; set; } = "*.csv";
        public int UVGpioIndex { get; set; } = 0;
        public int GasGpioOffset { get; set; } = 5;
        public Dictionary<int, string> GasNames { get; set; } = new Dictionary<int, string>()
        {
            { 0, "Example 0" },
            { 1, "Example 1" }
        };
        public double CdemEnabledAdditionalDivisionFactor { get; set; } = 1;
        public int BackgroundAMURoundingDigits { get; set; } = 2;
        public double BackgroundScaling { get; set; } = 1;
        public int MovingAverageWindowWidth { get; set; } = 1;
        public double CdemGain { get; set; } = 1;
        public bool GapEnabled { get; set; } = false;
    }
}
