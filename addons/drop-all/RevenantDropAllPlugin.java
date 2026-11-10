package net.revenant.dropall;

import com.google.inject.Provides;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.regex.Pattern;
import javax.inject.Inject;
import net.runelite.api.Client;
import net.runelite.api.Item;
import net.runelite.api.ItemComposition;
import net.runelite.api.ItemContainer;
import net.runelite.api.Menu;
import net.runelite.api.MenuAction;
import net.runelite.api.MenuEntry;
import net.runelite.api.events.MenuEntryAdded;
import net.runelite.api.events.PostMenuSort;
import net.runelite.client.config.ConfigManager;
import net.runelite.client.eventbus.Subscribe;
import net.runelite.client.plugins.Plugin;
import net.runelite.client.plugins.PluginDescriptor;

/**
 * Revenant custom: a "Drop-All" entry under an inventory item's Drop that drops every slot
 * holding that item, each as the player's own Drop click would. Only for the items listed in the
 * plugin's settings (or every item with "All items"); Shift + right-click an inventory item to
 * add it to the list or remove it. With "Shift-click Drop-All", Shift + left-click on a listed
 * item is Drop-All. Off until turned on in RuneLite's plugin list.
 */
@PluginDescriptor(name = "Revenant Drop-All",
    description = "Add Drop-All under Drop for the items you list, to drop every one in your inventory",
    tags = {"drop", "inventory", "items", "revenant"},
    enabledByDefault = false)
public class RevenantDropAllPlugin extends Plugin {
    // Revision 241: the inventory (inv 93) shown in the inventory tab (group 149).
    static final int INVENTORY = 93;
    static final int INVENTORY_GROUP = 149;
    // RuneLite's KeyCode.KC_SHIFT.
    static final int SHIFT = 81;
    static final String DROP = "Drop";
    static final String EXAMINE = "Examine";
    static final String DROP_ALL = "Drop-All";
    static final String ADD = "Add to Drop-All";
    static final String REMOVE = "Remove from Drop-All";

    @Inject private Client client;
    @Inject private RevenantDropAllConfig config;
    @Inject private ConfigManager configManager;

    @Provides RevenantDropAllConfig provideConfig(ConfigManager configManager) {
        return configManager.getConfig(RevenantDropAllConfig.class);
    }

    /**
     * Revenant's only world is 1 (official worlds start at 301). RSProx hands RuneLite a loopback
     * host of its own choosing (127.0.1.3 for the Voidburn launcher, 127.1.45.2 for the Revenant
     * setup), so any 127.x host counts, as do the public address and domain.
     */
    boolean isRevenant() {
        if (client == null || client.getWorld() != 1) return false;
        String host = client.getWorldHost();
        if (host == null) return false;
        return host.startsWith("127.") || "localhost".equalsIgnoreCase(host)
            || "186.101.219.92".equals(host) || "osrs.voidburn.net".equalsIgnoreCase(host);
    }

    @Subscribe public void onMenuEntryAdded(MenuEntryAdded event) {
        MenuEntry entry = event.getMenuEntry();
        if (entry == null || entry.getOption() == null) return;
        // Other plugins may colour an option (e.g. a yellow Drop); compare the plain text.
        String option = entry.getOption().replaceAll("<[^>]*>", "");
        if (DROP.equals(option)) addDropAll(entry);
        else if (EXAMINE.equals(option) && client.isKeyPressed(SHIFT)) addListToggle(entry);
    }

    /**
     * Shift-click Drop-All: after the menu is sorted, and after Menu Entry Swapper's own swaps
     * (it runs at the default priority), Drop-All moves to the top, where it is the left-click.
     */
    @Subscribe(priority = -1) public void onPostMenuSort(PostMenuSort event) {
        if (!config.shiftClick() || client.isMenuOpen() || !client.isKeyPressed(SHIFT) || !isRevenant()) return;
        Menu menu = client.getMenu();
        MenuEntry[] entries = menu.getMenuEntries();
        for (int i = 0; i < entries.length - 1; i++) {
            if (entries[i].getType() == MenuAction.RUNELITE && DROP_ALL.equals(entries[i].getOption())) {
                MenuEntry dropAll = entries[i];
                System.arraycopy(entries, i + 1, entries, i, entries.length - 1 - i);
                entries[entries.length - 1] = dropAll;
                menu.setMenuEntries(entries);
                return;
            }
        }
    }

    private boolean inInventory(MenuEntry entry) {
        MenuAction type = entry.getType();
        return (type == MenuAction.CC_OP || type == MenuAction.CC_OP_LOW_PRIORITY)
            && (entry.getParam1() >>> 16) == INVENTORY_GROUP && entry.getItemId() > 0;
    }

