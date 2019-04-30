using Mutagen.Bethesda.Oblivion;
using Noggog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Mutagen.Bethesda.GitSync
{
    public class GitConversionInstructions<M>
    {
        public Func<FilePath, Task<M>> CreateBinary;
        public Func<DirectoryPath, Task<M>> CreateXmlFolder;
        public Func<M, FilePath, Task> WriteBinary;
        public Func<M, DirectoryPath, Task> WriteXmlFolder;
    }

    public static class GitConversionInstructions
    {
        public static GitConversionInstructions<OblivionMod> Oblivion(ModKey modKey) => new GitConversionInstructions<OblivionMod>()
        {
            CreateBinary = async (f) => OblivionMod.Create_Binary(f.Path, modKey),
            CreateXmlFolder = async (f) => await OblivionMod.Create_Xml_Folder(f.Path, modKey),
            WriteBinary = async (m, f) => m.Write_Binary(f.Path, modKey),
            WriteXmlFolder = async (m, f) => await m.Write_XmlFolder(f.Path)
        };
    }
}
