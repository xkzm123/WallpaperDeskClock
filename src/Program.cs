using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WallpaperDeskClock
{
    #region Win32

    static class Native
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "SystemParametersInfoW")]
        public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc,
            int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)] public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", SetLastError = true)] public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_NOZORDER = 0x0004;

        public const int SPI_SETDESKWALLPAPER = 20;
        public const int SPIF_UPDATEINIFILE = 0x01;
        public const int SPIF_SENDCHANGE = 0x02;

        public const int ULW_ALPHA = 0x02;
        public const byte AC_SRC_OVER = 0x00;
        public const byte AC_SRC_ALPHA = 0x01;

        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_NCLBUTTONDOWN = 0x00A1;
        public const int HTCAPTION = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int CX, CY; public SIZE(int x, int y) { CX = x; CY = y; } }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
        }
    }

    #endregion

    #region 配置

    class Config
    {
        readonly Dictionary<string, string> kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string BaseDir;
        public string FilePath;

        public void Load(string path)
        {
            FilePath = path;
            kv.Clear();
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
            {
                var t = line.Trim();
                if (t.Length == 0 || t[0] == '#' || t[0] == ';' || t[0] == '[') continue;
                int i = t.IndexOf('=');
                if (i <= 0) continue;
                kv[t.Substring(0, i).Trim()] = t.Substring(i + 1).Trim();
            }
        }

        /// <summary>首次运行时写出带注释的默认配置（目录由 Paths 保证存在）</summary>
        public static void EnsureDefault(string path)
        {
            try
            {
                if (File.Exists(path)) return;
                File.WriteAllText(path, DefaultText, new UTF8Encoding(false));
            }
            catch { }
        }

        static readonly string[] DefaultLines = new string[]
        {
            "# ============================================================",
            "#  壁纸桌面时钟 配置文件",
            "#  位置：config/config.ini（首次运行自动生成）",
            "#  布局相关项由程序自动写入，无需手工编辑",
            "# ============================================================",
            "",
            "# 背景图（相对 exe 目录或绝对路径）",
            "image=resources/wallpaper.jpg",
            "",
            "# 壁纸库目录（留空 = resources/）",
            "library=",
            "",
            "# 显示位置：all = 全部显示器 / 0 = 显示器1 / 1 = 显示器2",
            "displays=all",
            "",
            "# 时间格式：use12h=0 -> HH:mm / use12h=1 -> hh:mm",
            "use12h=0",
            "dateformat=yyyy.MM.dd",
            "",
            "# 文字颜色与阴影",
            "color=#F2F2F2",
            "shadow=1",
            "",
            "# 右下角柔和压暗（烘焙进壁纸，输出到 cache/）",
            "vignette=1",
            "vignette_alpha=78",
            "",
            "# 显示开关",
            "showdate=1",
            "showweek=1",
            "showseconds=1",
            "",
            "# 窗口内部相对布局（一般不用改）",
            "rel_right=0.831",
            "rel_clock_y=0.323",
            "rel_date_y=0.695",
            "gap_seconds=14",
            "gap_week=26",
            "seconds_dy=10",
            "",
            "# 字体与基础字号（像素，实际字号 = 基础字号 x 缩放）",
            "font_clock=Bahnschrift",
            "size_clock=64",
            "font_date=Consolas",
            "size_date=18",
            "font_week=SimHei",
            "size_week=18",
            "font_seconds=Consolas",
            "size_seconds=18",
        };

        public static string DefaultText
        {
            get { return string.Join(Environment.NewLine, DefaultLines) + Environment.NewLine; }
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# ============================================================");
            sb.AppendLine("#  壁纸桌面时钟 配置文件（程序会自动改写布局相关项）");
            sb.AppendLine("# ============================================================");
            sb.AppendLine();
            foreach (var kvp in kv) sb.AppendLine(kvp.Key + "=" + kvp.Value);
            try { File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false)); }
            catch { }
        }

        public void Set(string k, string v) { kv[k] = v; }
        public string S(string k, string def) { string v; return kv.TryGetValue(k, out v) && v.Length > 0 ? v : def; }
        public float F(string k, float def)
        {
            string v; float r;
            return kv.TryGetValue(k, out v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out r) ? r : def;
        }
        public int I(string k, int def)
        {
            string v; int r;
            return kv.TryGetValue(k, out v) && int.TryParse(v, out r) ? r : def;
        }
        public bool B(string k, bool def)
        {
            string v;
            if (!kv.TryGetValue(k, out v)) return def;
            v = v.Trim().ToLowerInvariant();
            return v == "1" || v == "true" || v == "yes" || v == "on";
        }
        public Color C(string k, Color def)
        {
            string v;
            if (!kv.TryGetValue(k, out v)) return def;
            try
            {
                v = v.Trim().TrimStart('#');
                if (v.Length == 6)
                    return Color.FromArgb(255, Convert.ToInt32(v.Substring(0, 2), 16),
                                               Convert.ToInt32(v.Substring(2, 2), 16),
                                               Convert.ToInt32(v.Substring(4, 2), 16));
                if (v.Length == 8)
                    return Color.FromArgb(Convert.ToInt32(v.Substring(0, 2), 16),
                                          Convert.ToInt32(v.Substring(2, 2), 16),
                                          Convert.ToInt32(v.Substring(4, 2), 16),
                                          Convert.ToInt32(v.Substring(6, 2), 16));
            }
            catch { }
            return def;
        }
        public string Resolve(string p) { return Path.IsPathRooted(p) ? p : Path.Combine(BaseDir, p); }
    }

    #endregion

    #region 布局

    /// <summary>单个显示器上的时钟布局（位置用屏幕比例保存，分辨率变化不失效）</summary>
    class LayoutEntry
    {
        public float X = 0.575f;
        public float Y = 0.790f;
        public float Scale = 1.0f;

        /// <summary>时间块 / 日期块相对默认位置的偏移（占窗口宽高的比例）</summary>
        public float TimeDX, TimeDY, DateDX, DateDY;

        public const float BaseW = 0.425f;
        public const float BaseH = 0.210f;

        public LayoutEntry Clone()
        {
            return new LayoutEntry
            {
                X = X, Y = Y, Scale = Scale,
                TimeDX = TimeDX, TimeDY = TimeDY, DateDX = DateDX, DateDY = DateDY
            };
        }
    }

    static class LayoutStore
    {
        public static LayoutEntry Load(Config cfg, string screenKey)
        {
            var e = new LayoutEntry();
            e.X = cfg.F("layout." + screenKey + ".x", e.X);
            e.Y = cfg.F("layout." + screenKey + ".y", e.Y);
            e.Scale = cfg.F("layout." + screenKey + ".scale", e.Scale);
            e.TimeDX = cfg.F("layout." + screenKey + ".tdx", 0f);
            e.TimeDY = cfg.F("layout." + screenKey + ".tdy", 0f);
            e.DateDX = cfg.F("layout." + screenKey + ".ddx", 0f);
            e.DateDY = cfg.F("layout." + screenKey + ".ddy", 0f);
            return e;
        }

        public static void Save(Config cfg, string screenKey, LayoutEntry e)
        {
            cfg.Set("layout." + screenKey + ".x", e.X.ToString("0.#####", CultureInfo.InvariantCulture));
            cfg.Set("layout." + screenKey + ".y", e.Y.ToString("0.#####", CultureInfo.InvariantCulture));
            cfg.Set("layout." + screenKey + ".scale", e.Scale.ToString("0.###", CultureInfo.InvariantCulture));
            cfg.Set("layout." + screenKey + ".tdx", e.TimeDX.ToString("0.#####", CultureInfo.InvariantCulture));
            cfg.Set("layout." + screenKey + ".tdy", e.TimeDY.ToString("0.#####", CultureInfo.InvariantCulture));
            cfg.Set("layout." + screenKey + ".ddx", e.DateDX.ToString("0.#####", CultureInfo.InvariantCulture));
            cfg.Set("layout." + screenKey + ".ddy", e.DateDY.ToString("0.#####", CultureInfo.InvariantCulture));
            cfg.Save();
        }

        public static void Reset(Config cfg, string screenKey)
        {
            cfg.Set("layout." + screenKey + ".x", "0.575");
            cfg.Set("layout." + screenKey + ".y", "0.790");
            cfg.Set("layout." + screenKey + ".scale", "1");
            cfg.Set("layout." + screenKey + ".tdx", "0");
            cfg.Set("layout." + screenKey + ".tdy", "0");
            cfg.Set("layout." + screenKey + ".ddx", "0");
            cfg.Set("layout." + screenKey + ".ddy", "0");
            cfg.Save();
        }
    }

    #endregion

    #region 开机自启

    static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string AppName = "WallpaperDeskClock";

        public static string ExePath { get { return Application.ExecutablePath; } }

        public static bool IsEnabled()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    var v = k == null ? null : k.GetValue(AppName) as string;
                    return v != null && v.Length > 0;
                }
            }
            catch { return false; }
        }

        /// <summary>注册表里的路径是否仍指向当前 exe（迁移目录后可能失效）</summary>
        public static bool PointsToCurrentExe()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    var v = k == null ? null : k.GetValue(AppName) as string;
                    if (string.IsNullOrEmpty(v)) return false;
                    var p = v.Trim().Trim('"');
                    return string.Equals(Path.GetFullPath(p), Path.GetFullPath(ExePath), StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return false; }
        }

        public static void Enable()
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                    if (k != null) k.SetValue(AppName, "\"" + ExePath + "\"", RegistryValueKind.String);
            }
            catch { }
        }

        public static void Disable()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                    if (k != null) k.DeleteValue(AppName, false);
            }
            catch { }
        }

        /// <summary>启动时自愈：已开启自启但路径过期 → 改写为当前路径</summary>
        public static void RepairIfNeeded()
        {
            if (IsEnabled() && !PointsToCurrentExe()) Enable();
        }
    }

    #endregion

    #region 程序入口

    static class Program
    {
        public static AppContext App;

        [STAThread]
        static void Main()
        {
            Paths.Init();
            Config.EnsureDefault(Paths.ConfigFile);

            // 启动最早期探针：验证进程本身是否有壁纸设置权限（结果写入 logs/probe.log）
            try
            {
                int r0 = Native.SystemParametersInfo(Native.SPI_SETDESKWALLPAPER, 0,
                    @"C:\Windows\Web\Wallpaper\Windows\img0.jpg", Native.SPIF_UPDATEINIFILE | Native.SPIF_SENDCHANGE);
                File.WriteAllText(Paths.ProbeLog,
                    "early probe=" + r0 + " err=" + Marshal.GetLastWin32Error() + Environment.NewLine);
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Paths.ProbeLog, "probe ex: " + ex.Message); } catch { }
            }

            // DPI 由 manifest 声明，这里不再调用 SetProcessDPIAware

            bool created;
            using (var mutex = new Mutex(true, "WallpaperDeskClock_SingleInstance_v2", out created))
            {
                if (!created)
                {
                    MessageBox.Show("壁纸桌面时钟已在运行，请查看系统托盘图标。", "壁纸桌面时钟",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                App = new AppContext();
                Application.Run(App);
            }
        }
    }

    #endregion
}
