using System;
using System.Collections.Generic;
namespace vietlabs.fr2
{
    partial class FR2_WindowAll
    {
        [Serializable] internal class PanelSettings
        {
            public bool selection;
            public bool horzLayout;
            public bool scene = true;
            public bool asset = true;
            public bool details;
            public bool bookmark;
            public bool toolMode;

            public bool showFullPath = true;
            public bool showFileSize;
            public bool showFileExtension;
            public bool showUsageType = true;
            public bool writeImportLog;
            public bool recursiveUnusedScan = true;

            public FR2_RefDrawer.Mode toolGroupMode = FR2_RefDrawer.Mode.Type;
            public FR2_RefDrawer.Mode groupMode = FR2_RefDrawer.Mode.Dependency;
            public FR2_RefDrawer.Sort sortMode = FR2_RefDrawer.Sort.Path;

            public int mainTabIndex = 0; // For main tabs (e.g. Uses/Used By/Addressables)
            public int toolTabIndex = 0; // For toolTabs (Duplicate/GUID/Unused/In Build/Others)
            public int othersTabIndex = 0; // For vertical tab bar in 'Others' section

            // Remember pixel sizes for fixed panels
            public float selectionPanelPixel = 200f;
            public float detailsPanelPixel = 150f;  
            public float bookmarkPanelPixel = 150f;
        }
    }
}
