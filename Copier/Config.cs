using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Copier.Json;

namespace Copier
{
    class Config
    {
        internal class ConfigData
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public LoggingLevel LoggingLevel { get; set; }

            [JsonConverter(typeof(SingleToListConverter<string>))]
            public List<string> SourceDirectories { get; set; }
            
            public string DestinationDirectory { get; set; }

            public string LoggingDirectory { get; set; }
        }

        public ConfigData Data { get; private set; }

        public Config(string pathToConfFile)
        {
            if (File.Exists(pathToConfFile))
            {
                var errors = new List<string>();

                using (var file = File.OpenText(pathToConfFile))
                {
                    var serializer = JsonSerializer.Create();
                    serializer.Error += (sender, args) =>
                    {
                        args.ErrorContext.Handled = true;
                        var member = args.ErrorContext.Member;

                        try
                        {
                            if (string.IsNullOrEmpty((string)member))
                                errors.Add(args.ErrorContext.Error.Message);
                            else
                                errors.Add($"Invalid value for '{member}'");
                        }
                        catch (InvalidCastException) { }
                    };

                    Data = (ConfigData)serializer.Deserialize(file, typeof(ConfigData));
                }

                if (errors.Count != 0)
                    throw new JsonSerializationException(string.Join("\n", errors));
            }
            else throw new FileNotFoundException();

            Debug.WriteLine($"Raw config:\n{JsonConvert.SerializeObject(Data, Formatting.Indented)}\n");
        }

        
        /// <summary>
        /// Cleans Data object - removes invalid paths ... etc.
        /// </summary>
        /// <return>Dictionary where key is the name of property of ConfigData, value is the List of errors occured while cleaning that property</returns>
        public Dictionary<string, List<Exception>> CleanData()
        {
            var exceptions = new Dictionary<string, List<Exception>>(4);

            //destination directory
            var destEx = ValidateDirectory(Data.DestinationDirectory, true);
            if (destEx != null) 
                exceptions[nameof(Data.DestinationDirectory)] = new List<Exception> { destEx };

            //directory
            var logEx = ValidateDirectory(Data.LoggingDirectory, true);
            if (logEx != null)
            {
                exceptions[nameof(Data.LoggingDirectory)] = new List<Exception> { logEx };
                Data.LoggingDirectory = "";
            }

            //source directories - remove invalid
            var sourceEx = new List<Exception>();
            var sourceDirs = Data.SourceDirectories.Distinct().ToList();
            
            for (int i = sourceDirs.Count - 1; i >= 0; i--)
            {
                var e = ValidateDirectory(sourceDirs[i]);
                if (e != null)
                {
                    sourceEx.Add(e);
                    sourceDirs.RemoveAt(i);
                }
            }
            Data.SourceDirectories = sourceDirs;

            if (sourceEx.Count > 0)
                exceptions[nameof(Data.SourceDirectories)] = sourceEx;

            return exceptions;
        }

        public static Exception ValidateDirectory(string path, bool canWrite = false)
        {
            if (!Directory.Exists(path))
                return new DirectoryNotFoundException($"'{path}' does not exist");
            else
            {
                try
                {
                    var security = Directory.GetAccessControl(path);
                    var acl = security.GetAccessRules(true, true,
                        typeof(System.Security.Principal.NTAccount));
                    Directory.GetFiles(path);

                    if (canWrite)
                    {
                        string rndName;

                        do rndName = Path.Combine(path, Path.GetRandomFileName());
                        while (Directory.Exists(rndName));

                        Directory.CreateDirectory(rndName).Delete();
                    }
                }
                catch (UnauthorizedAccessException) { return new UnauthorizedAccessException($"'{path}' is inaccessible"); }
            }

            return null;
        }
    }
}
