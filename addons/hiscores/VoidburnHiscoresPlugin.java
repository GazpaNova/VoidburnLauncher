package net.voidburn.hiscores;

import java.lang.reflect.Field;
import java.util.EnumMap;
import java.util.Map;
import net.runelite.client.hiscore.HiscoreEndpoint;
import net.runelite.client.plugins.Plugin;
import net.runelite.client.plugins.PluginDescriptor;
import okhttp3.HttpUrl;

@PluginDescriptor(name="Voidburn Hiscores", description="Use Voidburn player levels, XP and recorded scores in the built-in Hiscores panel", enabledByDefault=true)
public final class VoidburnHiscoresPlugin extends Plugin {
    private static final HttpUrl SERVER=HttpUrl.get("https://osrs.voidburn.net/api/hiscores/player");
    private final Map<HiscoreEndpoint,HttpUrl> original=new EnumMap<>(HiscoreEndpoint.class);
    private final Map<HiscoreEndpoint,HttpUrl> installed=new EnumMap<>(HiscoreEndpoint.class);
    private Field urlField;

    static String mode(HiscoreEndpoint endpoint) {
        switch(endpoint.name()) {
            case "NORMAL":return "normal";
            case "IRONMAN":return "ironman";
            case "HARDCORE_IRONMAN":return "hardcore";
            case "ULTIMATE_IRONMAN":return "ultimate";
            case "PURE":return "pure";
            case "LEVEL_3_SKILLER":return "skiller";
            default:return endpoint.name().toLowerCase(java.util.Locale.ROOT);
        }
    }

    @Override protected void startUp() throws Exception { install(SERVER); }

    // The installed RuneLite has no configurable endpoint API. Change only its verified
    // endpoint fields in this private Voidburn process, leaving the shared HTTP client alone.
    void install(HttpUrl base) throws Exception {
        if(!original.isEmpty())throw new IllegalStateException("Hiscores routing already active");
        Field field=HiscoreEndpoint.class.getDeclaredField("hiscoreURL");
        if(field.getType()!=HttpUrl.class)throw new IllegalStateException("Unsupported RuneLite hiscores API");
        field.setAccessible(true);
        Map<HiscoreEndpoint,HttpUrl> replacements=new EnumMap<>(HiscoreEndpoint.class);
        for(HiscoreEndpoint endpoint:HiscoreEndpoint.values()) {
            original.put(endpoint,(HttpUrl)field.get(endpoint));
            replacements.put(endpoint,base.newBuilder().addQueryParameter("mode",mode(endpoint)).build());
        }
        urlField=field;
        try {
            for(Map.Entry<HiscoreEndpoint,HttpUrl> entry:replacements.entrySet()) {
                field.set(entry.getKey(),entry.getValue());installed.put(entry.getKey(),entry.getValue());
                if(!entry.getKey().getHiscoreURL().equals(entry.getValue()))throw new IllegalStateException("Could not route hiscores");
            }
        }catch(Exception failure){restore();throw failure;}
    }

    @Override protected void shutDown() throws Exception { restore(); }
    void restore() throws Exception {
        if(urlField!=null)for(Map.Entry<HiscoreEndpoint,HttpUrl> entry:original.entrySet()) {
            if(installed.get(entry.getKey())==urlField.get(entry.getKey()))urlField.set(entry.getKey(),entry.getValue());
        }
        original.clear();installed.clear();urlField=null;
    }
}
