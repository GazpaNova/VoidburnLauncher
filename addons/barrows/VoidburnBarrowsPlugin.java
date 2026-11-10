package net.voidburn.barrows;

import java.lang.reflect.Field;
import java.util.logging.Logger;
import javax.inject.Inject;
import net.runelite.api.Client;
import net.runelite.api.events.ClientTick;
import net.runelite.api.events.WidgetClosed;
import net.runelite.api.events.WidgetLoaded;
import net.runelite.api.gameval.InterfaceID;
import net.runelite.api.widgets.Widget;
import net.runelite.client.eventbus.Subscribe;
import net.runelite.client.plugins.Plugin;
import net.runelite.client.plugins.PluginDescriptor;
import net.runelite.client.plugins.PluginManager;
import net.runelite.client.plugins.barrows.BarrowsPlugin;

@PluginDescriptor(
    name = "Voidburn Barrows Compatibility",
    description = "Lets Barrows Brothers highlight the puzzle answer after the shapes arrive",
    enabledByDefault = true
)
public final class VoidburnBarrowsPlugin extends Plugin {
    private static final Logger LOG = Logger.getLogger(VoidburnBarrowsPlugin.class.getName());
    private static final int[] OPTIONS = {
        InterfaceID.BarrowsPuzzle.PIC_A,
        InterfaceID.BarrowsPuzzle.PIC_B,
        InterfaceID.BarrowsPuzzle.PIC_C
    };

    @Inject private Client client;
    @Inject private PluginManager plugins;

    private Field stockAnswer;
    private boolean waitingForModels;

    @Override
    protected void startUp() {
        try {
            Field field = BarrowsPlugin.class.getDeclaredField("puzzleAnswer");
            if (field.getType() == Widget.class && field.trySetAccessible()) {
                stockAnswer = field;
                return;
            }
        } catch (NoSuchFieldException | SecurityException ignored) {
            // A newer RuneLite build may have changed its implementation.
        }
        LOG.warning("This RuneLite build does not expose the Barrows answer field.");
    }

    @Override
    protected void shutDown() {
        waitingForModels = false;
        stockAnswer = null;
    }

    @Subscribe
    public void onWidgetLoaded(WidgetLoaded event) {
        if (event.getGroupId() == InterfaceID.BARROWS_PUZZLE) {
            waitingForModels = stockAnswer != null;
        }
    }

    @Subscribe
    public void onWidgetClosed(WidgetClosed event) {
        if (event.getGroupId() == InterfaceID.BARROWS_PUZZLE) {
            waitingForModels = false;
        }
    }

    @Subscribe
    public void onClientTick(ClientTick event) {
        if (!waitingForModels) return;

        for (Plugin plugin : plugins.getPlugins()) {
            if (plugin instanceof BarrowsPlugin && plugins.isPluginActive(plugin)) {
                waitingForModels = !tryAnswer((BarrowsPlugin) plugin);
                return;
            }
        }
        waitingForModels = false;
    }

    private boolean tryAnswer(BarrowsPlugin barrows) {
        if (barrows.getPuzzleAnswer() != null) return true;

        Widget firstShape = client.getWidget(InterfaceID.BarrowsPuzzle._1);
        if (firstShape == null || firstShape.getModelId() <= 3) return false;

        // The stock solver uses the first model minus three. Require all three distinct
        // option models so an incomplete update cannot produce an early highlight.
        int answerId = firstShape.getModelId() - 3;
        int received = 0;
        Widget answer = null;
        for (int optionId : OPTIONS) {
            Widget option = client.getWidget(optionId);
            if (option == null) return false;
            int offset = option.getModelId() - answerId;
            if (offset < 0 || offset > 2 || (received & (1 << offset)) != 0) return false;
            received |= 1 << offset;
            if (offset == 0) answer = option;
        }
        try {
            stockAnswer.set(barrows, answer);
        } catch (IllegalAccessException e) {
            LOG.warning("Could not update the Barrows answer for this RuneLite build.");
            stockAnswer = null;
        }
        return true;
    }
}
