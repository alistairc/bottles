using System;
using System.IO;
using AssemblyPackage;
using Bottles.Diagnostics;
using Bottles.Exploding;
using Bottles.Zipping;
using FubuCore;
using FubuCore.CommandLine;
using FubuTestingSupport;
using NUnit.Framework;
using Rhino.Mocks;
using System.Linq;
using System.Collections.Generic;

namespace Bottles.Tests.Exploding
{
    [TestFixture]
    public class integration_test_of_exploding_an_assembly_package
    {
        private BottleFiles theFiles;

        [SetUp]
        public void SetUp()
        {
            var fileSystem = new FileSystem();
            fileSystem.DeleteDirectory("app1");
            var exploder = new BottleExploder(new ZipFileService(fileSystem),
                                               new BottleExploderLogger(s => ConsoleWriter.Write(s)), fileSystem);

            var thePackage = new BottleInfo(new PackageManifest());

            theFiles = (BottleFiles)thePackage.Files;
            exploder.ExplodeAssembly("app1", typeof(AssemblyBottleMarker).Assembly, thePackage);
        }

        [Test]
        public void can_retrieve_data_from_package()
        {
            var text = "not the right thing";
            theFiles.GetFiles(WellKnownFiles.DataFolder, "1.txt", (name, data) =>
            {
                name.ShouldEqual("1.txt");
                text = new StreamReader(data).ReadToEnd();
            });

            // The text of this file in the AssemblyPackage data is just "1"
            text.ShouldEqual("1");
        }

        [Test]
        public void can_retrieve_web_content_folder_from_package()
        {
            var expected = "not this";
            theFiles.ForFolder(WellKnownFiles.WebContentFolder, folder =>
            {
                expected = folder;
            });

            expected.ShouldEqual(FileSystem.Combine("app1", "content", "AssemblyPackage", "WebContent").ToFullPath());
        }
    }


    [TestFixture]
    public class when_extracting_package_zip_files : BottleExploderContext
    {
        [Test]
        public void is_package_zip_positive()
        {
            WellKnownFiles.IsEmbeddedPackageZipFile("a.b.c.pak-webcontent.zip").ShouldBeTrue();
        }

        [Test]
        public void is_package_zip_negative_1()
        {
            WellKnownFiles.IsEmbeddedPackageZipFile("a.b.c.pak-webcontent.txt").ShouldBeFalse();
        }

        [Test]
        public void is_package_zip_negative_2()
        {
            WellKnownFiles.IsEmbeddedPackageZipFile("a.b.c.webcontent.zip").ShouldBeFalse();
        }

        [Test]
        public void get_package_folder_name()
        {
            WellKnownFiles.EmbeddedPackageFolderName("a.b.c.pak-webcontent.zip").ShouldEndWith("webcontent");
            WellKnownFiles.EmbeddedPackageFolderName("a.b.c.pak-data.zip").ShouldEndWith("data");
        }
    }

    [TestFixture]
    public class when_cleaning_all_the_packages : BottleExploderContext
    {
        protected override void beforeEach()
        {
            thePackagesAlreadyExplodedAre("pak1", "pak2", "pak3");
            ClassUnderTest.CleanAll(theApplicationDirectory);
        }

        [Test]
        public void should_clean_all_existing_package_folders()
        {
            assertPackageFolderWasDeleted("pak1");
            assertPackageFolderWasDeleted("pak2");
            assertPackageFolderWasDeleted("pak3");
        }
    }

    [TestFixture]
    public class when_reading_the_version_from_a_package_folder : BottleExploderContext
    {
        [Test]
        public void the_version_file_exists()
        {
            var guid = Guid.NewGuid();
            theExistingVersionIs("pak1", guid);

            string directory = WellKnownFiles.GetDirectoryForExplodedPackage(theApplicationDirectory, "pak1");

            ClassUnderTest.ReadVersion(directory).ShouldEqual(guid);
        }

        [Test]
        public void the_version_file_does_not_exist_so_return_empty()
        {
            theExistingVersionDoesNotExist("pak1");
            string directory = FileSystem.Combine(theApplicationDirectory, "bin", WellKnownFiles.PackagesFolder,
                                      "pak1");

            ClassUnderTest.ReadVersion(directory).ShouldEqual(Guid.Empty);
        }
    }

    [TestFixture]
    public class explode_all_with_zip_files_and_no_unzipped_directories : BottleExploderContext
    {
        protected override void beforeEach()
        {
            theZipFilesAre("pak1", "pak2", "pak3");

            // No packages are already exploded
            thePackagesAlreadyExplodedAre();

            ClassUnderTest.ExplodeAllZipsAndReturnPackageDirectories(theApplicationDirectory, new BottleLog());
        }

        [Test]
        public void should_have_exploded_all_three_packages()
        {
            assertZipFileWasExploded("pak1");
            assertZipFileWasExploded("pak2");
            assertZipFileWasExploded("pak3");
        }
    }

    [TestFixture]
    public class explode_a_zip_file_if_the_version_is_different_than_the_existing_package_folder : BottleExploderContext
    {
        private Guid zipGuid;
        private Guid folderGuid;

        protected override void beforeEach()
        {
            theZipFilesAre("pak1");
            zipGuid = Guid.NewGuid();
            folderGuid = Guid.NewGuid();
            thePackagesAlreadyExplodedAre("pak1");
        
            theExistingVersionIs("pak1", folderGuid);
            theZipVersionIs("pak1", zipGuid);

            ClassUnderTest.ExplodeAllZipsAndReturnPackageDirectories(theApplicationDirectory, new BottleLog());
        }

        [Test]
        public void should_extract_the_zip_file()
        {
            assertZipFileWasExploded("pak1");
        }

    }


