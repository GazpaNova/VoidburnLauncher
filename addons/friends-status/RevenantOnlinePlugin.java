package net.revenant.friendsonline;

import java.util.IdentityHashMap;
import java.util.Map;
import java.util.regex.Pattern;
import javax.inject.Inject;
import net.runelite.api.Client;
import net.runelite.api.events.GameTick;
import net.runelite.api.events.ScriptPostFired;
import net.runelite.api.events.WidgetLoaded;
import net.runelite.api.widgets.Widget;
import net.runelite.client.eventbus.Subscribe;
import net.runelite.client.plugins.Plugin;
import net.runelite.client.plugins.PluginDescriptor;

@PluginDescriptor(name = "Revenant Online Status",
    description = "Show online friends with a green Online label on Revenant",
    enabledByDefault = true)
public class RevenantOnlinePlugin extends Plugin {
    // Verified from the revision-241 cache and installed RuneLite WidgetInfo.
    static final int FRIENDS_GROUP = 429;
    static final int FRIENDS_ROWS = 11;
    static final int FRIENDS_UPDATE_SCRIPT = 125;
    static final int GREEN = 0x00ff00;
    private static final Pattern WORLD_LABEL = Pattern.compile("(?:World|Classic) \\d+");
    private static final Pattern TAG = Pattern.compile("<[^>]*>");

    @Inject private Client client;
    private final Map<Widget, OriginalLabel> changed = new IdentityHashMap<>();

    @Subscribe public void onScriptPostFired(ScriptPostFired event) {
        if (event.getScriptId() == FRIENDS_UPDATE_SCRIPT) updateLabels();
    }

    @Subscribe public void onWidgetLoaded(WidgetLoaded event) {
        if (event.getGroupId() == FRIENDS_GROUP) updateLabels();
    }

    @Subscribe public void onGameTick(GameTick event) { updateLabels(); }

    private boolean isRevenant() {
        if (client == null || client.getWorld() != 1) return false;
        String host = client.getWorldHost();
        return "186.101.219.92".equals(host) || "osrs.voidburn.net".equalsIgnoreCase(host)
            || "127.0.0.1".equals(host) || "127.1.45.2".equals(host) || "localhost".equalsIgnoreCase(host);
    }

    void updateLabels() {
        if (!isRevenant()) { restoreLabels(); return; }
        Widget root = client.getWidget(FRIENDS_GROUP, FRIENDS_ROWS);
        if (root == null) { changed.clear(); return; }
        Widget[] rows = root.getDynamicChildren();
        if (rows == null) { changed.clear(); return; }
        Map<Widget, OriginalLabel> current = new IdentityHashMap<>();
        for (Widget row : rows) {
            if (row == null || row.getXTextAlignment() != 2) continue;
            String text = row.getText();
            if (text == null) continue;
            String plain = TAG.matcher(text).replaceAll("");
            if (WORLD_LABEL.matcher(plain).matches()) {
                current.put(row, new OriginalLabel(text, row.getTextColor()));
                row.setText("Online");
                row.setTextColor(GREEN);
            } else if ("Online".equals(text) && changed.containsKey(row)) {
                current.put(row, changed.get(row));
            }
        }
        changed.clear();
        changed.putAll(current);
    }

    private void restoreLabels() {
        changed.forEach((widget, original) -> {
            if ("Online".equals(widget.getText())) {
                widget.setText(original.text);
                widget.setTextColor(original.color);
            }
        });
        changed.clear();
    }

    @Override protected void shutDown() { restoreLabels(); }

    private static final class OriginalLabel {
        final String text;
        final int color;
        OriginalLabel(String text, int color) { this.text = text; this.color = color; }
    }
}
