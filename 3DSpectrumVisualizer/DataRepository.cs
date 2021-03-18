﻿using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Timers;

namespace _3DSpectrumVisualizer
{
    public class DataRepository
    {
        public static SKColor[] LightGradient { get; set; } = new SKColor[]
        {
            SKColor.Parse("#00FFFFFF"),
            SKColor.Parse("#647B7B7B"),
            SKColor.Parse("#8F7B7B7B")
        };

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
        public SKColor[] ColorScheme { get; set; } = new SKColor[0];
        public float Min { get; private set; } = 0;
        public float Max { get; private set; } = 0;
        public float Left { get; private set; } = 0;
        public float Right { get; private set; } = 0;

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
                            AddFile(item);
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
        private void AddFile(FileInfo item)
        {
            var sr = new ScanResult(File.ReadLines(item.FullName));
            var b = sr.Path2D.Bounds;
            bool updateShader = false;
            if (b.Bottom > Max)
            {
                Max = b.Bottom;
                updateShader = true;
            }
            if (b.Top < Min)
            {
                Min = b.Top;
                updateShader = true;
            }
            if (b.Left < Left)
            {
                Left = b.Left;
                updateShader = true;
            }
            if (b.Right > Right)
            {
                Right = b.Right;
                updateShader = true;
            }
            if (updateShader)
            {
                Paint.Shader = SKShader.CreateColor(Paint.Color);
                var gradShader = SKShader.CreateLinearGradient(
                    new SKPoint(0, Min),
                    new SKPoint(0, Max),
                    ColorScheme,
                    SKShaderTileMode.Clamp
                    );
                var lightShader = SKShader.CreateLinearGradient(
                    new SKPoint(Left, 0),
                    new SKPoint(Right, 0),
                    LightGradient,
                    SKShaderTileMode.Clamp
                    );
                if (ColorScheme.Length > 1)
                    Paint.Shader = SKShader.CreateCompose(Paint.Shader, gradShader);
                if (LightGradient.Length > 1)
                    Paint.Shader = SKShader.CreateCompose(Paint.Shader, lightShader);
            }
            Results.Add(sr);
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
