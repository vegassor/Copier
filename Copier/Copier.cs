using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace Copier
{
    class Copier
    {
        public static Dictionary<string, (int success, int fail)> MakeCopies(List<DirectoryInfo> sourceDirs, DirectoryInfo destDir, string timeFormat)
        {
            var failed = new Dictionary<string, (int, int)>();

            foreach (var sourceDir in sourceDirs)
            {
                try
                {
                    if (!sourceDir.Exists) throw new DirectoryNotFoundException();
                    
                    var newDir = CreateDirectoryWithTimeStamp(destDir, sourceDir.Name, timeFormat);
                    failed[sourceDir.FullName] = CopyDirectory(sourceDir, newDir);

                    Logger.Info($"Copied directory '{sourceDir.FullName}'");
                }
                catch (DirectoryNotFoundException) { Logger.Error($"Directory '{sourceDir}' does not exist"); }
                catch (DuplicateNameException) { Logger.Error($"Directory '{sourceDir.FullName}' cannot be copied due its name"); }
                catch (PathTooLongException) { Logger.Error($"Directory '{sourceDir.FullName}' cannot be copied because the path is too long"); }
            }

           return failed;
        }

        public static DirectoryInfo CreateDirectoryWithTimeStamp(DirectoryInfo destDir, string dirName, string timeFormat)
        {
            string p = Path.Combine(destDir.FullName, $"{dirName}{DateTime.Now.ToString(timeFormat)}");
            string path = p;

            for (int i = 1; i <= 1000; ++i)
            {
                if (Directory.Exists(p))
                    p = $"{path} ({i})";
                else
                {
                    path = p;
                    break;
                }
            }

            if (path == null)
                throw new DuplicateNameException();

            return Directory.CreateDirectory(path);
        }

        public static (int success, int fail) CopyDirectory(DirectoryInfo sourceDir, DirectoryInfo destDir)
        {
            if (!sourceDir.Exists)
                throw new DirectoryNotFoundException("Source directory does not exist");

            return CopyRecursively(sourceDir, destDir, (0, 0));
        }

        private static (int, int) CopyRecursively (DirectoryInfo sourceDir, DirectoryInfo destDir, (int success, int fail) stats) 
        {
            try
            {
                Directory.CreateDirectory(destDir.FullName);
                var dirs = sourceDir.GetDirectories();
                var files = sourceDir.GetFiles();

                foreach (var file in files)
                {
                    var tempPath = Path.Combine(destDir.FullName, file.Name);
                    try
                    {
                        file.CopyTo(tempPath, false);
                        stats.success++;
                        Logger.Debug($"Copied file '{file.FullName}'");
                    }
                    catch (UnauthorizedAccessException)
                    {
                        stats.fail++;
                        Logger.Error($"Access denied to '{file.FullName}'");
                    }
                    catch (IOException)
                    {
                        stats.fail++;
                        Logger.Error($"Error occurred while copying '{file.FullName}'");
                    }
                }

                //recursive directory copy
                foreach (var subDir in dirs)
                    stats = CopyRecursively(subDir,
                        new DirectoryInfo(Path.Combine(destDir.FullName, subDir.Name)),
                        stats);
            }
            catch (UnauthorizedAccessException) { Logger.Error($"Directory {sourceDir.FullName} is inaccessible"); }

            return stats;
        }
    }
}
