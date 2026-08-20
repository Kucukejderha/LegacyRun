// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2026 ASCOS LegacyRun contributors
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LegacyRun
{
    internal sealed class AdminForm : Form
    {
        private static readonly Color Navy = Color.FromArgb(25, 48, 78);
        private static readonly Color Blue = Color.FromArgb(43, 111, 184);
        private static readonly Color Canvas = Color.FromArgb(244, 247, 251);
        private static readonly Color Border = Color.FromArgb(210, 220, 232);
        private static readonly Color Ink = Color.FromArgb(30, 43, 61);
        private static readonly Color Muted = Color.FromArgb(97, 113, 135);
        private static readonly Color Success = Color.FromArgb(105, 213, 164);

        private readonly ListBox list = new ListBox();
        private readonly Button add = new Button(), remove = new Button(), refresh = new Button(),
            shortcut = new Button(), credentials = new Button(), launch = new Button(),
            guide = new Button();
        private readonly Label status = new Label();
        private readonly Label accountStatus = new Label();
        private readonly string helperCommand;
        private readonly string helperId;

        internal AdminForm(string command, string id)
        {
            helperCommand = command;
            helperId = id;
            Text = ProductInfo.ManagementTitle;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(980, 640);
            MinimumSize = new Size(820, 540);
            BackColor = Canvas;

            ConfigureButton(credentials, "Yönetici hesabı", false);
            ConfigureButton(shortcut, "Masaüstü kısayolu", false);
            ConfigureButton(add, "Uygulama ekle…", false);
            ConfigureButton(remove, "Kaldır", false);
            ConfigureButton(refresh, "Yenile", false);
            ConfigureButton(guide, "Kılavuz", false);
            ConfigureButton(launch, "Seçili uygulamayı başlat", true);

            list.Dock = DockStyle.Fill;
            list.BorderStyle = BorderStyle.None;
            list.BackColor = Color.White;
            list.ForeColor = Ink;
            list.IntegralHeight = false;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = 54;
            list.DrawItem += DrawAppItem;
            list.SelectedIndexChanged += delegate { UpdateSelectionState(); };

            guide.Click += delegate {
                try
                {
                    System.Diagnostics.Process.Start(Path.Combine(
                        Application.StartupPath, "USER_GUIDE.html"));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Kullanıcı kılavuzu açılamadı.\n\n" + ex.Message,
                        ProductInfo.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            credentials.Click += delegate { ConfigureCredentials(); };
            shortcut.Click += delegate { RequestDesktopShortcut(); };
            add.Click += delegate { RunElevated("--add"); Reload(); };
            remove.Click += delegate { RequestRemoveApplication(); };
            refresh.Click += delegate { Reload(); };
            launch.Click += delegate { LaunchSelected(); };
            list.DoubleClick += delegate { LaunchSelected(); };

            TableLayoutPanel root = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                Margin = Padding.Empty, Padding = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(BuildRail(), 0, 0);
            root.Controls.Add(BuildWorkspace(), 1, 0);
            Controls.Add(root);

            Load += delegate { Reload(); };
            if (!String.IsNullOrEmpty(helperCommand))
            {
                Opacity = 0; ShowInTaskbar = false; FormBorderStyle = FormBorderStyle.None;
                Shown += delegate {
                    BeginInvoke(new MethodInvoker(delegate {
                        if (helperCommand == "--add") PrivilegedAddApplication();
                        else if (helperCommand == "--remove") PrivilegedRemoveApplication(helperId);
                        else if (helperCommand == "--shortcut") PrivilegedCreateDesktopShortcut(helperId);
                        Close();
                    }));
                };
            }
        }

        private Control BuildRail()
        {
            Panel rail = new Panel {
                Dock = DockStyle.Fill, BackColor = Navy,
                Padding = new Padding(22, 28, 18, 22)
            };

            PictureBox logo = new PictureBox {
                Size = new Size(52, 52), Location = new Point(20, 25),
                SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent
            };
            string logoPath = Path.Combine(Application.StartupPath, "ASCOS-LegacyRun.png");
            try
            {
                if (File.Exists(logoPath))
                using (Image loaded = Image.FromFile(logoPath)) logo.Image = new Bitmap(loaded);
            }
            catch { }

            Label logoFallback = new Label {
                Text = "A", ForeColor = Color.White, BackColor = Blue,
                Font = new Font("Segoe UI Semibold", 18F),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(48, 48), Location = new Point(22, 28),
                Visible = logo.Image == null
            };
            Label brand = new Label {
                Text = "ASCOS\nLegacyRun", ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 15F), AutoSize = true,
                Location = new Point(82, 28)
            };
            Label section = new Label {
                Text = "YETKİLİ BAŞLATMA", ForeColor = Color.FromArgb(145, 170, 202),
                Font = new Font("Segoe UI Semibold", 8F), AutoSize = true,
                Location = new Point(22, 126)
            };
            Label title = new Label {
                Text = "Uygulama\nyönetimi", ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 17F), AutoSize = true,
                Location = new Point(22, 153)
            };
            Label description = new Label {
                Text = "Eski uygulamaları kayıtlı\nyönetici hesabıyla güvenle\nbaşlatın.",
                ForeColor = Color.FromArgb(190, 207, 228), AutoSize = true,
                Location = new Point(22, 220)
            };
            Label websiteCaption = new Label {
                Text = "ASCOS HAKKINDA", ForeColor = Color.FromArgb(145, 170, 202),
                Font = new Font("Segoe UI Semibold", 8F), AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            };
            LinkLabel website = new LinkLabel {
                Text = "rotaniz.com  ↗", LinkColor = Color.White,
                ActiveLinkColor = Color.FromArgb(133, 190, 244),
                VisitedLinkColor = Color.White, Font = new Font("Segoe UI Semibold", 10F),
                AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            website.LinkClicked += delegate {
                try { System.Diagnostics.Process.Start("https://rotaniz.com/ascos-araclar/"); }
                catch (Exception ex) {
                    MessageBox.Show("Web sitesi açılamadı.\n\n" + ex.Message,
                        ProductInfo.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            accountStatus.Text = "●  Hesap durumu denetleniyor";
            accountStatus.ForeColor = Success;
            accountStatus.AutoSize = true;
            accountStatus.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;

            rail.Controls.AddRange(new Control[] { logo, logoFallback, brand, section, title,
                description, websiteCaption, website, accountStatus });
            rail.Resize += delegate {
                accountStatus.Top = rail.ClientSize.Height - 48;
                website.Top = accountStatus.Top - 46;
                websiteCaption.Top = website.Top - 22;
                accountStatus.Left = website.Left = websiteCaption.Left = 22;
            };
            return rail;
        }

        private Control BuildWorkspace()
        {
            TableLayoutPanel workspace = new TableLayoutPanel {
                Dock = DockStyle.Fill, Padding = new Padding(28, 24, 28, 22),
                ColumnCount = 1, RowCount = 5, BackColor = Canvas
            };
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            workspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));

            Panel heading = new Panel { Dock = DockStyle.Fill };
            heading.Controls.Add(new Label {
                Text = "Yetkili uygulamaları yönet", ForeColor = Ink,
                Font = new Font("Segoe UI Semibold", 18F), AutoSize = true,
                Location = new Point(0, 0)
            });
            heading.Controls.Add(new Label {
                Text = "Eklenen programları tek tıkla başlatın veya masaüstü kısayolu oluşturun.",
                ForeColor = Muted, AutoSize = true, Location = new Point(2, 38)
            });
            workspace.Controls.Add(heading, 0, 0);

            Panel notice = new Panel {
                Dock = DockStyle.Fill, BackColor = Color.FromArgb(232, 241, 251),
                Padding = new Padding(14, 10, 14, 8), Margin = new Padding(0, 0, 0, 12)
            };
            notice.Controls.Add(new Label {
                Text = "ⓘ  Yalnızca güvenilir ve sabit konumdaki programları onaylayın.",
                ForeColor = Color.FromArgb(42, 91, 143), AutoSize = true,
                Location = new Point(14, 12)
            });
            notice.Paint += delegate(object sender, PaintEventArgs e) {
                ControlPaint.DrawBorder(e.Graphics, notice.ClientRectangle,
                    Color.FromArgb(190, 213, 238), ButtonBorderStyle.Solid);
            };
            workspace.Controls.Add(notice, 0, 1);

            Panel listFrame = new Panel {
                Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(1),
                Margin = new Padding(0, 0, 0, 14)
            };
            listFrame.Paint += delegate(object sender, PaintEventArgs e) {
                ControlPaint.DrawBorder(e.Graphics, listFrame.ClientRectangle,
                    Border, ButtonBorderStyle.Solid);
            };
            listFrame.Controls.Add(list);
            workspace.Controls.Add(listFrame, 0, 2);

            FlowLayoutPanel toolbar = new FlowLayoutPanel {
                Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
                Padding = new Padding(0, 0, 0, 10), Margin = Padding.Empty
            };
            toolbar.Controls.AddRange(new Control[] { credentials, add, remove, refresh, guide });
            workspace.Controls.Add(toolbar, 0, 3);

            TableLayoutPanel footer = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
                Margin = Padding.Empty, Padding = new Padding(0, 10, 0, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            status.Text = "Hazır";
            status.ForeColor = Muted;
            status.AutoSize = true;
            status.Anchor = AnchorStyles.Left;
            shortcut.Margin = new Padding(8, 0, 0, 0);
            launch.Margin = new Padding(8, 0, 0, 0);
            footer.Controls.Add(status, 0, 0);
            footer.Controls.Add(shortcut, 1, 0);
            footer.Controls.Add(launch, 2, 0);
            workspace.Controls.Add(footer, 0, 4);
            return workspace;
        }

        private static void ConfigureButton(Button button, string text, bool primary)
        {
            button.Text = text;
            button.AutoSize = true;
            button.MinimumSize = new Size(0, 34);
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = primary ? Blue : Color.White;
            button.ForeColor = primary ? Color.White : Ink;
            button.Padding = new Padding(primary ? 14 : 10, 3, primary ? 14 : 10, 3);
            button.Margin = new Padding(0, 0, 8, 0);
            button.FlatAppearance.BorderColor = primary ? Blue : Border;
            button.FlatAppearance.MouseOverBackColor = primary
                ? Color.FromArgb(35, 96, 162) : Color.FromArgb(249, 251, 253);
        }

        private void DrawAppItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            AppItem item = list.Items[e.Index] as AppItem;
            bool selectedItem = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color background = selectedItem ? Color.FromArgb(232, 241, 251) : Color.White;
            using (Brush brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
            if (selectedItem)
            using (Brush accent = new SolidBrush(Blue))
                e.Graphics.FillRectangle(accent, e.Bounds.Left, e.Bounds.Top, 4, e.Bounds.Height);

            Rectangle nameBounds = new Rectangle(e.Bounds.Left + 16, e.Bounds.Top + 7,
                Math.Max(0, e.Bounds.Width - 28), 20);
            Rectangle pathBounds = new Rectangle(e.Bounds.Left + 16, e.Bounds.Top + 29,
                Math.Max(0, e.Bounds.Width - 28), 18);
            using (Font nameFont = new Font("Segoe UI Semibold", 9.5F))
                TextRenderer.DrawText(e.Graphics, item == null ? "" : item.Name, nameFont,
                    nameBounds, Ink, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(e.Graphics, item == null ? "" : item.Path, Font,
                pathBounds, Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            using (Pen separator = new Pen(Color.FromArgb(234, 239, 245)))
                e.Graphics.DrawLine(separator, e.Bounds.Left + 12, e.Bounds.Bottom - 1,
                    e.Bounds.Right - 12, e.Bounds.Bottom - 1);
            e.DrawFocusRectangle();
        }

        private void UpdateSelectionState()
        {
            bool selectedItem = list.SelectedItem is AppItem;
            remove.Enabled = selectedItem;
            shortcut.Enabled = selectedItem;
            launch.Enabled = selectedItem;
        }

        private void UpdateAccountStatus()
        {
            try
            {
                string domain, user, password;
                if (Common.LoadCurrentUserCredentials(out domain, out user, out password))
                {
                    accountStatus.Text = "●  Hesap kayıtlı: " + domain + "\\" + user;
                    password = null;
                }
                else accountStatus.Text = "●  Yönetici hesabı bekleniyor";
            }
            catch { accountStatus.Text = "●  Hesap durumu okunamadı"; }
        }

        private void ConfigureCredentials()
        {
            using (CredentialsForm dialog = new CredentialsForm())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string domain, user;
                SplitAccount(dialog.Account, out domain, out user);
                IntPtr token;
                if (!NativeLogon.LogonUser(user, domain, dialog.Password, 2, 0, out token))
                {
                    MessageBox.Show("Hesap doğrulanamadı.\n\n" +
                        new System.ComponentModel.Win32Exception(
                            System.Runtime.InteropServices.Marshal.GetLastWin32Error()).Message,
                        ProductInfo.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                NativeLogon.CloseHandle(token);
                Common.SaveCurrentUserCredentials(domain, user, dialog.Password);
                UpdateAccountStatus();
                MessageBox.Show("Yönetici hesabı güvenli olarak kaydedildi.", ProductInfo.DisplayName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static void SplitAccount(string account, out string domain, out string user)
        {
            int slash = account.IndexOf('\\');
            if (slash > 0) { domain = account.Substring(0, slash); user = account.Substring(slash + 1); }
            else { domain = "."; user = account; }
        }

        private void Reload()
        {
            string selectedId = null;
            AppItem selectedItem = list.SelectedItem as AppItem;
            if (selectedItem != null) selectedId = selectedItem.Id;
            list.Items.Clear();
            using (RegistryKey root = Common.OpenApplications(false))
            {
                if (root != null)
                {
                    foreach (string id in root.GetSubKeyNames())
                    using (RegistryKey key = root.OpenSubKey(id, false))
                    {
                        string name = key.GetValue("Name") as string;
                        string path = key.GetValue("Path") as string;
                        if (name != null && path != null)
                        {
                            int index = list.Items.Add(new AppItem(id, name, path));
                            if (String.Equals(selectedId, id, StringComparison.OrdinalIgnoreCase))
                                list.SelectedIndex = index;
                        }
                    }
                }
            }
            status.Text = list.Items.Count == 0
                ? "Henüz uygulama eklenmedi"
                : list.Items.Count + " uygulama kayıtlı";
            UpdateSelectionState();
            UpdateAccountStatus();
        }

        private void PrivilegedAddApplication()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Uygulamalar (*.exe)|*.exe";
                dialog.Title = "Onaylanacak uygulamayı seçin";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string fullPath = Path.GetFullPath(dialog.FileName);
                if (IsDangerous(fullPath))
                {
                    MessageBox.Show("Komut kabukları, betik çalıştırıcıları, kurucular ve sistem yönetim " +
                        "araçları izin listesine eklenemez.", ProductInfo.DisplayName, MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
                string id = Guid.NewGuid().ToString("D");
                using (RegistryKey root = Common.OpenApplications(true))
                using (RegistryKey key = root.CreateSubKey(id))
                {
                    key.SetValue("Name", FileVersionInfoSafe(fullPath));
                    key.SetValue("Path", fullPath);
                    key.SetValue("Sha256", Common.Sha256(fullPath));
                }
                Reload();
            }
        }

        private void RequestRemoveApplication()
        {
            AppItem item = list.SelectedItem as AppItem;
            if (item == null) return;
            if (MessageBox.Show(item.Name + " kaldırılsın mı?", ProductInfo.DisplayName,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            RunElevated("--remove " + item.Id);
            Reload();
        }

        private void PrivilegedRemoveApplication(string id)
        {
            Guid parsed;
            if (!Guid.TryParse(id, out parsed)) return;
            using (RegistryKey root = Common.OpenApplications(true))
                root.DeleteSubKeyTree(parsed.ToString("D"), false);
        }

        private void RequestDesktopShortcut()
        {
            AppItem item = list.SelectedItem as AppItem;
            if (item == null)
            {
                MessageBox.Show("Önce bir uygulama seçin.", ProductInfo.DisplayName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            RunElevated("--shortcut " + item.Id);
        }

        private void PrivilegedCreateDesktopShortcut(string id)
        {
            AppItem item = FindItem(id);
            if (item == null) return;
            try
            {
                string desktop = Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonDesktopDirectory);
                string name = SanitizeFileName(item.Name);
                string shortcutPath = Path.Combine(desktop, name + ".lnk");
                string launcherPath = Path.Combine(Application.StartupPath, "LegacyRun.exe");
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(shellType);
                object link = shellType.InvokeMember("CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod, null, shell,
                    new object[] { shortcutPath });
                Type linkType = link.GetType();
                linkType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty,
                    null, link, new object[] { launcherPath });
                linkType.InvokeMember("Arguments", System.Reflection.BindingFlags.SetProperty,
                    null, link, new object[] { "--launch " + item.Id });
                linkType.InvokeMember("WorkingDirectory",
                    System.Reflection.BindingFlags.SetProperty, null, link,
                    new object[] { Application.StartupPath });
                linkType.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty,
                    null, link, new object[] { item.Path + ",0" });
                linkType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty,
                    null, link, new object[] { item.Name + " — ASCOS LegacyRun" });
                linkType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod,
                    null, link, null);
                MessageBox.Show("Kısayol oluşturuldu:\n" + shortcutPath, ProductInfo.DisplayName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kısayol oluşturulamadı.\n\n" + ex.Message, ProductInfo.DisplayName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private AppItem FindItem(string id)
        {
            foreach (object value in list.Items)
            {
                AppItem item = value as AppItem;
                if (item != null && String.Equals(item.Id, id,
                    StringComparison.OrdinalIgnoreCase)) return item;
            }
            return null;
        }

        private void LaunchSelected()
        {
            AppItem item = list.SelectedItem as AppItem;
            if (item == null) return;
            try
            {
                string launcher = Path.Combine(Application.StartupPath, "LegacyRun.exe");
                System.Diagnostics.Process.Start(launcher, "--launch " + item.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Uygulama başlatılamadı.\n\n" + ex.Message, ProductInfo.DisplayName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunElevated(string arguments)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo start =
                    new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath, arguments);
                start.UseShellExecute = true;
                start.Verb = "runas";
                System.Diagnostics.Process process = System.Diagnostics.Process.Start(start);
                process.WaitForExit();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                if (ex.NativeErrorCode != 1223)
                    MessageBox.Show("Yönetici işlemi başlatılamadı.\n\n" + ex.Message,
                        ProductInfo.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return String.IsNullOrWhiteSpace(value) ? "ASCOS LegacyRun Uygulaması" : value;
        }

        private static string FileVersionInfoSafe(string path)
        {
            try
            {
                string description = System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileDescription;
                if (!String.IsNullOrWhiteSpace(description)) return description;
            }
            catch { }
            return Path.GetFileNameWithoutExtension(path);
        }

        private static bool IsDangerous(string path)
        {
            string name = Path.GetFileName(path).ToLowerInvariant();
            string[] blocked = { "cmd.exe", "powershell.exe", "powershell_ise.exe", "wscript.exe",
                "cscript.exe", "mshta.exe", "rundll32.exe", "regsvr32.exe", "regedit.exe",
                "mmc.exe", "msiexec.exe", "explorer.exe", "taskmgr.exe", "schtasks.exe",
                "sc.exe", "net.exe", "net1.exe", "wmic.exe", "debug.exe", "cdb.exe" };
            foreach (string item in blocked) if (name == item) return true;
            return false;
        }

        [STAThread]
        internal static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string[] args = Environment.GetCommandLineArgs();
            string command = args.Length >= 2 ? args[1].ToLowerInvariant() : null;
            string id = args.Length >= 3 ? args[2] : null;
            Application.Run(new AdminForm(command, id));
        }
    }

    internal sealed class CredentialsForm : Form
    {
        private static readonly Color Blue = Color.FromArgb(43, 111, 184);
        private static readonly Color Canvas = Color.FromArgb(244, 247, 251);
        private static readonly Color Border = Color.FromArgb(210, 220, 232);
        private static readonly Color Ink = Color.FromArgb(30, 43, 61);
        private static readonly Color Muted = Color.FromArgb(97, 113, 135);
        private readonly TextBox account = new TextBox(), password = new TextBox();
        internal string Account { get { return account.Text.Trim(); } }
        internal string Password { get { return password.Text; } }
        internal CredentialsForm()
        {
            Text = "ASCOS LegacyRun — Yönetici hesabı";
            Font = new Font("Segoe UI", 9F);
            BackColor = Canvas;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(470, 260);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Label heading = new Label {
                Text = "Yönetici hesabını kaydet", ForeColor = Ink,
                Font = new Font("Segoe UI Semibold", 16F), AutoSize = true,
                Left = 22, Top = 18
            };
            Label detail = new Label {
                Text = "Bilgiler Windows DPAPI ile bu kullanıcıya özel olarak şifrelenir.",
                ForeColor = Muted, AutoSize = true, Left = 24, Top = 52
            };
            Label l1 = new Label {
                Text = "KULLANICI ADI (DOMAIN\\kullanıcı)", ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8F), Left = 22, Top = 88, AutoSize = true
            };
            account.SetBounds(22, 110, 426, 26);
            account.BorderStyle = BorderStyle.FixedSingle;
            Label l2 = new Label {
                Text = "PAROLA", ForeColor = Muted, Font = new Font("Segoe UI Semibold", 8F),
                Left = 22, Top = 146, AutoSize = true
            };
            password.SetBounds(22, 168, 426, 26);
            password.BorderStyle = BorderStyle.FixedSingle;
            password.UseSystemPasswordChar = true;
            Button ok = new Button {
                Text = "Güvenli olarak kaydet", Left = 268, Top = 213,
                AutoSize = true, MinimumSize = new Size(180, 34), DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.Flat, BackColor = Blue, ForeColor = Color.White,
                Padding = new Padding(10, 3, 10, 3)
            };
            ok.FlatAppearance.BorderColor = Blue;
            Button cancel = new Button {
                Text = "İptal", Left = 176, Top = 213, Width = 82, Height = 34,
                DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat,
                BackColor = Color.White, ForeColor = Ink
            };
            cancel.FlatAppearance.BorderColor = Border;
            AcceptButton = ok; CancelButton = cancel;
            Controls.AddRange(new Control[] { heading, detail, l1, account, l2, password, ok, cancel });
        }
    }

    internal static class NativeLogon
    {
        [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool LogonUser(string user, string domain, string password,
            int logonType, int provider, out IntPtr token);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        internal static extern bool CloseHandle(IntPtr handle);
    }
}
