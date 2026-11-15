using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WallpaperDeskClock
{
    /// <summary>扁平滑块（替代原生 TrackBar）</summary>
    class Slider : Control
    {
        int min = 50, max = 250, val = 100;

        public event EventHandler ValueChanged;

        public Slider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(396, 26);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        public int Minimum { get { return min; } set { min = value; Value = val; } }
        public int Maximum { get { return max; } set { max = value; Value = val; } }

        public int Value
        {
            get { return val; }
            set
            {
                int v = Math.Max(min, Math.Min(max, value));
                if (v == val) { Invalidate(); return; }
                val = v;
                Invalidate();
                var h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        int TrackLeft { get { return 9; } }
        int TrackWidth { get { return Math.Max(1, Width - 18); } }

        void SetFromX(int x)
        {
            float r = (float)(x - TrackLeft) / TrackWidth;
            r = Math.Max(0f, Math.Min(1f, r));
            Value = min + (int)Math.Round(r * (max - min));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Capture = true;
            SetFromX(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (Capture) SetFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Capture = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Color.White);

            int cy = Height / 2;
            using (var track = Theme.Round(new Rectangle(TrackLeft, cy - 2, TrackWidth, 4), 2))
            using (var b = new SolidBrush(Theme.Track))
                g.FillPath(b, track);

            int kx = TrackLeft + (int)Math.Round((float)(val - min) / Math.Max(1, max - min) * TrackWidth);
            using (var fill = Theme.Round(new Rectangle(TrackLeft, cy - 2, Math.Max(2, kx - TrackLeft), 4), 2))
            using (var b = new SolidBrush(Theme.Accent))
                g.FillPath(b, fill);

            using (var b = new SolidBrush(Color.White))
            using (var pen = new Pen(Theme.Accent, 2f))
            {
                g.FillEllipse(b, kx - 7, cy - 7, 14, 14);
                g.DrawEllipse(pen, kx - 7, cy - 7, 14, 14);
            }
        }
    }

    /// <summary>设置面板：开机自启、显示器选择、布局、壁纸库</summary>
    class SettingsForm : ScaledForm
    {
        readonly AppContext app;
        ToggleSwitch swAutostart, swEdit;
        Segmented segDisplay, segTarget;
        Slider slider, sliderGap;
        Label lblScale, lblGap, lblHint, lblLibPath;
        Timer saveTimer;
        bool loading;

        public SettingsForm(AppContext app)
        {
            this.app = app;
            Text = "壁纸桌面时钟";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(460, 688);
            BackColor = Theme.Bg;
            Font = Theme.Ui(12.5f);
            Icon = Paths.LoadAppIcon();
            DoubleBuffered = true;

            BuildHeader();
            BuildStartCard();
            BuildLayoutCard();
            BuildWallpaperCard();
            BuildFooter();
            SyncFromApp();
        }

        static Label Put(Control parent, string text, float px, bool bold, Color color, int x, int y)
        {
            var l = Theme.Label(text, px, bold, color);
            l.Location = new Point(x, y);
            parent.Controls.Add(l);
            return l;
        }

        void BuildHeader()
        {
            var logo = new Panel
            {
                Location = new Point(20, 16),
                Size = new Size(34, 34),
                BackColor = Color.Transparent
            };
            logo.Paint += (s, e) => Theme.DrawLogo(e.Graphics, new Rectangle(0, 0, logo.Width, logo.Height));
            Controls.Add(logo);
            Put(this, "壁纸桌面时钟", 17, true, Theme.Text, 64, 14);
            Put(this, "桌面时钟 · 设置", 11.5f, false, Theme.Muted, 65, 40);
        }

        void BuildStartCard()
        {
            var card = new Card { Location = new Point(16, 72), Size = new Size(428, 118) };
            Controls.Add(card);

            Put(card, "启动与显示", 11.5f, true, Theme.Muted, 18, 12);
            Put(card, "开机自动启动", 13, false, Theme.Text, 18, 42);

            swAutostart = new ToggleSwitch { Location = new Point(364, 38) };
            swAutostart.CheckedChanged += (s, e) =>
            {
                if (loading) return;
                if (swAutostart.Checked) { Autostart.Enable(); Hint("已添加到开机启动", Theme.Ok); }
                else { Autostart.Disable(); Hint("已移除启动项", Theme.Muted); }
            };
            card.Controls.Add(swAutostart);

            Put(card, "显示位置", 13, false, Theme.Text, 18, 82);

            segDisplay = new Segmented
            {
                Location = new Point(120, 74),
                Size = new Size(290, 34),
                Font = Theme.Ui(12f)
            };
            segDisplay.Items.Add("全部显示器");
            var screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
                segDisplay.Items.Add("显示器 " + (i + 1));
            segDisplay.SelectedIndexChanged += (s, e) =>
            {
                if (loading) return;
                int idx = segDisplay.SelectedIndex;
                app.Cfg.Set("displays", idx <= 0 ? "all" : (idx - 1).ToString());
                app.Cfg.Save();
                app.RebuildFromSettings();
            };
            card.Controls.Add(segDisplay);
        }

        void BuildLayoutCard()
        {
            var card = new Card { Location = new Point(16, 202), Size = new Size(428, 272) };
            Controls.Add(card);

            Put(card, "布局", 11.5f, true, Theme.Muted, 18, 12);
            Put(card, "自定义布局模式", 13, false, Theme.Text, 18, 42);

            swEdit = new ToggleSwitch { Location = new Point(364, 38) };
            swEdit.CheckedChanged += (s, e) =>
            {
                if (loading) return;
                app.SetEditMode(swEdit.Checked);
                if (swEdit.Checked) Hint("拖动时钟可整体移动，拖动「时间」「日期」可分别定位", Theme.Accent);
                else { Hint("布局已保存", Theme.Ok); app.SaveLayouts(); }
            };
            card.Controls.Add(swEdit);

            // 调整对象：字号 / 秒间距按屏独立
            Put(card, "调整对象", 13, false, Theme.Text, 18, 82);
            segTarget = new Segmented { Location = new Point(120, 74), Size = new Size(290, 34), Font = Theme.Ui(12f) };
            var screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
                segTarget.Items.Add("显示器 " + (i + 1));
            if (screens.Length > 1) segTarget.Items.Add("全部");
            segTarget.SelectedIndexChanged += (s, e) =>
            {
                if (loading) return;
                SyncLayoutValues();
            };
            card.Controls.Add(segTarget);

            Put(card, "字号 / 缩放", 13, false, Theme.Text, 18, 122);
            lblScale = Put(card, "1.00×", 13, true, Theme.Accent, 352, 120);

            slider = new Slider { Location = new Point(14, 142), Size = new Size(400, 26), Minimum = 50, Maximum = 250, Value = 100 };
            slider.ValueChanged += (s, e) =>
            {
                if (loading) return;
                lblScale.Text = (slider.Value / 100f).ToString("0.00") + "×";
                app.ApplyScaleTo(TargetKey(), slider.Value / 100f);
                ScheduleSave();
            };
            card.Controls.Add(slider);

            Put(card, "秒间距", 13, false, Theme.Text, 18, 176);
            lblGap = Put(card, "14 px", 13, true, Theme.Accent, 352, 174);

            sliderGap = new Slider { Location = new Point(14, 196), Size = new Size(400, 26), Minimum = 0, Maximum = 80, Value = 14 };
            sliderGap.ValueChanged += (s, e) =>
            {
                if (loading) return;
                lblGap.Text = sliderGap.Value + " px";
                app.ApplyGapTo(TargetKey(), sliderGap.Value);
                ScheduleSave();
            };
            card.Controls.Add(sliderGap);

            var btnReset = Theme.Button("恢复默认布局", false);
            btnReset.Location = new Point(18, 232);
            btnReset.Size = new Size(136, 30);
            btnReset.Click += (s, e) =>
            {
                app.ResetLayouts(TargetKey());
                SyncFromApp();
                Hint("已恢复默认布局", Theme.Ok);
            };
            card.Controls.Add(btnReset);
        }

        /// <summary>当前「调整对象」对应的屏键；全部时返回 null</summary>
        string TargetKey()
        {
            int n = Screen.AllScreens.Length;
            int i = segTarget.SelectedIndex;
            if (i < 0 || i >= n) return null;          // 最后一项是「全部」
            return AppContext.KeyFor(Screen.AllScreens[i]);
        }

        /// <summary>把选中屏的缩放 / 间距读回控件</summary>
        void SyncLayoutValues()
        {
            float scale = 1f, gap = 14f;
            ClockWindow w = app.FindWindow(TargetKey());
            if (w == null && app.Windows.Count > 0) w = app.Windows[0];
            if (w != null)
            {
                scale = w.LayoutData.Scale;
                gap = w.LayoutData.GapSec;
            }

            bool old = loading;
            loading = true;
            try
            {
                slider.Value = (int)Math.Round(Math.Max(0.5f, Math.Min(2.5f, scale)) * 100);
                lblScale.Text = (slider.Value / 100f).ToString("0.00") + "×";
                sliderGap.Value = (int)Math.Round(Math.Max(0, Math.Min(80, gap)));
                lblGap.Text = sliderGap.Value + " px";
            }
            finally { loading = old; }
        }

        /// <summary>拖动滑杆时不写盘，松手 400 ms 后落盘一次</summary>
        void ScheduleSave()
        {
            if (saveTimer == null)
            {
                saveTimer = new Timer { Interval = 400 };
                saveTimer.Tick += (s, e) =>
                {
                    saveTimer.Stop();
                    app.SaveLayouts();
                };
            }
            saveTimer.Stop();
            saveTimer.Start();
        }

        void BuildWallpaperCard()
        {
            var card = new Card { Location = new Point(16, 490), Size = new Size(428, 104) };
            Controls.Add(card);

            Put(card, "壁纸", 11.5f, true, Theme.Muted, 18, 12);
            Put(card, "壁纸库", 13, false, Theme.Text, 18, 46);

            lblLibPath = new Label
            {
                AutoSize = false,
                Size = new Size(190, 22),
                Location = new Point(84, 46),
                Font = Theme.Ui(11.5f),
                ForeColor = Theme.Muted,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            };
            card.Controls.Add(lblLibPath);

            var btnLib = Theme.Button("壁纸管理器", true);
            btnLib.Location = new Point(288, 38);
            btnLib.Size = new Size(122, 34);
            btnLib.Click += (s, e) => app.ShowWallpaper();
            card.Controls.Add(btnLib);
        }

        void BuildFooter()
        {
            lblHint = new Label
            {
                AutoSize = false,
                Size = new Size(420, 20),
                Location = new Point(20, 608),
                Font = Theme.Ui(11.5f),
                ForeColor = Theme.Muted,
                BackColor = Color.Transparent
            };
            Controls.Add(lblHint);

            var btnExit = Theme.Button("退出程序", false);
            btnExit.Location = new Point(16, 636);
            btnExit.Size = new Size(96, 34);
            btnExit.ForeColor = Theme.Danger;
            btnExit.Click += (s, e) =>
            {
                if (MessageBox.Show("确定退出壁纸桌面时钟？桌面时钟会一并消失。", "退出程序",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    app.ExitApp();
            };
            Controls.Add(btnExit);

            var btnClose = Theme.Button("关闭", true);
            btnClose.Location = new Point(340, 636);
            btnClose.Size = new Size(104, 34);
            btnClose.Click += (s, e) => app.CloseSettings();
            Controls.Add(btnClose);
        }

        void Hint(string text, Color color)
        {
            lblHint.Text = text;
            lblHint.ForeColor = color;
        }

        public void SyncFromApp()
        {
            loading = true;
            try
            {
                swAutostart.Checked = Autostart.IsEnabled();

                string mode = app.Cfg.S("displays", "all").Trim().ToLowerInvariant();
                int idx = 0;
                if (mode != "all")
                {
                    int n;
                    if (int.TryParse(mode, out n) && n >= 0 && n < Screen.AllScreens.Length) idx = n + 1;
                }
                if (idx >= segDisplay.Items.Count) idx = 0;
                segDisplay.SelectedIndex = idx;

                if (segTarget.SelectedIndex < 0) segTarget.SelectedIndex = 0;
                SyncLayoutValues();

                lblLibPath.Text = WallpaperForm.LibraryFolder(app);
            }
            finally { loading = false; }
        }
    }

    /// <summary>运行状态：窄卡片 + 大字号，数值每秒自动刷新</summary>
    class StatsForm : ScaledForm
    {
        Label lblPid, lblCpu, lblMem, lblThreads, lblUptime;
        Timer timer;
        TimeSpan lastCpu;
        DateTime lastSample = DateTime.Now;
        readonly Process proc = Process.GetCurrentProcess();

        /// <summary>PROCESS_MEMORY_COUNTERS_EX2 的布局（EX 的 PrivateUsage + EX2 的专用/共享工作集）</summary>
        [StructLayout(LayoutKind.Sequential)]
        struct MEMORY_COUNTERS
        {
            public uint cb;
            public uint PageFaultCount;
            public IntPtr PeakWorkingSetSize;
            public IntPtr WorkingSetSize;
            public IntPtr QuotaPeakPagedPoolUsage;
            public IntPtr QuotaPagedPoolUsage;
            public IntPtr QuotaPeakNonPagedPoolUsage;
            public IntPtr QuotaNonPagedPoolUsage;
            public IntPtr PagefileUsage;
            public IntPtr PeakPagefileUsage;
            public IntPtr PrivateUsage;            // EX
            public IntPtr PrivateWorkingSetSize;   // EX2：任务管理器“内存”列用的就是这个
            public IntPtr SharedWorkingSetSize;    // EX2
        }

        [DllImport("psapi.dll", SetLastError = true)]
        static extern bool GetProcessMemoryInfo(IntPtr hProcess, ref MEMORY_COUNTERS counters, int cb);

        public StatsForm()
        {
            Text = "运行状态";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(300, 312);
            BackColor = Theme.Bg;
            Font = Theme.Ui(13.5f);
            Icon = Paths.LoadAppIcon();
            DoubleBuffered = true;

            var card = new Card { Location = new Point(14, 14), Size = new Size(272, 232) };
            Controls.Add(card);

            int y = 20;
            lblPid = Row(card, "进程 ID", ref y);
            lblCpu = Row(card, "CPU 占用", ref y);
            lblMem = Row(card, "内存占用", ref y);
            lblThreads = Row(card, "线程数", ref y);
            lblUptime = Row(card, "运行时长", ref y);

            var btnClose = Theme.Button("关闭", true);
            btnClose.Location = new Point(14, 258);
            btnClose.Size = new Size(272, 38);
            btnClose.Click += (s, e) => Close();
            Controls.Add(btnClose);

            lastCpu = proc.TotalProcessorTime;
            lastSample = DateTime.Now;

            timer = new Timer { Interval = 1000 };
            timer.Tick += (s, e) => Sample();
            timer.Start();
            Sample();
        }

        Label Row(Control parent, string title, ref int y)
        {
            var l = Theme.Label(title, 14f, false, Theme.Muted);
            l.Location = new Point(18, y);
            parent.Controls.Add(l);

            var v = new Label
            {
                Text = "-",
                AutoSize = false,
                Size = new Size(150, 26),
                Location = new Point(102, y - 3),
                TextAlign = ContentAlignment.MiddleRight,
                Font = Theme.Mono(17f),
                ForeColor = Theme.Text,
                BackColor = Color.Transparent
            };
            parent.Controls.Add(v);
            y += 44;
            return v;
        }

        void Sample()
        {
            try
            {
                proc.Refresh();
                var now = DateTime.Now;
                var cpu = proc.TotalProcessorTime;
                double ms = Math.Max(1, (now - lastSample).TotalMilliseconds);
                double pct = (cpu - lastCpu).TotalMilliseconds / ms / Environment.ProcessorCount * 100.0;
                lastCpu = cpu;
                lastSample = now;

                lblPid.Text = proc.Id.ToString();
                lblCpu.Text = pct.ToString("0.0") + " %";
                // 专用工作集（Private Working Set）：和任务管理器“内存”列同一个口径；
                // WorkingSet64 含共享页，会明显偏大，不能用。
                lblMem.Text = PrivateWorkingSetMb().ToString("0.0") + " MB";
                lblThreads.Text = proc.Threads.Count.ToString();
                // 启动时刻由 AppContext 记住（静态 8 字节）；满 24 小时归零重计，只在窗口打开时算
                var up = DateTime.Now - AppContext.StartedAt;
                if (up < TimeSpan.Zero) up = TimeSpan.Zero;
                up = TimeSpan.FromTicks(up.Ticks % TimeSpan.TicksPerDay);
                lblUptime.Text = string.Format("{0:00}:{1:00}:{2:00}", (int)up.TotalHours, up.Minutes, up.Seconds);
            }
            catch
            {
                lblCpu.Text = lblMem.Text = lblThreads.Text = lblUptime.Text = "N/A";
            }
        }

        /// <summary>专用工作集（MB）：优先 psapi 的 EX2.PrivateWorkingSetSize（= 任务管理器“内存”列），
        /// 老系统取不到就退回专用提交 PrivateMemorySize64。整条链路不加载性能计数器组件。</summary>
        double PrivateWorkingSetMb()
        {
            try
            {
                var c = new MEMORY_COUNTERS();
                c.cb = (uint)Marshal.SizeOf(typeof(MEMORY_COUNTERS));
                if (GetProcessMemoryInfo(proc.Handle, ref c, (int)c.cb))
                {
                    long ws = c.PrivateWorkingSetSize.ToInt64();
                    if (ws > 0) return ws / 1024.0 / 1024.0;
                    long pu = c.PrivateUsage.ToInt64();
                    if (pu > 0) return pu / 1024.0 / 1024.0;
                }
            }
            catch { }
            return proc.PrivateMemorySize64 / 1024.0 / 1024.0;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (timer != null) { timer.Stop(); timer.Dispose(); timer = null; }
            try { proc.Dispose(); } catch { }
            base.OnFormClosed(e);
        }
    }
}
