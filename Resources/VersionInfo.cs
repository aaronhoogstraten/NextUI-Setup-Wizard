using System.Reflection;
using System.Linq;

namespace NextUI_Setup_Wizard.Resources
{
    public static class VersionInfo
    {
        /// <summary>
        /// Gets the commit hash that was set during build time
        /// </summary>
        public static string CommitHash
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                
                // Try to get from assembly metadata first (set during build)
                var commitHashAttribute = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => a.Key == "CommitHash");
                
                if (commitHashAttribute?.Value != null)
                {
                    return commitHashAttribute.Value;
                }
                
                return "dev";
            }
        }
        
        public static string VersionString => $"{CommitHash}";
    }
}