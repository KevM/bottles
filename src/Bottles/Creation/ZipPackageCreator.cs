using System;
using System.Collections.Generic;
using System.IO;
using Bottles.Commands;
using Bottles.Diagnostics;
using Bottles.PackageLoaders.Assemblies;
using Bottles.Zipping;
using FubuCore;
using FubuCore.CommandLine;

namespace Bottles.Creation
{
    public class ZipPackageCreator
    {
        private readonly IFileSystem _fileSystem;
        private readonly IZipFileService _zipFileService;
        private readonly IBottleLogger _logger;
        private readonly IAssemblyFileFinder _assemblyFinder;

        public ZipPackageCreator(IFileSystem fileSystem, IZipFileService zipFileService, IBottleLogger logger, IAssemblyFileFinder assemblyFinder)
        {
            _fileSystem = fileSystem;
            _zipFileService = zipFileService;
            _logger = logger;
            _assemblyFinder = assemblyFinder;
        }

        public bool CreatePackage(CreateBottleInput input, PackageManifest manifest)
        {
            var binFolder = _fileSystem.FindBinaryDirectory(input.PackageFolder, input.TargetFlag);

            var assemblies = _assemblyFinder.FindAssemblies(binFolder, manifest.Assemblies);
            if (assemblies.Success)
            {
                writeZipFile(input, manifest, assemblies);
                return true;
            }

            _logger.WriteAssembliesNotFound(assemblies, manifest, input, binFolder);
            return false;
        }

        private void writeZipFile(CreateBottleInput input, PackageManifest manifest, AssemblyFiles assemblies)
        {
            var zipFileName = input.GetZipFileName(manifest);
            
            if (_fileSystem.FileExists(zipFileName))
            {
                LogWriter.Current.Highlight("    Deleting existing file at " + zipFileName);
                _fileSystem.DeleteFile(zipFileName);
            }

            _zipFileService.CreateZipFile(zipFileName, zipFile =>
            {
                assemblies.Files.Each(file =>
                {
                    zipFile.AddFile(file, "bin");
                });

                if (input.PdbFlag)
                {
                    assemblies.PdbFiles.Each(file =>
                    {
                        zipFile.AddFile(file, "bin");
                    });
                }

                WriteVersion(zipFile);

                zipFile.AddFile(FileSystem.Combine(input.PackageFolder, PackageManifest.FILE), "");

                AddDataFiles(input, zipFile, manifest);
                AddContentFiles(input, zipFile, manifest);
                AddConfigFiles(input, zipFile, manifest);
            });
        }

        public Guid WriteVersion(IZipFile zipFile)
        {
            var versionFile = Path.Combine(Path.GetTempPath(), BottleFiles.VersionFile);
            var guid = Guid.NewGuid();
            _fileSystem.WriteStringToFile(versionFile, guid.ToString());
            zipFile.AddFile(versionFile);

            return guid;
        }

        public void AddContentFiles(CreateBottleInput input, IZipFile zipFile, PackageManifest manifest)
        {
            if (manifest.ContentFileSet == null)
            {
                ConsoleWriter.Write("      No WebContent files");
                return;
            }

            ConsoleWriter.Write("      Adding WebContent folder for " + manifest.ContentFileSet);
            manifest.ContentFileSet.AppendExclude(FileSystem.Combine("bin","*.*"));

            zipFile.AddFiles(new ZipFolderRequest
                                 {
                                     FileSet = manifest.ContentFileSet,
                                     ZipDirectory = BottleFiles.WebContentFolder,
                                     RootDirectory = input.PackageFolder
                                 });
        }

        public void AddDataFiles(CreateBottleInput input, IZipFile zipFile, PackageManifest manifest)
        {
            zipFile.AddFiles(new ZipFolderRequest()
                             {
                                 FileSet = FileSet.Deep("*"),
                                 ZipDirectory = BottleFiles.DataFolder,
                                 RootDirectory = Path.Combine(input.PackageFolder, BottleFiles.DataFolder)
                             });
        }

        public void AddConfigFiles(CreateBottleInput input, IZipFile zipFile, PackageManifest manifest)
        {
            ConsoleWriter.Write("      Adding Config folder for " + BottleFiles.ConfigFiles);
            zipFile.AddFiles(new ZipFolderRequest(){
                FileSet = BottleFiles.ConfigFiles,
                RootDirectory = input.PackageFolder,
                ZipDirectory = BottleFiles.ConfigFolder
            });
        }

    }
}