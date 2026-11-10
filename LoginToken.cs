using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

static partial class Voidburn {
    internal sealed class LoginToken {
        internal string Username, Token;
        internal LoginToken(string username, string token) { Username=username; Token=token; }
    }
    static readonly byte[] LoginTokenEntropy = Encoding.UTF8.GetBytes("Voidburn login token v1");
    static string LoginTokenFile(string directory) { return Path.Combine(directory,"login-token.bin"); }
    static void ValidateLoginToken(LoginToken account) {
        if(account==null || String.IsNullOrWhiteSpace(account.Username) || account.Username.Length>64
            || String.IsNullOrEmpty(account.Token) || !System.Text.RegularExpressions.Regex.IsMatch(account.Token,"\\A[A-Za-z0-9_-]{43}\\z")
            || account.Username.IndexOfAny(new[]{'\r','\n','\0'})>=0 || account.Token.IndexOfAny(new[]{'\r','\n','\0'})>=0)
            throw new IOException("Invalid login token.");
    }
    static string EncodeLoginTokenField(string value) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(value)); }
    static string DecodeLoginTokenField(string value) { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
    internal static void SaveLoginToken(string directory, LoginToken account) {
        ValidateLoginToken(account);
        byte[] clear=Encoding.UTF8.GetBytes("1\n"+EncodeLoginTokenField(account.Username)+"\n"+EncodeLoginTokenField(account.Token));
        byte[] encrypted;
        try { encrypted=ProtectedData.Protect(clear,LoginTokenEntropy,DataProtectionScope.CurrentUser); }
        finally { Array.Clear(clear,0,clear.Length); }
        Directory.CreateDirectory(directory);
        string file=LoginTokenFile(directory), temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            File.WriteAllBytes(temporary,encrypted);
            if(File.Exists(file))File.Replace(temporary,file,null,true);else File.Move(temporary,file);
        } finally { if(File.Exists(temporary))File.Delete(temporary); }
    }
    internal static LoginToken ReadLoginToken(string directory) {
        string file=LoginTokenFile(directory);
        if(!File.Exists(file))return null;
        if(new FileInfo(file).Length>65536)throw new IOException("Invalid login token.");
        byte[] clear=ProtectedData.Unprotect(File.ReadAllBytes(file),LoginTokenEntropy,DataProtectionScope.CurrentUser);
        try {
            var parts=Encoding.UTF8.GetString(clear).Split('\n');
            if(parts.Length!=3 || parts[0]!="1")throw new IOException("Invalid login token.");
            var account=new LoginToken(DecodeLoginTokenField(parts[1]),DecodeLoginTokenField(parts[2]));
            ValidateLoginToken(account);return account;
        } finally { Array.Clear(clear,0,clear.Length); }
    }
    internal static void ForgetLoginToken(string directory) { File.Delete(LoginTokenFile(directory)); }
    internal static int LoginTokenCommand(string command, TextReader input, TextWriter output, string directory) {
        // Secrets travel only over redirected pipes, never command-line arguments or logs.
        try {
            if(command=="--login-token-store") {
                var builder=new StringBuilder();
                var buffer=new char[512];int count;
                while((count=input.Read(buffer,0,buffer.Length))>0) {builder.Append(buffer,0,count);if(builder.Length>4096)throw new IOException();}
                string data=builder.ToString();
                var parts=data.TrimEnd('\r','\n').Split('\n');
                if(parts.Length!=2)throw new IOException();
                SaveLoginToken(directory,new LoginToken(DecodeLoginTokenField(parts[0].TrimEnd('\r')),DecodeLoginTokenField(parts[1].TrimEnd('\r'))));
                output.WriteLine("OK");
            } else if(command=="--login-token-forget") { ForgetLoginToken(directory);output.WriteLine("OK"); }
            else {
                var account=ReadLoginToken(directory);
                if(account==null)output.WriteLine("NONE");
                else { output.WriteLine("OK");output.WriteLine(EncodeLoginTokenField(account.Username));if(command=="--login-token-read")output.WriteLine(EncodeLoginTokenField(account.Token)); }
            }
            output.Flush();return 0;
        } catch { output.WriteLine("ERROR");output.Flush();return 1; }
    }
}
