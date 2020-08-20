using System;
using System.IO;
using System.Reflection;

namespace Copier
{
    static class Logger
    {
        public static LoggingLevel Level { get; private set; }
        public static string LogPath { get; private set; }
        private static StreamWriter logFile;

        public static void Init(string logDirPath, LoggingLevel lvl = LoggingLevel.Info)
        {
            if (logFile != null)
            {
                logFile.Flush();
                logFile.Dispose();

                if (logDirPath == null) return;
            }

            if (lvl > LoggingLevel.None)
            {
                var fileName = $"{Assembly.GetCallingAssembly().GetName().Name}_log_{DateTime.Now:yyyy-MM-dd}.log";
                string path;

                if (Config.ValidateDirectory(logDirPath, true) == null)
                    path = Path.Combine(logDirPath, fileName);
                else
                    path = Path.Combine(Directory.GetCurrentDirectory(), fileName);

                logFile = File.AppendText(path);
                logFile.AutoFlush = true;
                LogPath = path;
            }
         
            Level = lvl;
        }

        public static void Fatal(string msg)
        {
            if (Level >= LoggingLevel.Fatal)
                WriteLineToLog(msg, LoggingLevel.Fatal);
        }

        public static void Error(string msg)
        {
            if (Level >= LoggingLevel.Error)
                WriteLineToLog(msg, LoggingLevel.Error);
        }

        public static void Info(string msg)
        {
            if (Level >= LoggingLevel.Info)
                WriteLineToLog(msg, LoggingLevel.Info);
        }

        public static void Debug(string msg)
        {
            if (Level >= LoggingLevel.Debug)
                WriteLineToLog(msg, LoggingLevel.Debug);
        }

        public static void Log(string msg, LoggingLevel level)
        {
            switch (level)
            {
                case LoggingLevel.Fatal:
                    Fatal(msg);
                    break;
                case LoggingLevel.Error:
                    Error(msg);
                    break;
                case LoggingLevel.Info:
                    Info(msg);
                    break;
                case LoggingLevel.Debug:
                    Debug(msg);
                    break;
            }
        }

        public static void Log(Exception e, string msg, LoggingLevel level)
        {
            var msg2 = $"[{e.GetType().Name}] {msg}";
            Log(msg2, level);
        }

        public static void WriteLineToLog(string msg, LoggingLevel level)
        {
            var formedMsg = string.Format("[{0}][{1,-5}] {2}\n",
                DateTime.Now.ToString("dd-MM-yy HH:mm:ss:ffff"),
                level,
                msg);
            WriteRawToLog(formedMsg);
        }

        public static void WriteRawToLog(string msg)
        {
            if (logFile != null)
            {
                System.Diagnostics.Debug.Write(msg);
                logFile.Write(msg);
            }
            else if (Level != LoggingLevel.None)
                throw new NullReferenceException("Logger is not initialized");
        }
    }
}
