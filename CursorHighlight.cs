using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuodePaletteCapture
{
    /// <summary>
    /// A small, click-through, always-on-top window that draws a translucent blue ring
    /// around the mouse pointer so its position is easy to follow in a recording.
    ///
    /// The ring is drawn once into a per-pixel-alpha layered window. After that, the only
    /// ongoing work is reading the cursor position on a short timer and moving the window
    /// when the pointer has actually moved, so the cost while recording is negligible.
    /// </summary>
    internal sealed class CursorHighlight : Form
    {
        // Accent blue for the highlight. Kept separate from the green UI accent on purpose.
        private static readonly Color RingBlue = Color.FromArgb(59, 130, 246);

        private const int BaseSize = 64;
        private const int TimerIntervalMs = 8;

        private readonly int _size;
        private readonly System.Windows.Forms.Timer _timer;
        private Point _lastCursor = new(int.MinValue, int.MinValue);

        public CursorHighlight(int deviceDpi)
        {
            _size = Math.Max(32, (int)Math.Round(BaseSize * deviceDpi / 96.0));

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            Size = new Size(_size, _size);

            _timer = new System.Windows.Forms.Timer { Interval = TimerIntervalMs };
            _timer.Tick += (s, e) => FollowCursor();
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // Layered for per-pixel alpha, transparent so mouse clicks pass through to
                // whatever is underneath, no-activate and tool-window so it never takes focus
                // or appears in Alt+Tab.
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        /// <summary>Starts following the pointer and makes the highlight visible.</summary>
        public void Begin()
        {
            // Force the native handle to exist so the layered bitmap can be applied
            // before the window becomes visible.
            _ = Handle;

            Point cursor = Cursor.Position;
            RenderBitmap(new Point(cursor.X - _size / 2, cursor.Y - _size / 2));
            _lastCursor = cursor;

            if (!Visible)
            {
                Show();
            }

            _timer.Start();
        }

        /// <summary>Stops following the pointer and hides the highlight.</summary>
        public void End()
        {
            _timer.Stop();
            if (Visible)
            {
                Hide();
            }
        }

        private void FollowCursor()
        {
            Point cursor = Cursor.Position;
            if (cursor == _lastCursor)
            {
                return;
            }

            _lastCursor = cursor;
            SetWindowPos(
                Handle,
                HWND_TOPMOST,
                cursor.X - _size / 2,
                cursor.Y - _size / 2,
                0,
                0,
                SWP_NOSIZE | SWP_NOACTIVATE);
        }

        /// <summary>Draws the ring once and hands it to the layered window.</summary>
        private void RenderBitmap(Point topLeft)
        {
            using var bitmap = new Bitmap(_size, _size, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                float stroke = Math.Max(2f, _size / 24f);
                float inset = stroke;
                var rect = new RectangleF(inset, inset, _size - inset * 2, _size - inset * 2);

                using var fill = new SolidBrush(Color.FromArgb(56, RingBlue));
                using var ring = new Pen(Color.FromArgb(215, RingBlue), stroke);
                g.FillEllipse(fill, rect);
                g.DrawEllipse(ring, rect);
            }

            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                oldBitmap = SelectObject(memDc, hBitmap);

                var size = new NativeSize { cx = _size, cy = _size };
                var source = new NativePoint { x = 0, y = 0 };
                var destination = new NativePoint { x = topLeft.X, y = topLeft.Y };
                var blend = new BlendFunction
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AC_SRC_ALPHA
                };

                UpdateLayeredWindow(Handle, screenDc, ref destination, ref size, memDc, ref source, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero)
                {
                    SelectObject(memDc, oldBitmap);
                }
                if (hBitmap != IntPtr.Zero)
                {
                    DeleteObject(hBitmap);
                }
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        protected override void WndProc(ref Message m)
        {
            // The highlight has a fixed pixel size. Ignoring DPI-change messages stops
            // WinForms from resizing it when the pointer crosses to a monitor with a
            // different scale, which would no longer match the bitmap.
            if (m.Msg == WM_DPICHANGED)
            {
                return;
            }

            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
            }
            base.Dispose(disposing);
        }

        // ---- Win32 interop ----

        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WM_DPICHANGED = 0x02E0;

        private const int ULW_ALPHA = 0x00000002;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private static readonly IntPtr HWND_TOPMOST = new(-1);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize
        {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BlendFunction
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(
            IntPtr hwnd, IntPtr hdcDst, ref NativePoint pptDst, ref NativeSize psize,
            IntPtr hdcSrc, ref NativePoint pptSrc, int crKey, ref BlendFunction pblend, int dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
