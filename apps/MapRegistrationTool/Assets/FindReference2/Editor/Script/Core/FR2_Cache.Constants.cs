using System.Collections.Generic;

namespace vietlabs.fr2
{
    internal partial class FR2_Cache
    {
        internal const string DEFAULT_CACHE_PATH = "Assets/FR2_Cache.asset";
        

        internal static int cacheStamp;
        internal static System.Action onReady;

        // internal static bool _triedToLoadCache;
        // internal static FR2_Cache _cache;
        // internal static string _cacheGUID;
        // internal static bool _cacheJustCreated;
        // internal static string _cachePath;
        public static readonly int priority = 5;

        private static readonly HashSet<string> SPECIAL_USE_ASSETS = new HashSet<string>
        {
            "Assets/link.xml", // this file used to control build/link process do not remove
            "Assets/csc.rsp",
            "Assets/mcs.rsp",
            "Assets/GoogleService-Info.plist",
            "Assets/google-services.json"
        };

        private static readonly HashSet<string> SPECIAL_EXTENSIONS = new HashSet<string>
        {
            ".asmdef",
            ".cginc",
            ".cs",
            ".dll",
            ".mdb",
            ".pdb",
            ".rsp",
            ".md",
            ".winmd",
            ".xml",
            ".XML",
            ".tsv",
            ".csv",
            ".json",
            ".pdf",
            ".txt",
            ".giparams",
            ".wlt",
            ".preset",
            ".exr",
            ".aar",
            ".srcaar",
            ".pom",
            ".bin",
            ".html",
            ".chm",
            ".data",
            ".jsp",
            ".unitypackage"
        };

        [System.NonSerialized] internal static int delayCounter;
    }
} 