using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
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
        bool contentPushed;
        int _lastSec = -1, _lastMin = -1, _lastDay = -1;

        // 随字体一起创建 / 销毁的 GDI+ 对象：每秒重绘不再重复 new
        SolidBrush brText, brShadow;
        StringFormat sfFar, sfNear;

        // 拖动状态
        bool dragging;
        int dragBlock;                 // 0 = 整体，1 = 时间块，2 = 日期块
        Point dragOrigin;              // 按下时的鼠标屏幕坐标
        Point winOrigin;               // 按下时的窗口位置
        RectangleF dragRect;           // 按下时被拖块的矩形
        float dx0, dy0;                // 按下时该块的偏移

        // 渲染出的命中区域（窗口坐标）
        RectangleF timeRect, dateRect;

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
            Render(contentPushed);
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
            fDate = MakeFont(app.Cfg.S("font_date", "Consolas"), app.Cfg.F("size_date", 18f) * s);
            fWeek = MakeFont(app.Cfg.S("font_week", "SimHei"), app.Cfg.F("size_week", 18f) * s);
            fSec = MakeFont(app.Cfg.S("font_seconds", "Consolas"), app.Cfg.F("size_seconds", 18f) * s);

            // 画刷与 StringFormat 只随字体生命周期创建一次
            brText = new SolidBrush(col);
            brShadow = new SolidBrush(Color.FromArgb(175, 0, 0, 0));
            sfFar = new StringFormat(StringFormatFlags.NoWrap)
            {
                Alignment = StringAlignment.Far,
                LineAlignment = StringAlignment.Center
            };
            sfNear = new StringFormat(StringFormatFlags.NoWrap)
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center
            };
        }

        void DisposeFonts()
        {
            if (fClock != null) { fClock.Dispose(); fClock = null; }
            if (fDate != null) { fDate.Dispose(); fDate = null; }
            if (fWeek != null) { fWeek.Dispose(); fWeek = null; }
            if (fSec != null) { fSec.Dispose(); fSec = null; }
            if (brText != null) { brText.Dispose(); brText = null; }
            if (brShadow != null) { brShadow.Dispose(); brShadow = null; }
            if (sfFar != null) { sfFar.Dispose(); sfFar = null; }
            if (sfNear != null) { sfNear.Dispose(); sfNear = null; }
        }

        // ---------- 渲染 ----------

        void Render() { Render(true); }

        void Render(bool push)
        {
            if (layer == null) return;
            var now = DateTime.Now;

            using (var g = Graphics.FromImage(layer))
            {
                g.Clear(Color.Transparent);
                // 分层窗口的透明像素不接收鼠标：编辑模式下铺一层 alpha 极低的底色，
                // 整个窗口才可拖动（视觉上几乎不可见）
                if (editMode)
                {
                    using (var bg = new SolidBrush(Color.FromArgb(14, 70, 100, 170)))
                        g.FillRectangle(bg, 0, 0, layer.Width, layer.Height);
                }
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                bool shadow = app.Cfg.B("shadow", true);
                bool use12h = app.Cfg.B("use12h", false);
                bool showDate = app.Cfg.B("showdate", true);
                bool showWeek = app.Cfg.B("showweek", true);
                bool showSec = app.Cfg.B("showseconds", true);

                float w = layer.Width, h = layer.Height;
                float baseX = w * app.Cfg.F("rel_right", 0.831f);
                float baseClockY = h * app.Cfg.F("rel_clock_y", 0.323f);
                float baseDateY = h * app.Cfg.F("rel_date_y", 0.695f);
                float gapSec = app.Cfg.F("gap_seconds", 14f) * LayoutData.Scale;
                float gapWeek = app.Cfg.F("gap_week", 26f) * LayoutData.Scale;
                float secDy = app.Cfg.F("seconds_dy", 10f) * LayoutData.Scale;

                // 时间块 / 日期块各自可独立偏移
                float timeX = baseX + w * LayoutData.TimeDX;
                float clockY = baseClockY + h * LayoutData.TimeDY;
                float dateX = baseX + w * LayoutData.DateDX;
                float dateY = baseDateY + h * LayoutData.DateDY;

                string timeText = now.ToString(use12h ? "hh:mm" : "HH:mm");
                string secText = now.ToString("ss");
                string dateText = now.ToString(app.Cfg.S("dateformat", "yyyy.MM.dd"));
                string weekText = WeekCN(now);

                var sf = StringFormat.GenericTypographic;
                SizeF ts = g.MeasureString(timeText, fClock, PointF.Empty, sf);
                SizeF ss = g.MeasureString(secText, fSec, PointF.Empty, sf);
                SizeF ds = g.MeasureString(dateText, fDate, PointF.Empty, sf);
                SizeF ws = g.MeasureString(weekText, fWeek, PointF.Empty, sf);

                // 命中区域（用于编辑模式下的独立拖动）
                float timeRight = timeX + (showSec ? gapSec + ss.Width : 0);
                timeRect = RectangleF.FromLTRB(timeX - ts.Width, clockY - ts.Height / 2f, timeRight, clockY + ts.Height / 2f);

                float dateLeft = dateX - ds.Width;
                if (showWeek) dateLeft -= gapWeek + ws.Width;
                dateRect = RectangleF.FromLTRB(dateLeft, dateY - ds.Height / 2f, dateX, dateY + ds.Height / 2f);

                if (showDate)
                    DrawText(g, dateText, fDate, dateX, dateY, shadow, StringAlignment.Far);

                if (showWeek)
                    DrawText(g, weekText, fWeek, dateX - ds.Width - gapWeek, dateY, shadow, StringAlignment.Far);

                DrawText(g, timeText, fClock, timeX, clockY, shadow, StringAlignment.Far);

                if (showSec)
                    DrawText(g, secText, fSec, timeX + gapSec, clockY + secDy, shadow, StringAlignment.Near);

                if (editMode) DrawEditHint(g);
            }

            if (push) PushLayered();
        }

        void DrawEditHint(Graphics g)
        {
            using (var pen = new Pen(Color.FromArgb(150, 90, 180, 255), 2f))
                g.DrawRectangle(pen, 1, 1, layer.Width - 3, layer.Height - 3);

            DrawBlockBox(g, timeRect, "时间", Color.FromArgb(170, 90, 180, 255));
            DrawBlockBox(g, dateRect, "日期", Color.FromArgb(170, 110, 200, 140));
        }

        void DrawBlockBox(Graphics g, RectangleF r, string label, Color c)
        {
            var box = RectangleF.Inflate(r, 8f, 5f);
            using (var pen = new Pen(c, 1.5f))
            {
                pen.DashStyle = DashStyle.Dash;
                g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
            }
            float px = Math.Max(10f, 11f * LayoutData.Scale);
            using (var f = new Font("Microsoft YaHei", px, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(c))
            {
                float ly = box.Y - f.Height - 1;
                if (ly < 0) ly = box.Y + 2;
                g.DrawString(label, f, b, box.X + 3, ly);
            }
        }

        void PushLayered()
        {
            if (layer == null || !IsHandleCreated) return;
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

            var sf = align == StringAlignment.Near ? sfNear : sfFar;
            if (sf == null) return;
            if (shadow && brShadow != null)
            {
                float off = Math.Max(1.5f, size / 24f);
                var sr = rect; sr.Offset(off, off);
                g.DrawString(text, font, brShadow, sr, sf);
            }
            if (brText != null)
                g.DrawString(text, font, brText, rect, sf);
        }

        // ---------- 编辑模式 / 拖动 ----------

        public bool EditMode
        {
            get { return editMode; }
            set
            {
                if (editMode == value) return;
                editMode = value;
                dragging = false;
                Capture = false;
                UpdateExStyle();
                if (editMode)
                    Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                else
                {
                    Native.SetWindowPos(Handle, Native.HWND_BOTTOM, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                    app.SaveLayout(this);
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

        /// <summary>显示前后都压到最底层，且首帧内容在沉底之后才推送 —— 启动不闪现</summary>
        protected override void SetVisibleCore(bool value)
        {
            if (value && IsHandleCreated)
                Native.SetWindowPos(Handle, Native.HWND_BOTTOM, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            base.SetVisibleCore(value);
            if (value)
                Native.SetWindowPos(Handle, Native.HWND_BOTTOM, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!editMode || e.Button != MouseButtons.Left) return;

            dragOrigin = Cursor.Position;
            winOrigin = Location;
            if (timeRect.Contains(e.Location)) dragBlock = 1;
            else if (dateRect.Contains(e.Location)) dragBlock = 2;
            else dragBlock = 0;

            if (dragBlock == 1) { dx0 = LayoutData.TimeDX; dy0 = LayoutData.TimeDY; dragRect = timeRect; }
            else if (dragBlock == 2) { dx0 = LayoutData.DateDX; dy0 = LayoutData.DateDY; dragRect = dateRect; }
            else { dx0 = dy0 = 0; }

            dragging = true;
            Capture = true;
            Cursor = Cursors.SizeAll;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!dragging)
            {
                if (editMode)
                    Cursor = (timeRect.Contains(e.Location) || dateRect.Contains(e.Location)) ? Cursors.SizeAll : Cursors.Default;
                return;
            }

            Point p = Cursor.Position;
            int dx = p.X - dragOrigin.X;
            int dy = p.Y - dragOrigin.Y;

            if (dragBlock == 0)
            {
                Location = new Point(winOrigin.X + dx, winOrigin.Y + dy);
                return;
            }

            // 让被拖的块始终留在窗口内
            int minDX = (int)Math.Round(-dragRect.Left);
            int maxDX = (int)Math.Round(Width - dragRect.Right);
            int minDY = (int)Math.Round(-dragRect.Top);
            int maxDY = (int)Math.Round(Height - dragRect.Bottom);
            dx = Math.Max(minDX, Math.Min(maxDX, dx));
            dy = Math.Max(minDY, Math.Min(maxDY, dy));

            float w = Math.Max(1, Width), h = Math.Max(1, Height);
            if (dragBlock == 1)
            {
                LayoutData.TimeDX = dx0 + dx / w;
                LayoutData.TimeDY = dy0 + dy / h;
            }
            else
            {
                LayoutData.DateDX = dx0 + dx / w;
                LayoutData.DateDY = dy0 + dy / h;
            }
            Render();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!dragging) return;
            dragging = false;
            Capture = false;
            Cursor = Cursors.Default;
            app.SaveLayout(this);
            Render();
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
            contentPushed = true;
            PushLayered();
        }

        /// <summary>每秒刷新（只在显示内容真的变化时重绘）</summary>
        public void Tick()
        {
            var now = DateTime.Now;
            if (now.Second == _lastSec && now.Minute == _lastMin && now.Day == _lastDay) return;
            // 不显示秒时，分钟与日期没变就完全不用重绘（配合 1000 ms 的定时器）
            if (!app.Cfg.B("showseconds", true) && now.Minute == _lastMin && now.Day == _lastDay) return;
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
