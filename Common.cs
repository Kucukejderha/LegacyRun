// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2026 LegacyRun contributors
using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace LegacyRun
{
    internal static class Common
    {
        internal const string ServiceName = "LegacyRunService";
        internal const string PipeName = "LegacyRun.Launch.v1";
        internal const string RegistryPath = @"SOFTWARE\LegacyRun\Applications";
        internal const string SettingsPath = @"SOFTWARE\LegacyRun\Settings";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("LegacyRun.Credentials.v1");

        internal static string Sha256(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }

        internal static RegistryKey OpenApplications(bool writable)
        {
            RegistryView view = Environment.Is64BitOperatingSystem
                ? RegistryView.Registry64 : RegistryView.Registry32;
            RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            return writable
                ? machine.CreateSubKey(RegistryPath, RegistryKeyPermissionCheck.ReadWriteSubTree)
                : machine.OpenSubKey(RegistryPath, false);
        }

        internal static RegistryKey OpenSettings(bool writable)
        {
            RegistryView view = Environment.Is64BitOperatingSystem
                ? RegistryView.Registry64 : RegistryView.Registry32;
            RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            return writable
                ? machine.CreateSubKey(SettingsPath, RegistryKeyPermissionCheck.ReadWriteSubTree)
                : machine.OpenSubKey(SettingsPath, false);
        }

        internal static void SaveCredentials(string domain, string user, string password)
        {
            byte[] plain = Encoding.UTF8.GetBytes(password);
            try
            {
                byte[] encrypted = ProtectedData.Protect(plain, Entropy,
                    DataProtectionScope.LocalMachine);
                using (RegistryKey key = OpenSettings(true))
                {
                    RegistrySecurity security = new RegistrySecurity();
                    security.SetAccessRuleProtection(true, false);
                    security.AddAccessRule(new RegistryAccessRule(
                        new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                        RegistryRights.FullControl, InheritanceFlags.ContainerInherit,
                        PropagationFlags.None, AccessControlType.Allow));
                    security.AddAccessRule(new RegistryAccessRule(
                        new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                        RegistryRights.FullControl, InheritanceFlags.ContainerInherit,
                        PropagationFlags.None, AccessControlType.Allow));
                    key.SetAccessControl(security);
                    key.SetValue("AdminDomain", domain, RegistryValueKind.String);
                    key.SetValue("AdminUser", user, RegistryValueKind.String);
                    key.SetValue("AdminSecret", encrypted, RegistryValueKind.Binary);
                }
            }
            finally { Array.Clear(plain, 0, plain.Length); }
        }

        internal static bool LoadCredentials(out string domain, out string user, out string password)
        {
            domain = user = password = null;
            using (RegistryKey key = OpenSettings(false))
            {
                if (key == null) return false;
                domain = key.GetValue("AdminDomain") as string;
                user = key.GetValue("AdminUser") as string;
                byte[] encrypted = key.GetValue("AdminSecret") as byte[];
                if (String.IsNullOrEmpty(user) || encrypted == null) return false;
                byte[] plain = ProtectedData.Unprotect(encrypted, Entropy,
                    DataProtectionScope.LocalMachine);
                try { password = Encoding.UTF8.GetString(plain); }
                finally { Array.Clear(plain, 0, plain.Length); }
                return true;
            }
        }

        internal static void SaveCurrentUserCredentials(string domain, string user, string password)
        {
            byte[] plain = Encoding.UTF8.GetBytes(password);
            try
            {
                byte[] encrypted = ProtectedData.Protect(plain, Entropy,
                    DataProtectionScope.CurrentUser);
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                    @"SOFTWARE\LegacyRun\Settings"))
                {
                    key.SetValue("Domain", domain, RegistryValueKind.String);
                    key.SetValue("User", user, RegistryValueKind.String);
                    key.SetValue("Secret", encrypted, RegistryValueKind.Binary);
                }
            }
            finally { Array.Clear(plain, 0, plain.Length); }
        }

        internal static bool LoadCurrentUserCredentials(out string domain, out string user,
            out string password)
        {
            domain = user = password = null;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\LegacyRun\Settings", false))
            {
                if (key == null) return false;
                domain = key.GetValue("Domain") as string;
                user = key.GetValue("User") as string;
                byte[] encrypted = key.GetValue("Secret") as byte[];
                if (String.IsNullOrEmpty(user) || encrypted == null) return false;
                byte[] plain = ProtectedData.Unprotect(encrypted, Entropy,
                    DataProtectionScope.CurrentUser);
                try { password = Encoding.UTF8.GetString(plain); }
                finally { Array.Clear(plain, 0, plain.Length); }
                return true;
            }
        }
    }

    internal sealed class AppItem
    {
        internal readonly string Id, Name, Path;
        internal AppItem(string id, string name, string path) { Id = id; Name = name; Path = path; }
        public override string ToString() { return Name + "   —   " + Path; }
    }
}
