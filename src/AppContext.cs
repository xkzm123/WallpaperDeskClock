using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WallpaperDeskClock
{
    class AppContext : ApplicationContext
    {
        public Config Cfg = new Config();
        readonly List<ClockWindow> windows = new List<ClockWindow>();
        NotifyIcon tray;
        System.Windows.Forms.Timer tick;
        System.Windows.Forms.Timer displayWatch;
        SettingsForm settingsForm;
        StatsForm statsForm;
        WallpaperForm wallpaperForm;
        string lastDisplaySignature = "";
        bool exiting;

        public AppContext()
        {
            Cfg.BaseDir = Paths.BaseDir;
            string cfgPath = Paths.ConfigFile;
            Cfg.Load(cfgPath);
            if (Cfg.FilePath == null) Cfg.FilePath = cfgPath;

            Autostart.RepairIfNeeded();

            BuildWindows();
            SetupTray();
            CreateSettingsWindow(true);   // 任务栏入口：最小化出现，不弹窗

            tick = new System.Windows.Forms.Timer { Interval = 200 };
            tick.Tick += (s, e) => { foreach (var w in windows) w.Tick(); };
            tick.Start();

            // 显示器变化监听（拔插 / 分辨率变化）
            displayWatch = new System.Windows.Forms.Timer { Interval = 2000 };
            displayWatch.Tick += (s, e) => CheckDisplays();
            displayWatch.Start();

            ApplyWallpaper();
            Log("启动完成，显示器数=" + Screen.AllScreens.Length);
        }

        public IList<ClockWindow> Windows { get { return windows; } }

        void Log(string msg)
        {
            try
            {
                File.AppendAllText(Paths.LogFile,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + msg + "\r\n", Encoding.UTF8);
            }
            catch { }
        }

        // ---------- 窗口 ----------

        static string Sig()
        {
            var sb = new StringBuilder();
            foreach (var s in Screen.AllScreens)
                sb.Append(s.DeviceName).Append(s.Bounds.Width).Append('x').Append(s.Bounds.Height).Append(';');
            return sb.ToString();
        }

        void CheckDisplays()
        {
            var sig = Sig();
            if (sig == lastDisplaySignature) return;
            lastDisplaySignature = sig;
            Log("显示器变化: " + sig);
            RebuildWindows();
        }

        void RebuildWindows()
        {
            foreach (var w in windows.ToArray())
            {
                w.Hide();
                w.Dispose();
            }
            windows.Clear();
            BuildWindows();
            ApplyWallpaper();
        }

        void BuildWindows()
        {
            lastDisplaySignature = Sig();
            var screens = Screen.AllScreens;
            string mode = Cfg.S("displays", "all").Trim().ToLowerInvariant();

            var targets = new List<Screen>();
            if (mode == "all") targets.AddRange(screens);
            else
            {
                int idx;
                if (int.TryParse(mode, out idx) && idx >= 0 && idx < screens.Length) targets.Add(screens[idx]);
                else targets.AddRange(screens);
            }

            foreach (var scr in targets)
            {
                string key = scr.DeviceName.Replace("\\", "_").Replace(".", "");
                var layout = LayoutStore.Load(Cfg, key);
                var w = new ClockWindow(this, scr, layout);
                w.Show();
                windows.Add(w);
            }
        }

        public void SaveLayout(ClockWindow w)
        {
            LayoutStore.Save(Cfg, w.ScreenKey, w.LayoutData);
        }

        public void SaveAllLayouts()
        {
            foreach (var w in windows) LayoutStore.Save(Cfg, w.ScreenKey, w.LayoutData);
        }

        public void ApplyScaleToAll(float scale)
        {
            foreach (var w in windows)
            {
                w.LayoutData.Scale = scale;
                w.ReloadFromConfig();
            }
        }

        public void SetEditMode(bool on)
        {
            foreach (var w in windows) w.EditMode = on;
        }

        public void ResetLayouts()
        {
            foreach (var w in windows)
            {
                w.LayoutData.X = 0.575f;
                w.LayoutData.Y = 0.790f;
                w.LayoutData.Scale = 1.0f;
                w.LayoutData.TimeDX = w.LayoutData.TimeDY = 0f;
                w.LayoutData.DateDX = w.LayoutData.DateDY = 0f;
                w.ReloadFromConfig();
                LayoutStore.Save(Cfg, w.ScreenKey, w.LayoutData);
            }
        }

        /// <summary>设置面板改了显示器选择后重建窗口</summary>
        public void RebuildFromSettings()
        {
            RebuildWindows();
        }

        public void ReloadAll()
        {
            Cfg.Load(Cfg.FilePath);
            foreach (var w in windows) w.ReloadFromConfig();
            ApplyWallpaper();
        }

        // ---------- 壁纸 ----------

        public void ApplyWallpaper()
        {
            try
            {
                // 诊断：身份 / 探针
                try
                {
                    var id = System.Security.Principal.WindowsIdentity.GetCurrent();
                    var pr = new System.Security.Principal.WindowsPrincipal(id);
                    Log("诊断 identity=" + id.Name + " admin=" + pr.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator) + " cwd=" + Environment.CurrentDirectory);
                    int probe = Native.SystemParametersInfo(Native.SPI_SETDESKWALLPAPER, 0,
                        @"C:\Windows\Web\Wallpaper\Windows\img0.jpg", Native.SPIF_UPDATEINIFILE | Native.SPIF_SENDCHANGE);
                    Log("探针壁纸(系统自带) 结果=" + probe + " err=" + System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                }
                catch (Exception dex) { Log("诊断异常: " + dex.Message); }

                string src = Cfg.Resolve(Cfg.S("image", Paths.DefaultImage));
                if (!File.Exists(src)) { Log("壁纸不存在: " + src); return; }

                var scr = Screen.PrimaryScreen.Bounds;
                string target = src;

                if (Cfg.B("vignette", true))
                {
                    int alpha = Cfg.I("vignette_alpha", 78);
                    using (var img = Image.FromFile(src))
                    using (var bmp = new Bitmap(scr.Width, scr.Height, PixelFormat.Format32bppArgb))
                    {
                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            float s = Math.Max((float)scr.Width / img.Width, (float)scr.Height / img.Height);
                            int nw = (int)Math.Round(img.Width * s), nh = (int)Math.Round(img.Height * s);
                            g.DrawImage(img, new Rectangle((scr.Width - nw) / 2, (scr.Height - nh) / 2, nw, nh));

                            if (alpha > 0)
                            {
                                var rect = new RectangleF(scr.Width * 0.42f, scr.Height * 0.42f,
                                                          scr.Width * 1.16f, scr.Height * 1.16f);
                                using (var path = new GraphicsPath())
                                {
                                    path.AddEllipse(rect);
                                    using (var pgb = new PathGradientBrush(path))
                                    {
                                        pgb.CenterColor = Color.FromArgb(alpha, 0, 0, 0);
                                        pgb.SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) };
                                        pgb.FocusScales = new PointF(0.55f, 0.55f);
                                        g.FillPath(pgb, path);
                                    }
                                }
                            }
                        }
                        target = Paths.CacheWallpaper;
                        // 先写内存再落盘，确保 GDI+ 不残留文件句柄
                        using (var ms = new MemoryStream())
                        {
                            bmp.Save(ms, ImageFormat.Jpeg);
                            File.WriteAllBytes(target, ms.ToArray());
                        }
                    }
                }

                // 壁纸填充模式（注册表可能受限，失败不影响壁纸设置）
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
                    {
                        if (k != null)
                        {
                            k.SetValue("WallpaperStyle", "10", RegistryValueKind.String);
                            k.SetValue("TileWallpaper", "0", RegistryValueKind.String);
                        }
                    }
                }
                catch (Exception rex) { Log("壁纸样式注册表写入失败(已忽略): " + rex.Message); }

                int ok = 0, err = 0;
                for (int attempt = 0; attempt < 3 && ok == 0; attempt++)
                {
                    if (attempt > 0) System.Threading.Thread.Sleep(300);
                    ok = Native.SystemParametersInfo(Native.SPI_SETDESKWALLPAPER, 0, target,
                                                     Native.SPIF_UPDATEINIFILE | Native.SPIF_SENDCHANGE);
                    err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                }
                Log("设置桌面壁纸: " + target + " 结果=" + ok + " err=" + err);
            }
            catch (Exception ex) { Log("设置壁纸失败: " + ex.Message); }
        }

        // ---------- 托盘 ----------

        void SetupTray()
        {
            tray = new NotifyIcon
            {
                Icon = Paths.LoadAppIcon(),
                Text = "壁纸桌面时钟",
                Visible = true
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("设置…", null, (s, e) => ShowSettings());
            menu.Items.Add("壁纸库…", null, (s, e) => ShowWallpaper());
            menu.Items.Add("运行状态", null, (s, e) => ShowStats());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("重新应用桌面壁纸", null, (s, e) => ApplyWallpaper());
            menu.Items.Add("恢复默认布局", null, (s, e) =>
            {
                if (MessageBox.Show("确定把所有显示器上的时钟恢复到默认位置和大小？", "恢复默认布局",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    ResetLayouts();
                    if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.SyncFromApp();
                }
            });
            menu.Items.Add("打开配置目录", null, (s, e) =>
            {
                try { Process.Start("explorer.exe", Paths.ConfigDir); } catch { }
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (s, e) => ExitApp());

            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (s, e) => ShowSettings();
        }

        // ---------- 设置窗口（同时充当任务栏入口） ----------

        /// <summary>创建设置窗口；startMinimized=true 时最小化出现：任务栏有按钮，桌面不弹窗</summary>
        void CreateSettingsWindow(bool startMinimized)
        {
            if (settingsForm != null && !settingsForm.IsDisposed) return;
            settingsForm = new SettingsForm(this);
            settingsForm.FormClosing += OnSettingsClosing;
            settingsForm.Activated += (s, e) => { if (settingsForm != null) settingsForm.SyncFromApp(); };
            if (startMinimized) settingsForm.WindowState = FormWindowState.Minimized;
            settingsForm.Show();
        }

        /// <summary>关闭按钮 / Alt+F4 只是收回任务栏，程序继续运行</summary>
        void OnSettingsClosing(object sender, FormClosingEventArgs e)
        {
            if (exiting) return;
            e.Cancel = true;
            if (settingsForm != null) settingsForm.WindowState = FormWindowState.Minimized;
        }

        public void ShowSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed) CreateSettingsWindow(false);
            if (settingsForm.WindowState == FormWindowState.Minimized)
                settingsForm.WindowState = FormWindowState.Normal;
            settingsForm.Show();
            settingsForm.Activate();
            settingsForm.SyncFromApp();
        }

        public void ShowWallpaper()
        {
            if (wallpaperForm == null || wallpaperForm.IsDisposed)
            {
                wallpaperForm = new WallpaperForm(this);
                wallpaperForm.FormClosed += (s, e) => wallpaperForm = null;
                wallpaperForm.Show();
            }
            else
            {
                wallpaperForm.Activate();
            }
        }

        void ShowStats()
        {
            if (statsForm == null || statsForm.IsDisposed)
            {
                statsForm = new StatsForm();
                statsForm.FormClosed += (s, e) => statsForm = null;
                statsForm.Show();
            }
            else
            {
                statsForm.Activate();
            }
        }

        public void ExitApp()
        {
            exiting = true;
            SaveAllLayouts();
            tick.Stop();
            displayWatch.Stop();
            foreach (var w in windows.ToArray()) { w.Hide(); w.Dispose(); }
            windows.Clear();
            if (settingsForm != null && !settingsForm.IsDisposed)
            {
                settingsForm.Hide();
                settingsForm.Dispose();
                settingsForm = null;
            }
            if (wallpaperForm != null && !wallpaperForm.IsDisposed)
            {
                wallpaperForm.Hide();
                wallpaperForm.Dispose();
                wallpaperForm = null;
            }
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && tray != null) tray.Dispose();
            base.Dispose(disposing);
        }
    }
}
