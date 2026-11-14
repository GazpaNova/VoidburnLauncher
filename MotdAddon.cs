using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;

static partial class Voidburn {
    internal const string MotdAddonHash = "3521E158E543938CC1667366A90E48A860794E500BD79D62B3D2EB429A0292B7";
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
