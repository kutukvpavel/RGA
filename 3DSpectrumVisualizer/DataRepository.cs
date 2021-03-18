using System;
using System.Collections.Generic;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using System.IO;
using System.Linq;
using MoreLinq;
using System.Globalization;
using System.Threading;
using System.Timers;
using System.Drawing;
using SkiaSharp;

namespace _3DSpectrumVisualizer
{
    public class DataRepository
    {
        public DataRepository(string folder, int pollPeriod = 3000)
        {
            Folder = folder;
            _PollTimer = new System.Timers.Timer(pollPeriod) { AutoReset = true, Enabled = false };
            _PollTimer.Elapsed += _PollTimer_Elapsed;
        }

        public string Folder { get; }
        public string Filter { get; set; } = "*.csv";
        public List<ScanResult> Results { get; } = new List<ScanResult>();
        public SKPaint Paint { get; set; }

        public bool Enabled
        {
            get { return _PollTimer.Enabled; }
            set { if (value) _PollTimer.Start(); else _PollTimer.Stop(); }
        }

        #region Private

        private DateTime _LastFileCreationTime = DateTime.MinValue;
        private System.Timers.Timer _PollTimer;
        private void _PollTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            bool lockTaken = Monitor.TryEnter(Program.UpdateSynchronizingObject, 10);
            if (!lockTaken) return;
            try
            {
                var newFiles = Directory.EnumerateFiles(Folder, Filter, SearchOption.TopDirectoryOnly)
                    .AsParallel().Select(x => new FileInfo(x)).Where(x => x.CreationTimeUtc > _LastFileCreationTime);
                foreach (var item in newFiles)
                {
                    try
                    {
                        if (!FileIsInUse(item))
                        {
                            Results.Add(new ScanResult(File.ReadLines(item.FullName)));
                            if (item.CreationTimeUtc > _LastFileCreationTime)
                                _LastFileCreationTime = item.CreationTimeUtc;
                        }
                    }
                    catch (Exception)
                    {

                    }
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                Monitor.Exit(Program.UpdateSynchronizingObject);
            }
        }
        private bool FileIsInUse(FileInfo file)
        {
            try
            {
                using (FileStream s = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    s.Close();
                }
            }
            catch (IOException)
            {
                return true;
            }
            return false;
        }

        #endregion
    }

    public class ScanResult
    {
        public static char Delimeter { get; set; } = ',';
        public static CultureInfo CurrentCulture { get; set; } = CultureInfo.InvariantCulture;
        public static int Capacity { get; set; } = 65 * 10;

        public ScanResult(IEnumerable<string> rawLines)
        {
            Path2D = new SKPath();
            Parse(rawLines);
        }

        public SKPath Path2D { get; private set; }

        private void Parse(IEnumerable<string> rawLines)
        {
            int consequtiveEmptyLines = 0;
            foreach (var item in rawLines)
            {
                var i = item.Trim('\r', '\n', ' ', Delimeter);
                if (consequtiveEmptyLines == 2)
                {
                    if (i.Length > 0)
                    {
                        //We've reached the data segment
                        try
                        {
                            var temp = i.Split(Delimeter).Select(x =>
                                float.Parse(x, 
                                NumberStyles.AllowLeadingWhite | 
                                NumberStyles.AllowTrailingWhite | 
                                NumberStyles.AllowExponent | 
                                NumberStyles.AllowDecimalPoint |
                                NumberStyles.AllowLeadingSign, 
                                CurrentCulture))
                                .ToArray();
                            Path2D.LineTo(temp[0], temp[1]);
                        }
                        catch (FormatException)
                        {

                        }
                    }
                    else
                    {
                        //EOF
                        break;
                    }
                }
                else
                {
                    //Going through instrument info segment
                    consequtiveEmptyLines = (i.Length > 0) ? 0 : (consequtiveEmptyLines + 1);
                }
            }
        }
    }
}
