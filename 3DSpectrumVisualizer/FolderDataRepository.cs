using MoreLinq;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Timers;
using System.Text.RegularExpressions;
using System.Diagnostics;

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
            string infoSubfolder = Path.Combine(Location, InfoSubfolder);
            _UVPath = Path.Combine(infoSubfolder, UVFileName);
            _GasPath = Path.Combine(infoSubfolder, GasFileName);
            _TempPath = Path.Combine(infoSubfolder, TemperatureFileName);
            string sensorsPattern = 
                $@"^{Regex.Escape(infoSubfolder + Path.DirectorySeparatorChar)}{SensorFileName.Replace("{0}", "[0-9]+").Replace(".", @"\.")}$";
            _SensorPathes = Directory.GetFiles(infoSubfolder).Where(x => Regex.IsMatch(x, sensorsPattern)).ToArray();
            _SensorStreams = new TextReader[_SensorPathes.Length];
        }

        public override void OpenDescriptionFile()
        {
            string path = Path.Combine(Location, InfoSubfolder, InfoFileName);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, Path.GetDirectoryName(Location) + Environment.NewLine);
            }
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

        protected override void LoadDataInternal()
        {
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
                for (int i = 0; i < _SensorStreams.Length; i++)
                {
                    ReadAvailableInfoLines(ref _SensorStreams[i], _SensorPathes[i], x => AddSensorInfoLine(x, i));
                }
            }
            catch (Exception ex)
            {
                Program.LogException(this, ex);
            }
            finally
            {
                Monitor.Exit(UpdateSynchronizingObject);
            }
        }

        #endregion

        #region Private

        private string _TempPath;
        private string _UVPath;
        private string _GasPath;
        private string[] _SensorPathes;
        private TextReader _TempStream;
        private TextReader _UVStream;
        private TextReader _GasStream;
        private TextReader[] _SensorStreams;
        private DateTime _LastFileCreationTime = DateTime.MinValue;
        private readonly System.Timers.Timer _PollTimer;
        private bool ReadAvailableInfoLines(ref TextReader s, string path, Action<string> addMethod)
        {
            string l;
            bool res = false;
            while ((l = TryReadLine(ref s, path)) != null)
            {
                if (l.Length == 0) continue;
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
