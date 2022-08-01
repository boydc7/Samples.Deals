using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Models;

namespace Rydr.Api.Core.Services.Internal
{
    public static class FileHelper
    {
        public static void Delete(string path)
        {
            // If the file doesn't exist, exception isn't thrown in the library, so no need for guard
            File.Delete(path);
        }

        public static Task<byte[]> ReadAllBytesAsync(string file) => File.ReadAllBytesAsync(file);

        public static Task<string> ReadAllTextAsync(string file) => File.ReadAllTextAsync(file);

        public static Task WriteAllBytesAsync(string file, byte[] bytes)
            => File.WriteAllBytesAsync(file, bytes);

        public static bool IsCompressed(string fileName) => new[]
                                                            {
                                                                ".zip", ".gz", ".gzip"
                                                            }.Any(fileName.EndsWith);

        public static string GetFileName(string fileAndPath, bool withExtension = false)
        {
            var x = new FileInfo(fileAndPath);

            return withExtension
                       ? string.Concat(x.Name, x.Extension)
                       : x.Name;
        }

        public static string GetFileExtension(string file)
        {
            var x = new FileInfo(file);

            return x.Extension.Replace(".", string.Empty);
        }

        public static bool Exists(string path) => File.Exists(path);

        public static void Move(string source, string target)
        {
            File.Move(source, target);
        }

        public static void Copy(string source, string target)
        {
            File.Copy(source, target, true);
        }
    }

    public static class PathHelper
    {
        public static void Delete(string path, bool recursive = true)
        {
            if (Exists(path))
            {
                Directory.Delete(path, recursive);
            }
        }

        public static bool Exists(string path) => Directory.Exists(path);

        public static DirectoryInfo Create(string path) => Directory.CreateDirectory(path);

        public static IEnumerable<string> ListFolder(string path, bool includePath = true, bool recursive = false)
        {
            var searchOption = recursive
                                   ? SearchOption.AllDirectories
                                   : SearchOption.TopDirectoryOnly;

            foreach (var file in Directory.EnumerateFiles(path, "*", searchOption))
            {
                if (includePath)
                {
                    yield return file;

                    continue;
                }

                var meta = new FileMetaData(file);

                yield return meta.FileNameAndExtension;
            }
        }
    }
}
