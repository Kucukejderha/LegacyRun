// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2026 LegacyRun contributors
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LegacyRun
{
    internal sealed class AdminForm : Form
    {
        private readonly ListBox list = new ListBox();
        private readonly Button add = new Button(), remove = new Button(), refresh = new Button(),
            shortcut = new Button(), credentials = new Button(), launch = new Button(),
            guide = new Button();
        private readonly string helperCommand;
        private readonly string helperId;

        internal AdminForm(string command, string id)
        {
            helperCommand = command;
            helperId = id;
            Text = "LegacyRun Yönetimi";
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(650, 380);
            MinimumSize = new Size(520, 300);
            Label warning = new Label {
                Text = "Yalnızca güvenilir ve sabit konumdaki programları onaylayın.",
                AutoSize = true, Left = 16, Top = 16, ForeColor = Color.DarkRed
            };
            guide.Text = "Kılavuz"; guide.Width = 80; guide.Height = 26;
            guide.Left = 554; guide.Top = 10; guide.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            guide.Click += delegate {
                try
                {
                    System.Diagnostics.Process.Start(Path.Combine(
                        Application.StartupPath, "USER_GUIDE.html"));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Kullanıcı kılavuzu açılamadı.\n\n" + ex.Message,
                        "LegacyRun", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            list.Left = 16; list.Top = 45; list.Width = 618; list.Height = 275;
            list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            credentials.Text = "Hesap…";
            shortcut.Text = "Masaüstü kısayolu";
            launch.Text = "Başlat";
            add.Text = "Ekle…"; remove.Text = "Kaldır"; refresh.Text = "Yenile";
            Button[] buttons = { credentials, shortcut, add, remove, refresh, launch };
            for (int i = 0; i < buttons.Length; i++) {
                buttons[i].Width = 95; buttons[i].Height = 30;
                buttons[i].Top = 334; buttons[i].Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            }
            credentials.Width = 100; credentials.Left = 16;
            shortcut.Width = 145; shortcut.Left = 124;
            add.Width = 80; add.Left = 277;
            remove.Width = 80; remove.Left = 365;
            refresh.Width = 80; refresh.Left = 453;
            launch.Width = 93; launch.Left = 541;
            credentials.Click += delegate { ConfigureCredentials(); };
            shortcut.Click += delegate { RequestDesktopShortcut(); };
            add.Click += delegate { RunElevated("--add"); Reload(); };
            remove.Click += delegate { RequestRemoveApplication(); };
            refresh.Click += delegate { Reload(); };
            launch.Click += delegate { LaunchSelected(); };
            list.DoubleClick += delegate { LaunchSelected(); };
            Controls.AddRange(new Control[] { warning, guide, list, credentials, shortcut, add,
                remove, refresh, launch });
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
                        "LegacyRun", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                NativeLogon.CloseHandle(token);
                Common.SaveCurrentUserCredentials(domain, user, dialog.Password);
                MessageBox.Show("Yönetici hesabı güvenli olarak kaydedildi.", "LegacyRun",
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
            list.Items.Clear();
            using (RegistryKey root = Common.OpenApplications(false))
            {
                if (root == null) return;
                foreach (string id in root.GetSubKeyNames())
                using (RegistryKey key = root.OpenSubKey(id, false))
                {
                    string name = key.GetValue("Name") as string;
                    string path = key.GetValue("Path") as string;
                    if (name != null && path != null) list.Items.Add(new AppItem(id, name, path));
                }
            }
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
                        "araçları izin listesine eklenemez.", "LegacyRun", MessageBoxButtons.OK,
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
            if (MessageBox.Show(item.Name + " kaldırılsın mı?", "LegacyRun",
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
                MessageBox.Show("Önce bir uygulama seçin.", "LegacyRun",
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
                    null, link, new object[] { item.Name + " — LegacyRun" });
                linkType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod,
                    null, link, null);
                MessageBox.Show("Kısayol oluşturuldu:\n" + shortcutPath, "LegacyRun",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kısayol oluşturulamadı.\n\n" + ex.Message, "LegacyRun",
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
                MessageBox.Show("Uygulama başlatılamadı.\n\n" + ex.Message, "LegacyRun",
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
                        "LegacyRun", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return String.IsNullOrWhiteSpace(value) ? "LegacyRun Uygulaması" : value;
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
        private readonly TextBox account = new TextBox(), password = new TextBox();
        internal string Account { get { return account.Text.Trim(); } }
        internal string Password { get { return password.Text; } }
        internal CredentialsForm()
        {
            Text = "Yönetici hesabı"; Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(410, 170);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            Label l1 = new Label { Text = "Kullanıcı adı (BİLGİSAYAR\\kullanıcı):", Left = 16, Top = 18, AutoSize = true };
            account.SetBounds(16, 42, 378, 24);
            Label l2 = new Label { Text = "Parola:", Left = 16, Top = 76, AutoSize = true };
            password.SetBounds(16, 98, 378, 24); password.UseSystemPasswordChar = true;
            Button ok = new Button { Text = "Kaydet", Left = 212, Top = 132, Width = 85, DialogResult = DialogResult.OK };
            Button cancel = new Button { Text = "İptal", Left = 309, Top = 132, Width = 85, DialogResult = DialogResult.Cancel };
            AcceptButton = ok; CancelButton = cancel;
            Controls.AddRange(new Control[] { l1, account, l2, password, ok, cancel });
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
