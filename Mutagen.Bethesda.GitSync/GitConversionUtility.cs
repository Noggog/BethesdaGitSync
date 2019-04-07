using Noggog;
using Noggog.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mutagen.Bethesda.GitSync
{
    public class GitConversionUtility
    {
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
                if (!string.IsNullOrWhiteSpace(backupFolder?.Path)
                    && xmlFolderTargetPath.Exists)
                {
                    var backupTarget = new DirectoryPath(Path.Combine(backupFolder.Value.Path, DateTime.Now.ToString("MM-dd-yyyy HH-mm-ss")));
                    Directory.Move(xmlFolderTargetPath.Path, backupTarget.Path);
                    CleanBackups(backupFolder.Value.Path, numBackups);
                }
                else
                {
                    xmlFolderTargetPath.DeleteEntireFolder();
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
