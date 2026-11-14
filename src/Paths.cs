using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WallpaperDeskClock
{
    /// <summary>
    /// 统一的运行目录约定：发布根目录只保留 exe / 图标 / README，
    /// 其余按用途分到 config、resources、logs、cache，启动时缺什么补什么。
    /// </summary>
    static class Paths
    {
        public static string BaseDir;          // exe 所在目录（发布根目录）
        public static string ConfigDir;        // config/
        public static string ConfigFile;       // config/config.ini
        public static string ResourceDir;      // resources/
        public static string LogDir;           // logs/
        public static string LogFile;          // logs/wallpaperdeskclock.log
        public static string CacheDir;         // cache/
        public static string CacheWallpaper;   // cache/_wallpaper_active.jpg
        public static string IconFile;         // WallpaperDeskClock.ico

        /// <summary>默认壁纸：相对 exe 目录</summary>
        public const string DefaultImage = "resources/wallpaper.jpg";

        public static void Init()
        {
            try { BaseDir = Path.GetDirectoryName(Application.ExecutablePath); }
            catch { BaseDir = Environment.CurrentDirectory; }

            ConfigDir = Ensure(Path.Combine(BaseDir, "config"));
            ResourceDir = Ensure(Path.Combine(BaseDir, "resources"));
            LogDir = Ensure(Path.Combine(BaseDir, "logs"));
            CacheDir = Ensure(Path.Combine(BaseDir, "cache"));

            ConfigFile = Path.Combine(ConfigDir, "config.ini");
            LogFile = Path.Combine(LogDir, "wallpaperdeskclock.log");
            CacheWallpaper = Path.Combine(CacheDir, "_wallpaper_active.jpg");
            IconFile = Path.Combine(BaseDir, "WallpaperDeskClock.ico");
        }

        static string Ensure(string dir)
        {
            try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); }
            catch { }
            return dir;
        }

        /// <summary>任务栏 / 托盘 / 窗口统一图标：优先根目录 ico，其次 exe 内嵌图标，最后系统默认</summary>
        public static Icon LoadAppIcon()
        {
            try
            {
                if (File.Exists(IconFile)) return new Icon(IconFile, 32, 32);
            }
            catch { }
            try
            {
                var ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (ico != null) return ico;
            }
            catch { }
            return SystemIcons.Application;
        }
    }
}
