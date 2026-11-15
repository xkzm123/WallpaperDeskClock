using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WallpaperDeskClock
{
    /// <summary>统一的配色 / 字体 / 圆角与控件工厂（零依赖自绘）</summary>
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(244, 246, 250);
        public static readonly Color Card = Color.White;
        public static readonly Color Track = Color.FromArgb(214, 219, 230);
        public static readonly Color TrackSoft = Color.FromArgb(237, 241, 248);
        public static readonly Color Border = Color.FromArgb(226, 230, 238);
        public static readonly Color Text = Color.FromArgb(30, 35, 46);
        public static readonly Color Muted = Color.FromArgb(142, 148, 164);
        public static readonly Color Accent = Color.FromArgb(47, 111, 237);
        public static readonly Color AccentSoft = Color.FromArgb(235, 241, 254);
        public static readonly Color Danger = Color.FromArgb(208, 66, 66);
        public static readonly Color Ok = Color.FromArgb(38, 152, 88);

        public static Font Ui(float px) { return new Font("Microsoft YaHei UI", px, FontStyle.Regular, GraphicsUnit.Pixel); }
        public static Font UiBold(float px) { return new Font("Microsoft YaHei UI", px, FontStyle.Bold, GraphicsUnit.Pixel); }
        public static Font Mono(float px) { return new Font("Consolas", px, FontStyle.Bold, GraphicsUnit.Pixel); }

        public static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        public static Color Lerp(Color a, Color b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            return Color.FromArgb(
                (int)Math.Round(a.A + (b.A - a.A) * t),
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        /// <summary>smoothstep：0→1 的缓入缓出，给开关过渡用</summary>
        public static float Ease(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || r.Width <= 0 || r.Height <= 0) { p.AddRectangle(r); return p; }
            // 直径不能超过短边，否则 AddArc 会退化成直角（高 DPI 下开关胶囊就变方块）
            int max = Math.Min(r.Width, r.Height);
            if (d > max) d = max;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>矢量绘制应用 Logo（与 ico 同一套：深色胶囊 + 白色沙漏 + 蓝色沙堆）</summary>
        public static void DrawLogo(Graphics g, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = r.Width;

            using (var path = Round(r, (int)Math.Max(4, s * 0.235f)))
            using (var lg = new LinearGradientBrush(r, Color.FromArgb(26, 36, 62), Color.FromArgb(11, 17, 32),
                       LinearGradientMode.Vertical))
                g.FillPath(lg, path);

            float cx = r.X + s / 2f;
            float top = r.Y + s * 0.20f, bot = r.Y + s * 0.80f, mid = r.Y + s * 0.50f;
            float halfW = s * 0.205f, neck = halfW * 0.13f;
            float w = Math.Max(1.1f, s * 0.038f);

            var sand = new PointF[]
            {
                new PointF(cx - halfW * 0.88f, bot - s * 0.015f),
                new PointF(cx + halfW * 0.88f, bot - s * 0.015f),
                new PointF(cx, mid + s * 0.15f)
            };
            using (var b = new SolidBrush(Color.FromArgb(92, 170, 255)))
                g.FillPolygon(b, sand);

            var glass = new PointF[]
            {
                new PointF(cx - halfW, top), new PointF(cx + halfW, top),
                new PointF(cx + neck, mid), new PointF(cx + halfW, bot),
                new PointF(cx - halfW, bot), new PointF(cx - neck, mid)
            };
            using (var pen = new Pen(Color.White, w))
            {
                pen.LineJoin = LineJoin.Round;
                g.DrawPolygon(pen, glass);
            }

            float capW = halfW * 2 + s * 0.055f;
            float capH = Math.Max(1.3f, s * 0.042f);
            using (var b = new SolidBrush(Color.White))
            {
                g.FillRectangle(b, cx - capW / 2, top - capH * 1.05f, capW, capH);
                g.FillRectangle(b, cx - capW / 2, bot + capH * 0.05f, capW, capH);
            }
        }

        public static Label Label(string text, float px, bool bold, Color color)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = bold ? UiBold(px) : Ui(px),
                ForeColor = color,
                BackColor = Color.Transparent
            };
        }

        public static Button Button(string text, bool primary)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                Font = Ui(12.5f),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                BackColor = primary ? Accent : Card,
                ForeColor = primary ? Color.White : Text,
                Size = new Size(104, 34)
            };
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(39, 99, 220) : Color.FromArgb(247, 249, 253);
            b.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(31, 88, 200) : Color.FromArgb(238, 242, 250);
            return b;
        }
    }

    /// <summary>Windows 11 现代标题栏 / 圆角；Win10 不支持的属性直接失败忽略</summary>
    static class Dwm
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;    // Win10 1809+
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;   // Win11 22000+
        const int DWMWCP_ROUND = 2;

        public static void Modernize(Form f)
        {
            if (f == null || !f.IsHandleCreated) return;
            int light = 0;                                   // 应用是浅色 UI，强制浅色标题栏
            Set(f.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, light);
            Set(f.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND);
        }

        static void Set(IntPtr h, int attr, int value)
        {
            try { DwmSetWindowAttribute(h, attr, ref value, sizeof(int)); }
            catch { }
        }
    }

    /// <summary>
    /// 弹窗基类：自己接管 WM_DPICHANGED。
    /// .NET Framework 的 Form.DpiChanged 在多显示器混合缩放下并不可靠 ——
    /// 窗口会被系统按建议矩形放大，子控件却不跟随，于是内容挤在左上角。
    /// 所以这里直接截消息：先让 WinForms 套用建议矩形，再按 DPI 比例重排整棵控件树。
    /// </summary>
    class ScaledForm : Form
    {
        const int WM_DPICHANGED = 0x02E0;

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int L, T, R, B; }

        int layoutDpi = 96;     // 当前控件排版对应的 DPI

        public ScaledForm()
        {
            AutoScaleMode = AutoScaleMode.None;   // 缩放完全由本类处理
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Dwm.Modernize(this);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            int dpi = DeviceDpi > 0 ? DeviceDpi : 96;
            float k = dpi / 96f;
            layoutDpi = 96;                        // 排版坐标是按 96 DPI 写的
            if (k > 1.02f) ApplyScale(k, ClientSize);   // 首次就落在高 DPI 屏上
            layoutDpi = dpi;
        }

        /// <summary>保留重写：WinForms 自己抛事件时不再重复缩放，真正的工作在 WndProc 里</summary>
        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_DPICHANGED && IsHandleCreated)
            {
                int newDpi = (int)(m.WParam.ToInt64() & 0xFFFF);
                Size before = ClientSize;

                base.WndProc(ref m);               // WinForms 会把窗口套到 lParam 的建议矩形

                if (m.LParam != IntPtr.Zero && ClientSize == before)
                {
                    // WinForms 没套用建议矩形时自己来（位置按建议，尺寸下面按比例重算）
                    var r = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
                    if (r.R > r.L && r.B > r.T) Location = new Point(r.L, r.T);
                }

                ApplyDpi(newDpi, before);
                return;
            }
            base.WndProc(ref m);
        }

        void ApplyDpi(int newDpi, Size baseClient)
        {
            if (newDpi <= 0) return;
            int old = layoutDpi > 0 ? layoutDpi : 96;
            layoutDpi = newDpi;
            float k = newDpi / (float)old;
            if (Math.Abs(k - 1f) < 0.02f) return;   // 变化太小不动，免得来回抖
            ApplyScale(k, baseClient);
        }

        /// <summary>按比例缩放窗口、自身字体与整棵控件树（位置 / 尺寸 / 自有字体）</summary>
        protected void ApplyScale(float k)
        {
            ApplyScale(k, ClientSize);
        }

        protected void ApplyScale(float k, Size baseClient)
        {
            SuspendLayout();
            try
            {
                var old = Font;
                Font = Scaled(old, k);
                if (old != null) old.Dispose();

                // 客户区严格按“原尺寸 × k”算，避免沿用系统建议矩形时因边框宽度不同产生偏差
                ClientSize = new Size(
                    Math.Max(1, (int)Math.Round(baseClient.Width * k)),
                    Math.Max(1, (int)Math.Round(baseClient.Height * k)));

                ScaleTree(this, k);
            }
            finally { ResumeLayout(true); }
            PerformLayout();

            // 高 DPI 下弹窗可能比屏幕还高，给一个滚动兜底
            var wa = Screen.FromControl(this).WorkingArea;
            AutoScroll = Height > wa.Height - 20 || Width > wa.Width - 20;

            Invalidate(true);
        }

        static Font Scaled(Font f, float k)
        {
            if (f == null) return null;
            return new Font(f.FontFamily, Math.Max(4f, f.Size * k), f.Style, f.Unit);
        }

        static void ScaleTree(Control parent, float k)
        {
            foreach (Control c in parent.Controls)
            {
                // 没显式设过字体的控件是继承父级字体（同一实例），不必也不应另建
                bool ownFont = !ReferenceEquals(c.Font, parent.Font);
                var old = c.Font;

                c.Location = new Point((int)Math.Round(c.Location.X * k), (int)Math.Round(c.Location.Y * k));
                if (!c.AutoSize)
                    c.Size = new Size(
                        Math.Max(1, (int)Math.Round(c.Size.Width * k)),
                        Math.Max(1, (int)Math.Round(c.Size.Height * k)));

                if (ownFont && old != null)
                {
                    c.Font = Scaled(old, k);
                    try { old.Dispose(); } catch { }
                }

                ScaleTree(c, k);
            }
        }
    }

    /// <summary>圆角白色卡片：抗锯齿绘制，不再用 Region 裁剪（Region 边缘是硬锯齿）</summary>
    class Card : Panel
    {
        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;   // 与窗体底色一致，四角露出的就是它
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 先用父容器底色清底，圆角外的四个角就是父级颜色
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.Round(r, 12))
            using (var fill = new SolidBrush(Theme.Card))
                g.FillPath(fill, path);

            using (var path = Theme.Round(r, 12))
            using (var pen = new Pen(Theme.Border, 1f))
                g.DrawPath(pen, path);
        }
    }

    /// <summary>扁平开关（平滑过渡，动画结束即停表）</summary>
    class ToggleSwitch : Control
    {
        const int AnimMs = 150;

        bool on;
        float anim;           // 0 = 关，1 = 开
        Timer timer;

        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Size = new Size(46, 26);
            Cursor = Cursors.Hand;
            BackColor = Theme.Card;
        }

        public bool Checked
        {
            get { return on; }
            set
            {
                if (on == value) return;
                on = value;
                StartAnim();
                var h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        void StartAnim()
        {
            if (timer == null)
            {
                timer = new Timer { Interval = 15 };
                timer.Tick += (s, e) => StepAnim();
            }
            if (!timer.Enabled) timer.Start();
        }

        void StepAnim()
        {
            float target = on ? 1f : 0f;
            float step = 15f / AnimMs;
            anim = anim < target ? Math.Min(target, anim + step) : Math.Max(target, anim - step);
            Invalidate();
            if (Math.Abs(anim - target) < 0.001f)
            {
                anim = target;
                timer.Stop();      // 只在过渡期间跑，静止时零唤醒
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Card);

            float t = Theme.Ease(anim);
            int h = Height, w = Width;
            var track = new Rectangle(0, 2, w - 1, h - 5);

            using (var path = Theme.Round(track, (h - 5) / 2))
            using (var b = new SolidBrush(Theme.Lerp(Theme.Track, Theme.Accent, t)))
                g.FillPath(b, path);

            int d = h - 9;
            float kx = Theme.Lerp(3f, w - d - 3f, t);
            using (var b = new SolidBrush(Color.White))
                g.FillEllipse(b, kx, 4, d, d);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && timer != null) { timer.Stop(); timer.Dispose(); timer = null; }
            base.Dispose(disposing);
        }
    }

    /// <summary>圆角分段选择器（替代原生 ComboBox，胶囊内滑动高亮）</summary>
    class Segmented : Control
    {
        readonly List<string> items = new List<string>();
        int selected = -1;
        int hover = -1;

        public event EventHandler SelectedIndexChanged;

        public Segmented()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Size = new Size(282, 34);
            BackColor = Theme.Card;
            Cursor = Cursors.Hand;
            Font = Theme.Ui(12f);
        }

        public IList<string> Items { get { return items; } }

        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                int v = items.Count == 0 ? -1 : Math.Max(0, Math.Min(items.Count - 1, value));
                if (v == selected) { Invalidate(); return; }
                selected = v;
                Invalidate();
                var h = SelectedIndexChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        int IndexAt(int x)
        {
            if (items.Count == 0 || Width <= 0) return -1;
            int idx = (int)((long)x * items.Count / Width);
            return Math.Max(0, Math.Min(items.Count - 1, idx));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            int idx = IndexAt(e.X);
            if (idx >= 0) SelectedIndex = idx;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = IndexAt(e.X);
            if (idx != hover) { hover = idx; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover != -1) { hover = -1; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Theme.Card);
            if (items.Count == 0) return;

            var track = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.Round(track, Height / 2))
            using (var b = new SolidBrush(Theme.TrackSoft))
                g.FillPath(b, path);

            float segW = Width / (float)items.Count;
            using (var sf = new StringFormat(StringFormatFlags.NoWrap))
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;

                for (int i = 0; i < items.Count; i++)
                {
                    var cell = new RectangleF(i * segW + 1.5f, 1.5f, segW - 3f, Height - 3f);
                    if (i == selected)
                    {
                        using (var pill = Theme.Round(Rectangle.Round(cell), (Height - 4) / 2))
                        using (var b = new SolidBrush(Theme.Accent))
                            g.FillPath(b, pill);
                    }
                    else if (i == hover)
                    {
                        using (var pill = Theme.Round(Rectangle.Round(cell), (Height - 4) / 2))
                        using (var b = new SolidBrush(Color.FromArgb(241, 244, 251)))
                            g.FillPath(b, pill);
                    }

                    using (var b = new SolidBrush(i == selected ? Color.White : Theme.Muted))
                        g.DrawString(items[i], Font, b, cell, sf);
                }
            }
        }
    }
}
