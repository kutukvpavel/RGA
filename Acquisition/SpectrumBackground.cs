using CsvHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Acquisition
{
    public class SpectrumBackground : Dictionary<double, double>
    {
        public static event EventHandler<ExceptionEventArgs> LogException;

        public SpectrumBackground() : base()
        { }

        public SpectrumBackground(Dictionary<double, List<double>> src, double scaling) : this()
        {
            foreach (var item in src)
            {
                Add(item.Key, item.Value.Average() * scaling);
            }
        }

        public static SpectrumBackground Load(string folderPath, string searchPattern, double scaling)
        {
            if (!Directory.Exists(folderPath)) return new SpectrumBackground();
            var f = Directory.GetFiles(folderPath, searchPattern);
            if (f.Length > 0)
            {
                var buf = new Dictionary<double, List<double>>();
                foreach (var item in f)
                {
                    using TextReader r = new StreamReader(item);
                    using CsvReader cr = new CsvReader(r, Configuration.CsvConfig);
                    cr.Read();
                    cr.ReadHeader();
                    while (cr.Read())
                    {
                        try
                        {
                            double amu = cr.GetField<double>(0);
                            double val = cr.GetField<double>(1);
                            if (!buf.ContainsKey(amu))
                            {
                                buf.Add(amu, new List<double>(f.Length));
                            }
                            buf[amu].Add(val);
                        }
                        catch (Exception ex)
                        {
                            LogException?.Invoke(null, new ExceptionEventArgs(ex, "Can't parse background line:"));
                        }
                    }
                }
                return new SpectrumBackground(buf, scaling);
            }
            return new SpectrumBackground();
        }
    }
}
