using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Data;

namespace Copier
{
    class Copier
    {
        public static Dictionary<string, int> MakeCopies(List<DirectoryInfo> sourceDirs, DirectoryInfo destDir, string timeFormat)
        {
            var failed = new Dictionary<string, int>();

            foreach (var sourceDir in sourceDirs)
            {
                try
                {
                    if (sourceDir.Exists)
                    {
                        string p = Path.Combine(destDir.FullName, $"{sourceDir.Name}{DateTime.Now.ToString(timeFormat)}"); ;
                        string path = p;

                        for (int i = 1; i <= 1000; ++i)
                            if (Directory.Exists(p))
                                p = $"{path} ({i})";
                            else
                            {
                                path = p;
                                break;
                            }

                        if (path == null)
                            throw new DuplicateNameException();

                        var newDir = Directory.CreateDirectory(path);
                        int failedFilesCount = 0;
                        CopyDirectory(sourceDir, newDir, ref failedFilesCount);
                        
                        if (failedFilesCount > 0)    
                            failed[sourceDir.FullName] = failedFilesCount;
                        
                        Logger.Info($"Directory '{sourceDir.FullName}' copied");
                    }
                    else throw new DirectoryNotFoundException();
                }
                catch (DirectoryNotFoundException) { Logger.Error($"Directory '{sourceDir}' does not exist"); }
                catch (DuplicateNameException) { Logger.Error($"Directory '{sourceDir.FullName}' cannot be copied due its name"); }
            }

            return failed;
        }

        public static void CopyDirectory(DirectoryInfo sourceDir, DirectoryInfo destDir, ref int failedFiles)
        {
            if (!sourceDir.Exists)
                throw new DirectoryNotFoundException("Source directory does not exist");

            Directory.CreateDirectory(destDir.FullName);
            var dirs = sourceDir.GetDirectories();
            var files = sourceDir.GetFiles();

            foreach (var file in files)
            {
                var tempPath = Path.Combine(destDir.FullName, file.Name);
                try
                {
                    file.CopyTo(tempPath, false);
                    Logger.Debug($"Copied file '{file.FullName}'");
                }
                catch (UnauthorizedAccessException)
                {
                    failedFiles++;
                    Logger.Error($"Access denied to '{file.FullName}'");
                }
                catch (IOException)
                {
                    failedFiles++;
                    Logger.Error($"Error occurred while copying '{file.FullName}'");
                }
            }

            //recursive directory copy
            foreach (var subDir in dirs)
            {
                var tempPath = Path.Combine(destDir.FullName, subDir.Name);
                CopyDirectory(subDir, new DirectoryInfo(tempPath), ref failedFiles);
            }
        }
    }
}
