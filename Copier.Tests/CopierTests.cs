using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Data;
using System.Linq;

namespace Copier.Tests
{
    [TestFixture]
    class CopierTests
    {
        private MockFileSystem fileSystem;

        [SetUp]
        public void SetUp()
        {
            fileSystem = new MockFileSystem();
            var rnd = new Random();
            var rndBytes = new byte[2048];
            rnd.NextBytes(rndBytes);
            var rndBytesFile = new MockFileData(rndBytes);

            fileSystem.AddDirectory(@"C:\Archive");
            fileSystem.AddFile(@"C:\source1\file.bin", rndBytesFile);
            fileSystem.AddFile(@"C:\source1\1\file2.bin", rndBytesFile);
            fileSystem.AddFile(@"C:\source1\2\file3.bin", rndBytesFile);
            fileSystem.AddFile(@"C:\source2\1\1\file4.bin", rndBytesFile);
            fileSystem.AddDirectory(@"C:\source3");
        }

        [Test]
        public void CopyDirectoryContnet_SourceDirDoesNotExist_ThrowsDirectoryNotFoundException()
        {
            var sourceDir = fileSystem.DirectoryInfo.FromDirectoryName(@"D:\not");
            var destDir = fileSystem.DirectoryInfo.FromDirectoryName(@"C:\Archive");

            Assert.That(() => Copier.CopyDirectoryContent(sourceDir, destDir, fileSystem),
                Throws.TypeOf(typeof(DirectoryNotFoundException)));
        }

        [Test]
        [Timeout(1000)]
        public void CopyDirectoryContnet_SourceDirIsDestDir_NoEndlessLoop()
        {
            var destDir = fileSystem.DirectoryInfo.FromDirectoryName(@"C:\Archive");
            var sourceDirs = new List<IDirectoryInfo> 
                { fileSystem.DirectoryInfo.FromDirectoryName(@"C:\Archive") };

            Assert.DoesNotThrow(() => Copier.MakeCopies(sourceDirs, destDir, "HH-mm-ss", fileSystem));
        }

        [Test]
        public void CopyDirectoryContnet_SourceDirectoryHasSystemFiles_NoExceptions()
        {
            var file = fileSystem.FileInfo.FromFileName(@"C:\source1\file.bin");
            file.Attributes = FileAttributes.System | FileAttributes.ReadOnly;
            var destDir = fileSystem.Directory.CreateDirectory(@"C:\Archive\source1");
            var sourceDir = fileSystem.DirectoryInfo.FromDirectoryName(@"C:\source1");
            var a = destDir.GetDirectories();

            Assert.DoesNotThrow(() => Copier.CopyDirectoryContent(sourceDir, destDir, fileSystem));
        }

        [Test]
        public void CopyDirectoryContnet_UsualCase_CorrectAmountOfFilesCopied()
        {
            var sourceDir = fileSystem.DirectoryInfo.FromDirectoryName(@"C:\source1");
            var destDir = fileSystem.Directory.CreateDirectory(@"C:\Archive\source1_copy");

            Copier.CopyDirectoryContent(sourceDir, destDir, fileSystem);
            int sourceFilesAmount = fileSystem.Directory.EnumerateFiles(sourceDir.FullName, "*", SearchOption.AllDirectories).Count();
            int destFilesAmount = fileSystem.Directory.EnumerateFiles(destDir.FullName, "*", SearchOption.AllDirectories).Count();

            Assert.That(sourceFilesAmount == destFilesAmount);
        }

        [Test]
        public void CopyDirectoryContnet_UsualCase_CorrectAmountOfCopiedFilesReturned()
        {
            var sourceDir = fileSystem.DirectoryInfo.FromDirectoryName(@"C:\source1");
            var destDir = fileSystem.Directory.CreateDirectory(@"C:\Archive\source1_copy");
            
            (int success, _) = Copier.CopyDirectoryContent(sourceDir, destDir, fileSystem);
            int sourceFilesAmount = fileSystem.Directory.EnumerateFiles(destDir.FullName, "*", SearchOption.AllDirectories).Count();
            
            Assert.That(sourceFilesAmount == success);
        }

        [Test]
        public void CreateDirectoryWithTimeStamp_DirAlreadyExists_CreateDirWithSuffix()
        {
            var destDir = fileSystem.DirectoryInfo.FromDirectoryName(@"C:\Archive");
            fileSystem.AddDirectory(@"C:\Archive\test_");

            var d = Copier.CreateDirectoryWithTimeStamp(destDir, "test", "'_'", fileSystem);

            Assert.That(d.Name == "test_ (1)");
        }

