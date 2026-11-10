package net.voidburn.barrows;

import java.lang.reflect.Field;
import java.lang.reflect.Proxy;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import net.runelite.api.Client;
import net.runelite.api.events.ClientTick;
import net.runelite.api.events.WidgetClosed;
import net.runelite.api.events.WidgetLoaded;
import net.runelite.api.gameval.InterfaceID;
import net.runelite.api.widgets.Widget;
import net.runelite.client.plugins.PluginManager;
import net.runelite.client.plugins.barrows.BarrowsPlugin;

public final class VoidburnBarrowsPluginTest {
    private static final int[] OPTION_IDS = {
        InterfaceID.BarrowsPuzzle.PIC_A, InterfaceID.BarrowsPuzzle.PIC_B, InterfaceID.BarrowsPuzzle.PIC_C
    };

    private static Field field(Class<?> type, String name) throws ReflectiveOperationException {
        Field field = type.getDeclaredField(name);
        field.setAccessible(true);
        return field;
    }

    private static void check(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }

    private static Widget shape(int model) {
        return (Widget) Proxy.newProxyInstance(Widget.class.getClassLoader(), new Class<?>[]{Widget.class},
            (proxy, method, arguments) -> {
                if (method.getName().equals("getModelId")) return model;
                throw new AssertionError(method.getName());
            });
    }

    public static void main(String[] args) throws ReflectiveOperationException {
        Map<Integer, Widget> widgets = new HashMap<>();
        Client client = (Client) Proxy.newProxyInstance(Client.class.getClassLoader(), new Class<?>[]{Client.class},
            (proxy, method, arguments) -> {
                if (method.getName().equals("getWidget") && arguments.length == 1) return widgets.get(arguments[0]);
                throw new AssertionError(method.getName());
            });
        BarrowsPlugin stock = new BarrowsPlugin();
        field(BarrowsPlugin.class, "client").set(stock, client);
        var constructor = PluginManager.class.getDeclaredConstructors()[0];
        constructor.setAccessible(true);
        PluginManager manager = (PluginManager) constructor.newInstance(true, false, null, null, null, null, null);
        manager.getPlugins().add(stock);
        var active = field(PluginManager.class, "activePlugins");
        active.set(manager, List.of(stock));

        VoidburnBarrowsPlugin fix = new VoidburnBarrowsPlugin();
        field(VoidburnBarrowsPlugin.class, "client").set(fix, client);
        field(VoidburnBarrowsPlugin.class, "plugins").set(fix, manager);
        fix.startUp();
        WidgetLoaded opened = new WidgetLoaded();
        opened.setGroupId(InterfaceID.BARROWS_PUZZLE);
        WidgetClosed closed = new WidgetClosed(InterfaceID.BARROWS_PUZZLE, 0, true);
        ClientTick tick = new ClientTick();

        int[][] orders = {{0, 1, 2}, {0, 2, 1}, {1, 0, 2}, {2, 0, 1}, {1, 2, 0}, {2, 1, 0}};
        for (int answerId : new int[]{6713, 6719, 6725, 6731}) {
            for (int[] order : orders) {
                widgets.clear();
                widgets.put(InterfaceID.BarrowsPuzzle._1, shape(-1));
                for (int id : OPTION_IDS) widgets.put(id, shape(-1));
                stock.onWidgetLoaded(opened);
                fix.onWidgetLoaded(opened);
                check(stock.getPuzzleAnswer() == null, "stock missed shapes at open");
                fix.onClientTick(tick);
                check(stock.getPuzzleAnswer() == null, "blank puzzle cannot have an answer");

                widgets.put(InterfaceID.BarrowsPuzzle._1, shape(answerId + 3));
                int answerSlot = 0;
                for (int slot = 0; slot < OPTION_IDS.length; slot++) {
                    widgets.put(OPTION_IDS[slot], shape(answerId + order[slot]));
                    if (order[slot] == 0) answerSlot = slot;
                    fix.onClientTick(tick);
                    if (slot < 2) check(stock.getPuzzleAnswer() == null, "wait for the remaining models");
                }
                check(stock.getPuzzleAnswer() == widgets.get(OPTION_IDS[answerSlot]), "highlight the correct answer");
                stock.onWidgetClosed(closed);
                fix.onWidgetClosed(closed);
                fix.onClientTick(tick);
                check(stock.getPuzzleAnswer() == null, "closing cancels the retry even if widgets still exist");
            }
        }

        fix.onWidgetLoaded(opened);
        stock.onWidgetLoaded(opened);
        Widget original = stock.getPuzzleAnswer();
        check(original != null, "stock can solve a complete puzzle at open");
        widgets.put(OPTION_IDS[2], shape(6731));
        fix.onClientTick(tick);
        check(stock.getPuzzleAnswer() == original, "keep a valid stock answer");

        stock.onWidgetClosed(closed);
        active.set(manager, List.of());
        fix.onWidgetLoaded(opened);
        fix.onClientTick(tick);
        check(stock.getPuzzleAnswer() == null, "do not activate a disabled plugin");
        fix.shutDown();
        System.out.println("PASS: reproduced the stock timing miss, verified all 24 pattern/order combinations and puzzle lifecycle");
    }
}
