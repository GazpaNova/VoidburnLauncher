using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.IO.Compression;

static partial class Voidburn {
    internal const string Java21ArchiveHash = "A4DBAA05FCA4BD89D84584E58C459BD732EFEBB1722AF80AB13C6516832004F9";
    internal const string Java21ExecutableHash = "F8DC082A7DEDBFD2924C3562405A5A1FD12EFD2641808C92E2DE819C76A58B7E";
    internal const string Java21ArchiveUrl = "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.13%2B9/OpenJDK21U-jdk_x64_windows_hotspot_21.0.13_9.zip";
    const long Java21ArchiveSize = 205421281;
    internal static string Java21Directory(string directory) { return Path.Combine(directory, "runtime", "temurin-21.0.13-9-jdk"); }
    internal static string Java21Executable(string directory) { return Path.Combine(Java21Directory(directory), "bin", "java.exe"); }

    internal static void PrepareJava21(string directory, Action<string> status) {
        string java = Java21Executable(directory);
        if (File.Exists(java) && FileHash(java) == Java21ExecutableHash && File.Exists(Path.Combine(Java21Directory(directory), "bin", "jarsigner.exe"))) return;
        if (status == null) status = _ => {};
        string runtime = Path.Combine(directory, "runtime");
        Directory.CreateDirectory(runtime);
        string archive = Path.Combine(runtime, "java21-" + Guid.NewGuid().ToString("N") + ".zip");
        string staging = Path.Combine(runtime, "unpack-" + Guid.NewGuid().ToString("N"));
        try {
            DownloadJava21(archive, status);
            if (new FileInfo(archive).Length != Java21ArchiveSize || FileHash(archive) != Java21ArchiveHash)
                throw new IOException("The Java 21 runtime failed its integrity check. Nothing was installed.");
            status("Preparing Java 21 for the Voidburn client...");
            ZipFile.ExtractToDirectory(archive, staging);
            string extracted = Path.Combine(staging, "jdk-21.0.13+9");
            string candidate = Path.Combine(extracted, "bin", "java.exe");
            if (!File.Exists(candidate) || FileHash(candidate) != Java21ExecutableHash)
                throw new IOException("The Java 21 runtime archive is incomplete.");
            string destination = Java21Directory(directory);
            if (Directory.Exists(destination)) Directory.Move(destination, destination + ".previous-" + Guid.NewGuid().ToString("N"));
            Directory.Move(extracted, destination);
        } finally {
            if (File.Exists(archive)) File.Delete(archive);
            // staging is a fresh GUID child of this application's runtime folder.
            if (Directory.Exists(staging)) {
                string fullStaging = Path.GetFullPath(staging);
                string expectedParent = Path.GetFullPath(runtime).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!fullStaging.StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid Java staging cleanup path.");
                Directory.Delete(fullStaging, true);
            }
        }
    }

    static void DownloadJava21(string destination, Action<string> status) {
        Uri url = new Uri(Java21ArchiveUrl);
        var timer = Stopwatch.StartNew();
        for (int redirects = 0; redirects <= 5; redirects++) {
            if (url.Scheme != "https" || !url.IsDefaultPort || url.UserInfo != "" ||
                !(url.Host == "github.com" || url.Host == "release-assets.githubusercontent.com" || url.Host == "objects.githubusercontent.com"))
                throw new IOException("The Java runtime download left its official release hosting.");
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.AllowAutoRedirect = false;
            request.Timeout = 30000; request.ReadWriteTimeout = 30000;
            request.UserAgent = "Voidburn-Java-Runtime";
            using (var response = (HttpWebResponse)request.GetResponse()) {
                int code = (int)response.StatusCode;
                if (code == 301 || code == 302 || code == 303 || code == 307 || code == 308) {
                    url = new Uri(url, response.Headers["Location"]); continue;
                }
                if (response.StatusCode != HttpStatusCode.OK || (response.ContentLength >= 0 && response.ContentLength != Java21ArchiveSize))
                    throw new IOException("The official Java runtime could not be downloaded.");
                using (var input = response.GetResponseStream()) using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write)) {
                    byte[] buffer = new byte[65536]; int count, previous = -1; long total = 0;
                    while ((count = input.Read(buffer, 0, buffer.Length)) > 0) {
                        total += count;
                        if (total > Java21ArchiveSize || timer.Elapsed.TotalMinutes > 8) throw new IOException("Java runtime download exceeded its size or time limit.");
                        output.Write(buffer, 0, count);
                        int percent = (int)(total * 100 / Java21ArchiveSize);
                        if (percent != previous) { previous = percent; status("Downloading official Java 21 runtime: " + percent + "%"); }
                    }
                }
                return;
            }
        }
        throw new IOException("Too many redirects downloading Java 21.");
    }
}

