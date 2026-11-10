using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Text.RegularExpressions;

// A session remembers CDN failures so one DNS outage does not repeat for every library.
sealed class RuntimeLibraryDownloader {
    readonly Func<string, byte[]> fetch;
    readonly Action<string> status;
    bool preferMirror;
    internal const string MirrorRoot = "https://osrs.voidburn.net/downloads/runtime/rsprox-1.0.5/";

    internal RuntimeLibraryDownloader(Func<string, byte[]> fetch, Action<string> status) {
        this.fetch = fetch; this.status = status;
    }
    internal byte[] Download(string name, string expectedHash, string officialUrl) {
        Uri official;
        if (!Regex.IsMatch(name, "\\A[A-Za-z0-9._+-]+\\.jar\\z") || name.Contains("..")
            || !Regex.IsMatch(expectedHash, "\\A[0-9A-Fa-f]{64}\\z")
            || !Uri.TryCreate(officialUrl, UriKind.Absolute, out official) || official.Scheme != "https"
            || official.Host != "cdn.rsprox.net" || !official.IsDefaultPort || official.UserInfo != "")
            throw new IOException("Invalid connection-library manifest entry.");
        string mirror = MirrorRoot + expectedHash.ToLowerInvariant() + "/" + Uri.EscapeDataString(name);
        Exception last = null;
        foreach (string url in preferMirror ? new[] { mirror } : new[] { official.AbsoluteUri, mirror }) {
            for (int attempt = 0; attempt < 2; attempt++) {
                try {
                    byte[] data = fetch(url);
                    if (Voidburn.Hash(data) != expectedHash.ToUpperInvariant()) throw new IOException("Connection library checksum mismatch: " + name);
                    return data;
                } catch (Exception error) {
                    if (!(error is WebException) && !(error is IOException)) throw;
                    last = error;
                    if (attempt == 0) Thread.Sleep(300);
                }
            }
            if (url == official.AbsoluteUri) {
                preferMirror = true;
                status("RSProx CDN unavailable; using verified Voidburn downloads...");
            }
        }
        throw new IOException("Could not download " + name + " from the available connection-library sources. Check your connection and retry setup. Already verified files will be reused.", last);
    }
}

static partial class Voidburn {
    internal static byte[] ReadRuntimeLibrary(string url) {
        var request = (HttpWebRequest)WebRequest.Create(url);
        request.Timeout = 10000; request.ReadWriteTimeout = 30000;
        request.AllowAutoRedirect = false;
        request.UserAgent = "Voidburn-Runtime-Downloader";
        using (var response = (HttpWebResponse)request.GetResponse()) {
            if (response.StatusCode != HttpStatusCode.OK) throw new IOException("Unexpected connection-library download response.");
            const int limit = 100 * 1024 * 1024;
            if (response.ContentLength > limit) throw new IOException("Connection library is too large.");
            using (var input = response.GetResponseStream()) using (var output = new MemoryStream()) {
                var buffer = new byte[65536]; int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) > 0) {
                    output.Write(buffer, 0, count);
                    if (output.Length > limit) throw new IOException("Connection library is too large.");
                }
                return output.ToArray();
            }
        }
    }
    internal static void WriteRuntimeLibrary(string destination, byte[] bytes) {
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".download";
        try {
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
        } finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
