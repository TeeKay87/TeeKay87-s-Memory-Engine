using System;
using System.Windows.Controls;

namespace TeeKay87.MemoryEngine.App.Controls;

internal static class ContextMenuUtilities
{
    public static void SetItemEnabled(
        ContextMenu? contextMenu,
        string tag,
        bool isEnabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        if (contextMenu is null)
        {
            return;
        }

        foreach (object item in contextMenu.Items)
        {
            if (item is MenuItem menuItem &&
                menuItem.Tag is string itemTag &&
                string.Equals(itemTag, tag, StringComparison.Ordinal))
            {
                menuItem.IsEnabled = isEnabled;
                return;
            }
        }
    }
}