    /** A non-stackable item whose fifth op is Drop: the items Drop-All can apply to. */
    private ItemComposition eligible(int itemId) {
        ItemComposition item = client.getItemDefinition(itemId);
        if (item == null || item.isStackable() || item.getNote() != -1 || item.getPlaceholderTemplateId() != -1) return null;
        String[] actions = item.getInventoryActions();
        if (actions == null || actions.length < 5 || !DROP.equalsIgnoreCase(actions[4])) return null;
        return item;
    }

    private void addDropAll(MenuEntry drop) {
        if (!inInventory(drop) || !isRevenant()) return;
        ItemComposition item = eligible(drop.getItemId());
        if (item == null || !(config.allItems() || matches(config.items(), item.getName()))) return;
        if (slotsOf(drop.getItemId()).size() < 2) return;
        int itemId = drop.getItemId(), widgetId = drop.getParam1(), op = drop.getIdentifier();
        MenuAction type = drop.getType();
        String target = drop.getTarget();
        // Just under Drop, so it is never the left-click option.
        insertUnder(drop).setOption(DROP_ALL).setTarget(target).setType(MenuAction.RUNELITE)
            .onClick(e -> dropAll(itemId, widgetId, op, type, target));
    }

    private void addListToggle(MenuEntry examine) {
        if (!inInventory(examine) || !isRevenant() || config.allItems()) return;
        ItemComposition item = eligible(examine.getItemId());
        if (item == null) return;
        String name = item.getName();
        insertUnder(examine).setOption(isListed(config.items(), name) ? REMOVE : ADD)
            .setTarget(examine.getTarget()).setType(MenuAction.RUNELITE)
            .onClick(e -> saveItems(toggleListed(config.items(), name)));
    }

    private MenuEntry insertUnder(MenuEntry entry) {
        Menu menu = client.getMenu();
        MenuEntry[] entries = menu.getMenuEntries();
        int at = entries.length - 1;
        for (int i = entries.length - 1; i >= 0; i--) {
            if (entries[i] == entry) { at = i; break; }
        }
        return menu.createMenuEntry(at);
    }

    void saveItems(String csv) {
        configManager.setConfiguration(RevenantDropAllConfig.GROUP, RevenantDropAllConfig.ITEMS, csv);
    }

    List<Integer> slotsOf(int itemId) {
        List<Integer> slots = new ArrayList<>();
        ItemContainer inventory = client.getItemContainer(INVENTORY);
        if (inventory == null) return slots;
        Item[] items = inventory.getItems();
        for (int slot = 0; slot < items.length; slot++) {
            if (items[slot] != null && items[slot].getId() == itemId) slots.add(slot);
        }
        return slots;
    }

    void dropAll(int itemId, int widgetId, int op, MenuAction type, String target) {
        if (!isRevenant()) return;
        for (int slot : slotsOf(itemId)) {
            client.menuAction(slot, widgetId, type, op, itemId, DROP, target);
        }
    }

    // ---- the item list: the same format as the browser plugin and Ground Items' lists ----

    static List<String> entries(String csv) {
        List<String> out = new ArrayList<>();
        if (csv == null) return out;
        for (String part : csv.split(",|\\r?\\n")) {
            String trimmed = part.trim();
            if (!trimmed.isEmpty()) out.add(trimmed);
        }
        return out;
    }

    static String normalize(String name) {
        String s = name.trim();
        if (s.length() >= 2 && s.startsWith("\"") && s.endsWith("\"")) s = s.substring(1, s.length() - 1);
        return s.trim().toLowerCase(Locale.ROOT).replaceAll("\\s+", " ");
    }

    private static boolean quoted(String entry) {
        return entry.length() >= 2 && entry.startsWith("\"") && entry.endsWith("\"");
    }

    static boolean matches(String csv, String itemName) {
        String name = normalize(itemName);
        for (String entry : entries(csv)) {
            String pattern = normalize(entry);
            if (!quoted(entry) && pattern.contains("*")) {
                StringBuilder regex = new StringBuilder();
                for (String part : pattern.split("\\*", -1)) {
                    if (regex.length() > 0) regex.append(".*");
                    regex.append(Pattern.quote(part));
                }
                if (name.matches(regex.toString())) return true;
            } else if (pattern.equals(name)) {
                return true;
            }
        }
        return false;
    }

    static boolean isListed(String csv, String itemName) {
        for (String entry : entries(csv)) {
            if ((quoted(entry) || !entry.contains("*")) && normalize(entry).equals(normalize(itemName))) return true;
        }
        return false;
    }

    /** The list without [itemName]'s exact entries, or with it added when it had none. */
    static String toggleListed(String csv, String itemName) {
        List<String> all = entries(csv);
        List<String> kept = new ArrayList<>();
        for (String entry : all) {
            boolean exact = (quoted(entry) || !entry.contains("*")) && normalize(entry).equals(normalize(itemName));
            if (!exact) kept.add(entry);
        }
        if (kept.size() == all.size()) kept.add(itemName);
        return String.join(", ", kept);
    }
}
