using UnityEngine;

namespace vietlabs.fr2
{
    partial class FR2_Theme
    {
        private static FR2_Theme CreateLightTheme()
        {
            return new FR2_Theme
            {
                SelectionHighlight = new Color(0.2275f, 0.4471f, 0.6902f, 1f), // Unity's light theme selection
                SelectionHighlightInactive = new Color(0.6824f, 0.6824f, 0.6824f, 1f),
                ErrorColor = new Color(0.3529f, 0.0f, 0.0f, 1f),
                WarningColor = new Color(0.2f, 0.2f, 0.0314f, 1f),
                InfoColor = new Color(0.2980f, 0.4941f, 1.0f, 1f),
                SuccessColor = new Color(0.0f, 0.8f, 0.0f, 1f),
                SceneHighlight = Color.blue,
                SeparatorLine = new Color(0.6f, 0.6f, 0.6f, 1f),
                DirtyIndicator = new Color(0.8f, 0.7f, 0.2f, 1f),
                SecondaryTextColor = Color.white * 0.5f, // Gray text for paths and extensions in light theme
                IconColor = new Color(0.6f, 0.6f, 0.6f, 1f), // Unity-like folder gray for light theme
                IconActiveColor = new Color(0.4f, 0.4f, 0.4f, 1f) // Consistent with Unity's folder selection color
            };
        }
        
    }
}