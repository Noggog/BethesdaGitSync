using Noggog;
using Noggog.Utility;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mutagen.Bethesda.GitSync
{
    public class GitConversionUtility : IEnableLogger
    {
        private static GitConversionUtility Instance = new GitConversionUtility();
        public enum Error { None, DidNotExist, ModKey, Corrupted }

        public static async Task<Error> ConvertToBinary<M>(
            DirectoryPath xmlFolderPath,
            FilePath binaryTargetPath,
            GitConversionInstructions<M> instr,
            bool checkCorrectness = true,
            DirectoryPath? backupFolder = null,
            int numBackups = 10)
        {
            if (!xmlFolderPath.Exists)
            {
                return Error.DidNotExist;
            }

            if (!ModKey.TryFactory(binaryTargetPath.Name, out var modKey))
            {
                return Error.ModKey;
            }

            using (var tmpFolder = new TempFolder(deleteAfter: true))
            {
                var targetTempPath = new FilePath(Path.Combine(tmpFolder.Dir.Path, "MutagenGitTemp"));
                var xmlInMod = await instr.CreateXmlFolder(xmlFolderPath);
                await instr.WriteBinary(xmlInMod, targetTempPath.Path);
                if (checkCorrectness)
                {
                    var rexportPath = Path.Combine(tmpFolder.Dir.Path, "Reexport");
                    var reimport = await instr.CreateBinary(targetTempPath.Path);
                    await instr.WriteXmlFolder(reimport, new DirectoryPath(rexportPath));
                    if (!FileComparison.FoldersAreEqual(
                        xmlFolderPath,
                        new DirectoryPath(rexportPath)))
                    {
                        return Error.Corrupted;
                    }
                }
                if (binaryTargetPath.Exists)
                {
                    // Short circuit if everything already equal
                    if (FileComparison.FilesAreEqual(targetTempPath.Path, binaryTargetPath.Path))
                    {
                        return Error.None;
                    }
                    // Make backup
                    if (!string.IsNullOrWhiteSpace(backupFolder?.Path)
                        && backupFolder.Value.Exists)
                    {
                        var backupTarget = new DirectoryPath(Path.Combine(backupFolder.Value.Path, DateTime.Now.ToString("MM-dd-yyyy HH-mm-ss")));
                        backupTarget.Create();
                        File.Move(binaryTargetPath.Path, Path.Combine(backupTarget.Path, binaryTargetPath.Name));
                        CleanBackups(backupFolder.Value.Path, numBackups);
                    }
                    else
                    {
                        binaryTargetPath.Delete();
                    }
                }
                File.Move(targetTempPath.Path, binaryTargetPath.Path);
            }

            return Error.None;
        }

        public static async Task<Error> ConvertToFolder<M>(
            FilePath binaryPath,
            DirectoryPath xmlFolderTargetPath,
            GitConversionInstructions<M> instr,
            bool checkCorrectness = true,
            DirectoryPath? backupFolder = null,
            int numBackups = 10)
        {
            if (!binaryPath.Exists)
            {
                return Error.DidNotExist;
            }

            if (!ModKey.TryFactory(binaryPath.Name, out var modKey))
            {
                return Error.ModKey;
            }

            using (var tmpFolder = new TempFolder(deleteAfter: true))
            {
                var targetTempPath = new DirectoryPath(Path.Combine(tmpFolder.Dir.Path, "Export"));
                targetTempPath.Create();
                bool doingBackup = !string.IsNullOrWhiteSpace(backupFolder?.Path)
                    && xmlFolderTargetPath.Exists;

                void CopyFolder(string source_dir, string destination_dir)
                {
                    foreach (string dir in System.IO.Directory.GetDirectories(source_dir, "*", System.IO.SearchOption.AllDirectories))
                    {
                        try
                        {
                            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(destination_dir, dir.Substring(source_dir.Length + 1)));
                        }
                        catch (Exception ex)
                        {
                            Instance.Log().Warn("Exception creating directory during temp copy over: " + ex);
                        }
                    }

                    foreach (string file_name in System.IO.Directory.GetFiles(source_dir, "*", System.IO.SearchOption.AllDirectories))
                    {
                        var targetPath = System.IO.Path.Combine(destination_dir, file_name.Substring(source_dir.Length + 1));
                        try
                        {
                            System.IO.File.Copy(file_name, targetPath);
                        }
                        catch (Exception ex)
                        {
                            Instance.Log().Warn($"Exception copying {file_name} to {targetPath} during temp copy over: " + ex);
                        }
                    }
                }

                // Move existing XML to preserve date modified information, etc
                if (xmlFolderTargetPath.Exists)
                {
                    CopyFolder(xmlFolderTargetPath.Path, targetTempPath.Path);
                }

                // Write XML folder in temp location
                var binInMod = await instr.CreateBinary(binaryPath);
                await instr.WriteXmlFolder(binInMod, targetTempPath);

                if (checkCorrectness)
                {
                    var rexportPath = Path.Combine(tmpFolder.Dir.Path, "Reexport");
                    var reimport = await instr.CreateXmlFolder(targetTempPath);
                    await instr.WriteBinary(reimport, rexportPath);
                    if (!FileComparison.FilesAreEqual(
                        new FilePath(binaryPath.Path),
                        new FilePath(rexportPath)))
                    {
                        return Error.Corrupted;
                    }
                }

                if (xmlFolderTargetPath.Exists)
                {
                    // Short circuit if everything already equal
                    if (xmlFolderTargetPath.Exists
                        && FileComparison.FoldersAreEqual(targetTempPath.Path, xmlFolderTargetPath.Path))
                    {
                        return Error.None;
                    }

                    // Make backup
                    if (doingBackup)
                    {
                        var backupTarget = new DirectoryPath(Path.Combine(backupFolder.Value.Path, DateTime.Now.ToString("MM-dd-yyyy HH-mm-ss")));
                        backupTarget.Directory.Create();
                        Directory.Move(xmlFolderTargetPath.Path, backupTarget.Path);
                        CleanBackups(backupFolder.Value.Path, numBackups);
                    }
                    else
                    {
                        xmlFolderTargetPath.DeleteEntireFolder();
                    }
                }
                Directory.Move(targetTempPath.Path, xmlFolderTargetPath.Path);
            }

            return Error.None;
        }

        public static void CleanBackups(string path, int numBackups)
        {
            var backupDir = new DirectoryPath(path);
            foreach (var dir in backupDir.EnumerateDirectories(includeSelf: false, recursive: false)
                .OrderByDescending(d => d.Name)
                .Skip(numBackups)
                .ToArray())
            {
                dir.DeleteEntireFolder();
            }
        }
    }
}
