using System.Windows.Forms;

namespace A3Tools.Plugins.Default.Forms;

public static class FormHotkeyHelper
{
    public static void Setup(Form form, Action? onEnter = null)
    {
        form.KeyPreview = true;
        form.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape) { form.Close(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Enter) { onEnter?.Invoke(); e.SuppressKeyPress = true; }
        };
    }
}
