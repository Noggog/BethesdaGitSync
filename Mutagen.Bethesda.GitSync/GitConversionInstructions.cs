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
}
