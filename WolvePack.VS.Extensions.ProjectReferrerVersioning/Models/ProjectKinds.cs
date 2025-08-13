using System.Text;
using System.Threading.Tasks;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Models
{
    public static class ProjectKinds
    {
        /// <summary>
        /// Solution folder project kind GUID.
        /// </summary>
        public const string SolutionFolder = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}";

        /// <summary>
        /// Classic C# project kind GUID (.NET Framework).
        /// </summary>
        public const string CSharpProject = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        
        /// <summary>
        /// SDK-style C# project kind GUID (.NET Core/.NET 5+).
        /// </summary>
        public const string CSharpSDKProject = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}";
    }
}
