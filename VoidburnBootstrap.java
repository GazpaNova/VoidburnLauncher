import java.io.IOException;
import java.lang.reflect.*;
import java.nio.channels.FileChannel;
import java.nio.channels.FileLock;
import java.nio.file.*;
import java.util.List;
import java.util.Locale;
import java.net.InetAddress;
import java.net.ServerSocket;

/** Small bridge to the pinned, unmodified RSProx 1.0.5 runtime. No GUI automation. */
public final class VoidburnBootstrap {
    private static Path status;
    private static Object service;
    private static volatile boolean started;

    private static void report(String text) {
        System.out.println("VOIDBURN " + text);
        if (status != null) {
            try {
                Path temp = status.resolveSibling(status.getFileName() + ".tmp");
                Files.writeString(temp, text);
                Files.move(temp, status, StandardCopyOption.REPLACE_EXISTING);
            } catch (IOException e) { System.err.println("Cannot update launcher status: " + e); }
        }
    }

    private static Object call(Object object, String name, Object... args) throws Exception {
        for (Method method : object.getClass().getMethods()) {
            if (method.getName().equals(name) && method.getParameterCount() == args.length)
                return method.invoke(object, args);
        }
        throw new NoSuchMethodException(name);
    }

    private static Class<?> verifyApi() throws Exception {
        Class<?> api = Class.forName("net.rsprox.proxy.ProxyService");
        api.getMethod("getProxyTargets");
        api.getMethod("setSelectedProxyTarget", int.class);
        api.getMethod("allocatePort");
        api.getMethod("initializeHttpServer", int.class);
        api.getMethod("safeShutdown");
        api.getDeclaredMethod("hasAliveProcesses");
        boolean launch = false, start = false;
        for (Method method : api.getMethods()) {
            if (method.getName().equals("launchRuneLiteClient") && method.getParameterCount() == 4) launch = true;
            if (method.getName().equals("start") && method.getParameterCount() == 3) start = true;
        }
        if (!launch || !start) throw new IllegalStateException("Unsupported RSProx runtime API");
        return api;
    }

    private static int selectTarget(List<?> targets, String expectedName) throws Exception {
        for (int i = 0; i < targets.size(); i++) {
            Object target = targets.get(i);
            if (!expectedName.equals(call(target, "getName"))) continue;
            if (!Integer.valueOf(43596).equals(call(target, "getGameServerPort")))
                throw new IllegalStateException("Unexpected Voidburn game port");
            if (!"https://186.101.219.92/downloads/gateway-layout-test/jav_config.ws".equals(call(target, "getJavConfigUrl")))
                throw new IllegalStateException("Unexpected Voidburn server address");
            return i;
        }
        throw new IllegalStateException("Voidburn target was not loaded; no other target will be launched");
    }

    private static Object empty(Class<?> type) {
        if (!type.isPrimitive() || type == void.class) return null;
        if (type == boolean.class) return false;
        if (type == long.class) return 0L;
        if (type == double.class) return 0.0;
        if (type == float.class) return 0f;
        return 0;
    }

    private static Object monitor(Class<?> monitorType) {
        return Proxy.newProxyInstance(monitorType.getClassLoader(), new Class<?>[]{monitorType}, (proxy, method, args) -> {
            if (method.getName().equals("forSession")) return proxy;
            if (method.getName().equals("equals")) return proxy == args[0];
            if (method.getName().equals("hashCode")) return System.identityHashCode(proxy);
            if (method.getName().equals("toString")) return "Voidburn session monitor";
            // Account details and chat callbacks are deliberately ignored.
            return empty(method.getReturnType());
        });
    }

