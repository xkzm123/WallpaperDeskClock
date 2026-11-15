using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace WallpaperDeskClock
{
    /// <summary>壁纸库：缩略图浏览、预览、一键应用、删除</summary>
    class WallpaperForm : ScaledForm
    {
        const int ThumbW = 160, ThumbH = 90, MaxItems = 120;

        readonly AppContext app;
        string folder;
        ListView list;
        ImageList thumbs;
        PictureBox preview;
        Label lblFolder, lblInfo, lblStatus;
        Bitmap previewBmp;
        readonly List<Bitmap> thumbBitmaps = new List<Bitmap>();
        Button btnApply, btnDelete;

        public WallpaperForm(AppContext app)
        {
            this.app = app;
            folder = LibraryFolder(app);

            Text = "壁纸库";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(800, 556);
            BackColor = Theme.Bg;
            Font = Theme.Ui(12.5f);
            Icon = Paths.LoadAppIcon();
            DoubleBuffered = true;

            BuildUi();
            Reload();
        }

        public static string LibraryFolder(AppContext app)
        {
            string f = app.Cfg.S("library", "").Trim();
            if (f.Length == 0) f = Paths.ResourceDir;
            try { return Path.GetFullPath(app.Cfg.Resolve(f)); }
            catch { return f; }
        }

        void BuildUi()
        {
            lblFolder = new Label
            {
                AutoSize = false,
                Size = new Size(536, 22),
                Location = new Point(20, 18),
                Font = Theme.Ui(11.5f),
                ForeColor = Theme.Muted,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            };
            Controls.Add(lblFolder);

            var btnPick = Theme.Button("选择文件夹", false);
            btnPick.Location = new Point(564, 12);
            btnPick.Size = new Size(108, 30);
            btnPick.Click += (s, e) => PickFolder();
            Controls.Add(btnPick);

            var btnRefresh = Theme.Button("刷新", false);
            btnRefresh.Location = new Point(680, 12);
            btnRefresh.Size = new Size(96, 30);
            btnRefresh.Click += (s, e) => Reload();
            Controls.Add(btnRefresh);

            thumbs = new ImageList
            {
                ImageSize = new Size(ThumbW, ThumbH),
                ColorDepth = ColorDepth.Depth24Bit
            };

            list = new ListView
            {
                Location = new Point(16, 52),
                Size = new Size(500, 440),
                View = View.LargeIcon,
                LargeImageList = thumbs,
                MultiSelect = false,
                HideSelection = false,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Font = Theme.Ui(11.5f),
                ForeColor = Theme.Text,
                ShowItemToolTips = true
            };
            list.SelectedIndexChanged += (s, e) => OnSelect();
            list.DoubleClick += (s, e) => ApplySelected();
            Controls.Add(list);

            preview = new PictureBox
            {
                Location = new Point(528, 52),
                Size = new Size(256, 144),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(preview);

            lblInfo = new Label
            {
                AutoSize = false,
                Size = new Size(256, 20),
                Location = new Point(528, 202),
                Font = Theme.Ui(11.5f),
                ForeColor = Theme.Muted,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            };
            Controls.Add(lblInfo);

            btnApply = Theme.Button("应用壁纸", true);
            btnApply.Location = new Point(528, 230);
            btnApply.Size = new Size(120, 34);
            btnApply.Click += (s, e) => ApplySelected();
            Controls.Add(btnApply);

            btnDelete = Theme.Button("删除", false);
            btnDelete.Location = new Point(656, 230);
            btnDelete.Size = new Size(128, 34);
            btnDelete.ForeColor = Theme.Danger;
            btnDelete.Click += (s, e) => DeleteSelected();
            Controls.Add(btnDelete);

            var btnOpen = Theme.Button("打开文件夹", false);
            btnOpen.Location = new Point(528, 274);
            btnOpen.Size = new Size(120, 34);
            btnOpen.Click += (s, e) => { try { Process.Start("explorer.exe", folder); } catch { } };
            Controls.Add(btnOpen);

            var btnClose = Theme.Button("关闭", false);
            btnClose.Location = new Point(656, 274);
            btnClose.Size = new Size(128, 34);
            btnClose.Click += (s, e) => Close();
            Controls.Add(btnClose);

            lblStatus = new Label
            {
                AutoSize = false,
                Size = new Size(760, 22),
                Location = new Point(20, 506),
                Font = Theme.Ui(11.5f),
                ForeColor = Theme.Muted,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            };
            Controls.Add(lblStatus);
        }

        // ---------- 数据 ----------

        string Selected()
        {
            if (list.SelectedItems.Count == 0) return null;
            return list.SelectedItems[0].Tag as string;
        }

        void Reload()
        {
            lblFolder.Text = folder;
            list.BeginUpdate();
            try
            {
                thumbs.Images.Clear();
                DisposeThumbs();       // ImageList 不会替我们释放原始位图，必须自己收
                list.Items.Clear();

                if (!Directory.Exists(folder))
                {
                    lblInfo.Text = "目录不存在";
                    return;
                }

                var files = new List<string>();
                foreach (var ext in new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp" })
                    files.AddRange(Directory.GetFiles(folder, ext));
                files.Sort(StringComparer.OrdinalIgnoreCase);

                string cur = null;
                try { cur = Path.GetFullPath(app.Cfg.Resolve(app.Cfg.S("image", Paths.DefaultImage))); }
                catch { }

                int n = 0;
                foreach (var f in files)
                {
                    if (n >= MaxItems) break;
                    var thumb = MakeThumb(f, ThumbW, ThumbH);
                    if (thumb == null) continue;
                    thumbs.Images.Add(thumb);
                    thumbBitmaps.Add(thumb);    // 自己记账，Reload / 关闭时统一释放

                    string name = Path.GetFileName(f);
                    bool isCur = cur != null && string.Equals(f, cur, StringComparison.OrdinalIgnoreCase);
                    var it = new ListViewItem(isCur ? "● " + name : name, n) { Tag = f, ToolTipText = f };
                    list.Items.Add(it);
                    n++;
                }

                lblInfo.Text = n == 0 ? "没有找到图片" : (n + " 张壁纸");
                if (list.Items.Count > 0) list.Items[0].Selected = true;
            }
            catch (Exception ex) { lblStatus.Text = "读取失败：" + ex.Message; }
            finally { list.EndUpdate(); }
            OnSelect();
        }

        void OnSelect()
        {
            string f = Selected();
            bool has = f != null;
            btnApply.Enabled = has;
            btnDelete.Enabled = has;

            SetPreview(f);
            if (!has) { lblStatus.Text = ""; return; }

            string size = "";
            try { size = new FileInfo(f).Length / 1024 + " KB"; } catch { }
            lblStatus.Text = Path.GetFileName(f) + "   ·   " + size;
        }

        void ApplySelected()
        {
            string f = Selected();
            if (f == null) return;
            app.Cfg.Set("image", f);
            app.Cfg.Save();
            app.ApplyWallpaper();
            lblStatus.Text = "已应用：" + Path.GetFileName(f);
            foreach (ListViewItem it in list.Items)
            {
                string name = Path.GetFileName((string)it.Tag);
                if (name == Path.GetFileName(f)) { it.Text = "● " + name; }
                else if (it.Text.StartsWith("● ")) { it.Text = name; }
            }
        }

        void DeleteSelected()
        {
            string f = Selected();
            if (f == null) return;
            if (MessageBox.Show("删除这个壁纸文件？\r\n" + Path.GetFileName(f), "删除",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                File.Delete(f);
                lblStatus.Text = "已删除：" + Path.GetFileName(f);
            }
            catch (Exception ex) { lblStatus.Text = "删除失败：" + ex.Message; }
            Reload();
        }

        void PickFolder()
        {
            using (var d = new FolderBrowserDialog())
            {
                d.Description = "选择壁纸库文件夹";
                d.SelectedPath = folder;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                folder = d.SelectedPath;
                app.Cfg.Set("library", folder);
                app.Cfg.Save();
                Reload();
            }
        }

        // ---------- 缩略图 / 预览 ----------

        /// <summary>优先让 Shell 缩略图服务出图（解码在 Explorer 进程里），失败才退回本地解码</summary>
        static Bitmap MakeThumb(string path, int w, int h)
        {
            var shot = ShellImage.Fill(path, w, h, Color.FromArgb(238, 241, 246));
            if (shot != null) return shot;
            return MakeThumbLegacy(path, w, h);
        }

        /// <summary>兜底：整图解码后缩放（小图很快，大图会短暂占内存）</summary>
        static Bitmap MakeThumbLegacy(string path, int w, int h)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var img = Image.FromStream(fs))
                {
                    var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.Clear(Color.FromArgb(238, 241, 246));
                        float s = Math.Max((float)w / img.Width, (float)h / img.Height);
                        int nw = (int)Math.Round(img.Width * s);
                        int nh = (int)Math.Round(img.Height * s);
                        g.DrawImage(img, (w - nw) / 2, (h - nh) / 2, nw, nh);
                    }
                    return bmp;
                }
            }
            catch { return null; }
        }

        void SetPreview(string path)
        {
            if (previewBmp != null) { preview.Image = null; previewBmp.Dispose(); previewBmp = null; }
            if (path == null) return;

            previewBmp = ShellImage.Fill(path, preview.Width, preview.Height, Color.White);
            if (previewBmp == null)
            {
                try
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var img = Image.FromStream(fs))
                    {
                        int w = preview.Width, h = preview.Height;
                        previewBmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
                        using (var g = Graphics.FromImage(previewBmp))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.Clear(Color.White);
                            float s = Math.Max((float)w / img.Width, (float)h / img.Height);
                            int nw = (int)Math.Round(img.Width * s);
                            int nh = (int)Math.Round(img.Height * s);
                            g.DrawImage(img, (w - nw) / 2, (h - nh) / 2, nw, nh);
                        }
                    }
                }
                catch { }
            }
            preview.Image = previewBmp;
        }

        void DisposeThumbs()
        {
            foreach (var b in thumbBitmaps) { try { b.Dispose(); } catch { } }
            thumbBitmaps.Clear();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (previewBmp != null) { preview.Image = null; previewBmp.Dispose(); previewBmp = null; }
            if (thumbs != null) { thumbs.Images.Clear(); thumbs.Dispose(); }
            DisposeThumbs();
            base.OnFormClosed(e);
        }
    }
}
