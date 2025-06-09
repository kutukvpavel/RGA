using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace _3DSpectrumVisualizer
{
    public class ZipDataRepository : DataRepositoryBase
    {
        public ZipDataRepository(string filePath) : base(filePath)
        {

        }

        public override bool CanPurge { get => false; protected set => throw new NotImplementedException(); }

        #region Public Methods

        public override void Initialize()
        {
            _Archive = ZipFile.OpenRead(Location);
            //Find root and info folders
            var e = _Archive.Entries;
            string root = ZipHelpers.FindRootFolder(e);
            string infoDir = root + InfoSubfolder + ZipHelpers.ZipDirectorySeparator;
            //Initialize objects
            string dataPattern = infoDir + TemperatureFileName;
            _TempInfoEntry = e.FirstOrDefault(x => x.FullName == dataPattern);
            dataPattern = infoDir + UVFileName;
            _UVInfoEntry = e.FirstOrDefault(x => x.FullName == dataPattern);
            dataPattern = infoDir + GasFileName;
            _GasInfoEntry = e.FirstOrDefault(x => x.FullName == dataPattern);
            dataPattern = $@"^{Regex.Escape(infoDir)}{SensorFileName.Replace("{0}", "[0-9]+").Replace(".", @"\.")}$";
            _SensorEntries = e.Where(x => Regex.IsMatch(x.FullName, dataPattern)).OrderBy(x => x.Name).ToArray();
            dataPattern = $@"^{Regex.Escape(root)}[^/]{Filter.Replace(".", @"\.")}$";
            _DataEntries = e.Where(x => Regex.IsMatch(x.FullName, dataPattern));
            dataPattern = infoDir + InfoFileName;
            _InfoFileEntry = e.FirstOrDefault(x => x.FullName == dataPattern);
        }

        public override void OpenDescriptionFile()
        {
            if (_InfoFileEntry == null) return;
            string path = Path.GetTempFileName();
            _InfoFileEntry.ExtractToFile(path, true);
            new Process()
            {
                StartInfo = new ProcessStartInfo(path)
                {
                    UseShellExecute = true
                }
            }.Start();
        }
        public override void OpenRepoLocation()
        {
            new Process()
            {
                StartInfo = new ProcessStartInfo(Location)
                {
                    UseShellExecute = true
                }
            }.Start();
        }
        public override void Purge()
        {
            throw new NotImplementedException();
        }
        protected override void LoadDataInternal()
        {
            if (_Archive == null) throw new InvalidOperationException("Zip repository is not initialized.");
            lock (UpdateSynchronizingObject)
            {
                //Load main data
                foreach (var item in _DataEntries)
                {
                    try
                    {
                        using StreamReader r = new StreamReader(item.Open());
                        AddFile(ReadLines(r), item.Name);
                    }
                    catch (Exception ex)
                    {
                        Program.LogException(this, ex);
                    }
                }
                //Load info
                if (_TempInfoEntry != null) LoadInfoFile(_TempInfoEntry, AddTempInfoLine);
                if (_UVInfoEntry != null) LoadInfoFile(_UVInfoEntry, AddUVInfoLine);
                if (_GasInfoEntry != null) LoadInfoFile(_GasInfoEntry, AddGasInfoLine);
                for (int i = 0; i < _SensorEntries.Length; i++)
                {
                    LoadInfoFile(_SensorEntries[i], x => AddSensorInfoLine(x, i));
                }
            }
            _Archive.Dispose();
        }

        #endregion

        #region Private

        private ZipArchive _Archive;

        private IEnumerable<ZipArchiveEntry> _DataEntries;

        private ZipArchiveEntry _TempInfoEntry;

        private ZipArchiveEntry _UVInfoEntry;

        private ZipArchiveEntry _GasInfoEntry;

        private ZipArchiveEntry[] _SensorEntries;
        private ZipArchiveEntry _InfoFileEntry;

        private void LoadInfoFile(ZipArchiveEntry f, Action<string> addLineMethod)
        {
            using StreamReader r = new StreamReader(f.Open());
            foreach (var item in ReadLines(r))
            {
                if ((item?.Length ?? 0) == 0) continue;
                addLineMethod(item);
            }
        }

        #endregion
    }
}
