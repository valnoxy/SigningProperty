using System;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace SigningProperty
{
    public partial class Settings : Form
    {
        public Settings()
        {
            InitializeComponent();

            if (!File.Exists(Config.AzureConfigPath)) return;
            var json = File.ReadAllText(Config.AzureConfigPath);
            var azureConfig = JsonConvert.DeserializeObject<AzureConfiguration>(json);
            endpointComboBox.Text = azureConfig.Endpoint;
            signingAccountName.Text = azureConfig.CodeSigningAccountName;
            certificateProfile.Text = azureConfig.CertificateProfileName;
            correlationId.Text = azureConfig.CorrelationId;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var azureConfig = new AzureConfiguration
            {
                Endpoint = endpointComboBox.Text,
                CodeSigningAccountName = signingAccountName.Text,
                CertificateProfileName = certificateProfile.Text,
                CorrelationId = correlationId.Text
            };

            if (!Directory.Exists(Config.ConfigurationDir)) Directory.CreateDirectory(Config.ConfigurationDir);
            var json = JsonConvert.SerializeObject(azureConfig);
            File.WriteAllText(Config.AzureConfigPath, json);
            Close();
        }
    }
}