        [Test]
        public void CreateDirectoryWithTimeStamp_NameIsTooLong_ThrowsPathTooLongException()
        {
            var longName = new string('1', 360);

            var destDir = fileSystem.Directory.CreateDirectory($"C:\\destDir");

            Assert.That(() => Copier.CreateDirectoryWithTimeStamp(destDir, longName, "'_'", fileSystem),
                Throws.TypeOf(typeof(PathTooLongException)));
        }

        [Test]
        public void CreateDirectoryWithTimeStamp_DestDirDoesntExist_CreatesDirectory()
        {
            var destDir = fileSystem.DirectoryInfo.FromDirectoryName(@"C:\fake");

            Assert.DoesNotThrow(() => Copier.CreateDirectoryWithTimeStamp(destDir, "test", "'_'", fileSystem));
            Assert.That(fileSystem.Directory.Exists(destDir.FullName));
        }

        [Test]
        public void CreateDirectoryWithTimeStamp_AllDirsAlreadyExist_ThrowsDuplicateNameException()
        {
            var destDir = fileSystem.DirectoryInfo.FromDirectoryName(@"C:\Archive");
            var path = @"C:\Archive\test_";
            fileSystem.AddDirectory(path);

            for (int i = 1; i <= 1000; i++)
                fileSystem.AddDirectory($"{path} ({i})");

            Assert.That(() => Copier.CreateDirectoryWithTimeStamp(destDir, "test", "'_'", fileSystem),
                Throws.TypeOf(typeof(DuplicateNameException)));
        }

        [Test]
        public void MakeCopies_SourceDirsListIsEmpty_NoCopiesMade()
        {
            var destDir = fileSystem.DirectoryInfo.FromDirectoryName(@"C:\Archive");
            var sourceDirs = new List<IDirectoryInfo>();

            Assert.DoesNotThrow(() => Copier.MakeCopies(sourceDirs, destDir, "HH-mm-ss", fileSystem));
            Assert.That(destDir.GetDirectories(), Is.Empty);
            Assert.That(destDir.GetFiles(), Is.Empty);
        }

        [Test]
        public void MakeCopies_UsualCase_AllDirsCopied()
        {
            var destDir = fileSystem.DirectoryInfo.FromDirectoryName(@"C:\Archive");
            var sourceDirs = new List<IDirectoryInfo>
            {
                fileSystem.DirectoryInfo.FromDirectoryName(@"C:\source1"),
                fileSystem.DirectoryInfo.FromDirectoryName(@"C:\source2"),
                fileSystem.DirectoryInfo.FromDirectoryName(@"C:\source3"),
            };

            Copier.MakeCopies(sourceDirs, destDir, "'_copy'", fileSystem);

            Assert.That(destDir.GetDirectories().Length == sourceDirs.Count);
            Assert.That(destDir.GetFiles(), Is.Empty);
        }

        [Test]
        public void MakeCopies_SourceListHasInvalidDirs_InvalidDirSkipped()
        {
            var destDir = fileSystem.DirectoryInfo.FromDirectoryName(@"C:\Archive");
            var sourceDirs = new List<IDirectoryInfo>
            {
                fileSystem.DirectoryInfo.FromDirectoryName(@"C:\source1"),
                fileSystem.DirectoryInfo.FromDirectoryName(@"C:\source2"),
                fileSystem.DirectoryInfo.FromDirectoryName(@"C:\source_invalid"),
            };

            Assert.DoesNotThrow(() => Copier.MakeCopies(sourceDirs, destDir, "'_copy'", fileSystem));
            Assert.That(destDir.GetDirectories().Length == sourceDirs.Count - 1);
        }

        [Test]
        public void MakeCopies_PathToNewDirectoryIsTooLong_SkipsThatDirectory()
        {
            var longName = new string('1', 252);
            var destDir = fileSystem.DirectoryInfo.FromDirectoryName(@"C:\Archive");
            var sourceDirs = new List<IDirectoryInfo>
            {
                fileSystem.Directory.CreateDirectory($"C:\\source1_copy\\{longName}"),
                fileSystem.Directory.CreateDirectory($"C:\\source2_copy\\short_name"),
                fileSystem.Directory.CreateDirectory($"C:\\source3_copy\\short_name2"),
                fileSystem.Directory.CreateDirectory($"C:\\source4_copy\\{longName}_2"),
            };

            var dict = Copier.MakeCopies(sourceDirs, destDir, "HH-mm-ss", fileSystem);

            Assert.That(dict.Count == 2);
        }
    }
}