    [TestFixture]
    public class explode_a_zip_file_if_the_version_is_missing_from_the_existing_package_folder : BottleExploderContext
    {
        private Guid zipGuid;

        protected override void beforeEach()
        {
            theZipFilesAre("pak1");
            zipGuid = Guid.NewGuid();
            thePackagesAlreadyExplodedAre("pak1");

            theExistingVersionDoesNotExist("pak1");
            theZipVersionIs("pak1", zipGuid);

            ClassUnderTest.ExplodeAllZipsAndReturnPackageDirectories(theApplicationDirectory, new BottleLog());
        }

        [Test]
        public void should_extract_the_zip_file()
        {
            assertZipFileWasExploded("pak1");
        }

    }

    [TestFixture]
    public class do_not_explode_a_zip_file_if_the_version_is_the_same_as_the_existing_folder : BottleExploderContext
    {
        private Guid theSameGuid;

        protected override void beforeEach()
        {
            theZipFilesAre("pak1");
            theSameGuid = Guid.NewGuid();
            thePackagesAlreadyExplodedAre("pak1");

            theExistingVersionIs("pak1", theSameGuid);
            theZipVersionIs("pak1", theSameGuid);

            ClassUnderTest.ExplodeAllZipsAndReturnPackageDirectories(theApplicationDirectory, new BottleLog());
        }

        [Test]
        public void should_NOT_extract_the_zip_file()
        {
            assertZipFileWasNotExploded("pak1");
        }
    }


    public abstract class BottleExploderContext : InteractionContext<BottleExploder>
    {
        protected readonly string theApplicationDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app");

        protected void assertZipFileWasExploded(string packageName)
        {
            var fileName = FileSystem.Combine(theApplicationDirectory, "bin", WellKnownFiles.PackagesFolder,
                                              packageName + ".zip");

            var directoryName = WellKnownFiles.GetDirectoryForExplodedPackage(theApplicationDirectory, packageName);

            MockFor<IZipFileService>().AssertWasCalled(x => x.ExtractTo(fileName, directoryName, ExplodeOptions.DeleteDestination));
            
        }

        protected void assertZipFileWasNotExploded(string packageName)
        {
            var fileName = FileSystem.Combine(theApplicationDirectory, "bin", WellKnownFiles.PackagesFolder,
                                              packageName + ".zip");

            var directoryName = WellKnownFiles.GetDirectoryForExplodedPackage(theApplicationDirectory, packageName);

            MockFor<IZipFileService>().AssertWasNotCalled(x => x.ExtractTo(fileName, directoryName, ExplodeOptions.DeleteDestination));

        }

        protected void theZipFilesAre(params string[] packageNames)
        {
            var zipFiles =
                packageNames.Select(
                    x =>
                    FileSystem.Combine(theApplicationDirectory, "bin", WellKnownFiles.PackagesFolder, x + ".zip"));

            var directory = WellKnownFiles.GetApplicationPackagesDirectory(theApplicationDirectory);

            MockFor<IFileSystem>().Stub(x => x.FileNamesFor(new FileSet(){
                Include = "*.zip"
            }, directory)).Return(zipFiles);
        }

        protected void theZipVersionIs(string packageName, Guid version)
        {
            var file = FileSystem.Combine(theApplicationDirectory, "bin", WellKnownFiles.PackagesFolder, packageName + ".zip");
            MockFor<IZipFileService>().Stub(x => x.GetVersion(file)).Return(version.ToString());
        }

        protected void thePackagesAlreadyExplodedAre(params string[] packageNames)
        {
            var directories = packageNames.Select(x =>
            {
                return WellKnownFiles.GetDirectoryForExplodedPackage(theApplicationDirectory, x);
            });

            directories.Each(dir =>
            {
                MockFor<IFileSystem>().Stub(x => x.DirectoryExists(dir)).Return(true);
            });

            MockFor<IFileSystem>().Stub(x => x.ChildDirectoriesFor(WellKnownFiles.GetExplodedPackagesDirectory(theApplicationDirectory)))
                .Return(directories);

            MockFor<IFileSystem>().Stub(x => x.ChildDirectoriesFor(WellKnownFiles.GetApplicationPackagesDirectory(theApplicationDirectory)))
                .Return(new string[0]);
        }

        protected void assertPackageFolderWasDeleted(string packageName)
        {
            var packageDirectory = WellKnownFiles.GetDirectoryForExplodedPackage(theApplicationDirectory, packageName);
            MockFor<IFileSystem>().AssertWasCalled(x => x.DeleteDirectory(packageDirectory));
        }

        protected void assertPackageFolderWasNotDeleted(string packageName)
        {
            var packageDirectory = FileSystem.Combine(theApplicationDirectory, "bin", WellKnownFiles.PackagesFolder,
                                                      packageName);
            MockFor<IFileSystem>().AssertWasNotCalled(x => x.DeleteDirectory(packageDirectory));
        }

        protected void theExistingVersionIs(string packageName, Guid guid)
        {

            string directory = WellKnownFiles.GetDirectoryForExplodedPackage(theApplicationDirectory, packageName);

            MockFor<IFileSystem>().Stub(x => x.FileExists(directory, WellKnownFiles.VersionFile)).Return(true);
            MockFor<IFileSystem>().Stub(x => x.ReadStringFromFile(directory, WellKnownFiles.VersionFile)).Return(guid.ToString());
        }

        protected void theExistingVersionDoesNotExist(string packageName)
        {
            var pathParts = new string[]{theApplicationDirectory, "bin", WellKnownFiles.PackagesFolder, packageName,
                             WellKnownFiles.VersionFile};

            
            MockFor<IFileSystem>().Stub(x => x.FileExists(pathParts)).Return(false);
        }


    }
}