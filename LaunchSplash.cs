using System;
using System.IO;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

sealed class LaunchSplash : Form {
    bool loading;
    public LaunchSplash(bool preview = false, bool skipUpdate = false) {
        Text = "Voidburn";
        AccessibleName = "Voidburn loading";
        ClientSize = new Size(240, 280);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(17, 17, 17);
        ShowInTaskbar = false;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var logo = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = BackColor };
        using (var stream = new MemoryStream(Voidburn.Resource("Voidburn.png")))
        using (var image = Image.FromStream(stream)) logo.Image = new Bitmap(image);
        Controls.Add(logo);
        var status = new Label { Dock = DockStyle.Bottom, Height = 40, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(0,211,166), BackColor = BackColor, Font = new Font("Segoe UI", 9) };
        Controls.Add(status);
        FormClosed += delegate { logo.Image.Dispose(); };
        FormClosing += delegate(object sender, FormClosingEventArgs e) { if (loading) e.Cancel = true; };
        if (!preview) Shown += async delegate {
            loading = true;
            try {
                await Task.Run(() => {
                    Action<string> progress = text => { if (IsHandleCreated) Invoke((Action)(() => status.Text = text)); };
                    if (!skipUpdate && Voidburn.CheckForClientUpdate(progress)) return;
                    progress("Opening RuneLite...");
                    Voidburn.PrepareInstalledLaunch();
                    Voidburn.Launch(_ => {});
                });
            } catch (Exception error) {
                MessageBox.Show(this, Voidburn.ErrorMessage(error), "Voidburn", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } finally { loading = false; Close(); }
        };
    }
}
