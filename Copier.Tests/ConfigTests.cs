using System;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Security.AccessControl;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Copier.Tests
{
    [TestFixture]
    public class ConfigTests
    {
        private MockFileSystem fileSystem;
        private string validPathToConf;

        [SetUp]
        public void SetFileSystem()
        {
            fileSystem = new MockFileSystem();
            validPathToConf = @"C:\ProgramData\Copier\conf.json";
            var rndBytes = new byte[2048];
            new Random().NextBytes(rndBytes);
            var file = new MockFileData(rndBytes);
            fileSystem.AddFile(@"C:\DataToCopy\file.bin", file);
            fileSystem.AddDirectory(@"C:\Archive");
        }

        public IDirectoryInfo CreateSecuredDir(IFileSystem fs, string path)
        {
            var security = new DirectorySecurity();
            security.AddAccessRule(new FileSystemAccessRule(
                fs.Path.Combine(Environment.MachineName, Environment.UserName),
                FileSystemRights.Read,
                AccessControlType.Deny));
            var dir = fs.Directory.CreateDirectory(path);
            dir.SetAccessControl(security);

            return dir;
        }

        [Test]
        public void Config_ConfFileNotFound_ThrowsFileNotFoundException()
        {
            Assert.That(() => new Config(@"C:\NoData\Copier\conf.json", fileSystem),
                Throws.TypeOf(typeof(System.IO.FileNotFoundException)));
        }

        [Test]
        public void Config_ConfFileIsInvalidJson_ThrowsJsonSerializationException()
        {
            var invalidJson = @"{""DestinationDirectory"": C:\path\path}";
            fileSystem.AddFile(validPathToConf, new MockFileData(invalidJson));

            Assert.That(() => new Config(validPathToConf, fileSystem),
                Throws.TypeOf(typeof(JsonSerializationException)));
        }

        [Test]
        [TestCase("fatalend")]
        [TestCase("NOnee")]
        [TestCase("debugdebug")]
        [TestCase("startfatal")]
        public void Config_ConfFileHasInvalidValueForLoggingLevel_ThrowsJsonSerializationException(string logLevel)
        {
            var conf = string.Format(@"
            {{
                ""SourceDirectories"": [
                    ""C:\\DataToCopy"",
	            ],
                ""DestinationDirectory"": ""C:\\Archive"",
	            ""LoggingLevel"": ""{0}"",
            }}", logLevel);
            
            fileSystem.AddFile(validPathToConf, new MockFileData(conf));

            Assert.That(() => new Config(validPathToConf, fileSystem),
                Throws.TypeOf(typeof(JsonSerializationException)));
        }

        [Test]
        public void Config_SourceDirectoriesFieldIsString_OK()
        {
            var conf = @"
            {
                ""SourceDirectories"": ""C:\\DataToCopy"",
                ""DestinationDirectory"": ""C:\\Archive"",
            }";
            fileSystem.AddFile(validPathToConf, new MockFileData(conf));

            Assert.DoesNotThrow(() => new Config(validPathToConf, fileSystem));
        }

        [Test]
        public void Config_SourceDirectoriesIsEmpty_OK()
        {
            var conf = @"
            {
                ""SourceDirectories"": [],
                ""DestinationDirectory"": ""C:\\Archive"",
            }";
            fileSystem.AddFile(validPathToConf, new MockFileData(conf));

            Assert.DoesNotThrow(() => new Config(validPathToConf, fileSystem));
        }

        [Test]
        public void CleanData_SourceDirectoriesIsEmpty_OK()
        {
            var conf = @"
            {
                ""SourceDirectories"": [],
                ""DestinationDirectory"": ""C:\\Archive"",
            }";
            fileSystem.AddFile(validPathToConf, new MockFileData(conf));
            var config = new Config(validPathToConf, fileSystem);

            Assert.DoesNotThrow(() => config.CleanData());
        }

        [Test]
        public void CleanData_DestDirDoesNotExist_DictionaryHasKey()
        {
            var confText = @"
            {
                ""SourceDirectories"": ""C:\\DataToCopy"",
                ""DestinationDirectory"": ""C:\\Archive\\DoesntExist"",
            }";
            fileSystem.AddFile(validPathToConf, new MockFileData(confText));

            var conf = new Config(validPathToConf, fileSystem);
            var exceptions = conf.CleanData();

            Assert.That(exceptions, Contains.Key(nameof(conf.Data.DestinationDirectory)));
        }

        [Test]
        public void CleanData_AllSourceDirsAreInvalid_ListBecomesEmpty()
        {
            var confText = @"
            {
                ""SourceDirectories"": [
                    ""C:\\Doesntexist"",
                    ""C:\\Doesntexist1"",
                    ""C:\\Doesntexist2"",
                ],
                ""DestinationDirectory"": ""C:\\Archive"",
            }";
            fileSystem.AddFile(validPathToConf, new MockFileData(confText));

            var conf = new Config(validPathToConf, fileSystem);
            conf.CleanData();

            Assert.That(conf.Data.SourceDirectories, Is.Empty);
        }

        [Test]
        [Ignore("It doesn't work with MockFileSystem for some reason")]
        public void CleanData_DestDirIsNotPermittedToWrite_DictionaryHasKey()
        {
            //Arrange
            var confText = @"
            {
                ""SourceDirectories"": ""C:\\DataToCopy"",
                ""DestinationDirectory"": ""C:\\Archive\\SecuredDir"",
            }";
            fileSystem.AddFile(validPathToConf, new MockFileData(confText));
            CreateSecuredDir(fileSystem, @"C:\\Archive\\SecuredDir");

            //Act
            var conf = new Config(validPathToConf, fileSystem);
            var exceptions = conf.CleanData();

            //Assert
            Assert.That(exceptions, Does.ContainKey(nameof(conf.Data.DestinationDirectory)));
        }

        [Test]
        [Ignore("It's not unit test")]
        public void CleanData_DestDirIsNotPermittedToWrite_DictionaryHasKey_RealFileSystem()
        {
            //Arrange
            var realFileSystem = new FileSystem();
            CreateSecuredDir(realFileSystem, $"C:\\Users\\{Environment.UserName}\\Desktop\\11");
            validPathToConf = $"C:\\Users\\{Environment.UserName}\\Desktop\\config.json";
            var confText = string.Format(@"
            {{
                ""SourceDirectories"": ""C:\\Users\\{0}\\Desktop\\1"",
                ""DestinationDirectory"": ""C:\\Users\\{0}\\Desktop\\11"",
            }}", Environment.UserName);
            realFileSystem.File.WriteAllText(validPathToConf, confText);

            //Act
            var conf = new Config(validPathToConf, realFileSystem);
            var exceptions = conf.CleanData();

            //Assert
            Assert.That(exceptions, Does.ContainKey(nameof(conf.Data.DestinationDirectory)));
        }

        [Test]
        [Ignore("It doesn't work with MockFileSystem for some reason")]
        public void ValidateDirectory_UserHasNoAccessToRead_ReturnsUnauthorizedAccessException()
        {
            var dir = CreateSecuredDir(fileSystem, @"C:\SecuredFolder");
            
            var exception = Config.ValidateDirectory(dir.FullName, true, fileSystem);
            
            Assert.That(exception is UnauthorizedAccessException);
        }

        [Test]
        [TestCase(@"F:\not exist")]
        [TestCase(@"12454")]
        [TestCase(@"C:\Archive\NotExist")]
        public void ValidateDirectory_DirectoryDoesNotExist_ReturnsDirectoryNotFoundException(string path)
        {
            var exception = Config.ValidateDirectory(path, true, fileSystem);

            Assert.That(exception is DirectoryNotFoundException);
        }
    }
}
