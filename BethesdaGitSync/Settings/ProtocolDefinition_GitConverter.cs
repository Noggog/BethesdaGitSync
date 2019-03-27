using Loqui;
using BethesdaGitSync;

namespace Loqui
{
    public class ProtocolDefinition_GitConverter : IProtocolRegistration
    {
        public readonly static ProtocolKey ProtocolKey = new ProtocolKey("GitConverter");
        public void Register()
        {
            LoquiRegistration.Register(BethesdaGitSync.Internals.Settings_Registration.Instance);
            LoquiRegistration.Register(BethesdaGitSync.Internals.Mapping_Registration.Instance);
        }
    }
}
