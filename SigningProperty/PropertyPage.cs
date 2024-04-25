using SharpShell.SharpPropertySheet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;

namespace SigningProperty
{
    public partial class PropertyPage : SharpPropertyPage
    {
        public class Certificate
        {
            public string Thumbprint { get; set; }
            public string DisplayName { get; set; }
        }

        public List<Certificate> CertificateList { get; private set; }
        private string SelectedItemPath { get; set; }

        public PropertyPage()
        {
            InitializeComponent();
            PageTitle = @"Signieren"; 
        }

        protected override void OnPropertyPageInitialised(SharpPropertySheet parent)
        {
            SelectedItemPath = parent.SelectedItemPaths.First();
            timestampComboBox.SelectedIndex = 0;
            LoadCertificates();
            ActiveControl = button1;
            button1.Focus();
        }

        public void LoadCertificates()
        {
            CertificateList = new List<Certificate>();
            try
            {
                var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);

                foreach (var cert in store.Certificates)
                {
                    if (cert.HasPrivateKey)
                    {
                        CertificateList.Add(new Certificate
                        {
                            DisplayName = $"{cert.FriendlyName} ({cert.NotBefore.Date:dd.MM.yyyy} - {cert.NotAfter.Date:dd.MM.yyyy})",
                            Thumbprint = cert.Thumbprint
                        });
                    }
                }

                store.Close();
                certComboBox.DataSource = CertificateList;
                certComboBox.DisplayMember = "DisplayName";
            }
            catch (Exception ex)
            {
                var messageText = @"Das Abrufen der verfügbaren Zertifikate im Zertifikatsspeicher ist fehlgeschlagen: " + ex.Message;
                MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            string certificateThumbprint, timestampUrl = null;
            if (!string.IsNullOrEmpty(timestampComboBox.Text))
                timestampUrl = "/t " + timestampComboBox.Text;
            if (certComboBox.SelectedItem is Certificate selectedCertificate)
            {
                certificateThumbprint = selectedCertificate.Thumbprint;
            }
            else
            {
                const string messageText = @"Dieses Zertifikat besitzt kein gültigen Thumbprint.";
                MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var fullPath = FindSignTool();
            if (fullPath == null)
            {
                const string messageText = @"Windows Signierungstools wurden nicht gefunden. Bitte stelle sicher, dass die Windows SDK installiert ist.";
                MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string algorithm = null;
            if (radioSha1.Checked)
                algorithm = "sha1";
            else if (radioSha256.Checked)
                algorithm = "sha256";

            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fullPath,
                    Arguments = $"sign /sha1 {certificateThumbprint} {timestampUrl} /fd {algorithm} /v {SelectedItemPath}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            p.Start();
            p.WaitForExit();

            if (p.ExitCode != 0)
            {
                var messageText = $@"Fehler beim Signieren: SignTool wurde mit Fehlercode {p.ExitCode} beendet.";
                MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                const string messageText = @"Die Datei wurde erfolgreich signiert.";
                MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void Button2_Click(object sender, EventArgs e)
        {
            var fullPath = FindSignTool();
            if (fullPath == null)
            {
                const string messageText = @"Windows Signierungstools wurden nicht gefunden. Bitte stelle sicher, dass die Windows SDK installiert ist.";
                MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fullPath,
                    Arguments = $"remove /s {SelectedItemPath}",
                    UseShellExecute = true,
                    CreateNoWindow = true
                }
            };
            p.Start();
            p.WaitForExit();

            if (p.ExitCode != 0)
            {
                var messageText = $@"Fehler beim Entfernen der Digitalen Signatur: SignTool wurde mit Fehlercode {p.ExitCode} beendet.";
                MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                const string messageText = @"Die Digitale Signatur der Datei wurde erfolgreich entfernt.";
                MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public static string FindSignTool()
        {
            const string fileName = "signtool.exe";
            var basePath = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? "C:\\Program Files (x86)", "Windows Kits", "10", "bin");
            var versionDirectories = Directory.GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(versionDirectories, StringComparer.InvariantCulture);

            for (var i = versionDirectories.Length - 1; i >= 0; i--)
            {
                var versionDirectory = versionDirectories[i];
                var fullPath = Path.Combine(versionDirectory, "x64", fileName);

                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }
    }
}
