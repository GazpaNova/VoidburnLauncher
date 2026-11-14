using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO.Compression;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Runtime.InteropServices;

static partial class Voidburn {
    internal const string BootstrapHash = "751013704A574C09798685D1FAC8E28ADDF5D67C957D096101BA1875F14ADD8E";
    internal static string ProfileDir { get { return Path.Combine(AppDir, "profile"); } }
    internal static string LastLogPath { get; private set; }
    internal const string DropAllHash = "9437B7B560439E4A1CF82F0E39817269E6594FBB60B0A7D649E617DAA0FE42A7";
    internal const string PingHash = "464F78344C959D808086132B613F3728E0D349B551ED7B924793E664E899EF57";
    internal const string LoginHash = "4EC4ED1BFD13233A2FC1245C087D2C508D03D1C54C1071444149C50C30690F5C";

    internal static void InstallDropAllAddon(string applicationDirectory) {
        InstallBundledAddon(applicationDirectory, "RevenantDropAll.jar", DropAllHash);
    }

    internal static void InstallPingAddon(string applicationDirectory) {
        InstallBundledAddon(applicationDirectory, "VoidburnPing.jar", PingHash);
    }

    internal static void RetireLoginAddon(string applicationDirectory) {
        string file = Path.Combine(applicationDirectory, "profile", ".rlcustom", "sideloaded-plugins", "VoidburnLogin.jar");
        if (File.Exists(file) && FileHash(file) == LoginHash)
            File.Move(file, file + ".disabled-" + Guid.NewGuid().ToString("N"));
    }

