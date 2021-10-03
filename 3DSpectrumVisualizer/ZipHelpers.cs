using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;

namespace _3DSpectrumVisualizer
{
    public static class ZipHelpers
    {
        public const char ZipDirectorySeparator = '/';
        public const string ZipFileExtension = ".zip";
        public const string ZipRootSearchEntity = "/backup/";

        public static string FindRootFolder(ReadOnlyCollection<ZipArchiveEntry> e, string searchEntity = ZipRootSearchEntity)
        {
            string entity = e.First(x => x.FullName.EndsWith(searchEntity)).FullName;
            string root = "";
            int i;
            try
            {
                i = entity.LastIndexOf(ZipDirectorySeparator, entity.Length - 2);
            }
            catch (ArgumentOutOfRangeException)
            {
                i = -1;
            }
            if (i > -1) root = entity.Remove(i + 1);
            return root;
        }

        public static string ReadConfigurationFile(string archivePath, 
            string configurationFileName, string searchEntity = ZipRootSearchEntity)
        {
            using ZipArchive a = ZipFile.OpenRead(archivePath);
            string filePath = FindRootFolder(a.Entries, searchEntity) + configurationFileName;
            using StreamReader r = new StreamReader(a.GetEntry(filePath).Open());
            return r.ReadToEnd();
        }

        public static void WriteConfigurationFile(string archivePath, string contents,
            string configurationFileName, string searchEntity = ZipRootSearchEntity)
        {
            using (ZipArchive a = ZipFile.Open(archivePath, ZipArchiveMode.Update))
            {
                string filePath = FindRootFolder(a.Entries, searchEntity) + configurationFileName;
                var e = a.GetEntry(filePath);
                e.Delete();
                e = a.CreateEntry(filePath);
                using (StreamWriter w = new StreamWriter(e.Open()))
                {
                    w.Write(contents);
                }
            }
        }
    }
}
