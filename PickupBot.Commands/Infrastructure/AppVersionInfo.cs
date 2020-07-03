using System.Linq;
using System.Reflection;

namespace PickupBot.Commands.Infrastructure
{
    public class AppVersionInfo
    {
        private string _gitHash;
        private string _gitShortHash;

        public string GitHash
        {
            get
            {
                if (string.IsNullOrEmpty(_gitHash))
                {
                    var version = "1.0.0+LOCALBUILD"; // Dummy version for local dev
                    var appAssembly = typeof(AppVersionInfo).Assembly;
                    var infoVerAttr = (AssemblyInformationalVersionAttribute)appAssembly
                        .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute)).FirstOrDefault();

                    if (infoVerAttr.InformationalVersion.Length > 6)
                    {
                        // Hash is embedded in the version after a '+' symbol, e.g. 1.0.0+a34a913742f8845d3da5309b7b17242222d41a21
                        version = infoVerAttr.InformationalVersion;
                    }
                    _gitHash = version.Substring(version.IndexOf('+') + 1);

                }

                return _gitHash;
            }
        }

        public string ShortGitHash
        {
            get
            {
                if (string.IsNullOrEmpty(_gitShortHash))
                {
                    _gitShortHash = GitHash.Substring(GitHash.Length - 6, 6);
                }
                return _gitShortHash;
            }
        }
    }
}
