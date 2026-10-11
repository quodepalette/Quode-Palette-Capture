using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuodePaletteCapture
{
    /// <summary>Central dark color palette, used everywhere in the UI instead of scattering literals.</summary>
    internal static class AppTheme
    {
        public static readonly Color BgWindow = Color.FromArgb(18, 19, 23);
        public static readonly Color BgPanel = Color.FromArgb(26, 27, 32);
        public static readonly Color BgControl = Color.FromArgb(33, 34, 40);
        public static readonly Color BgControlHover = Color.FromArgb(41, 42, 49);
        public static readonly Color Border = Color.FromArgb(54, 56, 64);
        public static readonly Color TextPrimary = Color.FromArgb(236, 237, 240);
        public static readonly Color TextSecondary = Color.FromArgb(150, 152, 163);
        public static readonly Color TextMuted = Color.FromArgb(104, 106, 117);
        public static readonly Color Accent = Color.FromArgb(0, 181, 100);
        public static readonly Color AccentText = Color.White;

        /// <summary>
        /// Enables the Windows immersive dark mode for a form's title bar (Windows 10 20H1
        /// and later, and Windows 11). It does nothing on older versions. This is purely
        /// cosmetic and must never be allowed to break startup.
        /// </summary>
        public static void ApplyDarkTitleBar(Form form)
        {
            try
            {
                form.HandleCreated += (s, e) =>
                {
                    int useDark = 1;
                    // Attribute 20 applies to Windows 10 20H1 (build 19041) and later,
                    // including Windows 11. Attribute 19 covers a small range of earlier
                    // Windows 10 preview builds. The modern attribute is tried first.
                    int hr = DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
                    if (hr != 0)
                    {
                        DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref useDark, sizeof(int));
                    }
                };
            }
            catch
            {
                // Cosmetic only, so this is never treated as fatal.
            }
        }

        /// <summary>
        /// Opts the process into the Windows dark common-control mode where supported, so
        /// built-in chrome such as scrollbars and some dialog elements leans dark. This is a
        /// best-effort, cosmetic call to an unofficial but long-stable uxtheme.dll entry
        /// point. If it is unavailable, the app behaves the same.
        /// </summary>
        public static void TryEnableAppDarkMode()
        {
            try
            {
                SetPreferredAppMode(1); // 1 = AllowDark
            }
            catch
            {
                // Not available on this Windows version.
            }
        }

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("uxtheme.dll", EntryPoint = "#135")]
        private static extern int SetPreferredAppMode(int preferredAppMode);
    }

    /// <summary>A Button that keeps drawing its text in ForeColor when disabled, instead
    /// of the default grayed-out WinForms disabled color.</summary>
    internal sealed class ThemedButton : Button
    {
        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            if (!Enabled)
            {
                TextRenderer.DrawText(
                    pevent.Graphics,
                    Text,
                    Font,
                    ClientRectangle,
                    ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }
    }

    /// <summary>A checkbox that draws its own small box (accent green fill with a white
    /// checkmark when checked) next to a label. It is built on plain
    /// <see cref="Control"/> instead of <see cref="CheckBox"/> or <see cref="ButtonBase"/>.
    /// The flat-style rendering of the built-in controls fills the entire control with the
    /// accent color once checked and adds hover and press effects, so the checkbox ends up
    /// looking like a button. Building on Control avoids that, and only the small box and
    /// its label are drawn, with no hover state.</summary>
    internal sealed class ThemedCheckBox : Control
    {
        private const int BoxSize = 16;
        private const int TextGap = 8;

        private bool _checked;
        private bool _autoSize;

        public event EventHandler? CheckedChanged;

        public ThemedCheckBox()
        {
            // ControlStyles.OptimizedDoubleBuffer is deliberately not set. Controls that
            // opt into it share one reusable BufferedGraphicsContext bitmap, and when
            // several owner-drawn controls of different sizes use that shared buffer, one
            // control can end up drawing another control's leftover pixels. UserPaint and
            // AllPaintingInWmPaint, together with a solid background painted on every
            // OnPaint, avoid flicker without using the shared buffer.
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            TabStop = true;
            Cursor = Cursors.Default;
            Size = new Size(200, BoxSize);
        }

        /// <summary>Whether the box is checked. Mirrors CheckBox.Checked so existing call
        /// sites that read or set this property keep working.</summary>
        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked == value)
                {
                    return;
                }
                _checked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Mirrors the public AutoSize of CheckBox and Label so it can be set in an
        /// object initializer. The AutoSize on Control itself is protected.</summary>
        public new bool AutoSize
        {
            get => _autoSize;
            set
            {
                _autoSize = value;
                if (_autoSize)
                {
                    ApplyAutoSize();
                }
            }
        }

        public override string? Text
        {
            get => base.Text;
            set
            {
                base.Text = value;
                if (_autoSize)
                {
                    ApplyAutoSize();
                }
            }
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (_autoSize)
            {
                ApplyAutoSize();
            }
        }

        private void ApplyAutoSize()
        {
            Size textSize = TextRenderer.MeasureText(Text ?? string.Empty, Font ?? DefaultFont);
            Size = new Size(BoxSize + TextGap + textSize.Width, Math.Max(BoxSize, textSize.Height));
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (Enabled)
            {
                Checked = !Checked;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (Enabled && e.KeyCode == Keys.Space)
            {
                Checked = !Checked;
                e.Handled = true;
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Space)
            {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Intentionally empty. OnPaint paints the whole background itself. Calling the
            // base implementation, or using a transparent BackColor, can let stale pixels
            // from earlier drawing show through around an owner-drawn control.
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;

            using (var bgBrush = new SolidBrush(Parent?.BackColor ?? AppTheme.BgWindow))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int boxTop = (ClientRectangle.Height - BoxSize) / 2;
            var boxRect = new Rectangle(0, boxTop, BoxSize, BoxSize);

            if (Checked)
            {
                using (var fillBrush = new SolidBrush(AppTheme.Accent))
                {
                    g.FillRectangle(fillBrush, boxRect);
                }

                using (var checkPen = new Pen(Color.White, 2f)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round,
                    LineJoin = System.Drawing.Drawing2D.LineJoin.Round
                })
                {
                    g.DrawLines(checkPen, new[]
                    {
                        new Point(boxRect.Left + 4, boxRect.Top + 8),
                        new Point(boxRect.Left + 7, boxRect.Top + 11),
                        new Point(boxRect.Left + 12, boxRect.Top + 4)
                    });
                }
            }
            else
            {
                using (var borderPen = new Pen(AppTheme.Border, 1.5f))
                {
                    g.DrawRectangle(borderPen, boxRect.X, boxRect.Y, boxRect.Width - 1, boxRect.Height - 1);
                }
            }

            var textRect = new Rectangle(BoxSize + TextGap, 0, ClientRectangle.Width - BoxSize - TextGap, ClientRectangle.Height);
            Color textColor = Enabled ? ForeColor : AppTheme.TextMuted;
            TextRenderer.DrawText(g, Text, Font, textRect, textColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
    }

    /// <summary>Dark color table for the tray icon's right-click context menu.</summary>
    internal sealed class DarkMenuColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => AppTheme.BgControlHover;
        public override Color MenuItemSelectedGradientBegin => AppTheme.BgControlHover;
        public override Color MenuItemSelectedGradientEnd => AppTheme.BgControlHover;
        public override Color MenuItemBorder => AppTheme.Accent;
        public override Color MenuBorder => AppTheme.Border;
        public override Color ToolStripDropDownBackground => AppTheme.BgPanel;
        public override Color ImageMarginGradientBegin => AppTheme.BgPanel;
        public override Color ImageMarginGradientMiddle => AppTheme.BgPanel;
        public override Color ImageMarginGradientEnd => AppTheme.BgPanel;
        public override Color SeparatorDark => AppTheme.Border;
        public override Color SeparatorLight => AppTheme.Border;
        public override Color MenuItemPressedGradientBegin => AppTheme.BgControlHover;
        public override Color MenuItemPressedGradientEnd => AppTheme.BgControlHover;
    }
}
