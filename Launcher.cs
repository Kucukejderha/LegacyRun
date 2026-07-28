// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2026 LegacyRun contributors
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Security;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LegacyRun
{
    internal sealed class LauncherForm : Form
    {
        private readonly ListBox list = new ListBox();
        private readonly Button launch = new Button();
        private readonly Button account = new Button();
        private readonly List<AppItem> apps = new List<AppItem>();
        private readonly string directLaunchId;

        internal LauncherForm() : this(null) { }

        internal LauncherForm(string directId)
        {
            directLaunchId = directId;
            Text = "LegacyRun";
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(520, 330);
            MinimumSize = new Size(430, 270);

            Label info = new Label {
                Text = "Yönetici tarafından onaylanan uygulamalar",
                AutoSize = true, Left = 16, Top = 16
            };
            list.Left = 16; list.Top = 44; list.Width = 488; list.Height = 230;
            list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            list.DoubleClick += delegate { LaunchSelected(); };
            launch.Text = "Başlat";
            launch.Width = 100; launch.Height = 30; launch.Left = 404; launch.Top = 286;
            launch.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            launch.Click += delegate { LaunchSelected(); };
            account.Text = "Hesap…"; account.Width = 100; account.Height = 30;
            account.Left = 296; account.Top = 286;
            account.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            account.Click += delegate { ConfigureCredentials(); };
            Controls.AddRange(new Control[] { info, list, account, launch });
            Load += delegate {
                Reload();
                string d, u, p;
                if (!Common.LoadCurrentUserCredentials(out d, out u, out p))
                    ConfigureCredentials();
                p = null;
            };
            if (!String.IsNullOrEmpty(directLaunchId))
            {
                Opacity = 0;
                ShowInTaskbar = false;
                FormBorderStyle = FormBorderStyle.None;
                Shown += delegate {
                    BeginInvoke(new MethodInvoker(delegate {
                        AppItem target = apps.Find(delegate(AppItem item) {
                            return String.Equals(item.Id, directLaunchId,
                                StringComparison.OrdinalIgnoreCase);
                        });
                        if (target == null)
                            MessageBox.Show("Kısayoldaki uygulama artık izin listesinde değil.",
                                "LegacyRun", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        else LaunchApplication(target);
                        Close();
                    }));
                };
            }
        }

        private void Reload()
        {
            apps.Clear(); list.Items.Clear();
            using (RegistryKey root = Common.OpenApplications(false))
            {
                if (root == null) return;
                foreach (string id in root.GetSubKeyNames())
                using (RegistryKey key = root.OpenSubKey(id, false))
                {
                    string name = key.GetValue("Name") as string;
                    string path = key.GetValue("Path") as string;
                    if (!String.IsNullOrEmpty(name) && !String.IsNullOrEmpty(path))
                        apps.Add(new AppItem(id, name, path));
                }
            }
            apps.Sort(delegate(AppItem a, AppItem b) {
                return StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name);
            });
            foreach (AppItem app in apps) list.Items.Add(app);
            if (list.Items.Count > 0) list.SelectedIndex = 0;
        }

        private void LaunchSelected()
        {
            AppItem app = list.SelectedItem as AppItem;
            if (app == null) return;
            LaunchApplication(app);
        }

        private void LaunchApplication(AppItem app)
        {
            string domain, user, password;
            try
            {
                if (!Common.LoadCurrentUserCredentials(out domain, out user, out password))
                {
                    ConfigureCredentials();
                    if (!Common.LoadCurrentUserCredentials(out domain, out user, out password))
                        return;
                }
                string expected;
                using (RegistryKey root = Common.OpenApplications(false))
                using (RegistryKey key = root == null ? null : root.OpenSubKey(app.Id, false))
                    expected = key == null ? null : key.GetValue("Sha256") as string;
                if (String.IsNullOrEmpty(expected) || !File.Exists(app.Path) ||
                    !String.Equals(Common.Sha256(app.Path), expected,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "Uygulama dosyası eksik veya yönetici onayından sonra değişmiş.");

                SecureString secure = new SecureString();
                foreach (char c in password) secure.AppendChar(c);
                secure.MakeReadOnly();
                System.Diagnostics.ProcessStartInfo start =
                    new System.Diagnostics.ProcessStartInfo(app.Path);
                start.UseShellExecute = false;
                start.UserName = user;
                start.Domain = domain;
                start.Password = secure;
                start.LoadUserProfile = true;
                start.WorkingDirectory = Path.GetDirectoryName(app.Path);
                System.Diagnostics.Process process = System.Diagnostics.Process.Start(start);
                Log("Started path=" + app.Path + "; account=" + domain + "\\" + user +
                    "; PID=" + process.Id);
                password = null;
                secure.Dispose();
            }
            catch (Exception ex)
            {
                Log("Launch failed for " + app.Path + ": " + ex);
                MessageBox.Show("Başlatma başarısız.\n\n" + ex.Message, "LegacyRun",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { password = null; }
        }

        private void ConfigureCredentials()
        {
            using (UserCredentialsForm dialog = new UserCredentialsForm())
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
                Log("Credentials configured for " + domain + "\\" + user);
            }
        }

        private static void SplitAccount(string value, out string domain, out string user)
        {
            int slash = value.IndexOf('\\');
            if (slash > 0) { domain = value.Substring(0, slash); user = value.Substring(slash + 1); }
            else { domain = "."; user = value; }
        }

        private static void Log(string message)
        {
            try
            {
                string directory = Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData), "LegacyRun");
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "LegacyRun.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message +
                    Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        [STAThread]
        internal static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string[] args = Environment.GetCommandLineArgs();
            string directId = null;
            if (args.Length == 3 &&
                String.Equals(args[1], "--launch", StringComparison.OrdinalIgnoreCase))
                directId = args[2];
            Application.Run(new LauncherForm(directId));
        }
    }

    internal sealed class UserCredentialsForm : Form
    {
        private readonly TextBox account = new TextBox(), password = new TextBox();
        internal string Account { get { return account.Text.Trim(); } }
        internal string Password { get { return password.Text; } }
        internal UserCredentialsForm()
        {
            Text = "Çalıştırma hesabı"; Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(410, 170);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            Label l1 = new Label { Text = "Domain ve kullanıcı (DOMAIN\\kullanıcı):", Left = 16, Top = 18, AutoSize = true };
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
