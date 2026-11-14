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
        bool started;      // 启动阶段不重复应用壁纸
        bool trimming;     // 防重入：修剪过程中不再触发修剪

        public AppContext()
        {
            Cfg.BaseDir = Paths.BaseDir;
            string cfgPath = Paths.ConfigFile;
            Cfg.Load(cfgPath);
            if (Cfg.FilePath == null) Cfg.FilePath = cfgPath;

            Autostart.RepairIfNeeded();

            BuildWindows();
            SetupTray();

            // 设置窗口不预创建：它只在用户打开时存在，关闭即销毁（省下一整套 GDI 控件与字体）
            tick = new System.Windows.Forms.Timer { Interval = TickInterval() };
            tick.Tick += (s, e) =>
            {
                ApplyTickInterval();   // 配置被外部改动时也能立刻跟上（只读一次字典，代价可忽略）
                foreach (var w in windows) w.Tick();
            };
            tick.Start();

            // 显示器变化监听（拔插 / 分辨率变化）
            displayWatch = new System.Windows.Forms.Timer { Interval = 2000 };
            displayWatch.Tick += (s, e) => CheckDisplays();
            displayWatch.Start();

            started = true;
            ApplyWallpaper();              // 全流程只应用一次
            Log("启动完成，显示器数=" + Screen.AllScreens.Length);

            TrimMemory();                  // 启动完成：回收初始化期的大图与 GDI 残留
        }

        // ---------- 内存 ----------

        /// <summary>
        /// 压缩托管堆（含大对象堆 LOH）并把物理页交还系统。
        /// 只在低频时机调用：启动完成 / 壁纸应用完毕 / 弹窗关闭。定时器里绝不调用。
        /// </summary>
        public void TrimMemory()
        {
            if (trimming) return;
            trimming = true;
            try
            {
                // compact: true 才是真正压缩 LOH（.NET 4.5+）；先收一轮、等终结器跑完再收一轮
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);

                using (var p = Process.GetCurrentProcess())
                    Native.SetProcessWorkingSetSize(p.Handle, new IntPtr(-1), new IntPtr(-1));
            }
            catch { }
            finally { trimming = false; }
        }

        /// <summary>弹窗关闭后再修剪：投递到消息队列，等窗体 Dispose 完成、对象真正不可达之后再回收</summary>
        void TrimAfterUi()
        {
            var ctx = System.Threading.SynchronizationContext.Current;
            if (ctx == null) { TrimMemory(); return; }
            ctx.Post(delegate { TrimMemory(); }, null);
        }

        /// <summary>不显示秒时没必要 200 ms 醒一次，改成 1 s</summary>
        int TickInterval()
        {
            return Cfg.B("showseconds", true) ? 200 : 1000;
        }

        void ApplyTickInterval()
        {
            if (tick == null) return;
            int ms = TickInterval();
            if (tick.Interval != ms) tick.Interval = ms;
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
            if (started) ApplyWallpaper();   // 启动阶段由构造函数统一应用，避免重复烘焙大图
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
            ApplyTickInterval();
            foreach (var w in windows) w.ReloadFromConfig();
            ApplyWallpaper();
        }

        // ---------- 壁纸 ----------

        public void ApplyWallpaper()
        {
            try
            {
                // 原来的 identity / img0.jpg 试写探针已移除：它会把系统壁纸短暂改掉，
                // 还让每次应用壁纸都多一次 4K 级的重绘与日志写入。
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
                        // 直接落盘：不再经过 MemoryStream + ToArray()，省掉一整份 JPEG 字节数组
                        // （1080p 壁纸约 250 KB，4K 壁纸可达数 MB，且会直接进 LOH）
                        bmp.Save(target, ImageFormat.Jpeg);
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
            finally
            {
                // 壁纸应用完毕：大图、Graphics、GraphicsPath 都已释放，回收 LOH 并交还物理页
                ApplyTickInterval();
                TrimMemory();
            }
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

        // ---------- 弹窗（懒加载：打开才创建，关闭即销毁） ----------

        /// <summary>设置窗口按需创建：平时根本不存在，不占控件 / 字体 / GDI 句柄</summary>
        void CreateSettingsWindow()
        {
            if (settingsForm != null && !settingsForm.IsDisposed) return;
            settingsForm = new SettingsForm(this);
            settingsForm.FormClosing += OnSettingsClosing;
            settingsForm.FormClosed += OnSettingsClosed;
            settingsForm.Activated += (s, e) => { if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.SyncFromApp(); };
            settingsForm.Show();
        }

        /// <summary>关闭按钮 / Alt+F4 / 点 ✕：不再 e.Cancel 拦截，允许窗体真正关闭</summary>
        void OnSettingsClosing(object sender, FormClosingEventArgs e)
        {
            // 不做任何拦截：窗体随后由 WinForms 正常 Close + Dispose
        }

        void OnSettingsClosed(object sender, FormClosedEventArgs e)
        {
            settingsForm = null;     // 丢掉最后一个强引用
            if (exiting) return;     // 退出流程里不再做整代压缩
            TrimAfterUi();           // 窗体 Dispose 完成后再压缩堆、交还物理页
        }

        /// <summary>关闭设置窗口（等价于点标题栏 ✕）：真正 Close，而不是 Hide</summary>
        public void CloseSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed) return;
            if (settingsForm.WindowState == FormWindowState.Minimized)
                settingsForm.WindowState = FormWindowState.Normal;
            settingsForm.Close();
        }

        public void ShowSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed) CreateSettingsWindow();
            settingsForm.Show();
            if (settingsForm.WindowState == FormWindowState.Minimized)
                settingsForm.WindowState = FormWindowState.Normal;
            settingsForm.Activate();
            settingsForm.SyncFromApp();
        }

        public void ShowWallpaper()
        {
            if (wallpaperForm == null || wallpaperForm.IsDisposed)
            {
                wallpaperForm = new WallpaperForm(this);
                wallpaperForm.FormClosed += (s, e) => { wallpaperForm = null; TrimAfterUi(); };
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
                statsForm.FormClosed += (s, e) => { statsForm = null; TrimAfterUi(); };
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
            if (statsForm != null && !statsForm.IsDisposed)
            {
                statsForm.Hide();
                statsForm.Dispose();
                statsForm = null;
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
