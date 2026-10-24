using System.Drawing;
using System.Windows.Forms;

namespace ChatMixOverlay;

// A real NotifyIcon-owned menu gets correct foreground, keyboard and dismissal behavior.
public sealed class TrayMenu : ContextMenuStrip
{
    private readonly Settings settings;
    public TrayMenu(Settings settings)
    {
        this.settings = settings;
        ShowImageMargin = false;
        Font = new Font("Segoe UI", 9.5f);
        Padding = new Padding(5);
        RenderMode = ToolStripRenderMode.Professional;
        Opening += (_, _) =>
        {
            var light = Appearance.IsLight(settings);
            BackColor = light ? Color.FromArgb(247, 247, 245) : Color.FromArgb(43, 43, 43);
            ForeColor = light ? Color.FromArgb(30, 32, 35) : Color.FromArgb(239, 239, 239);
            Renderer = new ToolStripProfessionalRenderer(new MenuColors(light));
            foreach (ToolStripItem item in Items) item.ForeColor = ForeColor;
        };
    }
    public void AddCommand(string text, Action action)
    {
        var item = new ToolStripMenuItem(text) { Padding = new Padding(10, 7, 18, 7) };
        item.Click += (_, _) => action(); Items.Add(item);
    }
    private sealed class MenuColors(bool light) : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => light ? Color.FromArgb(247, 247, 245) : Color.FromArgb(43, 43, 43);
        public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
        public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
        public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;
        public override Color MenuItemSelected => light ? Color.FromArgb(228, 234, 239) : Color.FromArgb(63, 67, 72);
        public override Color MenuItemBorder => MenuItemSelected;
        public override Color MenuBorder => light ? Color.FromArgb(212, 214, 216) : Color.FromArgb(67, 67, 67);
        public override Color SeparatorDark => MenuBorder;
        public override Color SeparatorLight => ToolStripDropDownBackground;
    }
}
