package com.voidburn.plugin;

import com.google.gson.*;
import java.awt.*;
import java.awt.image.BufferedImage;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import javax.imageio.ImageIO;
import net.runelite.api.Skill;
import net.runelite.client.game.SkillIconManager;

/** Static artwork/tiers mirror the browser's shipped definition; text remains live server content. */
final class MotdArtwork {
    record Tier(String label, List<String> skills) {}
    static final Color TEAL = new Color(0x2fd4b4);
    static final Color BODY = new Color(0xe8ded0);
    static final BufferedImage LOGO;
    static final List<Tier> TIERS;
    private static final SkillIconManager ICONS = new SkillIconManager();
    static {
        try (InputStream logo = resource("logo.png");
             Reader reader = new InputStreamReader(resource("motd-defaults.json"), StandardCharsets.UTF_8)) {
            LOGO = ImageIO.read(logo);
            if (LOGO == null) throw new IOException("Invalid MOTD logo");
            JsonObject defaults = new JsonParser().parse(reader).getAsJsonObject();
            List<Tier> tiers = new ArrayList<>();
            for (JsonElement element : defaults.getAsJsonArray("ratesTiers")) {
                JsonObject tier = element.getAsJsonObject();
                List<String> skills = new ArrayList<>();
                for (JsonElement skill : tier.getAsJsonArray("skills")) skills.add(skill.getAsString());
                tiers.add(new Tier(tier.get("label").getAsString(), List.copyOf(skills)));
            }
            TIERS = List.copyOf(tiers);
        } catch (IOException e) { throw new ExceptionInInitializerError(e); }
    }
    private static InputStream resource(String name) throws IOException {
        InputStream stream = MotdArtwork.class.getResourceAsStream(name);
        if (stream == null) throw new IOException("Missing MOTD asset " + name);
        return stream;
    }
    static Font font(int size, boolean bold) {
        // Do not derive from RuneLite's pixel font: it retains its bitmap family.
        return new Font(Font.SANS_SERIF, bold ? Font.BOLD : Font.PLAIN, size);
    }
    static BufferedImage skillImage(String skill) {
        try { return ICONS.getSkillImage(Skill.valueOf(skill.equals("Runecrafting") ? "RUNECRAFT" : skill.toUpperCase(java.util.Locale.ROOT))); }
        catch (IllegalArgumentException e) { return null; }
    }
    static void draw(Graphics2D g, MotdPresentation p, int x, int y) {
        g.setRenderingHint(RenderingHints.KEY_INTERPOLATION, RenderingHints.VALUE_INTERPOLATION_BILINEAR);
        g.drawImage(LOGO, x+16, y+10, 44,44,null);
        line(g,p.title(),x+68,y+10,396,24,18,true,TEAL);
        line(g,p.tagline(),x+68,y+36,396,18,11,false,BODY);
        int[][] boxes={{64,82,64},{152,170,112},{288,306,26},{338,356,34}};
        for(int i=0;i<Math.min(4,p.sections().size());i++){
            var s=p.sections().get(i); var b=boxes[i];
            line(g,s.heading(),x+16,y+b[0],448,16,13,true,TEAL);
            if(i!=1) paragraph(g,s.body(),x+16,y+b[1],448,b[2]);
        }
        for(int row=0;row<Math.min(4,TIERS.size());row++){
            var tier=TIERS.get(row); int ry=y+170+row*28;
            g.setColor(new Color(row%2==0?0x251b32:0x1e1829));g.fillRect(x+16,ry,448,26);
            line(g,tier.label(),x+20,ry+4,32,22,13,true,TEAL);
            for(int slot=0;slot<Math.min(11,tier.skills().size());slot++){
                String skill=tier.skills().get(slot); int ix=x+52+slot*28;
                BufferedImage icon=skillImage(skill);
                if(icon!=null) g.drawImage(icon,ix,ry+1,24,24,null);
                else line(g,skill.substring(0,Math.min(3,skill.length())),ix,ry+5,27,20,10,false,BODY);
            }
        }
    }
    private static void line(Graphics2D g,String text,int x,int y,int w,int h,int size,boolean bold,Color color){
        Graphics2D c=(Graphics2D)g.create();
        try{c.clipRect(x,y,w,h);c.setFont(font(size,bold));c.setColor(color);
            c.drawString(text==null?"":text,x,y+c.getFontMetrics().getAscent());}
        finally{c.dispose();}
    }
    static int fitSize(Graphics2D g,String text,int width,int height) {
        for(int size=12;size>=10;size--){
            FontMetrics fm=g.getFontMetrics(font(size,false)); int lines=0;
            for(String paragraph:text.split("\\n",-1)){
                String line=""; lines++;
                for(String word:paragraph.split(" ")){
                    String candidate=line.isEmpty()?word:line+" "+word;
                    if(!line.isEmpty() && fm.stringWidth(candidate)>width){lines++;line=word;}
                    else line=candidate;
                }
            }
            if(lines*fm.getHeight()<=height) return size;
        }
        return 10;
    }
    private static void paragraph(Graphics2D g,String text,int x,int y,int w,int h){
        Graphics2D c=(Graphics2D)g.create();
        try{c.clipRect(x,y,w,h);c.setFont(font(fitSize(c,text,w,h),false));c.setColor(BODY);
            FontMetrics fm=c.getFontMetrics();int cy=y;
            for(String paragraph:text.split("\n",-1)){
                String line="";
                for(String word:paragraph.split(" ")){
                    String candidate=line.isEmpty()?word:line+" "+word;
                    if(!line.isEmpty() && fm.stringWidth(candidate)>w){
                        c.drawString(line,x,cy+fm.getAscent());cy+=fm.getHeight();line=word;
                    }else line=candidate;
                }
                c.drawString(line,x,cy+fm.getAscent());cy+=fm.getHeight();
            }
        }finally{c.dispose();}
    }
}
