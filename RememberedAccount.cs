using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

static partial class Voidburn {
    internal sealed class RememberedAccount {
        internal string Username, Password;
        internal RememberedAccount(string username, string password) { Username=username; Password=password; }
    }
    static readonly byte[] AccountEntropy = Encoding.UTF8.GetBytes("Voidburn remembered account v1");
    static string AccountFile(string directory) { return Path.Combine(directory,"remembered-account.bin"); }
    static void ValidateAccount(RememberedAccount account) {
        if(account==null || String.IsNullOrWhiteSpace(account.Username) || account.Username.Length>64
            || String.IsNullOrEmpty(account.Password) || account.Password.Length>256
            || account.Username.IndexOfAny(new[]{'\r','\n','\0'})>=0 || account.Password.IndexOfAny(new[]{'\r','\n','\0'})>=0)
            throw new IOException("Invalid remembered account.");
    }
    static string EncodeAccountField(string value) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(value)); }
    static string DecodeAccountField(string value) { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
    internal static void SaveRememberedAccount(string directory, RememberedAccount account) {
        ValidateAccount(account);
        byte[] clear=Encoding.UTF8.GetBytes("1\n"+EncodeAccountField(account.Username)+"\n"+EncodeAccountField(account.Password));
        byte[] encrypted;
        try { encrypted=ProtectedData.Protect(clear,AccountEntropy,DataProtectionScope.CurrentUser); }
        finally { Array.Clear(clear,0,clear.Length); }
        Directory.CreateDirectory(directory);
        string file=AccountFile(directory), temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            File.WriteAllBytes(temporary,encrypted);
            if(File.Exists(file))File.Replace(temporary,file,null,true);else File.Move(temporary,file);
        } finally { if(File.Exists(temporary))File.Delete(temporary); }
    }
    internal static RememberedAccount ReadRememberedAccount(string directory) {
        string file=AccountFile(directory);
        if(!File.Exists(file))return null;
        if(new FileInfo(file).Length>65536)throw new IOException("Invalid remembered account.");
        byte[] clear=ProtectedData.Unprotect(File.ReadAllBytes(file),AccountEntropy,DataProtectionScope.CurrentUser);
        try {
            var parts=Encoding.UTF8.GetString(clear).Split('\n');
            if(parts.Length!=3 || parts[0]!="1")throw new IOException("Invalid remembered account.");
            var account=new RememberedAccount(DecodeAccountField(parts[1]),DecodeAccountField(parts[2]));
            ValidateAccount(account);return account;
        } finally { Array.Clear(clear,0,clear.Length); }
    }
    internal static void ForgetRememberedAccount(string directory) { File.Delete(AccountFile(directory)); }
    internal static int AccountCommand(string command, TextReader input, TextWriter output, string directory) {
        // Secrets travel only over redirected pipes, never command-line arguments or logs.
        try {
            if(command=="--account-store") {
                var builder=new StringBuilder();
                var buffer=new char[512];int count;
                while((count=input.Read(buffer,0,buffer.Length))>0) {builder.Append(buffer,0,count);if(builder.Length>4096)throw new IOException();}
                string data=builder.ToString();
                var parts=data.TrimEnd('\r','\n').Split('\n');
                if(parts.Length!=2)throw new IOException();
                SaveRememberedAccount(directory,new RememberedAccount(DecodeAccountField(parts[0].TrimEnd('\r')),DecodeAccountField(parts[1].TrimEnd('\r'))));
                output.WriteLine("OK");
            } else if(command=="--account-forget") { ForgetRememberedAccount(directory);output.WriteLine("OK"); }
            else {
                var account=ReadRememberedAccount(directory);
                if(account==null)output.WriteLine("NONE");
                else { output.WriteLine("OK");output.WriteLine(EncodeAccountField(account.Username));if(command=="--account-read")output.WriteLine(EncodeAccountField(account.Password)); }
            }
            output.Flush();return 0;
        } catch { output.WriteLine("ERROR");output.Flush();return 1; }
    }
}
