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

#if DEBUG
            dbgLabel.Visible = true;
#endif
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
            X509Store store = null;

            try
            {
                store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);

                foreach (var cert in store.Certificates)
                {
                    try
                    {
                        if (!cert.HasPrivateKey)
                        {
                            cert.Dispose();
                            continue;
                        }

                        var isCodeSigningCert = false;
                        foreach (var extension in cert.Extensions)
                        {
                            if (extension is X509EnhancedKeyUsageExtension enhancedKeyUsage)
                            {
                                foreach (var oid in enhancedKeyUsage.EnhancedKeyUsages)
                                {
                                    if (oid.Value != "1.3.6.1.5.5.7.3.3") continue; // Code signing
                                    isCodeSigningCert = true;
                                    break;
                                }
                            }
                            if (isCodeSigningCert)
                            {
                                break;
                            }
                        }

                        if (!isCodeSigningCert)
                        {
                            cert.Dispose();
                            continue;
                        }

                        var displayName = !string.IsNullOrEmpty(cert.FriendlyName)
                            ? cert.FriendlyName
                            : cert.GetNameInfo(X509NameType.SimpleName, false);

                        if (string.IsNullOrEmpty(displayName))
                        {
                            displayName = cert.Subject;
                        }

                        CertificateList.Add(new Certificate
                        {
                            DisplayName = $"{displayName} ({cert.NotBefore.Date:dd.MM.yyyy} - {cert.NotAfter.Date:dd.MM.yyyy})",
                            Thumbprint = cert.Thumbprint
                        });
                    }
                    finally
                    {
                        cert.Dispose();
                    }
                }

                certComboBox.DataSource = CertificateList;
                certComboBox.DisplayMember = "DisplayName";
            }
            catch (Exception ex)
            {
                var messageText = @"Das Abrufen der verfügbaren Zertifikate im Zertifikatsspeicher ist fehlgeschlagen: " + ex.Message;
                MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                store?.Close();
            }
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            var certificateThumbprint = string.Empty;
            var timestampUrl = string.Empty;
            
            if (!string.IsNullOrEmpty(timestampComboBox.Text))
                timestampUrl = "/tr " + timestampComboBox.Text;
            if (certComboBox.SelectedItem is Certificate selectedCertificate)
            {
                certificateThumbprint = selectedCertificate.Thumbprint;
            }
            else if (!azureSigningRadio.Checked)
            {
                const string messageText = @"Dieses Zertifikat besitzt kein gültigen Thumbprint.";
                MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var extension = Path.GetExtension(SelectedItemPath);
            if (extension == ".rdp") 
            {
                if (azureSigningRadio.Checked)
                {
                    const string messageText = @"RDP-Dateien können nicht mit einem Azure Code Signing Zertifikat signiert werden.";
                    MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                var fullPath = FindRdpSign();
                if (fullPath == null)
                {
                    const string messageText = @"Das RDP-Signierungstool wurde nicht gefunden. Bitte stelle sicher, dass du Windows 10 oder höher verwendest.";
                    MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fullPath,
                        Arguments = $"/sha256 {certificateThumbprint} \"{SelectedItemPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };
                p.Start();
                p.WaitForExit();

                if (p.ExitCode != 0)
                {
                    var messageText = $"Fehler beim Signieren: RDPSign wurde mit Fehlercode {p.ExitCode} beendet.\n\n{p.StandardOutput.ReadToEnd()}\n\nArgs: {p.StartInfo.Arguments}";
                    MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    const string messageText = @"Die Datei wurde erfolgreich signiert.";
                    MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return;
            }

            if (azureSigningRadio.Checked)
            {
                var fullPath = FindSignTool();
                if (fullPath == null)
                {
                    const string messageText = @"Windows Signierungstools wurden nicht gefunden. Bitte stelle sicher, dass die Windows SDK installiert ist.";
                    MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var azureCodeSigningDlib = FindAzureSigningDlib();
                if (azureCodeSigningDlib == null)
                {
                    const string messageText = @"Azure Artefakt Signierungsbibliothek wurde nicht gefunden. Bitte stelle sicher, dass die Azure Artefakt Signierungs-Client-Tools installiert sind.";
                    MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!File.Exists(Config.AzureConfigPath))
                {
                    const string messageText = @"Es wurde keine Konfiguration für das Azure Artefakt Signieren festgelegt. Bitte lege alle notwendigen Einstellungen im Client fest.";
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
                        Arguments = $"sign /v {timestampUrl} /fd {algorithm} /td {algorithm} /dlib \"{azureCodeSigningDlib}\" /dmdf \"{Config.AzureConfigPath}\" \"{SelectedItemPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };

                p.Start();
                p.WaitForExit();

                if (p.ExitCode != 0)
                {
                    var messageText = $"Fehler beim Signieren: SignTool wurde mit Fehlercode {p.ExitCode} beendet.\n\n{p.StandardOutput.ReadToEnd()}\n\nArgs: {p.StartInfo.Arguments}";
                    MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    const string messageText = @"Die Datei wurde erfolgreich signiert.";
                    MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
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
                        Arguments = $"sign /sha1 {certificateThumbprint} {timestampUrl} /fd {algorithm} /td {algorithm} /v \"{SelectedItemPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };
                p.Start();
                p.WaitForExit();

                if (p.ExitCode != 0)
                {
                    var messageText = $"Fehler beim Signieren: SignTool wurde mit Fehlercode {p.ExitCode} beendet.\n\n{p.StandardOutput.ReadToEnd()}\n\nArgs: {p.StartInfo.Arguments}";
                    MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    const string messageText = @"Die Datei wurde erfolgreich signiert.";
                    MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        public void Button2_Click(object sender, EventArgs e)
        {
            var extension = Path.GetExtension(SelectedItemPath);
            if (extension == ".rdp")
            {
                const string messageText = @"RDP-Dateien werden mit rdpsign.exe signiert. Es ist nicht möglich, die Signatur von RDP-Dateien zu entfernen.";
                MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            else
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
                        Arguments = $"remove /s \"{SelectedItemPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };
                p.Start();
                p.WaitForExit();

                if (p.ExitCode != 0)
                {
                    var messageText = $"Fehler beim Entfernen der Digitalen Signatur: SignTool wurde mit Fehlercode {p.ExitCode} beendet.\n\n{p.StandardOutput.ReadToEnd()}";
                    MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    const string messageText = @"Die Digitale Signatur der Datei wurde erfolgreich entfernt.";
                    MessageBox.Show(messageText, @"Windows-Explorer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
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

        public static string FindAzureSigningDlib()
        {
            const string fileName = "Azure.CodeSigning.Dlib.dll";
            var clientToolsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "MicrosoftArtifactSigningClientTools"
            );

            if (!Directory.Exists(clientToolsPath))
            {
                return null;
            }

            /*
            var versionDirectories = Directory.GetDirectories(clientToolsPath);
            Array.Sort(versionDirectories, StringComparer.InvariantCulture);
            for (var i = versionDirectories.Length - 1; i >= 0; i--)
            {
                var versionDirectory = versionDirectories[i];
                var fullPath = Path.Combine(versionDirectory, "bin", "x64", fileName);

                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            */
            var fullPath = Path.Combine(clientToolsPath, fileName);
            return File.Exists(fullPath) ? fullPath : null;
        }

        public static string FindRdpSign()
        {
            const string fileName = "rdpsign.exe";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", fileName);
            return File.Exists(filePath) ? filePath : null;
        }

        private void label1_Click(object sender, EventArgs e)
        {
            const string messageTitle = @"Windows-Explorer";
            var signToolExistsMessage = FindSignTool() != null
                    ? "✅ Das Windows-Signierungstool wurde gefunden."
                    : "❎ Das Windows-Signierungstool wurde nicht gefunden.";
            var rdpSignExistsMessage = FindRdpSign() != null
                    ? "✅ Das RDP-Signierungstool wurde gefunden."
                    : "❎ Das RDP-Signierungstool wurde nicht gefunden.";
            var azureSignLibExistsMessage = FindAzureSigningDlib() != null
                    ? "✅ Die Azure CodeSigning Bibliothek wurde gefunden."
                    : "❎ Die Azure CodeSigning Bibliothek wurde nicht gefunden.";

            var messageText = $"SigningProperty\nVersion: 1.2\n\n{signToolExistsMessage}\n{rdpSignExistsMessage}\n{azureSignLibExistsMessage}";
            MessageBox.Show(messageText, messageTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            var settingsWindow = new Settings();
            settingsWindow.ShowDialog();
        }
    }
}
