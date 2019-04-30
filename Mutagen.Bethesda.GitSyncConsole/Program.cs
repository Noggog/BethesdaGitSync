using Mutagen.Bethesda.GitSync;
using Mutagen.Bethesda.Oblivion;
using Noggog;
using Noggog.Utility;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Mutagen.Bethesda.GitSyncConsole
{
    class Program
    {
        enum Type { ToBinary, ToXML }

        static async Task Main(string[] args)
        {
            if (args.Length < 3
                || !Enum.TryParse<Type>(args[0], out var type))
            {
                WriteBadInputWarning();
                return;
            }

            string pathFrom = args[1];
            string pathTo = args[2];
            string backupPath = args.Length > 3 ? args[3] : "";

            var binaryPath = new FilePath(type == Type.ToBinary ? pathTo : pathFrom);
            var folder = new DirectoryPath(type == Type.ToBinary ? pathFrom : pathTo);

            if (!ModKey.TryFactory(binaryPath.Name, out var modKey))
            {
                System.Console.Error.WriteLine($"Could not create modKey from: {binaryPath}");
                return;
            }

            var instr = GitConversionInstructions.Oblivion(modKey);

            GitConversionUtility.Error error;
            switch (type)
            {
                case Type.ToBinary:
                    error = await GitConversionUtility.ConvertToBinary(
                        folder,
                        binaryPath,
                        instr,
                        backupFolder: backupPath);
                    break;
                case Type.ToXML:
                    error = await GitConversionUtility.ConvertToFolder(
                        binaryPath,
                        folder,
                        instr,
                        backupFolder: backupPath);
                    break;
                default:
                    throw new NotImplementedException();
            }

            switch (error)
            {
                case GitConversionUtility.Error.None:
                    return;
                case GitConversionUtility.Error.DidNotExist:
                    System.Console.Error.WriteLine($"Folder path did not exist: {folder.Path}");
                    System.Console.ReadKey();
                    return;
                case GitConversionUtility.Error.ModKey:
                    System.Console.Error.WriteLine($"Could not create modKey from: {binaryPath}");
                    System.Console.ReadKey();
                    return;
                case GitConversionUtility.Error.Corrupted:
                    System.Console.WriteLine("Export results did not match source. Force export? (Y/N):");
                    var input = System.Console.ReadKey();
                    if (input.KeyChar != 'Y') return;
                    break;
                default:
                    break;
            }

            // Force export
            switch (type)
            {
                case Type.ToBinary:
                    error = await GitConversionUtility.ConvertToBinary(
                        folder,
                        binaryPath,
                        instr,
                        checkCorrectness: false,
                        backupFolder: backupPath);
                    break;
                case Type.ToXML:
                    error = await GitConversionUtility.ConvertToFolder(
                        binaryPath,
                        folder,
                        instr,
                        checkCorrectness: false,
                        backupFolder: backupPath);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        static void WriteBadInputWarning()
        {
            System.Console.Error.WriteLine("Incorrect input.  Expected:");
            System.Console.Error.WriteLine("[From/To] [Binary/XML] Path1 Path2");
        }
    }
}
