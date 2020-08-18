using System;
using System.IO;
using System.Security;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Linq;

namespace Copier
{
    class Program
    {
        static void Main(string[] args)
        {
            Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));

            if (args.Length == 1)
            {

            }

            try
            {
                var conf = new Config("conf.json");
                var exceptions = conf.CleanData();
                Logger.Init(conf.Data.LoggingDirectory, conf.Data.LoggingLevel);
                Logger.WriteRawToLog($"Log started at {DateTime.Now:yyyy-MM-dd_hh-mm-ss-ffff}\nLogging level: {conf.Data.LoggingLevel}\n");

                if (exceptions.ContainsKey(nameof(conf.Data.DestinationDirectory)))
                    throw new SecurityException(nameof(conf.Data.DestinationDirectory));
                
                if (exceptions.ContainsKey(nameof(conf.Data.SourceDirectories)))
                    foreach (var dir in exceptions[nameof(conf.Data.SourceDirectories)])
                        Logger.Error($"Source directory {dir.Message}");

                var toDir = new DirectoryInfo(conf.Data.DestinationDirectory);
                var fromDirs = (from dir in conf.Data.SourceDirectories
                                select new DirectoryInfo(dir))
                                .ToList();

                var failedDirs = Copier.MakeCopies(fromDirs, toDir, "' copy from 'yyyy-MM-dd_hh-mm-ss");
                EndLog(fromDirs, failedDirs);
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
            catch (SecurityException)
            {
                var msg = "Destination directory is inaccessible";
                Logger.Fatal(msg);
                Console.WriteLine(msg);
            }
            catch (Exception e) { Logger.Log(e, e.ToString(), LoggingLevel.Fatal); }


#if DEBUG
            Console.WindowWidth = 200;
            Console.WindowHeight = 40;
            Debug.WriteLine($"\nPress any key to exit\n{Environment.CommandLine}");
            Console.SetWindowPosition(0, 0);
            Console.ReadKey();
#endif
        }

        public static void EndLog(List<DirectoryInfo> fromDirs, Dictionary<string, int> failedDirs)
        {
            Logger.WriteRawToLog(new string('-', 50)+'\n');

            foreach (var dir in fromDirs)
            {
                string dirName = dir.FullName;
                int total = Directory.GetFiles(dirName, "*", SearchOption.AllDirectories).Length;
                int failed = failedDirs.TryGetValue(dirName, out int failCount) ? failCount : 0;
                Logger.WriteRawToLog($"'{dir.FullName}': failed - {failed}, copied - {total - failed} files\n");
            }

            Logger.WriteRawToLog($"Log ended at {DateTime.Now:yyyy-MM-dd_hh-mm-ss-ffff}\n\n");
        }
    }
}
