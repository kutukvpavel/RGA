using System;
using System.Collections.Generic;
using System.Text;
using CommandLine;

namespace Acquisition
{
    [Verb("acquire", true, HelpText = "Acquire data")]
    public class AcquisitionOptions
    {
        /* Acquisition mode */
        [Option('p', "port", Required = true)]
        public string Port { get; set; }
        [Option('b', "begin", Required = true)]
        public string StartAMU { get; set; }
        [Option('e', "end", Required = true)]
        public string StopAMU { get; set; }
        [Option('n', "resolution", Required = false)]
        public string PointsPerAMU { get; set; } = null;
        [Option('m', "multiplier", Required = false)]
        public bool UseCDEM { get; set; } = false;
        [Option('g', "gaps", Required = false)]
        public IEnumerable<string> Gaps { get; set; } = null;
    }

    [Verb("restore", false, HelpText = "Restore data from raw data backup (reapplies CDEM dvivision factor etc)")]
    public class RestoreOptions
    {
        /* Backup restore mode */
        [Option('d', "directory", Required = true)]
        public string BackupDirectory { get; set; }
        [Option('s', "search", Required = false)]
        public string SearchPattern { get; set; } = null;
    }
}
