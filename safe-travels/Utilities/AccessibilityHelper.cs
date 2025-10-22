using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace safe_travels.Utilities
{
    public static class AccessibilityHelper
    {
        public static void ToggleLayout(View normalView, View accessibleView, bool isOn)
        {
            if (normalView == null || accessibleView == null) return;

            normalView.IsVisible = !isOn;
            accessibleView.IsVisible = isOn;

            SemanticScreenReader.Announce(isOn
                ? "Accessibility mode active. Simplified layout enabled."
                : "Accessibility mode off. Standard layout restored.");
        }
    }
}
