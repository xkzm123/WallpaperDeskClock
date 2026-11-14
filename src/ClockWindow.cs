using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WallpaperDeskClock
{
    /// <summary>单个显示器上的时钟窗口（分层、可穿透、可拖动）</summary>
    class ClockWindow : Form
    {
        readonly AppContext app;
        readonly Screen screen;
        public readonly string ScreenKey;
        public LayoutEntry LayoutData;

        Bitmap layer;
        Font fClock, fDate, fWeek, fSec;
        Color col;
        bool editMode;
        int _lastSec = -1, _lastMin = -1, _lastDay = -1;

        public ClockWindow(AppContext app, Screen screen, LayoutEntry layout)
        {
            this.app = app;
            this.screen = screen;
            this.LayoutData = layout;
            this.ScreenKey = screen.DeviceName.Replace("\\", "_").Replace(".", "");

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = false;
            SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint, true);

            col = app.Cfg.C("color", Color.FromArgb(242, 242, 242));
            BuildFonts();
            ApplyLayout();
        }

        // ---------- 尺寸 / 位置 ----------

        public Rectangle TargetBounds()
        {
            var b = screen.Bounds;
            int w = (int)Math.Round(b.Width * LayoutEntry.BaseW * LayoutData.Scale);
            int h = (int)Math.Round(b.Height * LayoutEntry.BaseH * LayoutData.Scale);
            w = Math.Max(120, w);
            h = Math.Max(60, h);
            int x = b.Left + (int)Math.Round(b.Width * LayoutData.X);
            int y = b.Top + (int)Math.Round(b.Height * LayoutData.Y);
            // 防止拖出屏幕后不可见
            if (x > b.Right - 40) x = b.Right - 40;
            if (y > b.Bottom - 40) y = b.Bottom - 40;
            if (x + w < b.Left + 40) x = b.Left + 40 - w;
            if (y + h < b.Top + 40) y = b.Top + 40 - h;
            return new Rectangle(x, y, w, h);
        }

        void ApplyLayout()
        {
            var r = TargetBounds();
            Bounds = r;
            if (layer == null || layer.Width != r.Width || layer.Height != r.Height)
            {
                if (layer != null) layer.Dispose();
                layer = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
            }
            Render();
        }

        public void ReloadFromConfig()
        {
            col = app.Cfg.C("color", Color.FromArgb(242, 242, 242));
            BuildFonts();
            ApplyLayout();
        }

        public void RefreshScale()
        {
            ApplyLayout();
        }

        // ---------- 字体 ----------

        static Font MakeFont(string name, float px)
        {
            try
            {
                var f = new Font(name, Math.Max(4f, px), FontStyle.Regular, GraphicsUnit.Pixel);
                if (f.Name.Length > 0) return f;
            }
            catch { }
            return new Font("Segoe UI", Math.Max(4f, px), FontStyle.Regular, GraphicsUnit.Pixel);
        }

        void BuildFonts()
        {
            DisposeFonts();
            float s = LayoutData.Scale;
            fClock = MakeFont(app.Cfg.S("font_clock", "Bahnschrift"), app.Cfg.F("size_clock", 64f) * s);
            fDate  = MakeFont(app.Cfg.S("font_date", "Consolas"),      app.Cfg.F("size_date", 18f) * s);
            fWeek  = MakeFont(app.Cfg.S("font_week", "SimHei"),        app.Cfg.F("size_week", 18f) * s);
            fSec   = MakeFont(app.Cfg.S("font_seconds", "Consolas"),   app.Cfg.F("size_seconds", 18f) * s);
        }

        void DisposeFonts()
        {
            if (fClock != null) fClock.Dispose();
            if (fDate != null) fDate.Dispose();
            if (fWeek != null) fWeek.Dispose();
            if (fSec != null) fSec.Dispose();
        }

        // ---------- 渲染 ----------

        void Render()
        {
            if (layer == null) return;
            var now = DateTime.Now;

            using (var g = Graphics.FromImage(layer))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                bool shadow = app.Cfg.B("shadow", true);
                bool use12h = app.Cfg.B("use12h", false);
                string timeFmt = use12h ? "hh:mm" : "HH:mm";
                string dateFmt = app.Cfg.S("dateformat", "yyyy.MM.dd");

                float w = layer.Width, h = layer.Height;
                float rightX = w * app.Cfg.F("rel_right", 0.831f);
                float clockY = h * app.Cfg.F("rel_clock_y", 0.323f);
                float dateY = h * app.Cfg.F("rel_date_y", 0.695f);
                float gapSec = app.Cfg.F("gap_seconds", 14f) * LayoutData.Scale;
                float gapWeek = app.Cfg.F("gap_week", 26f) * LayoutData.Scale;

                string dateText = now.ToString(dateFmt);
                string weekText = WeekCN(now);

                if (app.Cfg.B("showdate", true))
                    DrawText(g, dateText, fDate, rightX, dateY, shadow, StringAlignment.Far);

                if (app.Cfg.B("showweek", true))
                {
                    SizeF ds = g.MeasureString(dateText, fDate, new PointF(0, 0), StringFormat.GenericTypographic);
                    DrawText(g, weekText, fWeek, rightX - ds.Width - gapWeek, dateY, shadow, StringAlignment.Far);
                }

                DrawText(g, now.ToString(timeFmt), fClock, rightX, clockY, shadow, StringAlignment.Far);

                if (app.Cfg.B("showseconds", true))
                    DrawText(g, now.ToString("ss"), fSec, rightX + gapSec,
                             clockY + app.Cfg.F("seconds_dy", 10f) * LayoutData.Scale, shadow, StringAlignment.Near);

                if (editMode) DrawEditHint(g);
            }

            PushLayered();
        }

        void DrawEditHint(Graphics g)
        {
            using (var pen = new Pen(Color.FromArgb(150, 90, 180, 255), 2f))
                g.DrawRectangle(pen, 1, 1, layer.Width - 3, layer.Height - 3);
            using (var f = new Font("Microsoft YaHei", 12f * LayoutData.Scale, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(Color.FromArgb(210, 120, 190, 255)))
                g.DrawString("拖动调整位置", f, b, 8, 6);
        }

        void PushLayered()
        {
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            IntPtr hBmp = layer.GetHbitmap(Color.FromArgb(0));
            IntPtr old = Native.SelectObject(memDc, hBmp);

            var size = new Native.SIZE(layer.Width, layer.Height);
            var src = new Native.POINT(0, 0);
            var dst = new Native.POINT(Left, Top);
            var blend = new Native.BLENDFUNCTION
            {
                BlendOp = Native.AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = Native.AC_SRC_ALPHA
            };
            Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, Native.ULW_ALPHA);

            Native.SelectObject(memDc, old);
            Native.DeleteObject(hBmp);
            Native.DeleteDC(memDc);
            Native.ReleaseDC(IntPtr.Zero, screenDc);
        }

        static string WeekCN(DateTime d)
        {
            string[] w = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
            return w[(int)d.DayOfWeek];
        }

        void DrawText(Graphics g, string text, Font font, float x, float y, bool shadow, StringAlignment align)
        {
            if (string.IsNullOrEmpty(text) || font == null) return;
            float size = font.Size;
            float boxW = Math.Max(420f * LayoutData.Scale, size * text.Length * 1.8f);
            float boxH = size * 2.4f;
            RectangleF rect;
            if (align == StringAlignment.Far) rect = new RectangleF(x - boxW, y - boxH / 2f, boxW, boxH);
            else if (align == StringAlignment.Near) rect = new RectangleF(x, y - boxH / 2f, boxW, boxH);
            else rect = new RectangleF(x - boxW / 2f, y - boxH / 2f, boxW, boxH);

            using (var sf = new StringFormat(StringFormatFlags.NoWrap))
            {
                sf.Alignment = align;
                sf.LineAlignment = StringAlignment.Center;
                if (shadow)
                {
                    float off = Math.Max(1.5f, size / 24f);
                    using (var sb = new SolidBrush(Color.FromArgb(175, 0, 0, 0)))
                    {
                        var sr = rect; sr.Offset(off, off);
                        g.DrawString(text, font, sb, sr, sf);
                    }
                }
                using (var b = new SolidBrush(col))
                    g.DrawString(text, font, b, rect, sf);
            }
        }

        // ---------- 编辑模式 ----------

        public bool EditMode
        {
            get { return editMode; }
            set
            {
                if (editMode == value) return;
                editMode = value;
                // 编辑模式需要接收鼠标，取消穿透并置顶
                UpdateExStyle();
                if (editMode)
                    Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                else
                {
                    Native.SetWindowPos(Handle, Native.HWND_BOTTOM, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                    app.SaveLayout(this);   // 退出编辑模式时落盘
                }
                Render();
            }
        }

        void UpdateExStyle()
        {
            if (!IsHandleCreated) return;
            int ex = Native.GetWindowLong(Handle, -20);
            if (editMode) ex &= ~0x00000020;   // 去掉 WS_EX_TRANSPARENT，接收鼠标
            else ex |= 0x00000020;
            Native.SetWindowLong(Handle, -20, ex);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00080000; // WS_EX_LAYERED
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                if (!editMode)
                    cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT（穿透）
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (editMode && m.Msg == Native.WM_LBUTTONDOWN)
            {
                Native.ReleaseCapture();
                Native.SendMessage(Handle, Native.WM_NCLBUTTONDOWN, (IntPtr)Native.HTCAPTION, IntPtr.Zero);
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            if (editMode)
            {
                // 记录拖动结果
                var b = screen.Bounds;
                if (b.Width > 0 && b.Height > 0)
                {
                    LayoutData.X = (float)(Left - b.Left) / b.Width;
                    LayoutData.Y = (float)(Top - b.Top) / b.Height;
                    LayoutData.X = Math.Max(-0.05f, Math.Min(1.05f, LayoutData.X));
                    LayoutData.Y = Math.Max(-0.05f, Math.Min(1.05f, LayoutData.Y));
                }
                PushLayered();
            }
        }

        protected override void OnPaint(PaintEventArgs e) { }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!editMode)
                Native.SetWindowPos(Handle, Native.HWND_BOTTOM, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            PushLayered();
        }

        /// <summary>每秒刷新（秒变化才重绘）</summary>
        public void Tick()
        {
            var now = DateTime.Now;
            if (now.Second == _lastSec && now.Minute == _lastMin && now.Day == _lastDay) return;
            _lastSec = now.Second; _lastMin = now.Minute; _lastDay = now.Day;
            Render();
            if (!editMode)
                Native.SetWindowPos(Handle, Native.HWND_BOTTOM, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        public void SavePreview(string path)
        {
            if (layer != null) layer.Save(path, ImageFormat.Png);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            DisposeFonts();
            if (layer != null) layer.Dispose();
            base.OnFormClosing(e);
        }
    }
}
