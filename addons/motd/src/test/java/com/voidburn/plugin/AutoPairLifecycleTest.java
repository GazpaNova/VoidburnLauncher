package com.voidburn.plugin;

import java.lang.reflect.Field;
import java.util.concurrent.atomic.AtomicReference;
import net.runelite.api.Client;
import net.runelite.api.Player;
import net.runelite.api.GameState;
import net.runelite.api.events.GameTick;
import net.runelite.api.events.GameStateChanged;
import net.runelite.api.events.ChatMessage;
import net.runelite.api.ChatMessageType;
import net.runelite.client.config.ConfigManager;
import org.junit.jupiter.api.Test;
import static org.mockito.Mockito.*;

class AutoPairLifecycleTest {
    private void inject(Object target, String name, Object value) throws Exception {
        Field field = target.getClass().getDeclaredField(name);
        field.setAccessible(true);
        field.set(target, value);
    }

    @Test void delayed_player_after_both_login_and_token_is_captured_once() throws Exception {
        var plugin = new VoidburnPlugin();
        var client = mock(Client.class);
        var config = mock(VoidburnConfig.class);
        var manager = mock(ConfigManager.class);
        var player = mock(Player.class);
        var name = new AtomicReference<>("");
        when(config.autoPair()).thenReturn(true);
        when(config.username()).thenAnswer(i -> name.get());
        when(config.token()).thenReturn("");
        when(client.getGameState()).thenReturn(GameState.LOGGED_IN);
        inject(plugin,"client",client); inject(plugin,"config",config); inject(plugin,"configManager",manager);
        doAnswer(i -> { name.set(i.getArgument(2)); return null; }).when(manager)
            .setConfiguration(eq(VoidburnConfig.GROUP),eq(VoidburnConfig.KEY_USERNAME),any());
        var login = new GameStateChanged(); login.setGameState(GameState.LOGGED_IN);
        plugin.onGameStateChanged(login);
        var chat = new ChatMessage(); chat.setType(ChatMessageType.GAMEMESSAGE);
        chat.setMessage("RuneLite plugin token: test-only-token");
        plugin.onChatMessage(chat);
        plugin.onGameTick(new GameTick()); // still null
        when(client.getLocalPlayer()).thenReturn(player);
        when(player.getName()).thenReturn("");
        plugin.onGameTick(new GameTick()); // name not initialized either
        when(player.getName()).thenReturn("Gazpa");
        plugin.onGameTick(new GameTick());
        plugin.onGameTick(new GameTick());
        verify(manager,times(1)).setConfiguration(VoidburnConfig.GROUP,VoidburnConfig.KEY_USERNAME,"Gazpa");
        verify(manager,times(1)).setConfiguration(VoidburnConfig.GROUP,VoidburnConfig.KEY_TOKEN,"test-only-token");
    }

    @Test void retry_ignores_logged_out_and_disabled_auto_pair() throws Exception {
        var plugin = new VoidburnPlugin(); var client=mock(Client.class);
        var config=mock(VoidburnConfig.class); var manager=mock(ConfigManager.class);
        inject(plugin,"client",client); inject(plugin,"config",config); inject(plugin,"configManager",manager);
        when(config.autoPair()).thenReturn(true);
        when(client.getGameState()).thenReturn(GameState.LOGIN_SCREEN);
        plugin.onGameTick(new GameTick());
        when(config.autoPair()).thenReturn(false);
        when(client.getGameState()).thenReturn(GameState.LOGGED_IN);
        plugin.onGameTick(new GameTick());
        verifyNoInteractions(manager); verify(client,never()).getLocalPlayer();
    }
}