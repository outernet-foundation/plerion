using System.Collections.Generic;
namespace vietlabs.fr2
{
    internal class FR2_AssetGroup
    {
        public string name;
        public HashSet<string> extension;
        public FR2_AssetGroup(string name, params string[] exts)
        {
            this.name = name;
            extension = new HashSet<string>();
            for (var i = 0; i < exts.Length; i++)
            {
                extension.Add(exts[i]);
            }
        }
    }
}
