using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace _3DSpectrumVisualizer
{
    public class ScanResult
    {
        public static char Delimeter { get; set; } = ',';
        public static NumberStyles ValueNumberStyle { get; set; } = NumberStyles.Float;
        public static CultureInfo CurrentCulture { get; set; } = CultureInfo.InvariantCulture;

        private static readonly string PlacholderName = "N/A";

        public ScanResult()
        {
            Name = PlacholderName;
            CreationTime = DateTime.Now;
        }
        public ScanResult(IEnumerable<string> rawLines, string name, DateTime creationTime)
        {
            Name = name;
            CreationTime = creationTime;
            Path2D = new SKPath();
            LogPath2D = new SKPath();
            Parse(rawLines);
        }

        public float PositiveTopBound { get; private set; } = 1;
        public SKPath Path2D { get; private set; }
        public SKPath LogPath2D { get; private set; }
        public string Name { get; }
        public DateTime CreationTime { get; }

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
                            string[] spl = i.Split(Delimeter);
                            float x, y;
                            x = float.Parse(spl[0], ValueNumberStyle, CurrentCulture);
                            y = float.Parse(spl[1], ValueNumberStyle, CurrentCulture);
                            if (!float.IsFinite(y)) continue;
                            if (y > 0 && y < PositiveTopBound) PositiveTopBound = y;
                            if (Path2D.Points.Length > 0)
                            {
                                Path2D.LineTo(x, y);
                                if (y <= 0)
                                {
                                    LogPath2D.LineTo(x, LogPath2D.LastPoint.Y);
                                }
                                else
                                {
                                    LogPath2D.LineTo(x, MathF.Log10(y));
                                }
                            }
                            else
                            {
                                if (y <= 0) continue;
                                Path2D.MoveTo(x, y);
                                LogPath2D.MoveTo(x, MathF.Log10(y));
                            }
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
