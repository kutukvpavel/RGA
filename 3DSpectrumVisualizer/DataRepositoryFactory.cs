using System.IO;

namespace _3DSpectrumVisualizer
{
    public static class DataRepositoryFactory
    {
        public static T CreateRepository<T>(string path) where T : DataRepositoryBase
        {
            return (T)CreateRepository(path);
        }

        public static DataRepositoryBase CreateRepository(string path)
        {
            string ext = Path.GetExtension(path);
            if (ext.Length == 0) //dir
            {
                return new FolderDataRepository(path);
            }
            return ext switch
            {
                ZipHelpers.ZipFileExtension => new ZipDataRepository(path),
                _ => null,
            };
        }
    }
}
