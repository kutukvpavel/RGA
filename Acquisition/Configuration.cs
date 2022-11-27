using CsvHelper.Configuration;
using Newtonsoft.Json;
using System;
using System.Globalization;

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
        public string SensorFileName { get; set; } = "Sensor{0}.txt";
        public string SensorNumberFormat { get; set; } = "E3";
        public string InfoLineFormat { get; set; } = "{0} | {1}";
        public string TemperatureFormat { get; set; } = "F1";
        public string AMUFormat { get; set; } = "F2";
        public string IntensityFormat { get; set; } = "E4";
        public string LabPidPipeName { get; set; } = "LabPID_Profile_Broadcast";
        public string MgaPipeName { get; set; } = "MGA_Broadcast_Pipe";
        [JsonIgnore]
        public static string WorkingDirectory { get => Environment.CurrentDirectory; }
        [JsonIgnore]
        public static CsvConfiguration CsvConfig { get; } = new CsvConfiguration(CultureInfo.InvariantCulture);
        public string BackgroundFolderName { get; set; } = "background";
        public string BackgroundSearchPattern { get; set; } = "*.csv";
        public int GasGpioOffset { get; set; } = 1;
        public string[] GasPriority { get; set; } = new string[]
        {
            "Electrolysis",
            "NO2",
            "He"
        };
        public string NoGasLabel { get; set; } = "N/A";
        public string UVGpioLabel { get; set; } = "UV";
        public double CdemEnabledAdditionalDivisionFactor { get; set; } = 1000;
        public int BackgroundAMURoundingDigits { get; set; } = 2;
        public double BackgroundScaling { get; set; } = 1;
        public int MovingAverageWindowWidth { get; set; } = 1;
        public double CdemGain { get; set; } = 1;
        public bool GapEnabled { get; set; } = false;
        public bool LogPipeMessages { get; set; } = false;
        public bool LogTerminalCommunication { get; set; } = false;
        public int NoiseFloorSetting { get; set; } = 3;
        public int WriterThreadLimit { get; set; } = 12;
        public int ScanTimeout { get; set; } = 30000;
    }
}
