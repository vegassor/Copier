using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Security;
using Newtonsoft.Json;

namespace Copier
{
    class Program
    {
        static void Main(string[] args)
        {
            Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));

            try
            {
                var fileSystem = new FileSystem();
                var conf = Init(args, fileSystem);
                var toDir = fileSystem.DirectoryInfo.FromDirectoryName(conf.Data.DestinationDirectory);
                var fromDirs = (from dir in conf.Data.SourceDirectories
                                select fileSystem.DirectoryInfo.FromDirectoryName(dir))
                                .ToList();
                var dirsStats = Copier.MakeCopies(fromDirs,
                    toDir,
                    "' copy from 'yyyy-MM-dd_HH-mm-ss",
                    fileSystem);
                EndLog(fromDirs, dirsStats);
            }
            catch (FileNotFoundException)
            {
                Logger.Fatal("Config file is not found");
                Console.WriteLine("Config file is not found");
            }
            catch (JsonSerializationException e)
            {
                var msg = $"Config file is not formed correctly:\n{e.Message}";
                Logger.Fatal(msg);
                Console.WriteLine(msg);
            }
            catch (SecurityException e)
            {
                if (e.Message != nameof(Config.Data.DestinationDirectory)) throw e;

                var msg = "Destination directory is invalid";
                Logger.Fatal(msg);
                Console.WriteLine(msg);
            }
            catch (UnauthorizedAccessException) { Console.WriteLine("[Fatal] Cannot create log file, try different directory"); }
            catch (Exception e) { Logger.Log(e, e.ToString(), LoggingLevel.Fatal); }
            finally
            {
                Logger.WriteRawToLog($"Log ended at {DateTime.Now:yyyy-MM-dd_HH-mm-ss-ffff}\n\n");

                #if DEBUG
                Console.WindowWidth = 200;
                Console.WindowHeight = 40;
                Debug.WriteLine($"\nPress any key to exit\n{Environment.CommandLine}");
                Console.SetWindowPosition(0, 0);
                Console.ReadKey();
                #endif
            }
        }

        public static Config Init(string[] args, IFileSystem fileSystem)
        {
            var pathToConf = "conf.json";

            if (args.Length > 0)
            {
                if (args.Length == 1)
                    pathToConf = args[0];
                else
                {
                    Console.WriteLine("Usage: [pathToConfigFile]");
                    Environment.Exit(1);
                }
            }

            var conf = new Config(pathToConf, fileSystem);
            var exceptions = conf.CleanData();

            if (exceptions.ContainsKey(nameof(conf.Data.DestinationDirectory)))
                throw new SecurityException(nameof(conf.Data.DestinationDirectory));

            if (exceptions.ContainsKey(nameof(conf.Data.LoggingDirectory)))
            {
                var eType = exceptions[nameof(conf.Data.LoggingDirectory)].GetType();

                if (eType == typeof(UnauthorizedAccessException))
                    Console.WriteLine("Logging directory is inaccessible");
                else if (eType == typeof(DirectoryNotFoundException))
                    Console.WriteLine("Logging directory does not exist");
                var path = Directory.GetCurrentDirectory();
                Console.WriteLine($"Log file will be created in current directory - '{path}'");
                conf.Data.LoggingDirectory = path;
            }

            Debug.WriteLine($"Cleaned config:\n{JsonConvert.SerializeObject(conf.Data, Formatting.Indented)}\n");
            Logger.Init(conf.Data.LoggingDirectory, fileSystem, conf.Data.LoggingLevel);
            Logger.WriteRawToLog($"Log started at {DateTime.Now:yyyy-MM-dd_HH-mm-ss-ffff}\nLogging level: {conf.Data.LoggingLevel}\n");
            Logger.WriteRawToLog($"Destination directory: {conf.Data.DestinationDirectory}\n");

            if (exceptions.ContainsKey(nameof(conf.Data.SourceDirectories)))
                foreach (var dir in exceptions[nameof(conf.Data.SourceDirectories)])
                    Logger.Error($"Source directory {dir.Message}");

            return conf;
        }

        public static void EndLog(List<IDirectoryInfo> fromDirs, Dictionary<string, (int success, int fail)> dirsStats)
        {
            Logger.WriteRawToLog(new string('-', 50) + '\n');

            foreach (var dir in fromDirs)
                if (dirsStats.ContainsKey(dir.FullName))
                {
                    (int success, int fail) = dirsStats[dir.FullName];
                    Logger.WriteRawToLog($"'{dir.FullName}': failed - {fail}, copied - {success} files\n");
                }

        }
    }
}
