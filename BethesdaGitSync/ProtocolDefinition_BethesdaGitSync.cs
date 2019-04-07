using Loqui;
using BethesdaGitSync;

namespace Loqui
{
    public class ProtocolDefinition_BethesdaGitSync : IProtocolRegistration
    {
        public readonly static ProtocolKey ProtocolKey = new ProtocolKey("BethesdaGitSync");
        public void Register()
        {
            LoquiRegistration.Register(BethesdaGitSync.Internals.Settings_Registration.Instance);
            LoquiRegistration.Register(BethesdaGitSync.Internals.Mapping_Registration.Instance);
        }
    }
}
