package com.voidburn.plugin;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;
import com.google.gson.*;
import java.awt.*;
import java.awt.image.BufferedImage;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.Optional;
import javax.imageio.ImageIO;
import net.runelite.api.Client;
import org.junit.jupiter.api.Test;

class MotdArtworkTest {
    @Test void shipped_artwork_and_all_skill_icons_are_available() {
        assertTrue(MotdArtwork.LOGO.getWidth()>40);
        assertEquals(4,MotdArtwork.TIERS.size());
        int skills=0;
        for(var tier:MotdArtwork.TIERS) for(String skill:tier.skills()){
            assertNotNull(MotdArtwork.skillImage(skill),skill+" icon missing");skills++;
        }
        assertEquals(24,skills);
        assertEquals(Font.SANS_SERIF,MotdArtwork.font(12,false).getFamily());
    }
    @Test void render_real_size_and_keep_button_hit_regions_aligned() throws Exception {
        var state=new InterfaceState(); state.apply(new Frame.WidgetSetRoot(30020));
        try(var reader=new InputStreamReader(MotdArtwork.class.getResourceAsStream("motd-defaults.json"),StandardCharsets.UTF_8)){
            var j=new JsonParser().parse(reader).getAsJsonObject();
            String[] keys={"title","tagline","aboutHeading","aboutBody","ratesHeading","changesHeading","changesBody","whatsNewHeading","whatsNewBody"};
            int[] ids={3,4,10,11,20,160,161,170,171};
            for(int i=0;i<ids.length;i++) state.apply(new Frame.WidgetSetText((30020<<16)|ids[i],j.get(keys[i]).getAsString()));
            state.apply(new Frame.WidgetSetText((30020<<16)|180,"Discord: "+j.get("discordUrl").getAsString()));
            state.apply(new Frame.WidgetSetText((30020<<16)|181,"Source: "+j.get("githubUrl").getAsString()));
            state.apply(new Frame.WidgetSetText((30020<<16)|182,"Launcher: "+j.get("launcherUrl").getAsString()));
        }
        var client=mock(Client.class); Canvas canvas=new Canvas();when(client.getCanvas()).thenReturn(canvas);
        var overlay=new MotdOverlay(client,()->Optional.of(state));
        for(int[] size:new int[][]{{765,503},{1024,768}}){
            canvas.setSize(size[0],size[1]);var image=new BufferedImage(size[0],size[1],BufferedImage.TYPE_INT_ARGB);
            Graphics2D g=image.createGraphics();g.setFont(new Font(Font.MONOSPACED,Font.PLAIN,8));
            try {overlay.render(g);} finally {g.dispose();}
            int px=(size[0]-480)/2,py=(size[1]-460)/2;
            assertTrue(overlay.hitTest(px+340,py+415).isPresent(),"painted Play button clickable");
            assertEquals(0xff14100b,image.getRGB(0,0),"opaque fullscreen scrim");
            assertNotEquals(image.getRGB(px+17,py+11),image.getRGB(px+35,py+30),"logo has painted pixels");
            Path out=Path.of("build","motd-preview-"+size[0]+".png");Files.createDirectories(out.getParent());
            ImageIO.write(image,"png",out.toFile());
        }
    }
}
