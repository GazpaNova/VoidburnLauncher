using System;
using System.IO;
using System.Net;
using System.Diagnostics;

static partial class Voidburn {
    internal const string RSProxInstallerUrl = "https://github.com/blurite/rsprox/releases/download/v1.0/RSProxSetup.exe";
    const long MaxInstallerBytes = 100 * 1024 * 1024;

    internal static void VerifyRSProxInstaller(string file) {
        var info = new FileInfo(file);
        if (!info.Exists || info.Length == 0 || info.Length > MaxInstallerBytes || FileHash(file) != InstallerHash)
            throw new IOException("The RSProx download did not match the tested official release. Nothing was installed. Please try again or contact the server owner.");
    }

    internal static void DownloadRSProxInstaller(string destination, Action<string> status) {
        string partial = destination + ".download";
        var timer = Stopwatch.StartNew();
        try {
            Uri url = new Uri(RSProxInstallerUrl);
            for (int redirects = 0; redirects <= 5; redirects++) {
                if (url.Scheme != "https" || !url.IsDefaultPort || url.UserInfo != "" ||
                    !(url.Host == "github.com" || url.Host == "release-assets.githubusercontent.com" || url.Host == "objects.githubusercontent.com"))
                    throw new IOException("The RSProx download redirected outside its official release hosting.");
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.AllowAutoRedirect = false;
                request.Timeout = 30000; request.ReadWriteTimeout = 30000;
                request.UserAgent = "Voidburn-Dependency-Installer";
                using (var response = (HttpWebResponse)request.GetResponse()) {
                    int code = (int)response.StatusCode;
                    if (code == 301 || code == 302 || code == 303 || code == 307 || code == 308) {
                        url = new Uri(url, response.Headers["Location"]);
                        continue;
                    }
                    if (response.StatusCode != HttpStatusCode.OK || response.ContentLength > MaxInstallerBytes)
                        throw new IOException("The official RSProx release could not be downloaded.");
                    using (var input = response.GetResponseStream()) using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write)) {
                        byte[] buffer = new byte[65536]; int count, progress = -1; long total = 0;
                        while ((count = input.Read(buffer, 0, buffer.Length)) > 0) {
                            total += count;
                            if (total > MaxInstallerBytes || timer.Elapsed.TotalMinutes > 5) throw new IOException("The RSProx download exceeded its size or time limit.");
                            output.Write(buffer, 0, count);
                            int percent = response.ContentLength > 0 ? (int)(total * 100 / response.ContentLength) : 0;
                            if (percent != progress) { progress = percent; status("Downloading official RSProx release: " + percent + "%"); }
                        }
                    }
                    status("Verifying the official RSProx download...");
                    VerifyRSProxInstaller(partial);
                    File.Move(partial, destination);
                    return;
                }
            }
            throw new IOException("Too many redirects while downloading RSProx.");
        } finally { if (File.Exists(partial)) File.Delete(partial); }
    }
}
