package net.voidburn.login;

import java.io.*;
import java.net.URI;
import java.net.http.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.lang.reflect.*;
import java.time.Duration;
import java.util.*;
import java.util.concurrent.*;
import javax.inject.Inject;
import com.google.inject.Provides;
import net.runelite.api.*;
import net.runelite.api.events.GameStateChanged;
import net.runelite.client.callback.ClientThread;
import net.runelite.client.config.*;
import net.runelite.client.events.ConfigChanged;
import net.runelite.client.eventbus.Subscribe;
import net.runelite.client.plugins.*;

@PluginDescriptor(name="Login Utilities",description="Remember your Voidburn account using the original RuneScape Play Now screen",tags={"voidburn","login","account"},enabledByDefault=true)
public class LoginUtilitiesPlugin extends Plugin {
    @ConfigGroup("voidburnLoginUtilities")
    public interface Settings extends Config {
        @ConfigItem(keyName="remember",name="Remember account",description="Store a revocable login token protected by your Windows account",position=0)
        default boolean remember(){return true;}
        @ConfigItem(keyName="forget",name="Forget account",description="Enable once to remove the saved login and return to the original username/password screen",position=1)
        default boolean forget(){return false;}
    }
    @Inject private Client client;
    @Inject private ClientThread clientThread;
    @Inject private ConfigManager configManager;
    @Inject private Settings settings;
    private ExecutorService worker;
    private NativeAccess nativeAccess;
    private Vault vault;
    private volatile Account account;
    private volatile boolean stopped,busy,ready,tokenMode;
    private boolean bypassPassword,wasLoggedIn;
    private final java.util.concurrent.atomic.AtomicInteger generation=new java.util.concurrent.atomic.AtomicInteger();
    private volatile Reply prepared;
    private final HttpClient http=HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(8)).followRedirects(HttpClient.Redirect.NEVER).build();
    @Provides Settings settings(ConfigManager manager){return manager.getConfig(Settings.class);}

    @Override protected void startUp() {
        stopped=false;ready=false;
        if(!System.getProperty("os.name","").startsWith("Windows"))return;
        vault=new Vault(Paths.get(System.getProperty("user.home")).getParent().resolve("Voidburn.exe"));
        worker=Executors.newSingleThreadExecutor(r->{Thread t=new Thread(r,"Login Utilities");t.setDaemon(true);return t;});
        worker.execute(()->{try{account=vault.read();}catch(Exception ignored){account=null;}finally{ready=true;}});
        clientThread.invokeLater((java.util.function.BooleanSupplier)()->{
            if(stopped)return true;
            if(ready && nativeAccess==null && client.getGameState()==GameState.LOGIN_SCREEN && target()) {
                try { nativeAccess=new NativeAccess(client);if(account!=null && settings.remember())showRemembered(); }
                catch(Exception error){nativeAccess=null;stopped=true;}
            }
            return false;
        });
    }
    @Override protected void shutDown() {
        stopped=true;generation.incrementAndGet();account=null;prepared=null;
        if(worker!=null)worker.shutdownNow();
        if(nativeAccess!=null)clientThread.invokeLater(()->{try{standard();}catch(Exception ignored){}});
    }
    private boolean target() {
        String host=client.getWorldHost();return client.getWorld()==1 && host!=null && (host.startsWith("127.") || host.equals("localhost") || host.equals("osrs.voidburn.net") || host.equals("186.101.219.92"));
    }
    @Subscribe public void onConfigChanged(ConfigChanged event) {
        if(!event.getGroup().equals("voidburnLoginUtilities") || worker==null)return;
        if((event.getKey().equals("forget") && settings.forget()) || (event.getKey().equals("remember") && !settings.remember())) {
            generation.incrementAndGet();account=null;prepared=null;
            worker.execute(()->{try{vault.forget();}catch(Exception ignored){};});
            configManager.setConfiguration("voidburnLoginUtilities","forget",false);
            clientThread.invokeLater(()->{if(client.getGameState()==GameState.LOGIN_SCREEN)try{standard();}catch(Exception ignored){}});
        }
    }
    @Subscribe public void onGameStateChanged(GameStateChanged event) {
        if(stopped || !ready || nativeAccess==null || !target())return;
        if(event.getGameState()==GameState.LOGGED_IN){prepared=null;wasLoggedIn=true;return;}
        if(event.getGameState()==GameState.LOGIN_SCREEN && wasLoggedIn) {
            wasLoggedIn=false;prepared=null;
            // The native client retains session login state after logout. A consumed OTL ticket
            // must never become the next login credential; keep only the persistent vault token.
            try {nativeAccess.clearOneTimeToken();}catch(Exception ignored){}
            clientThread.invokeLater(()->{
                if(stopped || client.getGameState()!=GameState.LOGIN_SCREEN)return;
                try {if(account!=null && settings.remember())showRemembered();else standard();}catch(Exception ignored){}
            });
            return;
        }
        if(event.getGameState()!=GameState.LOGGING_IN || tokenMode || busy || !settings.remember())return;
        if(bypassPassword){bypassPassword=false;return;}
        try {
            final String username=client.getUsername(),password=nativeAccess.password();
            if(username==null || username.isBlank() || password==null || password.isEmpty())return;
            final int epoch=generation.get();
            busy=true;
            client.setGameState(GameState.LOGIN_SCREEN);
            client.setPassword("");
            worker.execute(()->{
                try {
                    Reply reply=request("password",username,password);
                    Account saved=new Account(reply.username,reply.token);
                    if(stopped || generation.get()!=epoch){busy=false;return;}
                    vault.save(saved);account=saved;prepared=reply;
                    if(stopped || generation.get()!=epoch){account=null;prepared=null;busy=false;return;}
                    clientThread.invokeLater(()->{try{showRemembered();}catch(Exception ignored){}finally{busy=false;}});
                } catch(Exception error) {
                    // Let the original client/gateway show its normal error response.
                    clientThread.invokeLater(()->{try{standard();client.setUsername(username);client.setPassword(password);bypassPassword=true;client.setGameState(GameState.LOGGING_IN);}catch(Exception ignored){}finally{busy=false;}});
                }
            });
        }catch(Exception ignored){busy=false;}
    }
    private void showRemembered() throws Exception {
        if(account==null || stopped || !settings.remember())return;
        ClassLoader loader=client.getClass().getClassLoader();
        Class<?> refresh=Class.forName("com.jagex.oldscape.pub.RefreshAccessTokenRequester",true,loader);
        Class<?> otl=Class.forName("com.jagex.oldscape.pub.OtlTokenRequester",true,loader);
        client.getClass().getMethod("setRefreshTokenRequester",refresh).invoke(client,requester(refresh,true));
        client.getClass().getMethod("setOtlTokenRequester",otl).invoke(client,requester(otl,false));
        nativeAccess.tokenMode(true);
        nativeAccess.clearOneTimeToken();client.setPassword("");
        tokenMode=true;
        client.setUsername(account.username);
        client.switchJagexAccount("voidburn-routing-session","voidburn-routing-character",account.username);
    }
    private Object requester(Class<?> type,boolean refresh) {
        return Proxy.newProxyInstance(type.getClassLoader(),new Class<?>[]{type},(proxy,method,args)->{
            if(!method.getName().equals("request"))return objectMethod(proxy,method,args);
            if(refresh) return CompletableFuture.completedFuture(response(type.getClassLoader(),"RefreshAccessTokenResponse",true,"voidburn-native-access","voidburn-native-refresh"));
            return CompletableFuture.supplyAsync(()->{
                try {
                    final int epoch=generation.get();
                    Account saved=account;if(saved==null || stopped)throw new IOException();
                    Reply reply=prepared;prepared=null;
                    if(reply==null || System.currentTimeMillis()-reply.created>35_000)reply=request("token",saved.username,saved.token);
                    Account rotated=new Account(reply.username,reply.token);
                    if(stopped || generation.get()!=epoch)throw new IOException();
                    vault.save(rotated);account=rotated;
                    if(stopped || generation.get()!=epoch){account=null;throw new IOException();}
                    return response(type.getClassLoader(),"OtlTokenResponse",true,reply.ticket,null);
                } catch(Exception error) {
                    // A session still being released (code 5) is not a rejected credential.
                    // Only an explicit invalid-token response forgets the remembered account.
                    prepared=null;
                    if(error instanceof LoginRejected && ((LoginRejected)error).code==3) {
                        generation.incrementAndGet();account=null;
                        try{vault.forget();}catch(Exception ignored){}
                        clientThread.invokeLater(()->{try{standard();}catch(Exception ignored){}});
                    }
                    return response(type.getClassLoader(),"OtlTokenResponse",false,"",null);
                }
            },worker);
        });
    }
    private Object response(ClassLoader loader,String name,boolean success,String token,String refresh) {
        try {
            Class<?> type=Class.forName("com.jagex.oldscape.pub."+name,true,loader);
            return Proxy.newProxyInstance(loader,new Class<?>[]{type},(proxy,method,args)->{
                switch(method.getName()) {case "isSuccess":return success;case "getToken":case "getAccessToken":return token;case "getRefreshToken":return refresh;default:return objectMethod(proxy,method,args);}
            });
        }catch(Exception error){throw new CompletionException(new IOException("Native login response unavailable"));}
    }
    private static Object objectMethod(Object proxy,Method method,Object[] args) {
        if(method.getName().equals("toString"))return "Login Utilities native callback";
        if(method.getName().equals("hashCode"))return System.identityHashCode(proxy);
        if(method.getName().equals("equals"))return proxy==args[0];
        return null;
    }
    private void standard() throws Exception {
        tokenMode=false;nativeAccess.clearOneTimeToken();nativeAccess.tokenMode(false);nativeAccess.clearRequesters();client.setPassword("");client.switchJagexAccount(null,null,null);
    }
    private Reply request(String kind,String username,String secret) throws Exception {
        HttpRequest request=HttpRequest.newBuilder(URI.create("https://osrs.voidburn.net/login-utilities/v1/session"))
            .timeout(Duration.ofSeconds(25)).header("Content-Type","text/plain; charset=utf-8")
            .POST(HttpRequest.BodyPublishers.ofString(kind+"\n"+encode(username)+"\n"+encode(secret)+"\n")).build();
        HttpResponse<String> result=http.send(request,HttpResponse.BodyHandlers.ofString());
        String[] fields=result.body().split("\n");
        if(fields.length==2 && fields[0].equals("ERROR") && fields[1].matches("-?[0-9]{1,3}"))throw new LoginRejected(Integer.parseInt(fields[1]));
        if(result.statusCode()!=200 || fields.length!=4 || !fields[0].equals("OK"))throw new IOException("Login not accepted");
        Reply reply=new Reply(decode(fields[1]),decode(fields[2]),decode(fields[3]));
        if(!reply.token.matches("[A-Za-z0-9_-]{43}") || !reply.ticket.matches("[A-Za-z0-9_-]{43}"))throw new IOException("Invalid login response");
        return reply;
    }
    static String encode(String value){return Base64.getEncoder().encodeToString(value.getBytes(StandardCharsets.UTF_8));}
    static String decode(String value){return new String(Base64.getDecoder().decode(value),StandardCharsets.UTF_8);}
    static final class Account {final String username,token;Account(String username,String token){this.username=username;this.token=token;}}
    static final class LoginRejected extends IOException {final int code;LoginRejected(int code){super("Login was not accepted ("+code+")");this.code=code;}}
    static final class Reply {final String username,token,ticket;final long created=System.currentTimeMillis();Reply(String username,String token,String ticket){this.username=username;this.token=token;this.ticket=ticket;}}
    static final class Vault {
        private final Path helper;Vault(Path helper){this.helper=helper;}
        private String[] call(String command,String input)throws Exception {
            if(!Files.isRegularFile(helper))throw new IOException("Login helper unavailable");
            Process process=new ProcessBuilder(helper.toString(),command).redirectError(ProcessBuilder.Redirect.DISCARD).start();
            try {
                try(OutputStream out=process.getOutputStream()){if(input!=null)out.write(input.getBytes(StandardCharsets.UTF_8));}
                if(!process.waitFor(5,TimeUnit.SECONDS)){process.destroyForcibly();throw new IOException("Login helper timed out");}
                byte[] bytes=process.getInputStream().readNBytes(4097);if(bytes.length>4096)throw new IOException();
                String[] fields=new String(bytes,StandardCharsets.UTF_8).replace("\r","").split("\n");
                if(process.exitValue()!=0 || fields.length==0 || fields[0].equals("ERROR"))throw new IOException("Protected login unavailable");
                return fields;
            }finally{process.destroy();}
        }
        Account read()throws Exception {String[] fields=call("--login-token-read",null);if(fields[0].equals("NONE"))return null;if(fields.length!=3)throw new IOException();return new Account(decode(fields[1]),decode(fields[2]));}
        void save(Account account)throws Exception {call("--login-token-store",encode(account.username)+"\n"+encode(account.token)+"\n");}
        void forget()throws Exception {call("--login-token-forget",null);}
    }
}
