using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoreLinq;
using CsvHelper;
using CsvHelper.Configuration;
using System.Drawing;

namespace DebugSpectraHelper
{
    class Program
    {
        static readonly CsvConfiguration Config = new CsvConfiguration(CultureInfo.InvariantCulture);

        static void Main(string[] args)
        {
            Console.WriteLine("Reading seed...");

            PointF[] points;
            using (TextReader tr = new StreamReader(args[0]))
            using (CsvReader cr = new CsvReader(tr, Config))
            {
                points = cr.GetRecords<PointF>().ToArray();
            }

            Console.WriteLine("Writing generated spectra...");
            string dir = Path.GetDirectoryName(args[0]);

            for (int i = 1; i < 5000; i++)
            {
                using TextWriter tw = new StreamWriter(Path.Combine(dir, $"gen{i:D4}.txt"));
                using CsvWriter cw = new CsvWriter(tw, Config);
                cw.NextRecord();
                cw.NextRecord();
                foreach (var item in points)
                {
                    cw.WriteField(item.X);
                    cw.WriteField((item.Y * i / 1000).ToString("E2", CultureInfo.InvariantCulture));
                    cw.WriteField("");
                    cw.NextRecord();
                }
            }

            Console.WriteLine("Finished.");
        }
    }
}
