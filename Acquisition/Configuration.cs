using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CsvHelper.Configuration;

namespace Acquisition
{
    public static class Configuration
    {
        public static string BackupSubfolderName { get; set; } = "backup";
        public static string InfoSubfolderName { get; set; } = "info";
        public static string FileNameFormat { get; set; } = "Scan_{0:yyyy-MM-dd_HH-mm-ss}.csv";
        public static string TemperatureFileName { get; set; } = "Temp.txt";
        public static string GasFileName { get; set; } = "Gas.txt";
        public static string UVFileName { get; set; } = "UV.txt";
        public static string InfoLineFormat { get; set; } = "{0} | {1}";
        public static string TemperatureFormat { get; set; } = "F1";
        public static string AMUForamt { get; set; } = "F2";
        public static string IntensityFormat { get; set; } = "E4";
        public static string PipeName { get; set; } = "LabPID_Profile_Broadcast";
        public static string WorkingDirectory { get => Environment.CurrentDirectory; }
        public static CsvConfiguration CsvConfig { get; } = new CsvConfiguration(CultureInfo.InvariantCulture);
    }
}
