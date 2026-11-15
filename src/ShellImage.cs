using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WallpaperDeskClock
{
    /// <summary>
    /// 走 Windows Shell 的图像工厂拿缩略图 / 预览 / 壁纸尺寸位图。
    /// 解码发生在 Explorer 的缩略图提供程序里，本进程只拿到目标尺寸的 HBITMAP，
    /// 因此不会再出现「打开壁纸库 = 把 4K 原图整张解码进内存」的峰值。
    /// </summary>
    static class ShellImage
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public int cx, cy;
            public SIZE(int w, int h) { cx = w; cy = h; }
        }

        [Flags]
        public enum SIIGBF
        {
            NONE = 0x00,
            BIGGERSIZEOK = 0x01,
            MEMORYONLY = 0x02,
            ICONONLY = 0x04,
            THUMBNAILONLY = 0x08,
            INCACHEONLY = 0x10,
            CROPTOSQUARE = 0x20,
            WIDETHUMBNAILS = 0x40,
            ICONBACKGROUND = 0x80,
            SCALEUP = 0x100
        }

        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItemImageFactory
        {
            void GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
        }

        static readonly Guid IID_IShellItemImageFactory = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        static extern void SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath, IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi,
            uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);
        [DllImport("gdi32.dll")] static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint lines,
            IntPtr bits, ref BITMAPINFO bmi, uint usage);
        [DllImport("gdi32.dll")] static extern int GetObject(IntPtr h, int cb, out BITMAP bm);
        [DllImport("gdi32.dll")] static extern bool StretchBlt(IntPtr hdcDst, int x, int y, int w, int h,
            IntPtr hdcSrc, int sx, int sy, int sw, int sh, uint rop);
        [DllImport("gdi32.dll")] static extern int SetStretchBltMode(IntPtr hdc, int mode);
        [DllImport("gdi32.dll")] static extern bool SetBrushOrgEx(IntPtr hdc, int x, int y, IntPtr prev);

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAP
        {
            public int bmType, bmWidth, bmHeight, bmWidthBytes;
            public ushort bmPlanes, bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public int bmiColors;
        }

        /// <summary>缩略图 / 预览：等比填充到 w×h 的 24bpp 位图，失败返回 null</summary>
        public static Bitmap Fill(string path, int w, int h, Color background)
        {
            return Render(path, w, h, background, true);
        }

        /// <summary>
        /// 烘焙壁纸：Shell 出图后直接用 GetDIBits 写进我们自己的 24bpp DIB，
        /// 全程只有「Shell 那张 HBITMAP + 目标 DIB」两份缓冲，没有 GDI+ 的整屏拷贝。
        /// 比例与目标不一致（Shell 会等比缩进框内）时返回 false，交调用方兜底。
        /// </summary>
        public static bool Bake(string path, string outFile, int w, int h, Action<Graphics> paint)
        {
            if (w <= 0 || h <= 0 || string.IsNullOrEmpty(outFile)) return false;

            IntPtr hbm = IntPtr.Zero, dc = IntPtr.Zero, dib = IntPtr.Zero;
            IShellItemImageFactory factory = null;
            try
            {
                SHCreateItemFromParsingName(path, IntPtr.Zero, IID_IShellItemImageFactory, out factory);
                factory.GetImage(new SIZE(w, h), SIIGBF.BIGGERSIZEOK | SIIGBF.SCALEUP, out hbm);
                if (hbm == IntPtr.Zero) return false;

                BITMAP bm;
                if (GetObject(hbm, Marshal.SizeOf(typeof(BITMAP)), out bm) == 0) return false;
                if (bm.bmWidth <= 0 || bm.bmHeight <= 0) return false;

                int stride = ((w * 3 + 3) / 4) * 4;
                var bmi = new BITMAPINFO();
                bmi.bmiHeader.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bmi.bmiHeader.biWidth = w;
                bmi.bmiHeader.biHeight = -h;          // 顶朝下
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 24;
                bmi.bmiHeader.biCompression = 0;
                bmi.bmiHeader.biSizeImage = stride * h;

                dc = CreateCompatibleDC(IntPtr.Zero);
                IntPtr bits;
                dib = CreateDIBSection(dc, ref bmi, 0, out bits, IntPtr.Zero, 0);
                if (dib == IntPtr.Zero || bits == IntPtr.Zero) return false;

                IntPtr old = SelectObject(dc, dib);
                bool copied;
                if (bm.bmWidth == w && bm.bmHeight == h)
                {
                    copied = GetDIBits(dc, hbm, 0, (uint)h, bits, ref bmi, 0) != 0;
                }
                else
                {
                    // Shell 等比缩进框内（比例不同）→ 用 GDI 拉伸裁剪补齐，避免整图再解码一次
                    copied = CoverBlit(dc, hbm, bm.bmWidth, bm.bmHeight, w, h);
                }
                SelectObject(dc, old);
                if (!copied) return false;

                DeleteObject(hbm);
                hbm = IntPtr.Zero;    // Shell 那张可以先走了，剩下只有目标 DIB

                using (var canvas = new Bitmap(w, h, stride, PixelFormat.Format24bppRgb, bits))
                {
                    if (paint != null)
                        using (var g = Graphics.FromImage(canvas)) paint(g);
                    canvas.Save(outFile, ImageFormat.Jpeg);
                }
                return true;
            }
            catch { return false; }
            finally
            {
                if (hbm != IntPtr.Zero) { try { DeleteObject(hbm); } catch { } }
                if (dib != IntPtr.Zero) { try { DeleteObject(dib); } catch { } }
                if (dc != IntPtr.Zero) { try { DeleteDC(dc); } catch { } }
                if (factory != null) { try { Marshal.ReleaseComObject(factory); } catch { } }
            }
        }

        /// <summary>把源 HBITMAP 按“填充”方式拉伸画进目标 DC（HALFTONE 走的是面积采样，缩小质量够用）</summary>
        static bool CoverBlit(IntPtr dstDc, IntPtr hbm, int bw, int bh, int w, int h)
        {
            IntPtr srcDc = CreateCompatibleDC(dstDc);
            if (srcDc == IntPtr.Zero) return false;
            IntPtr old = SelectObject(srcDc, hbm);
            try
            {
                float s = Math.Max((float)w / bw, (float)h / bh);
                int sw = Math.Max(1, (int)Math.Round(w / s));
                int sh = Math.Max(1, (int)Math.Round(h / s));
                int sx = (bw - sw) / 2, sy = (bh - sh) / 2;

                SetStretchBltMode(dstDc, 4);          // HALFTONE
                SetBrushOrgEx(dstDc, 0, 0, IntPtr.Zero);
                return StretchBlt(dstDc, 0, 0, w, h, srcDc, sx, sy, sw, sh, 0x00CC0020); // SRCCOPY
            }
            finally
            {
                SelectObject(srcDc, old);
                DeleteDC(srcDc);
            }
        }

        static Bitmap Render(string path, int w, int h, Color background, bool thumbnailOnly)
        {
            if (w <= 0 || h <= 0) return null;

            IntPtr hbm = IntPtr.Zero;
            IShellItemImageFactory factory = null;
            try
            {
                SHCreateItemFromParsingName(path, IntPtr.Zero, IID_IShellItemImageFactory, out factory);

                var flags = SIIGBF.BIGGERSIZEOK | SIIGBF.SCALEUP;
                if (thumbnailOnly) flags |= SIIGBF.THUMBNAILONLY;

                factory.GetImage(new SIZE(w, h), flags, out hbm);
                if (hbm == IntPtr.Zero) return null;

                // FromHbitmap 会整份拷贝，拷完立刻删句柄，别让两份同时活着
                Bitmap src = null;
                try
                {
                    src = Image.FromHbitmap(hbm);
                    DeleteObject(hbm);
                    hbm = IntPtr.Zero;

                    var dst = new Bitmap(w, h, PixelFormat.Format24bppRgb);
                    try
                    {
                        using (var g = Graphics.FromImage(dst))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.Clear(background);
                            float s = Math.Max((float)w / src.Width, (float)h / src.Height);
                            int nw = (int)Math.Round(src.Width * s);
                            int nh = (int)Math.Round(src.Height * s);
                            g.DrawImage(src, (w - nw) / 2, (h - nh) / 2, nw, nh);
                        }
                    }
                    catch { dst.Dispose(); throw; }
                    return dst;
                }
                finally { if (src != null) src.Dispose(); }
            }
            catch { return null; }
            finally
            {
                if (hbm != IntPtr.Zero) { try { DeleteObject(hbm); } catch { } }
                if (factory != null) { try { Marshal.ReleaseComObject(factory); } catch { } }
            }
        }
    }
}
