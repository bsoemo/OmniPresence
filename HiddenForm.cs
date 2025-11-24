using System.Windows.Forms;

namespace OmniPresence;

public partial class HiddenForm : Form
{
    public HiddenForm()
    {
        InitializeComponent();
        this.ShowInTaskbar = false;
        this.WindowState = FormWindowState.Minimized;
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.ClientSize = new System.Drawing.Size(0, 0);
        this.ShowInTaskbar = false;
        this.WindowState = FormWindowState.Minimized;
        this.ResumeLayout(false);
    }
}
