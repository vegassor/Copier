using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Abstractions; //"Interface" for System.IO
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Copier.Json;

namespace Copier
{
    public class Config
    {
        public class ConfigData
        {
            [JsonProperty(Required = Required.Always)]
            [JsonConverter(typeof(SingleToListConverter<string>))]
            public List<string> SourceDirectories { get; set; }

            [JsonProperty(Required = Required.Always)]
            public string DestinationDirectory { get; set; }
            
            [JsonConverter(typeof(StringEnumConverter))]
            public LoggingLevel LoggingLevel { get; set; }
            public string LoggingDirectory { get; set; }

        }

        public ConfigData Data { get; private set; }
        private readonly IFileSystem fileSystem;

        /// <summary>
        /// Reads json-file located at 'pathToConfFile'
        /// </summary>
        /// <param name="pathToConfFile">Path to configuration json-file</param>
        /// <param name="_fileSystem">File system from System.IO.Abstractions</param>
        public Config(string pathToConfFile, IFileSystem _fileSystem)
        {
            this.fileSystem = _fileSystem;

            if (fileSystem.File.Exists(pathToConfFile))
            {
                var errors = new List<string>();

                using (var file = fileSystem.File.OpenText(pathToConfFile))
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
            else throw new System.IO.FileNotFoundException($"Config file not found at '{pathToConfFile}'");

            Debug.WriteLine($"Raw config:\n{JsonConvert.SerializeObject(Data, Formatting.Indented)}\n");
        }

        
        /// <summary>
        /// Cleans this.Data - removes invalid paths ... etc.
        /// </summary>
        /// <return>Dictionary:
        ///     Key is the name of property of ConfigData;
        ///     Value is the List of errors occured while cleaning that property</returns>
        public Dictionary<string, List<Exception>> CleanData()
        {
            var exceptions = new Dictionary<string, List<Exception>>(4);

            var destEx = ValidateDirectory(Data.DestinationDirectory, true, fileSystem);
            if (destEx != null) 
                exceptions[nameof(Data.DestinationDirectory)] = new List<Exception> { destEx };

            var logEx = ValidateDirectory(Data.LoggingDirectory, true, fileSystem);
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
                var e = ValidateDirectory(sourceDirs[i], false, fileSystem);
                if (e != null)
                {
                    sourceEx.Add(e);
                    sourceDirs.RemoveAt(i);
                }
            }
            //if copying the destDirectory to destDirectory it should be first in the list
            int destIndex = sourceDirs.IndexOf(Data.DestinationDirectory);
            destIndex = destIndex >= 0 ? destIndex : 0;
            if (sourceDirs.Count > 0)
                (sourceDirs[destIndex], sourceDirs[0]) = (sourceDirs[0], sourceDirs[destIndex]);
            
            Data.SourceDirectories = sourceDirs;

            if (sourceEx.Count > 0)
                exceptions[nameof(Data.SourceDirectories)] = sourceEx;

            return exceptions;
        }

        /// <summary>
        /// Checks if directory can be accessed
        /// </summary>
        /// <param name="path">Full path to directory</param>
        /// <param name="canWrite">If true, the method checks if new directories can be created in this directory</param>
        /// <param name="fileSystem">File system from System.IO.Abstractions</param>
        /// <returns>Exception if directory is not valid, null if valid</returns>
        public static Exception ValidateDirectory(string path, bool canWrite, IFileSystem fileSystem)
        {
            if (!fileSystem.Directory.Exists(path))
                return new System.IO.DirectoryNotFoundException($"'{path}' does not exist");
            else
            {
                try
                {
                    var security = fileSystem.Directory.GetAccessControl(path);
                    var acl = security.GetAccessRules(true, true,
                        typeof(System.Security.Principal.NTAccount));
                    fileSystem.Directory.GetFiles(path);

                    if (canWrite)
                    {
                        string rndName;

                        do rndName = fileSystem.Path.Combine(path, fileSystem.Path.GetRandomFileName());
                        while (fileSystem.Directory.Exists(rndName));

                        fileSystem.Directory.CreateDirectory(rndName).Delete();
                    }
                }
                catch (UnauthorizedAccessException) { return new UnauthorizedAccessException($"'{path}' is inaccessible"); }
            }

            return null;
        }
    }
}
