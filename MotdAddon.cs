using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;

static partial class Voidburn {
    internal const string MotdAddonHash = "FC8E7B364E8F47141E65196E977338FC892AC86C3AB20CC26855F9DC7C80BA1E";
    internal const string MotdGatewayUrl = "wss://osrs.voidburn.net/voidburn-plugin/plugin";
    internal static void InstallMotdAddon(string applicationDirectory) {
        InstallBundledAddon(applicationDirectory, "VoidburnMotd.jar", MotdAddonHash);
        ConfigureMotdGateway(Path.Combine(applicationDirectory, "profile", ".rlcustom"));
    }
    internal static void ConfigureMotdGateway(string runeLiteDirectory) {
        Directory.CreateDirectory(runeLiteDirectory);
        // A fresh RuneLite installation migrates settings.properties into its first
        // profile. Existing installations use profiles2; preserve all other bytes.
        SetMotdGateway(Path.Combine(runeLiteDirectory, "settings.properties"));
        string profiles = Path.Combine(runeLiteDirectory, "profiles2");
        if (Directory.Exists(profiles)) foreach (string file in Directory.GetFiles(profiles, "*.properties")) {
            if (!Path.GetFileName(file).StartsWith("$", StringComparison.Ordinal)) SetMotdGateway(file);
        }
    }
    static void SetMotdGateway(string file) {
        string text = File.Exists(file) ? File.ReadAllText(file, Encoding.UTF8) : "";
        var values = Regex.Matches(text, @"(?m)^voidburn\.gatewayUrl\s*[=:]\s*(.*)$");
        if (values.Count > 0 && values[values.Count - 1].Groups[1].Value.Trim().Replace("\\:", ":") == MotdGatewayUrl) return;
        File.AppendAllText(file, "\r\nvoidburn.gatewayUrl=" + MotdGatewayUrl + "\r\n", new UTF8Encoding(false));
    }
}
