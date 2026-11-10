using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using YamlDotNet.RepresentationModel;

[assembly: AssemblyTitle("Voidburn Setup and Launcher")]
[assembly: AssemblyDescription("Installs RSProx and configures the Voidburn RuneLite connection")]
[assembly: AssemblyVersion("2.1.12.0")]

static partial class Voidburn {
    internal const string Host = "186.101.219.92";
    internal const string TargetUrl = "https://186.101.219.92/downloads/voidburn-layout/proxy-targets.yaml";
    internal const string InstallerHash = "5E042D7A872FCE0ED94BAD285FE003430F82AA8B1CFAA9C37B0B53D0EFFDA3C9";
    internal static readonly string AppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Voidburn");
    internal static readonly string RSProxDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RSProx");
    internal static readonly string RSProxExe = Path.Combine(RSProxDir, "RSProx.exe");
    internal static readonly string ConfigDir = Path.Combine(ProfileDir, ".rsprox");

    [STAThread] static int Main(string[] args) {
        if(args.Length==1 && (args[0]=="--account-store" || args[0]=="--account-read" || args[0]=="--account-info" || args[0]=="--account-forget")) {
            using(var input=new StreamReader(Console.OpenStandardInput(),new UTF8Encoding(false)))
            using(var output=new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)))
                return AccountCommand(args[0],input,output,AppDir);
        }
        if(args.Length==1 && (args[0]=="--login-token-store" || args[0]=="--login-token-read" || args[0]=="--login-token-info" || args[0]=="--login-token-forget")) {
            using(var input=new StreamReader(Console.OpenStandardInput(),new UTF8Encoding(false)))
            using(var output=new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)))
                return LoginTokenCommand(args[0],input,output,AppDir);
        }
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e) {
            if (new AssemblyName(e.Name).Name != "YamlDotNet") return null;
            return Assembly.Load(Resource("YamlDotNet.dll"));
        };
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if(args.Length==2 && args[0]=="--preflight") { try { Preflight(Path.GetFullPath(args[1])); return 0; } catch(Exception e) { File.WriteAllText(args[1],e.ToString()); return 1; } }
        if (args.Length == 2 && args[0] == "--self-test") {
            try { SelfTest(Path.GetFullPath(args[1])); return 0; }
            catch(Exception e) { File.WriteAllText(args[1], e.ToString()); return 1; }
        }
        if(args.Length == 2 && args[0] == "--preview") {
            using(var form=new SetupForm(false)) { form.ShowInTaskbar=false;form.Opacity=0;form.Show();Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size)); image.Save(args[1]); }form.Close(); } return 0;
        }
        if(args.Length==2 && args[0]=="--preview-launch") { using(var form=new LaunchSplash(true)) { form.ShowInTaskbar=false;form.Show();Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)) {form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(args[1]);}form.Close();}return 0; }
        if(args.Length==3 && args[0]=="--apply-update") { int parent; if(!Int32.TryParse(args[2],out parent))return 1; return ApplyClientUpdate(args[1],parent); }
        if(args.Length==2 && args[0]=="--update-health") { try { UpdateHealth(args[1]); return 0; } catch(Exception e) { Log(e); return 1; } }
        if(args.Length==3 && args[0]=="--update-verify") { try { var release=VerifyUpdate(File.ReadAllBytes(args[1])); File.WriteAllText(args[2],release.Version.ToString()); return 0; } catch(Exception e) { File.WriteAllText(args[2],e.Message); return 1; } }
        Log(new Exception("Voidburn 2.1.12 started; "+(args.Contains("--play") ? "shortcut launch" : "setup")));
        Application.Run(args.Contains("--play") ? (Form)new LaunchSplash(false, args.Contains("--skip-update-once")) : new SetupForm(false));
        return 0;
    }
    internal static byte[] Resource(string name) {
        using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) {
            if(s==null) throw new IOException("Missing installer component: "+name);
            using(var m=new MemoryStream()) { s.CopyTo(m); return m.ToArray(); }
        }
    }
    internal static string Hash(byte[] bytes) {
        using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
    }
    internal static string DownloadTarget() {
        var req=(HttpWebRequest)WebRequest.Create(TargetUrl);
        req.Timeout=15000; req.ReadWriteTimeout=15000; req.UserAgent="VoidburnLauncher/1.0";
        using(var response=req.GetResponse()) using(var stream=response.GetResponseStream()) using(var m=new MemoryStream()) {
            byte[] b=new byte[4096]; int n;
            while((n=stream.Read(b,0,b.Length))>0) { m.Write(b,0,n); if(m.Length>65536)throw new IOException("Server configuration is too large."); }
            string text=Encoding.UTF8.GetString(m.ToArray()); ValidateTarget(text); return text;
        }
    }
    static YamlStream Parse(string text) { var y=new YamlStream(); y.Load(new StringReader(text)); if(y.Documents.Count!=1)throw new IOException("Expected one YAML document."); return y; }
    static YamlSequenceNode Entries(YamlStream y) {
        var map=y.Documents[0].RootNode as YamlMappingNode;
        YamlNode entries;
        if(map==null || !map.Children.TryGetValue(new YamlScalarNode("config"),out entries) || !(entries is YamlSequenceNode))
            throw new IOException("The existing RSProx targets file has an unsupported format. It has been left unchanged. Contact the server owner.");
        return (YamlSequenceNode)entries;
    }
    static string Value(YamlMappingNode m,string key) { YamlNode n; return m.Children.TryGetValue(new YamlScalarNode(key),out n) ? ((YamlScalarNode)n).Value : null; }
    internal static void ValidateTarget(string text) {
        var entries=Entries(Parse(text));
        if(entries.Children.Count!=1)throw new IOException("Invalid Voidburn configuration.");
        var map=entries.Children[0] as YamlMappingNode;
        if(map==null || Value(map,"name")!="Voidburn" || Value(map,"jav_config_url")!="https://"+Host+"/downloads/gateway-layout-test/jav_config.ws" || Value(map,"game_server_port")!="43596")
            throw new IOException("Invalid Voidburn server address.");
        var modulus=Value(map,"modulus"); var revision=Value(map,"revision");
        if(modulus==null || !System.Text.RegularExpressions.Regex.IsMatch(modulus,"\\A[0-9a-fA-F]{256,1024}\\z") || revision==null || !System.Text.RegularExpressions.Regex.IsMatch(revision,"\\A[0-9]{3,4}\\.[0-9]+\\z"))throw new IOException("Invalid server revision or login key.");
    }
    internal static string MergeTargets(string original,string incoming) {
        ValidateTarget(incoming);
        if(String.IsNullOrWhiteSpace(original))return incoming;
        var y=Parse(original); var entries=Entries(y);
        foreach(var entry in entries.Children.ToArray()) {
            var map=entry as YamlMappingNode;
            if(map!=null && String.Equals(Value(map,"name"),"Voidburn",StringComparison.OrdinalIgnoreCase))entries.Children.Remove(entry);
        }
        entries.Add(Entries(Parse(incoming)).Children[0]);
        using(var writer=new StringWriter()) { y.Save(writer,false); return writer.ToString(); }
    }
    internal static string UpdateTarget(string dir,string incoming) {
        Directory.CreateDirectory(dir);
        string yaml=Path.Combine(dir,"proxy-targets.yaml"), yml=Path.Combine(dir,"proxy-targets.yml");
        // Match RSProx's precedence: .yaml wins; otherwise use the existing .yml.
        string[] files=new[]{File.Exists(yaml)?yaml:File.Exists(yml)?yml:yaml};
        foreach(string file in files) {
            string before=File.Exists(file)?File.ReadAllText(file):"";
            string after=MergeTargets(before,incoming);
            if(after==before)continue;
            string temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
            File.WriteAllText(temporary,after,new UTF8Encoding(false));
            if(File.Exists(file))File.Replace(temporary,file,file+".revenant-backup-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
            else File.Move(temporary,file);
        }
        return files[0];
    }
    internal static void CheckConnection(string target) {
        int revision=Int32.Parse(Value((YamlMappingNode)Entries(Parse(target)).Children[0],"revision").Split('.')[0]);
        using(var client=new TcpClient()) {
            var connection=client.BeginConnect(Host,43596,null,null);
            if(!connection.AsyncWaitHandle.WaitOne(10000))throw new IOException("The game gateway is not reachable. Please try again later.");
            client.EndConnect(connection);
            using(var stream=client.GetStream()) {
                stream.ReadTimeout=10000; stream.WriteTimeout=10000;
                byte[] hello=new byte[21]; hello[0]=15; hello[1]=(byte)(revision>>24);hello[2]=(byte)(revision>>16);hello[3]=(byte)(revision>>8);hello[4]=(byte)revision;
                stream.Write(hello,0,hello.Length);int reply=stream.ReadByte();
                if(reply!=0)throw new IOException("The gateway is updating or unavailable (response "+reply+"). Please try again later.");
            }
        }
    }
    internal static void Install(Action<string> status,string target) {
        Directory.CreateDirectory(AppDir);
        if(!File.Exists(RSProxExe) || !File.Exists(Path.Combine(RSProxDir,"jdk","bin","java.exe")) || !File.Exists(Path.Combine(RSProxDir,"jdk","bin","server","jvm.dll")) || !File.Exists(Path.Combine(RSProxDir,"jdk","lib","modules"))) {
            status("Preparing the official RSProx download...");
            string temporary=Path.Combine(Path.GetTempPath(),"Voidburn-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            string installer=Path.Combine(temporary,"RSProxSetup.exe");
            try {
                DownloadRSProxInstaller(installer,status);
                status("Installing RSProx and its Java runtime...");
                var start=new ProcessStartInfo(installer,"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /TASKS=\"\" /DIR=\""+RSProxDir+"\" /LOG=\""+Path.Combine(AppDir,"rsprox-install.log")+"\"");
                start.UseShellExecute=false;
                using(var p=Process.Start(start)) { p.WaitForExit(); if(p.ExitCode!=0)throw new IOException("RSProx installation returned "+p.ExitCode+". See rsprox-install.log."); }
            } finally { if(File.Exists(installer))File.Delete(installer); Directory.Delete(temporary); }
            if(!File.Exists(RSProxExe))throw new IOException("The RSProx installation could not be found.");
        }
        status("Adding Voidburn and creating your shortcut...");
        UpdateTarget(ConfigDir,target);
        status("Preparing the private connection runtime...");PrepareRuntime(AppDir,status);
        string installed=Path.Combine(AppDir,"Voidburn.exe");
        if(!String.Equals(Path.GetFullPath(Application.ExecutablePath),installed,StringComparison.OrdinalIgnoreCase))File.Copy(Application.ExecutablePath,installed,true);
        File.WriteAllBytes(Path.Combine(AppDir,"Read me.txt"),Resource("Readme.txt"));
        File.WriteAllBytes(Path.Combine(AppDir,"Third-party licenses.txt"),Resource("Licenses.txt"));
        CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Voidburn.lnk"),installed);
        CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Voidburn.lnk"),installed);
    }
    static void CreateShortcut(string file,string exe) {
        Type t=Type.GetTypeFromProgID("WScript.Shell");
        object shell=Activator.CreateInstance(t);
        object shortcut=t.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{file});
        Type st=shortcut.GetType();
        st.InvokeMember("TargetPath",BindingFlags.SetProperty,null,shortcut,new object[]{exe});
        st.InvokeMember("Arguments",BindingFlags.SetProperty,null,shortcut,new object[]{"--play"});
        st.InvokeMember("IconLocation",BindingFlags.SetProperty,null,shortcut,new object[]{exe+",0"});
        st.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,shortcut,new object[]{AppDir});
        st.InvokeMember("Description",BindingFlags.SetProperty,null,shortcut,new object[]{"Play Voidburn with RuneLite"});
        st.InvokeMember("Save",BindingFlags.InvokeMethod,null,shortcut,null);
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
    }
    internal static void Launch(Action<string> status) { LaunchDirect(AppDir,ProfileDir,"--play",status); }
    internal static void Log(Exception e) { WriteDiagnosticLog(e); }
    static void Assert(bool b,string message) { if(!b)throw new Exception(message); }
    static void SelfTest(string report) {
        string target=Encoding.UTF8.GetString(Resource("proxy-targets.yaml")); ValidateTarget(target);
        string old="config:\n  - name: Another world\n    jav_config_url: https://example.com/config.ws\n    game_server_port: 12345\n  - name: Voidburn\n    jav_config_url: http://127.0.0.1:43594/downloads/gateway-layout-test/jav_config.ws\n";
        string merged=MergeTargets(old,target);
        var entries=Entries(Parse(merged));
        Assert(entries.Children.Count==2,"Other target was lost or Voidburn duplicated.");
        Assert(Value((YamlMappingNode)entries.Children[0],"game_server_port")=="12345","Existing target changed.");
        Assert(MergeTargets(merged,target)==merged,"Merging is not idempotent.");
        Assert(Entries(Parse(MergeTargets("",target))).Children.Count==1,"Fresh target failed.");
        bool rejected=false;try{MergeTargets("invalid: true",target);}catch(IOException){rejected=true;}Assert(rejected,"Malformed configuration accepted.");
        rejected=false;try{ValidateTarget(target.Replace(Host,"example.com"));}catch(IOException){rejected=true;}Assert(rejected,"Foreign endpoint accepted.");
        string dir=Path.Combine(Path.GetDirectoryName(report),"target-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        PrepareRuntime(Path.Combine(dir,"runtime-test"));
        string yml=Path.Combine(dir,"proxy-targets.yml");File.WriteAllText(yml,old);
        Assert(UpdateTarget(dir,target)==yml,"Existing .yml file was ignored.");
        Assert(Directory.GetFiles(dir,"*.revenant-backup-*").Length==1,"Original file was not backed up.");
        Assert(File.ReadAllText(Directory.GetFiles(dir,"*.revenant-backup-*")[0])==old,"Backup contents changed.");
        File.WriteAllText(yml,"# inactive file\n"+old);
        File.WriteAllText(Path.Combine(dir,"proxy-targets.yaml"),old);UpdateTarget(dir,target);
        Assert(File.ReadAllText(yml)=="# inactive file\n"+old,"Inactive .yml file changed.");
        Assert(Entries(Parse(File.ReadAllText(Path.Combine(dir,"proxy-targets.yaml")))).Children.Count==2,"Primary target merge failed.");
        Assert(!Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains("RSProxSetup.exe"),"Dependency installer must not be embedded.");
        string online=DownloadTarget();ValidateTarget(online);CheckConnection(online);
        string shortcut=Path.Combine(dir,"Voidburn test.lnk");CreateShortcut(shortcut,Application.ExecutablePath);Assert(File.Exists(shortcut),"Desktop shortcut creation failed.");
        File.WriteAllText(report,"PASS: YAML validity; new installs; preservation of other targets; stale target replacement; idempotence; malformed-file rejection; endpoint validation; .yml support; primary-file precedence; exact backups; Windows shortcut creation; official dependency download policy; pinned runtime library hashes and bootstrap hash; live HTTPS configuration; live public native handshake.\r\nNo real profile files or installed applications were changed.\r\n");
    }
}