    static void InstallBundledAddon(string applicationDirectory, string name, string expectedHash) {
        byte[] addon = Resource(name);
        if (Hash(addon) != expectedHash) throw new IOException(name + " addon integrity check failed.");
        string directory = Path.Combine(applicationDirectory, "profile", ".rlcustom", "sideloaded-plugins");
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, name);
        if (File.Exists(destination) && FileHash(destination) == expectedHash) return;
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            File.WriteAllBytes(temporary, addon);
            if (File.Exists(destination)) File.Replace(temporary, destination, destination + ".backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"), true);
            else File.Move(temporary, destination);
        } finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static void WriteDiagnosticLog(Exception error) {
        foreach(string path in new[]{Path.Combine(AppDir,"setup.log"),Path.Combine(Path.GetTempPath(),"Voidburn-setup.log")}) {
            try { Directory.CreateDirectory(Path.GetDirectoryName(path));File.AppendAllText(path,DateTime.UtcNow.ToString("o")+" "+error+Environment.NewLine);LastLogPath=path;return; }
            catch(IOException) {} catch(UnauthorizedAccessException) {}
        }
    }

    internal static string ErrorMessage(Exception error) {
        Log(error);
        return error.Message+(LastLogPath==null ? "\r\n\r\nWindows could not write a setup log." : "\r\n\r\nSetup log: "+LastLogPath);
    }

    internal static void PrepareInstalledLaunch() {
        if (!File.Exists(Path.Combine(RSProxDir,"jdk","bin","java.exe")))
            throw new IOException("Voidburn needs its connection tools. Run VoidburnSetup.exe once, then use the Voidburn desktop shortcut.");
        string target=DownloadTarget();
        CheckConnection(target);
        UpdateTarget(ConfigDir,target);
        PrepareRuntime(AppDir);
    }

    static Dictionary<string,string> RuntimeFiles() {
        var files=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(string line in Encoding.UTF8.GetString(Resource("RuntimeFiles.txt")).Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)) {
            var parts=line.Split('|');
            if(parts.Length!=3 || Path.GetFileName(parts[1])!=parts[1] || !parts[1].EndsWith(".jar",StringComparison.Ordinal) || !parts[2].StartsWith("https://cdn.rsprox.net/",StringComparison.Ordinal))throw new IOException("Invalid runtime manifest.");
            files.Add(parts[1],parts[0]);
        }
        return files;
    }

    internal static void PrepareRuntime(string applicationDirectory, Action<string> status = null) {
        string runtime=Path.Combine(applicationDirectory,"proxy-runtime-1.0.5-rev242");
        var files=RuntimeFiles();
        string cached=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".rsprox","launcher","repository");
        if(Directory.Exists(cached) && files.All(p=>File.Exists(Path.Combine(cached,p.Key)) && Hash(File.ReadAllBytes(Path.Combine(cached,p.Key)))==p.Value))runtime=cached;
        bool ready=Directory.Exists(runtime) && files.All(p=>File.Exists(Path.Combine(runtime,p.Key)) && Hash(File.ReadAllBytes(Path.Combine(runtime,p.Key)))==p.Value);
        if(!ready) {
            Directory.CreateDirectory(runtime);
            var downloader = new RuntimeLibraryDownloader(ReadRuntimeLibrary, status ?? (_ => {}));
            int completed = 0;
            foreach(string line in Encoding.UTF8.GetString(Resource("RuntimeFiles.txt")).Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)) {
                var parts=line.Split('|'); string destination=Path.Combine(runtime,parts[1]);
                completed++;
                if(File.Exists(destination) && Hash(File.ReadAllBytes(destination))==parts[0])continue;
                if(status != null) status("Downloading connection libraries: " + completed + "/" + files.Count);
                byte[] data=downloader.Download(parts[1],parts[0],parts[2]);
                WriteRuntimeLibrary(destination,data);
            }
        }
        PrepareJava21(applicationDirectory, status);
        Directory.CreateDirectory(applicationDirectory);
        byte[] friendsAddon=Resource("RevenantOnlineStatus.jar");
        if(Hash(friendsAddon)!="AA4E8944B8CEFF662E936D4C0CA3355E1F882DE2FFE1885BDB1237A1F8E35E36")throw new IOException("Friends-list addon integrity check failed.");
        string addons=Path.Combine(applicationDirectory,"profile",".rlcustom","sideloaded-plugins");
        Directory.CreateDirectory(addons);
        string addonPath=Path.Combine(addons,"RevenantOnlineStatus.jar");
        if(!File.Exists(addonPath)||Hash(File.ReadAllBytes(addonPath))!=Hash(friendsAddon))File.WriteAllBytes(addonPath,friendsAddon);
        byte[] hiscoresAddon=Resource("VoidburnHiscores.jar");
        if(Hash(hiscoresAddon)!="D8038322688D49AF50C4A271EDD2654C49E587878B83EC9C70223724E35C3498")throw new IOException("Hiscores addon integrity check failed.");
        string hiscoresPath=Path.Combine(addons,"VoidburnHiscores.jar");
        if(!File.Exists(hiscoresPath)||Hash(File.ReadAllBytes(hiscoresPath))!=Hash(hiscoresAddon))File.WriteAllBytes(hiscoresPath,hiscoresAddon);
        InstallMotdAddon(applicationDirectory);
        InstallDropAllAddon(applicationDirectory);
        InstallPingAddon(applicationDirectory);
        InstallBundledAddon(applicationDirectory,"VoidburnBarrows.jar","5BAA80BAE3456F3B7EC5BE6FE1C5E75C5290D0BA5A8336D98191160D67EB5B22");
        RetireLoginAddon(applicationDirectory);
        InstallBundledAddon(applicationDirectory,"LoginUtilities.jar","B25002304AB0027C04118BD087DDC88ACD0C2931EAFCCAC3EA955BB9BDD534D3");
        File.WriteAllText(Path.Combine(applicationDirectory,"runtime.location"),runtime,new UTF8Encoding(false));
        byte[] bootstrap=Resource("VoidburnBootstrap.jar");
        if(Hash(bootstrap)!=BootstrapHash)throw new IOException("The bundled Voidburn launcher failed its integrity check.");
        string helper=Path.Combine(applicationDirectory,"VoidburnBootstrap.jar");
        if(!File.Exists(helper) || Hash(File.ReadAllBytes(helper))!=BootstrapHash)File.WriteAllBytes(helper,bootstrap);
    }

    static string JavaArgument(string value) {
        if(value.IndexOfAny(new[]{'\r','\n','"'})>=0)throw new IOException("Unsupported character in launcher path.");
        return "\""+value.Replace('\\','/')+"\"";
    }

    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern uint GetShortPathName(string path,StringBuilder output,uint length);

    static string JavaNativePath(string path) {
        // junixsocket's Windows native bind fails on accented paths even when Java's
        // default code page can represent them. Keep the shared Java home ASCII.
        if(path.All(c => c < 128))return path;
        var buffer=new StringBuilder(32768);
        uint count=GetShortPathName(path,buffer,(uint)buffer.Capacity);
        string shortPath=buffer.ToString();
        if(count>0 && count<buffer.Capacity && shortPath.All(c => c < 128))return shortPath;
        throw new IOException("The local connection needs an ASCII Windows path, but this folder has no usable short-path alias: "+path+". Please send the setup log to the server owner; do not rename your Windows account or delete your profile.");
    }

    // Java 11 reads @argument files using Windows' legacy code page. A UTF-8
    // argument file corrupts accented paths before our Java logging can start.
    // Manifest URLs are ASCII escaped; the short command line uses Windows Unicode.
    static void WriteClasspathJar(string destination,string applicationDirectory,string runtime) {
        var paths=new[]{Path.Combine(applicationDirectory,"VoidburnBootstrap.jar")}
            .Concat(RuntimeFiles().Keys.Select(f=>Path.Combine(runtime,f)));
        string classpath=String.Join(" ",paths.Select(p=>new Uri(Path.GetFullPath(p)).GetComponents(UriComponents.AbsoluteUri,UriFormat.UriEscaped)));
        var manifest=new StringBuilder("Manifest-Version: 1.0\r\nMain-Class: VoidburnBootstrap\r\n");
        string line="Class-Path: "+classpath;
        while(line.Length>70){manifest.Append(line.Substring(0,70)).Append("\r\n");line=" "+line.Substring(70);}
        manifest.Append(line).Append("\r\n\r\n");
        using(var file=File.Create(destination))using(var zip=new ZipArchive(file,ZipArchiveMode.Create))
        using(var output=new StreamWriter(zip.CreateEntry("META-INF/MANIFEST.MF").Open(),new UTF8Encoding(false)))output.Write(manifest.ToString());
    }

    static string StartupFailure(string log,int exitCode) {
        string details="";
        try { details=String.Join(Environment.NewLine,File.ReadAllLines(log).Reverse().Take(12).Reverse()); } catch(IOException) {}
        if(details.Length>1800)details=details.Substring(details.Length-1800);
        return "The connection helper stopped before RuneLite started (Java exit code "+exitCode+")."+
            "\r\n\r\n"+details+"\r\n\r\nStartup log: "+log;
    }

    internal static void LaunchDirect(string applicationDirectory,string profileDirectory,string mode,Action<string> progress) {
        string java=Java21Executable(applicationDirectory);
        if(!File.Exists(java))throw new IOException("The bundled Java runtime was not installed. Run Voidburn setup again.");
        Directory.CreateDirectory(profileDirectory);
        string config=Path.Combine(profileDirectory,".rsprox");Directory.CreateDirectory(config);
        Directory.CreateDirectory(Path.Combine(config,"logs"));
        string properties=Path.Combine(config,"proxy.properties");
        if(!File.Exists(properties))File.WriteAllText(properties,"proxy.port.min=44600\n",new UTF8Encoding(false));
        string run=Path.Combine(applicationDirectory,"launch-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(run);
        string state=Path.Combine(run,"status.txt"),classpathJar=Path.Combine(run,"classpath.jar"),startupLog=Path.Combine(run,"java-startup.log");
        string runtime=File.ReadAllText(Path.Combine(applicationDirectory,"runtime.location"));
        WriteClasspathJar(classpathJar,applicationDirectory,runtime);
        File.WriteAllText(state,"",new UTF8Encoding(false));
        string nativeProfile=JavaNativePath(profileDirectory);
        File.WriteAllText(startupLog,"Voidburn Java startup\r\nRuntime: "+java+"\r\nWindows: "+Environment.OSVersion+"\r\n64-bit Windows: "+Environment.Is64BitOperatingSystem+"\r\n",new UTF8Encoding(false));
        var start=new ProcessStartInfo(java,JavaArgument("-Duser.home="+nativeProfile)+" -jar "+JavaArgument(JavaNativePath(classpathJar))+" "+JavaArgument(JavaNativePath(state))+" "+mode) {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=applicationDirectory,RedirectStandardOutput=true,RedirectStandardError=true};
        // The client launcher must use the same home to locate the proxy's local handshake socket.
        start.EnvironmentVariables["JAVA_TOOL_OPTIONS"]="-Duser.home="+JavaArgument(nativeProfile);
        // Keep Windows' real profile environment: Swing's native file picker resolves
        // shell folders through it. Java's user.home alone isolates RuneLite and RSProx.
        foreach(string key in new[]{"JX_CHARACTER_ID","JX_SESSION_ID","JX_REFRESH_TOKEN","JX_DISPLAY_NAME","JX_ACCESS_TOKEN"})start.EnvironmentVariables.Remove(key);
        using(var process=new Process {StartInfo=start}) {
            var logLock=new object();
            DataReceivedEventHandler capture=(sender,eventArgs)=>{if(eventArgs.Data==null)return;lock(logLock){try{File.AppendAllText(startupLog,eventArgs.Data+Environment.NewLine,new UTF8Encoding(false));}catch(IOException){}}};
            process.OutputDataReceived+=capture;process.ErrorDataReceived+=capture;
            process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();
            DateTime deadline=DateTime.UtcNow.AddMinutes(20);string previous="";
            while(DateTime.UtcNow<deadline) {
                string text="";try{if(File.Exists(state))text=File.ReadAllText(state);}catch(IOException){}
                if(text!=previous && text.Length>0){progress(text);previous=text;}
                if(text.StartsWith("READY:",StringComparison.Ordinal))return;
                if(text.StartsWith("ERROR:",StringComparison.Ordinal))throw new IOException(text.Substring(6).Trim()+"\r\n\r\nConnection diagnostic folder: "+run);
                if(process.HasExited) {
                    process.WaitForExit();
                    if(mode=="--preflight" && process.ExitCode==0 && text.StartsWith("PREFLIGHT_OK:",StringComparison.Ordinal))return;
                    throw new IOException(StartupFailure(startupLog,process.ExitCode));
                }
                Thread.Sleep(250);
            }
            File.WriteAllText(Path.Combine(run,"stop-request"),"timeout");
            throw new IOException("RuneLite is taking longer than expected to download. Check the Voidburn connection logs before starting another session.");
        }
    }

    internal static void Preflight(string report) {
        string directory=Path.Combine(Path.GetDirectoryName(report),"voidburn-preflight-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);PrepareRuntime(directory);
        string profile=Path.Combine(directory,"profile");
        string target=DownloadTarget();CheckConnection(target);UpdateTarget(Path.Combine(profile,".rsprox"),target);
        LaunchDirect(directory,profile,"--preflight",_=>{});
        File.WriteAllText(report,"PASS: pinned runtime library SHA256; private proxy profile; exact Voidburn target selection; local HTTP/protocol startup; clean shutdown. No game client, player login or normal RSProx profile was used.\r\n");
    }
}

