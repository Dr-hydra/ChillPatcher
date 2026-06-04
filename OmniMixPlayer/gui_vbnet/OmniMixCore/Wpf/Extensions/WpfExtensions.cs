using System.Windows.Controls;

namespace OmniMixCore.Wpf.Extensions;
public static class WpfExtensions {

    public static bool Any(this UIElementCollection? arr) 
        => arr?.Count > 0;

}