    public static void main(String[] args) {
        int result = 0;
        try {
            if (args.length == 2) {
                status = Path.of(args[0]).toAbsolutePath();
                Files.createDirectories(status.getParent());
                java.io.PrintStream output = new java.io.PrintStream(Files.newOutputStream(status.resolveSibling("bootstrap.log"), StandardOpenOption.CREATE, StandardOpenOption.APPEND), true, "UTF-8");
                System.setOut(output);
                System.setErr(output);
            }
            Class<?> api = verifyApi();
            if (args.length == 1 && args[0].equals("--verify")) {
                System.out.println("PASS: pinned RSProx runtime supports direct target selection, RuneLite launch and lifecycle tracking");
                return;
            }
            if (args.length != 2) throw new IllegalArgumentException("Expected status file and launch mode");
            status = Path.of(args[0]).toAbsolutePath();
            Files.createDirectories(status.getParent());
            boolean preflight = args[1].equals("--preflight");
            if (!preflight && !args[1].equals("--play")) throw new IllegalArgumentException("Unsupported launch mode");
            Path home = Path.of(System.getProperty("user.home"));
            Files.createDirectories(home);
            try (FileChannel channel = FileChannel.open(home.resolve("voidburn.lock"), StandardOpenOption.CREATE, StandardOpenOption.WRITE);
                 FileLock lock = channel.tryLock()) {
                if (lock == null) throw new IllegalStateException("Voidburn is already running. Close its RuneLite window before starting again.");
                report("Preparing Voidburn...");
                Locale.setDefault(Locale.US);
                Class<?> allocator = Class.forName("io.netty.buffer.ByteBufAllocator");
                service = api.getConstructor(allocator).newInstance(allocator.getField("DEFAULT").get(null));
                Method start = null;
                for (Method m : api.getMethods()) if (m.getName().equals("start") && m.getParameterCount() == 3) start = m;
                Class<?> progressType = start.getParameterTypes()[2];
                Object progress = Proxy.newProxyInstance(progressType.getClassLoader(), new Class<?>[]{progressType}, (proxy, method, values) -> {
                    if (method.getName().equals("update") && values != null && values.length > 2)
                        report("Preparing connection: " + String.valueOf(values[2]));
                    return empty(method.getReturnType());
                });
                start.invoke(service, null, null, progress);
                started = true;
                Runtime.getRuntime().addShutdownHook(new Thread(VoidburnBootstrap::shutdown, "Voidburn-cleanup"));
                Thread cancel = new Thread(() -> {
                    try {
                        while (started) {
                            if (Files.exists(status.resolveSibling("stop-request"))) { shutdown(); System.exit(0); }
                            Thread.sleep(1000);
                        }
                    } catch (InterruptedException ignored) { Thread.currentThread().interrupt(); }
                }, "Voidburn-stop-request");
                cancel.setDaemon(true);
                cancel.start();
                @SuppressWarnings("unchecked") List<?> targets = (List<?>) call(service, "getProxyTargets");
                int selected = selectTarget(targets, "Voidburn");
                call(service, "setSelectedProxyTarget", selected);
                // RSProx fixes its HTTP base at 43600 independently of proxy.port.min.
                // Reserve a different session range from an ordinary open RSProx window.
                int port = -1;
                for (int offset = 0; offset < 300; offset++) {
                    int candidate = (int) call(service, "allocatePort");
                    if (offset < 100) continue;
                    if (portAvailable(candidate) && portAvailable(43600 + offset)) { port = candidate; break; }
                }
                if (port < 0) throw new IllegalStateException("No free local connection port; close another Voidburn session and retry");
                Object target = call(service, "initializeHttpServer", port);
                if (preflight) {
                    report("PREFLIGHT_OK: Voidburn selected; local HTTP and protocol connection prepared on port " + port);
                } else {
                    report("Opening RuneLite. The first launch downloads client files...");
                    Method launch = null;
                    for (Method m : api.getMethods()) if (m.getName().equals("launchRuneLiteClient") && m.getParameterCount() == 4) launch = m;
                    launch.invoke(service, monitor(launch.getParameterTypes()[0]), null, port, target);
                    Method alive = api.getDeclaredMethod("hasAliveProcesses");
                    alive.setAccessible(true);
                    if (!Boolean.TRUE.equals(alive.invoke(service))) throw new IllegalStateException("RuneLite did not start; see the Voidburn connection log");
                    report("READY: RuneLite is open. Log in with your game username and password.");
                    while (Boolean.TRUE.equals(alive.invoke(service))) Thread.sleep(1000);
                    report("CLOSED: RuneLite was closed; the background connection is stopping.");
                }
                shutdown();
            }
        } catch (Throwable error) {
            while (error instanceof InvocationTargetException && error.getCause() != null) error = error.getCause();
            error.printStackTrace(System.err);
            report("ERROR: " + error.getClass().getSimpleName() + ": " + String.valueOf(error.getMessage()));
            shutdown();
            result = 1;
        }
        System.exit(result);
    }

    private static synchronized void shutdown() {
        if (!started || service == null) return;
        started = false;
        try { call(service, "safeShutdown"); }
        catch (Throwable error) { System.err.println("Voidburn cleanup: " + error.getClass().getSimpleName()); }
    }

    private static boolean portAvailable(int port) {
        try (ServerSocket socket = new ServerSocket(port, 1, InetAddress.getByName("127.0.0.1"))) { return true; }
        catch (IOException error) { return false; }
    }
}
