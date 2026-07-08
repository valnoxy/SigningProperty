using System;
using System.IO;

namespace SigningProperty
{
    public class Config
    {
        public static string ConfigurationDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SigningProperty");
        public static string AzureConfigPath = Path.Combine(ConfigurationDir, "azure-artifact-signing.json");
    }

    public class AzureConfiguration
    {
        public string Endpoint { get; set; }
        public string CodeSigningAccountName { get; set; }
        public string CertificateProfileName { get; set; }
        public string CorrelationId { get; set; }
    }
}
