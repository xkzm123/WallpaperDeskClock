using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WallpaperDeskClock
{
    /// <summary>统一的配色 / 字体 / 圆角与控件工厂（零依赖自绘）</summary>
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(244, 246, 250);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(226, 230, 238);
        public static readonly Color Text = Color.FromArgb(30, 35, 46);
        public static readonly Color Muted = Color.FromArgb(142, 148, 164);
        public static readonly Color Accent = Color.FromArgb(47, 111, 237);
        public static readonly Color AccentSoft = Color.FromArgb(235, 241, 254);
        public static readonly Color Danger = Color.FromArgb(208, 66, 66);
        public static readonly Color Ok = Color.FromArgb(38, 152, 88);
        public static readonly Color Track = Color.FromArgb(214, 219, 230);

        public static Font Ui(float px) { return new Font("Microsoft YaHei UI", px, FontStyle.Regular, GraphicsUnit.Pixel); }
        public static Font UiBold(float px) { return new Font("Microsoft YaHei UI", px, FontStyle.Bold, GraphicsUnit.Pixel); }
        public static Font Mono(float px) { return new Font("Consolas", px, FontStyle.Bold, GraphicsUnit.Pixel); }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || r.Width <= d || r.Height <= d) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>矢量绘制应用 Logo（避免 Icon.ToBitmap 在 .NET Framework 下的花屏）</summary>
        public static void DrawLogo(Graphics g, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Round(r, Math.Max(4, r.Width / 5)))
            using (var lg = new LinearGradientBrush(r, Color.FromArgb(59, 130, 246), Color.FromArgb(23, 37, 84),
                       LinearGradientMode.ForwardDiagonal))
                g.FillPath(lg, path);

            float pad = r.Width * 0.21f;
            var face = new RectangleF(r.X + pad, r.Y + pad, r.Width - pad * 2, r.Height - pad * 2);
            float w = Math.Max(1.2f, r.Width * 0.075f);
            using (var pen = new Pen(Color.White, w))
                g.DrawEllipse(pen, face);

            float cx = face.X + face.Width / 2f, cy = face.Y + face.Height / 2f;
            using (var pen = new Pen(Color.White, w))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx, cy, cx, cy - face.Height * 0.30f);
                g.DrawLine(pen, cx, cy, cx + face.Width * 0.26f, cy + face.Height * 0.11f);
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

    /// <summary>圆角白色卡片容器</summary>
    class Card : Panel
    {
        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width <= 0 || Height <= 0) return;
            using (var p = Theme.Round(new Rectangle(0, 0, Width, Height), 12))
                Region = new Region(p);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), 12))
            using (var pen = new Pen(Theme.Border, 1))
                e.Graphics.DrawPath(pen, p);
        }
    }

    /// <summary>扁平开关（替代原生 CheckBox，状态一眼可见）</summary>
    class ToggleSwitch : Control
    {
        bool on;

        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(46, 26);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
        }

        public bool Checked
        {
            get { return on; }
            set
            {
                if (on == value) return;
                on = value;
                Invalidate();
                var h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
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
            g.Clear(Parent != null ? Parent.BackColor : Color.White);

            int h = Height, w = Width;
            var track = new Rectangle(0, 2, w - 1, h - 5);
            using (var path = Theme.Round(track, (h - 5) / 2))
            using (var b = new SolidBrush(on ? Theme.Accent : Theme.Track))
                g.FillPath(b, path);

            int d = h - 9;
            int kx = on ? (w - d - 3) : 3;
            using (var b = new SolidBrush(Color.White))
                g.FillEllipse(b, kx, 4, d, d);
        }
    }
}
