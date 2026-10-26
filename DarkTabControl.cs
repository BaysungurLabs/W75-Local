using System;
using System.Drawing;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Sekme çubuğunun sağında kalan boş alanı da uygulamanın koyu rengine boyayan TabControl.
    // Sekme başlıkları DrawItem olayıyla (Tabs_DrawItem) çizilmeye devam eder.
    public class DarkTabControl : TabControl
    {
        public Color StripColor = Theme.Bg;

        public DarkTabControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            pevent.Graphics.Clear(StripColor);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(StripColor);
            for (int i = 0; i < TabCount; i++)
            {
                DrawItemState st = i == SelectedIndex ? DrawItemState.Selected : DrawItemState.None;
                OnDrawItem(new DrawItemEventArgs(e.Graphics, Font, GetTabRect(i), i, st));
            }
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            Invalidate();
        }
    }
}
