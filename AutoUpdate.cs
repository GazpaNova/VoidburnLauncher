using System;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

static partial class Voidburn {
    internal const string UpdateUrl = "https://osrs.voidburn.net/downloads/updates/stable.xml";
    const string UpdatePublicKey = "<RSAKeyValue><Modulus>oMjxBIhFCRsryp3AKvojekMy5ul59q2kWTyBBaywclz4YIBgWpHSdYtAQfG/hyP6rVaBtZd3I6ldDKXwgG1tVMORWm7qexsfn2W5W9fHmjaL53zeCjCIFsde/VIGXUBBry5v6Wc9e6YbeQGsrrPUAJ4Z7mdb8Fef6vOXD4iZSzfZm1mL3cVcsaTnQazf4tzlqFZ9nVdd9cMExRjGS3jqvn5zu9h25MMOzjU26gt8BAsiHWuOh3kaFl1g00aw8vcNBJclZUamgmB5FM/amzyJhi/BLxMdf4cpZt78KE97+Lv/93gFKm1lvdUH82fu9OiD+VVDIhVpzMKiKLD+gRYHecR4Qw7Yn+jaZIMR0NilixkoQGMsZajOwm5IYWHxCI1x3rlynRS6M8uExrmwGI0Px+N6kVN6H5bm42Fi9XjWSGEwF33kN1c3aHIyQKxotUUe7Kv6/9+qoQVIPDykj77VKF8r6GxjNinb4q23Iszgt7L8FKVZX/Vyb7bs4+BwbeKp</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
    const long MaxUpdateBytes = 150 * 1024 * 1024;
    internal sealed class ClientRelease {
        internal Version Version;
        internal string Url, Sha256;
        internal long Size;
    }
    static XmlDocument SafeXml(byte[] bytes) {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 };
        var doc = new XmlDocument { XmlResolver = null };
        using (var input = new MemoryStream(bytes)) using (var reader = XmlReader.Create(input, settings)) doc.Load(reader);
        return doc;
    }
    internal static ClientRelease VerifyUpdate(byte[] envelope) {
        if (envelope.Length > 32768) throw new IOException("Update information is too large.");
        var doc = SafeXml(envelope);
        if (doc.DocumentElement.Name != "signedUpdate" || doc.DocumentElement.ChildNodes.Count != 2) throw new IOException("Invalid update information.");
        byte[] payload = Convert.FromBase64String(doc.SelectSingleNode("/signedUpdate/payload").InnerText);
        byte[] signature = Convert.FromBase64String(doc.SelectSingleNode("/signedUpdate/signature").InnerText);
        using (var rsa = new RSACryptoServiceProvider()) {
            rsa.PersistKeyInCsp = false;
            rsa.FromXmlString(UpdatePublicKey);
            if (!rsa.VerifyData(payload, CryptoConfig.MapNameToOID("SHA256"), signature)) throw new IOException("Voidburn update signature is invalid.");
        }
        var release = SafeXml(payload).DocumentElement;
        Version version; long size; Uri url;
        if (release.Name != "release" || !Version.TryParse(release.GetAttribute("version"), out version)
            || !Int64.TryParse(release.GetAttribute("size"), out size) || size < 1 || size > MaxUpdateBytes
            || !Uri.TryCreate(release.GetAttribute("url"), UriKind.Absolute, out url)
            || url.Scheme != "https" || url.Host != "osrs.voidburn.net" || !url.IsDefaultPort || url.UserInfo != ""
            || !url.AbsolutePath.StartsWith("/downloads/updates/releases/", StringComparison.Ordinal) || url.Query != "" || url.Fragment != ""
            || !System.Text.RegularExpressions.Regex.IsMatch(release.GetAttribute("sha256"), "\\A[0-9A-Fa-f]{64}\\z"))
            throw new IOException("Invalid Voidburn release details.");
        return new ClientRelease { Version = version, Url = url.AbsoluteUri, Size = size, Sha256 = release.GetAttribute("sha256").ToUpperInvariant() };
    }
    static HttpWebResponse UpdateResponse(string url) {
        var request = (HttpWebRequest)WebRequest.Create(url);
        request.AllowAutoRedirect = false;
        request.Timeout = 12000; request.ReadWriteTimeout = 30000;
        request.UserAgent = "VoidburnLauncher/" + Assembly.GetExecutingAssembly().GetName().Version;
        request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
        var response = (HttpWebResponse)request.GetResponse();
        if (response.StatusCode != HttpStatusCode.OK) { response.Dispose(); throw new IOException("Update service returned an unexpected response."); }
        return response;
    }
    internal static byte[] ReadUpdateManifest() {
        using (var response = UpdateResponse(UpdateUrl)) using (var stream = response.GetResponseStream()) using (var output = new MemoryStream()) {
            var buffer = new byte[4096]; int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) > 0) { output.Write(buffer, 0, count); if (output.Length > 32768) throw new IOException("Update information is too large."); }
            return output.ToArray();
        }
    }
    static string FileHash(string file) {
        using (var sha = SHA256.Create()) using (var stream = File.OpenRead(file)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }
    internal static void VerifyReleaseFile(string file, ClientRelease release) {
        if (new FileInfo(file).Length != release.Size || FileHash(file) != release.Sha256) throw new IOException("Voidburn update checksum does not match. Your installed version was kept.");
        if (AssemblyName.GetAssemblyName(file).Version != release.Version) throw new IOException("Voidburn update version does not match its signed release.");
    }
    static Mutex UpdateMutex() { return new Mutex(false, "Local\\VoidburnUpdate-" + Hash(Encoding.UTF8.GetBytes(AppDir.ToUpperInvariant())).Substring(0, 24)); }
    static bool Acquire(Mutex mutex) { try { return mutex.WaitOne(0); } catch (AbandonedMutexException) { return true; } }
    internal static void ReplaceClientRelease(string source, string installed, ClientRelease release, Action<string> healthCheck) {
        VerifyReleaseFile(source, release);
        if (AssemblyName.GetAssemblyName(installed).Version >= release.Version) return;
        string backup = Path.Combine(Path.GetDirectoryName(installed), "Voidburn.previous.exe");
        File.Replace(source, installed, backup, true);
        try { healthCheck(installed); }
        catch {
            File.Replace(backup, installed, Path.Combine(Path.GetDirectoryName(installed), "Voidburn.failed.exe"), true);
            throw;
        }
    }
    internal static bool CheckForClientUpdate(Action<string> status) {
        // Only the installed desktop launcher self-updates. Setup remains an explicit action.
        if (!String.Equals(Path.GetFullPath(Application.ExecutablePath), Path.Combine(AppDir, "Voidburn.exe"), StringComparison.OrdinalIgnoreCase)) return false;
        using (var mutex = UpdateMutex()) {
            if (!Acquire(mutex)) throw new IOException("Voidburn is already updating. Please wait a moment and open it again.");
            try {
                status("Checking for updates...");
                byte[] manifest = ReadUpdateManifest();
                var release = VerifyUpdate(manifest);
                if (release.Version <= Assembly.GetExecutingAssembly().GetName().Version) return false;
                var drive = new DriveInfo(Path.GetPathRoot(AppDir));
                if (drive.AvailableFreeSpace < release.Size * 4 + 20 * 1024 * 1024) throw new IOException("Not enough disk space for the Voidburn update.");
                string directory = Path.Combine(AppDir, ".updates", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, "release.xml"), manifest);
                string file = Path.Combine(directory, "Voidburn.exe");
                using (var response = UpdateResponse(release.Url)) using (var input = response.GetResponseStream()) using (var output = new FileStream(file, FileMode.CreateNew, FileAccess.Write)) {
                    var buffer = new byte[65536]; int count; long total = 0; var timer = Stopwatch.StartNew(); int progress = -1;
                    while ((count = input.Read(buffer, 0, buffer.Length)) > 0) {
                        total += count; if (total > release.Size || timer.Elapsed.TotalMinutes > 10) throw new IOException("Voidburn update download exceeded its expected size or time.");
                        output.Write(buffer, 0, count);
                        int percent = (int)(total * 100 / release.Size);
                        if (percent != progress) { progress = percent; status("Updating Voidburn: " + percent + "%"); }
                    }
                }
                VerifyReleaseFile(file, release);
                string helper = Path.Combine(directory, "Updater.exe");
                File.Copy(Application.ExecutablePath, helper);
                status("Installing update...");
                Process.Start(new ProcessStartInfo(helper, "--apply-update \"" + directory + "\" " + Process.GetCurrentProcess().Id) { UseShellExecute = false, CreateNoWindow = true });
                return true;
            } catch (Exception error) {
                Log(new IOException("Client update check failed; keeping the installed launcher.", error));
                status("Update unavailable; opening installed version...");
                Thread.Sleep(1500);
                return false;
            } finally { mutex.ReleaseMutex(); }
        }
    }
    internal static int ApplyClientUpdate(string directory, int parentId) {
        string installed = Path.Combine(AppDir, "Voidburn.exe");
        try {
            string full = Path.GetFullPath(directory);
            if (!String.Equals(Path.GetDirectoryName(full), Path.Combine(AppDir, ".updates"), StringComparison.OrdinalIgnoreCase)
                || !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(full), "\\A[a-f0-9]{32}\\z")
                || !String.Equals(Path.GetFullPath(Application.ExecutablePath), Path.Combine(full, "Updater.exe"), StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid update staging directory.");
            try { using (var parent = Process.GetProcessById(parentId)) if (!parent.WaitForExit(30000)) throw new IOException("The previous Voidburn launcher did not close."); } catch (ArgumentException) {}
            using (var mutex = UpdateMutex()) {
                if (!Acquire(mutex)) throw new IOException("Another Voidburn update is in progress.");
                try {
                    var release = VerifyUpdate(File.ReadAllBytes(Path.Combine(full, "release.xml")));
                    string source = Path.Combine(full, "Voidburn.exe");
                    ReplaceClientRelease(source, installed, release, delegate(string executable) {
                        string health = Path.Combine(full, "health.txt");
                        using (var child = Process.Start(new ProcessStartInfo(executable, "--update-health \"" + health + "\"") { UseShellExecute = false, CreateNoWindow = true })) {
                            if (!child.WaitForExit(30000)) { child.Kill(); child.WaitForExit(); throw new IOException("The new launcher startup check timed out."); }
                            if (child.ExitCode != 0 || !File.Exists(health) || File.ReadAllText(health) != release.Version.ToString()) throw new IOException("The new Voidburn launcher failed its startup check.");
                        }
                    });
                } finally { mutex.ReleaseMutex(); }
            }
            Process.Start(new ProcessStartInfo(installed, "--play --skip-update-once") { UseShellExecute = false });
            // Keep only the small signed receipt; this helper remains locked until it exits.
            foreach (var name in new[] { "Voidburn.exe", "health.txt" }) { try { string p = Path.Combine(full, name); if (File.Exists(p)) File.Delete(p); } catch (IOException) {} catch (UnauthorizedAccessException) {} }
            return 0;
        } catch (Exception error) {
            Log(error);
            try {
                MessageBox.Show("Voidburn could not finish its update. Your previous version has been kept.\r\n\r\n" + error.Message, "Voidburn update", MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (File.Exists(installed)) Process.Start(new ProcessStartInfo(installed, "--play --skip-update-once") { UseShellExecute = false });
            } catch (Exception rollbackError) { Log(rollbackError); }
            return 1;
        }
    }
    internal static void UpdateHealth(string report) {
        if (Hash(Resource("VoidburnBootstrap.jar")) != BootstrapHash) throw new IOException("Bundled bootstrap checksum failed.");
        if (Hash(Resource("RevenantDropAll.jar")) != DropAllHash) throw new IOException("Bundled Drop-All addon checksum failed.");
        if (Hash(Resource("VoidburnPing.jar")) != PingHash) throw new IOException("Bundled Ping addon checksum failed.");
        if (Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains("RSProxSetup.exe")) throw new IOException("Unexpected embedded dependency installer.");
        RuntimeFiles();
        File.WriteAllText(report, Assembly.GetExecutingAssembly().GetName().Version.ToString());
    }
}
