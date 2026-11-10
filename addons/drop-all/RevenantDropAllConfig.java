package net.revenant.dropall;

import net.runelite.client.config.Config;
import net.runelite.client.config.ConfigGroup;
import net.runelite.client.config.ConfigItem;

@ConfigGroup(RevenantDropAllConfig.GROUP)
public interface RevenantDropAllConfig extends Config {
    String GROUP = "revenantdropall";
    String ITEMS = "items";

    @ConfigItem(keyName = ITEMS, name = "Items", position = 0,
        description = "Items that get Drop-All under Drop. Format: item1, item2, *ore, \"exact name\". "
            + "Shift + right-click an inventory item to add or remove it.")
    default String items() { return ""; }

    @ConfigItem(keyName = "allItems", name = "All items", position = 1,
        description = "Give every droppable, non-stackable item Drop-All, ignoring the list")
    default boolean allItems() { return false; }

    @ConfigItem(keyName = "shiftClick", name = "Shift-click Drop-All", position = 2,
        description = "Shift + left-click drops every one of a listed item (overrides Menu Entry Swapper's shift-click for those items)")
    default boolean shiftClick() { return false; }
}
