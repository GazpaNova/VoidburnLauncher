package net.voidburn.ping;

import java.awt.Color;
import java.awt.Dimension;
import java.awt.Graphics2D;
import java.awt.FontMetrics;
import java.io.IOException;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;
import java.util.function.LongSupplier;
import javax.inject.Inject;
import net.runelite.api.Client;
import net.runelite.api.GameState;
import net.runelite.api.Point;
import net.runelite.api.widgets.Widget;
import net.runelite.api.events.GameTick;
import net.runelite.api.events.GameStateChanged;
import net.runelite.client.eventbus.Subscribe;
import net.runelite.client.plugins.Plugin;
import net.runelite.client.plugins.PluginDescriptor;
import net.runelite.client.plugins.PluginManager;
import net.runelite.client.plugins.worldhopper.WorldHopperPlugin;
import net.runelite.client.ui.overlay.Overlay;
import net.runelite.client.ui.overlay.OverlayManager;
import net.runelite.client.ui.overlay.OverlayLayer;
import net.runelite.client.ui.overlay.OverlayPosition;
import net.runelite.client.ui.overlay.OverlayUtil;

@PluginDescriptor(name = "Voidburn Ping", description = "Estimated TCP latency to the remote Voidburn gateway, rather than the local proxy", tags = {"ping", "latency", "voidburn"}, enabledByDefault = true)
public class VoidburnPingPlugin extends Plugin {
    static final String HOST = "186.101.219.92";
    static final int PORT = 43596;
    static final int TIMEOUT_MS = 2000;
    private static final String STOCK_OVERLAY = "net.runelite.client.plugins.worldhopper.WorldHopperPingOverlay";
    @Inject private Client client;
    @Inject private OverlayManager overlays;
    @Inject private PluginManager plugins;
    private final List<Overlay> hidden = new ArrayList<>();
    private final Reading reading = new Reading();
    private PingOverlay overlay;
    private ScheduledExecutorService worker;

    static boolean isVoidburn(int world, String host) {
        return world == 1 && host != null && (host.startsWith("127.") || "localhost".equalsIgnoreCase(host)
            || HOST.equals(host) || "osrs.voidburn.net".equalsIgnoreCase(host));
    }

    @Override protected void startUp() {
        overlay = new PingOverlay(this);
        overlays.add(overlay);
        refresh();
        worker = Executors.newSingleThreadScheduledExecutor(r -> {
            Thread t = new Thread(r, "Voidburn-ping"); t.setDaemon(true); return t;
        });
        worker.scheduleWithFixedDelay(this::sample, 0, 5, TimeUnit.SECONDS);
    }

    @Override protected void shutDown() {
        reading.activate(false);
        if (worker != null) { worker.shutdownNow(); worker = null; }
        if (overlay != null) { overlays.remove(overlay); overlay = null; }
        restoreStock();
    }

    @Subscribe public void onGameTick(GameTick event) { refresh(); }
    @Subscribe public void onGameStateChanged(GameStateChanged event) { refresh(); }

    private void refresh() {
        boolean active = client.getGameState() == GameState.LOGGED_IN && isVoidburn(client.getWorld(), client.getWorldHost());
        reading.activate(active);
        if (active) {
            // Suppress only the local-socket ping overlay; retain the original plugin and settings.
            overlays.removeIf(item -> {
                if (!STOCK_OVERLAY.equals(item.getClass().getName())) return false;
                if (!hidden.contains(item)) hidden.add(item);
                return true;
            });
        } else restoreStock();
    }

    private void restoreStock() {
        boolean running = plugins.getPlugins().stream().anyMatch(p -> p instanceof WorldHopperPlugin && plugins.isPluginActive(p));
        if (running) for (Overlay item : hidden) overlays.add(item);
        hidden.clear();
    }

    private void sample() {
        long token = reading.begin();
        if (token < 0) return;
        int value;
        try { value = measure(VoidburnPingPlugin::connect, System::nanoTime); }
        catch (IOException | RuntimeException e) { value = -1; }
        reading.complete(token, value);
    }

    interface Connector { void connect() throws IOException; }
    static void connect() throws IOException {
        try (Socket socket = new Socket()) { socket.connect(new InetSocketAddress(HOST, PORT), TIMEOUT_MS); }
    }
    static int measure(Connector connector, LongSupplier clock) throws IOException {
        long start = clock.getAsLong();
        connector.connect();
        return (int)Math.max(1L, Math.min(Integer.MAX_VALUE, Math.round((clock.getAsLong() - start) / 1_000_000.0)));
    }

    static final class Reading {
        private boolean active;
        private long generation;
        private int value = -2;
        synchronized void activate(boolean next) {
            if (active != next) { active = next; generation++; value = -2; }
        }
        synchronized long begin() { return active ? generation : -1; }
        synchronized void complete(long token, int ms) { if (active && generation == token) value = ms; }
        synchronized int value() { return value; }
        synchronized boolean active() { return active; }
    }

    static final class PingOverlay extends Overlay {
        // Same resizable side-panel widget and offsets used by RuneLite 1.13.4's ping overlay.
        private static final int SIDE_PANEL_WIDGET = (164 << 16) | 35;
        private final VoidburnPingPlugin owner;
        PingOverlay(VoidburnPingPlugin owner) {
            super(owner);
            this.owner = owner;
            setLayer(OverlayLayer.ABOVE_WIDGETS);
            setPriority(0.75f);
            setPosition(OverlayPosition.DYNAMIC);
        }
        @Override public Dimension render(Graphics2D graphics) {
            if (!owner.reading.active()) return null;
            int ms = owner.reading.value();
            String text = ms == -2 ? "Ping: measuring..." : ms < 0 ? "Ping: unavailable" : ms + " ms";
            int rightMargin = 1;
            Widget sidePanel = owner.client.getWidget(SIDE_PANEL_WIDGET);
            if (sidePanel != null && !sidePanel.isHidden()) rightMargin += sidePanel.getWidth();
            FontMetrics font = graphics.getFontMetrics();
            int x = owner.client.getRealDimensions().width - font.stringWidth(text) - rightMargin;
            int y = font.getAscent() - font.getDescent() + 11;
            OverlayUtil.renderTextLocation(graphics, new Point(x, y), text, Color.YELLOW);
            return null;
        }
    }
}
