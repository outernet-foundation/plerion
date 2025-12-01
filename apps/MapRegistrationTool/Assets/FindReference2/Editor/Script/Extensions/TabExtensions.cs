using System;

namespace vietlabs.fr2
{
    internal static class TabExtensions
    {
        internal static bool IsFocusing(this FR2_TabView tabView, int index)
        {
            return tabView != null && tabView.current == index;
        }

        internal static bool IsFocusingAny(this FR2_TabView tabView, params int[] indices)
        {
            if (tabView == null) return false;
            foreach (int index in indices)
            {
                if (tabView.current == index) return true;
            }
            return false;
        }
    }
} 