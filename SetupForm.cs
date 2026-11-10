using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Diagnostics;

sealed class SetupForm:Form {
    readonly Button action; readonly Label status; bool busy; readonly bool autoPlay;
    static readonly Color Purple=Color.FromArgb(111,0,245);
    static readonly Color Teal=Color.FromArgb(0,211,166);
    static readonly Color Background=Color.FromArgb(17,17,20);
    public SetupForm(bool play) {
        autoPlay=play;Text="Voidburn — RuneLite";ClientSize=new Size(650,650);
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
        Font=new Font("Segoe UI",10);BackColor=Background;ForeColor=Color.FromArgb(224,230,235);
        Controls.Add(new Panel{BackColor=Purple,Location=Point.Empty,Size=new Size(650,6)});
        var picture=new PictureBox{Location=new Point(30,28),Size=new Size(78,78),SizeMode=PictureBoxSizeMode.Zoom,BackColor=Background};
        using(var stream=new MemoryStream(Voidburn.Resource("Voidburn.png")))using(var image=Image.FromStream(stream))picture.Image=new Bitmap(image);
        Controls.Add(picture);
        Controls.Add(new Label{Text="VOIDBURN",Location=new Point(125,28),Size=new Size(490,48),Font=new Font("Segoe UI",28,FontStyle.Bold),ForeColor=Teal});
        Controls.Add(new Label{Text="Your world. One click away.",Location=new Point(129,81),Size=new Size(470,27),ForeColor=Color.FromArgb(167,132,255)});
        Controls.Add(new Label{Text="Play the Voidburn OSRS server with RuneLite.",Location=new Point(32,139),Size=new Size(582,30),Font=new Font("Segoe UI",13,FontStyle.Bold)});
        Controls.Add(new Label{Text="Install and Play downloads the tested RSProx installer directly from its official GitHub release, verifies its SHA256 checksum, and installs RSProx with Java automatically if needed. The RSProx installer is not bundled inside Voidburn.\r\n\r\nRSProx connects RuneLite to Voidburn through a local proxy. It runs in the background while you play and stops when the game closes. RuneLite files are downloaded on first launch. Connection libraries come from the RSProx CDN, with a checksum-verified Voidburn mirror if it is unavailable.\r\n\r\nFiles are stored in your Local AppData folders: Voidburn (launcher and private profile) and RSProx (connection tools and Java). Setup creates your Voidburn shortcut; later launches check for verified client updates.\r\n\r\nUse your existing game username and password only in RuneLite. Setup does not ask for them. Your character progress stays on the server.",Location=new Point(32,181),Size=new Size(582,330)});
        status=new Label{Text="Internet access is required. Existing player settings are preserved.",Location=new Point(32,530),Size=new Size(582,46),ForeColor=Teal};Controls.Add(status);
        action=new Button{Text="Install and Play",Location=new Point(32,592),Size=new Size(255,38),BackColor=Purple,ForeColor=Color.White,FlatStyle=FlatStyle.Flat,UseVisualStyleBackColor=false};
        action.FlatAppearance.BorderColor=Purple;action.FlatAppearance.MouseOverBackColor=Color.FromArgb(135,48,255);Controls.Add(action);
        action.Click+=async delegate{await RunSetup();};
        var site=new LinkLabel{Text="Server Website",Location=new Point(472,601),Size=new Size(145,26),LinkColor=Teal,ActiveLinkColor=Color.White,VisitedLinkColor=Teal};
        site.LinkClicked+=delegate{Process.Start("https://osrs.voidburn.net/");};Controls.Add(site);
        FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy)e.Cancel=true;};
        Shown+=async delegate{if(autoPlay)await RunSetup();};
    }
    void Status(string text){if(IsHandleCreated)Invoke((Action)(()=>status.Text=text));}
    async Task RunSetup(){
        if(busy)return;busy=true;action.Enabled=false;
        try{
            await Task.Run(()=>{
                Status("Checking Voidburn and preparing your connection...");
                string target=Voidburn.DownloadTarget();Voidburn.CheckConnection(target);
                Voidburn.Install(Status,target);
                Status("Opening RuneLite...");Voidburn.Launch(Status);
            });
            busy=false;Close();
        }catch(Exception e){status.Text="Unable to finish. See the message below.";MessageBox.Show(this,Voidburn.ErrorMessage(e),"Voidburn",MessageBoxButtons.OK,MessageBoxIcon.Information);}
        finally{busy=false;action.Enabled=true;}
    }
}
