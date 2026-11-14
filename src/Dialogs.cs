using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace WallpaperDeskClock
{
    /// <summary>设置面板：开机自启、显示器选择、自定义布局、字号缩放</summary>
    class SettingsForm : Form
    {
        readonly AppContext app;
        CheckBox chkAutostart;
        ComboBox cboDisplay;
        CheckBox chkEdit;
        TrackBar trkScale;
        Label lblScale;
        Label lblHint;
        bool loading;

        public SettingsForm(AppContext app)
        {
            this.app = app;
            Text = "壁纸桌面时钟 - 设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(430, 430);
            Font = new Font("Microsoft YaHei", 9.5f);
            BackColor = Color.FromArgb(250, 250, 252);

            int y = 16;

            var lblTitle = new Label
            {
                Text = "壁纸桌面时钟",
                Font = new Font("Microsoft YaHei", 14f, FontStyle.Bold),
                Location = new Point(20, y),
                AutoSize = true
            };
            Controls.Add(lblTitle);
            y += 46;

            // 开机自启
            chkAutostart = new CheckBox
            {
                Text = "开机自动启动（默认关闭）",
                Location = new Point(24, y),
                AutoSize = true
            };
            chkAutostart.CheckedChanged += (s, e) =>
            {
                if (loading) return;
                if (chkAutostart.Checked)
                {
                    Autostart.Enable();
                    lblHint.Text = "已写入启动项：" + Autostart.ExePath;
                }
                else
                {
                    Autostart.Disable();
                    lblHint.Text = "已移除启动项";
                }
            };
            Controls.Add(chkAutostart);
            y += 34;

            var sep1 = new Label { BorderStyle = BorderStyle.Fixed3D, Location = new Point(20, y), Size = new Size(390, 2) };
            Controls.Add(sep1);
            y += 14;

            // 显示器
            Controls.Add(new Label { Text = "显示位置", Location = new Point(24, y), AutoSize = true });
            y += 26;

            cboDisplay = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(24, y),
                Width = 380
            };
            cboDisplay.Items.Add("全部显示器");
            var screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
                cboDisplay.Items.Add(string.Format("显示器 {0}  ({1} × {2})",
                    i + 1, screens[i].Bounds.Width, screens[i].Bounds.Height));
            cboDisplay.SelectedIndexChanged += (s, e) =>
            {
                if (loading) return;
                int idx = cboDisplay.SelectedIndex;
                app.Cfg.Set("displays", idx <= 0 ? "all" : (idx - 1).ToString());
                app.Cfg.Save();
                app.RebuildFromSettings();
            };
            Controls.Add(cboDisplay);
            y += 36;

            // 自定义布局
            chkEdit = new CheckBox
            {
                Text = "自定义布局模式（勾选后可用鼠标拖动时钟）",
                Location = new Point(24, y),
                AutoSize = true
            };
            chkEdit.CheckedChanged += (s, e) =>
            {
                if (loading) return;
                app.SetEditMode(chkEdit.Checked);
                lblHint.Text = chkEdit.Checked
                    ? "已进入编辑模式：拖动时钟到想要的位置，取消勾选后自动保存"
                    : "编辑模式已关闭，布局已保存";
            };
            Controls.Add(chkEdit);
            y += 30;

            var lblEditHint = new Label
            {
                Text = "提示：编辑模式下时钟窗口会置顶并显示蓝色边框；\n拖动结束后取消勾选即保存位置。",
                Location = new Point(44, y),
                Size = new Size(360, 40),
                ForeColor = Color.FromArgb(110, 110, 118)
            };
            Controls.Add(lblEditHint);
            y += 46;

            var sep2 = new Label { BorderStyle = BorderStyle.Fixed3D, Location = new Point(20, y), Size = new Size(390, 2) };
            Controls.Add(sep2);
            y += 14;

            // 字号
            Controls.Add(new Label { Text = "字号 / 缩放", Location = new Point(24, y), AutoSize = true });
            lblScale = new Label
            {
                Text = "1.00×",
                Location = new Point(340, y - 1),
                AutoSize = true,
                Font = new Font("Microsoft YaHei", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 110, 220)
            };
            Controls.Add(lblScale);
            y += 26;

            trkScale = new TrackBar
            {
                Minimum = 50,
                Maximum = 250,
                TickFrequency = 25,
                SmallChange = 5,
                LargeChange = 10,
                Location = new Point(20, y),
                Width = 390,
                Value = 100
            };
            trkScale.Scroll += (s, e) =>
            {
                if (loading) return;
                float sc = trkScale.Value / 100f;
                lblScale.Text = sc.ToString("0.00") + "×";
                app.ApplyScaleToAll(sc);
            };
            Controls.Add(trkScale);
            y += 62;

            var btnReset = new Button
            {
                Text = "恢复默认布局",
                Location = new Point(24, y),
                Size = new Size(140, 32)
            };
            btnReset.Click += (s, e) =>
            {
                app.ResetLayouts();
                SyncFromApp();
                lblHint.Text = "已恢复默认布局";
            };
            Controls.Add(btnReset);

            var btnClose = new Button
            {
                Text = "关闭",
                Location = new Point(310, y),
                Size = new Size(100, 32),
                DialogResult = DialogResult.OK
            };
            btnClose.Click += (s, e) => Close();
            Controls.Add(btnClose);
            y += 46;

            lblHint = new Label
            {
                Text = "",
                Location = new Point(24, y),
                Size = new Size(386, 40),
                ForeColor = Color.FromArgb(70, 120, 70)
            };
            Controls.Add(lblHint);

            SyncFromApp();
        }

        public void SyncFromApp()
        {
            loading = true;
            try
            {
                chkAutostart.Checked = Autostart.IsEnabled();

                string mode = app.Cfg.S("displays", "all").Trim().ToLowerInvariant();
                int idx = 0;
                if (mode != "all")
                {
                    int n;
                    if (int.TryParse(mode, out n) && n >= 0 && n < Screen.AllScreens.Length) idx = n + 1;
                }
                if (idx >= cboDisplay.Items.Count) idx = 0;
                cboDisplay.SelectedIndex = idx;

                float scale = 1f;
                if (app.Windows.Count > 0) scale = app.Windows[0].LayoutData.Scale;
                trkScale.Value = (int)Math.Round(Math.Max(0.5f, Math.Min(2.5f, scale)) * 100);
                lblScale.Text = (trkScale.Value / 100f).ToString("0.00") + "×";
            }
            finally { loading = false; }
        }
    }

    /// <summary>资源占用 / 运行状态</summary>
    class StatsForm : Form
    {
        Label lblCpu, lblMem, lblThreads, lblUptime, lblHandle;
        Button btnRefresh;
        CheckBox chkAuto;
        System.Windows.Forms.Timer timer;
        TimeSpan lastCpu;
        DateTime lastSample = DateTime.Now;
        readonly DateTime startTime = DateTime.Now;
        readonly Process proc = Process.GetCurrentProcess();

        public StatsForm()
        {
            Text = "资源占用 / 运行状态";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(320, 230);
            Font = new Font("Microsoft YaHei", 9.5f);
            BackColor = Color.FromArgb(250, 250, 252);

            int y = 16;
            lblCpu = MakeRow("CPU 占用", ref y);
            lblMem = MakeRow("内存占用", ref y);
            lblThreads = MakeRow("线程数", ref y);
            lblHandle = MakeRow("句柄数", ref y);
            lblUptime = MakeRow("运行时长", ref y);

            btnRefresh = new Button { Text = "刷新", Location = new Point(20, y + 10), Size = new Size(90, 30) };
            btnRefresh.Click += (s, e) => Sample();
            Controls.Add(btnRefresh);

            chkAuto = new CheckBox { Text = "自动刷新(1s)", Location = new Point(122, y + 15), AutoSize = true, Checked = true };
            chkAuto.CheckedChanged += (s, e) => { if (timer != null) timer.Enabled = chkAuto.Checked; };
            Controls.Add(chkAuto);

            var btnClose = new Button { Text = "关闭", Location = new Point(230, y + 10), Size = new Size(70, 30) };
            btnClose.Click += (s, e) => Close();
            Controls.Add(btnClose);

            lastCpu = proc.TotalProcessorTime;
            lastSample = DateTime.Now;

            timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += (s, e) => Sample();
            timer.Start();

            Sample();
        }

        Label MakeRow(string title, ref int y)
        {
            var l = new Label { Text = title, Location = new Point(20, y), AutoSize = true, ForeColor = Color.FromArgb(100, 100, 108) };
            Controls.Add(l);
            var v = new Label
            {
                Text = "-",
                Location = new Point(120, y - 1),
                AutoSize = true,
                Font = new Font("Consolas", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 90, 200)
            };
            Controls.Add(v);
            y += 30;
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

                lblCpu.Text = pct.ToString("0.0") + " %";
                lblMem.Text = (proc.WorkingSet64 / 1024.0 / 1024.0).ToString("0.0") + " MB";
                lblThreads.Text = proc.Threads.Count.ToString();
                lblHandle.Text = proc.HandleCount.ToString();
                var up = DateTime.Now - startTime;
                lblUptime.Text = string.Format("{0:00}:{1:00}:{2:00}", (int)up.TotalHours, up.Minutes, up.Seconds);
            }
            catch
            {
                lblCpu.Text = lblMem.Text = lblThreads.Text = lblHandle.Text = lblUptime.Text = "N/A";
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (timer != null) timer.Stop();
            base.OnFormClosed(e);
        }
    }
}
