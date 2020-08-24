using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

namespace Copier
{
    public class Copier
    {
        /// <summary>
        /// Copies all directories from 'sourceDirs' into 'destDir' and gives them names with time stamps
        /// </summary>
        /// <param name="sourceDirs">List of directories to be copied</param>
        /// <param name="destDir">Directory in which new directories will be created</param>
        /// <param name="timeFormat">String used to create time stamp</param>
        /// <param name="fileSystem">File system from System.IO.Abstractions</param>
        /// <returns>Dictionary: 
        ///     Key is full name of directory from 'sourceDirs';
        ///     Value is Tuple with amount of copied files (Item1) and amount of failed files (Item2) in the directory</returns>
        public static Dictionary<string, (int success, int fail)> MakeCopies(
            List<IDirectoryInfo> sourceDirs, 
            IDirectoryInfo destDir, 
            string timeFormat, 
            IFileSystem fileSystem)
        {
            var failed = new Dictionary<string, (int, int)>();

            foreach (var sourceDir in sourceDirs)
            {
                try
                {
                    if (!sourceDir.Exists) throw new DirectoryNotFoundException();
                    
                    var newDir = CreateDirectoryWithTimeStamp(destDir, sourceDir.Name, timeFormat, fileSystem);
                    failed[sourceDir.FullName] = CopyDirectoryContent(sourceDir, newDir, fileSystem);

                    Logger.Info($"Copied directory '{sourceDir.FullName}'");
                }
                catch (DirectoryNotFoundException) { Logger.Error($"Directory '{sourceDir}' does not exist"); }
                catch (DuplicateNameException) { Logger.Error($"Directory '{sourceDir.FullName}' cannot be copied due its name"); }
                catch (PathTooLongException) { Logger.Error($"Directory '{sourceDir.FullName}' cannot be copied because the path is too long"); }
            }

           return failed;
        }

        /// <summary>
        /// Creates new directory with time stamp
        /// </summary>
        /// <param name="destDir">Directory in which new directory will be created</param>
        /// <param name="dirName">Name of the folder to create</param>
        /// <param name="timeFormat">String that forms a suffix for directory name using DateTime formatting</param>
        /// <param name="fileSystem">File system from System.IO.Abstractions</param>
        /// <returns>New directory in 'destDir' with given 'dirName' and 'timeFormat' suffix</returns>
        public static IDirectoryInfo CreateDirectoryWithTimeStamp(
            IDirectoryInfo destDir, 
            string dirName, 
            string timeFormat, 
            IFileSystem fileSystem)
        {
            string p = fileSystem.Path.Combine(destDir.FullName,
                $"{dirName}{DateTime.Now.ToString(timeFormat)}");
            string path = p;

            for (int i = 1; i <= 1000; ++i)
            {
                if (fileSystem.Directory.Exists(p))
                    p = $"{path} ({i})";
                else
                {
                    path = p;
                    p = null;
                    break;
                }
            }

            if (p != null)
                throw new DuplicateNameException();

            return fileSystem.Directory.CreateDirectory(path);
        }

        /// <summary>
        /// Copies all files and subdirectories of 'sourceDir' to 'destDir'
        /// </summary>
        /// <param name="sourceDir">Source directory</param>
        /// <param name="destDir">Destination directory</param>
        /// <param name="fileSystem">File system from System.IO.Abstractions</param>
        /// <returns>Tuple with amount of copied files (Item1) and amount of failed files (Item2)</returns>
        public static (int success, int fail) CopyDirectoryContent(
            IDirectoryInfo sourceDir, 
            IDirectoryInfo destDir, 
            IFileSystem fileSystem)
            => CopyRecursively(sourceDir, destDir, (0, 0), fileSystem);

        private static (int, int) CopyRecursively (
            IDirectoryInfo sourceDir, 
            IDirectoryInfo destDir, 
            (int success, int fail) stats, 
            IFileSystem fileSystem)
        {
            try
            {
                fileSystem.Directory.CreateDirectory(destDir.FullName);
                var subDirs = sourceDir
                    .GetDirectories()
                    .Where(sDir => !destDir.FullName.Contains(sDir.FullName)) //prevent endless copying itself
                    .OrderBy(d => d.FullName.Length);
                var files = sourceDir
                    .GetFiles()
                    .OrderBy(f => f.FullName.Length);

                foreach (var file in files)
                {
                    var tempPath = fileSystem.Path.Combine(destDir.FullName, file.Name);
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
                foreach (var subDir in subDirs)
                    stats = CopyRecursively(
                        subDir,
                        fileSystem.DirectoryInfo.FromDirectoryName(fileSystem.Path.Combine(destDir.FullName, subDir.Name)),
                        stats,
                        fileSystem);
            }
            catch (UnauthorizedAccessException) { Logger.Error($"Directory {sourceDir.FullName} is inaccessible"); }

            return stats;
        }
    }
}
