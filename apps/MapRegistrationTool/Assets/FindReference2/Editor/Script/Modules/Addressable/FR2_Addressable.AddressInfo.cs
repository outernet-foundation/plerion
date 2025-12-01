using System;
using System.Collections.Generic;
namespace vietlabs.fr2
{
    public static partial class FR2_Addressable
    {
        [Serializable]
        public class AddressInfo
        {
            public string address;
            public string bundleGroup;
            public HashSet<string> assetGUIDs;
            public HashSet<string> childGUIDs;
        }
    }
}
