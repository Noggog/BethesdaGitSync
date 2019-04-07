using Loqui;
using Loqui.Generation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mutagen.Bethesda.GitSync.Generator
{
    class Program
    {
        static void Main(string[] args)
        {
            LoquiGenerator gen = new LoquiGenerator(typical: true)
            {
                RaisePropertyChangedDefault = false,
                NotifyingDefault = NotifyingType.ReactiveUI,
                ObjectCentralizedDefault = true,
                HasBeenSetDefault = false,
                ToStringDefault = false,
            };
            var proto = gen.AddProtocol(
                new ProtocolGeneration(
                    gen,
                    new ProtocolKey("BethesdaGitSync"),
                    new DirectoryInfo("../../../BethesdaGitSync"))
                {
                    DefaultNamespace = "BethesdaGitSync",
                });
            proto.AddProjectToModify(
                new FileInfo(Path.Combine(proto.GenerationFolder.FullName, "BethesdaGitSync.csproj")));
            gen.Generate().Wait();
        }
    }
}
