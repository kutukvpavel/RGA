using CsvHelper;
using CsvHelper.Configuration;
using MoreLinq;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace _3DSpectrumVisualizer
{
    public class FolderDataRepository : DataRepositoryBase
    {
        public FolderDataRepository(string folder, int pollPeriod = 3000) : base(folder)
        {
            _PollTimer = new System.Timers.Timer(pollPeriod) { AutoReset = true, Enabled = false };
            _PollTimer.Elapsed += PollTimer_Elapsed;
        }

        #region Properties

        public override bool Enabled
        {
            get { return _PollTimer.Enabled; }
            set { if (value) _PollTimer.Start(); else _PollTimer.Stop(); }
        }

        #endregion

        #region Public Methods

        public override void Initialize()
        {
            _UVPath = Path.Combine(Location, InfoSubfolder, UVFileName);
            _GasPath = Path.Combine(Location, InfoSubfolder, GasFileName);
            _TempPath = Path.Combine(Location, InfoSubfolder, TemperatureFileName);
        }

        public override void LoadData()
        {
            bool raiseDataAdded = false;
            bool lockTaken = Monitor.TryEnter(UpdateSynchronizingObject, 10);
            if (!lockTaken) return;
            try
            {
                //Scan files
                var newFiles = Directory.EnumerateFiles(Location, Filter, SearchOption.TopDirectoryOnly)
                    .AsParallel().Select(x => new FileInfo(x)).Where(x => x.CreationTimeUtc > _LastFileCreationTime)
                    .OrderBy(x => x.Name);
                foreach (var item in newFiles)
                {
                    try
                    {
                        using FileStream f = File.Open(item.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                        using StreamReader r = new StreamReader(f);
                        AddFile(ReadLines(r), item.Name);
                        if (item.CreationTimeUtc > _LastFileCreationTime)
                            _LastFileCreationTime = item.CreationTimeUtc;
                        raiseDataAdded = true;
                    }
                    catch (IOException)
                    { }
                    catch (Exception)
                    {
                        Results.Add(new ScanResult());
                    }
                }
                //Info files
                if (Results.Count == 0) return; //Do not read info files before we have a valid StartTime
                ReadAvailableInfoLines(ref _TempStream, _TempPath, AddTempInfoLine);
                ReadAvailableInfoLines(ref _UVStream, _UVPath, AddUVInfoLine);
                ReadAvailableInfoLines(ref _GasStream, _GasPath, AddGasInfoLine);
            }
            catch (Exception ex)
            {
                Program.LogException(this, ex);
            }
            finally
            {
                Monitor.Exit(UpdateSynchronizingObject);
            }
            if (raiseDataAdded) RaiseDataAdded(this);
        }

        #endregion

        #region Private

        private string _TempPath;
        private string _UVPath;
        private string _GasPath;
        private TextReader _TempStream;
        private TextReader _UVStream;
        private TextReader _GasStream;
        private DateTime _LastFileCreationTime = DateTime.MinValue;
        private readonly System.Timers.Timer _PollTimer;
        private bool ReadAvailableInfoLines(ref TextReader s, string path, Action<string> addMethod)
        {
            string l;
            bool res = false;
            while ((l = TryReadLine(ref s, path)) != null)
            {
                try
                {
                    addMethod(l);
                    res = true;
                }
                catch (Exception ex)
                {
                    Program.LogException(this, ex);
                    if (l != null) Program.LogInfo(this, l);
                }
            }
            return res;
        }
        private string TryReadLine(ref TextReader s, string path)
        {
            try
            {
                if (VerifyInfoStreamOpen(ref s, path))
                {
                    return s.ReadLine();
                }
            }
            catch (Exception)
            {
                
            }
            return null;
        }
        private bool VerifyInfoStreamOpen(ref TextReader s, string path)
        {
            if (s == null)
            {
                if (!File.Exists(path)) return false;
                int retry = 3;
                while (retry-- > 0)
                {
                    try
                    {
                        s = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                        break;
                    }
                    catch (IOException)
                    {

                    }
                }
            }
            return true;
        }
        private void PollTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            LoadData();
        }

        #endregion
    }
}
