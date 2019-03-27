using Noggog;
using System;
using System.Collections.Generic;
using System.Text;

namespace Mutagen.Bethesda.GitSync
{
    public class GitConversionInstructions<M>
    {
        public Func<FilePath, M> CreateBinary;
        public Func<DirectoryPath, M> CreateXmlFolder;
        public Action<M, FilePath> WriteBinary;
        public Action<M, DirectoryPath> WriteXmlFolder;
    }
}
