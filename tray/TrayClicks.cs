using System;
using System.Drawing;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

internal sealed class TrayClicks : IDisposable {
    readonly NotifyIcon icon;
    readonly Action open;
    readonly Func<string> status;
    readonly Timer single = new Timer { Interval = SystemInformation.DoubleClickTime + 50 };
    readonly Timer dismiss = new Timer { Interval = 6000 };
    readonly StatusWindow popup = new StatusWindow();
    Point clickLocation;
    readonly Label label = new Label {
        AutoSize = true, MaximumSize = new Size(340,0), Padding = new Padding(12,8,12,8),
        BackColor = SystemColors.Info, ForeColor = SystemColors.InfoText
    };

    internal TrayClicks(NotifyIcon icon, Func<string> status, Action open) {
        this.icon = icon;
        this.status = status;
        this.open = open;
        popup.Controls.Add(label);
        icon.MouseClick += Click;
        icon.MouseDoubleClick += DoubleClick;
        single.Tick += delegate { single.Stop(); ShowStatus(); };
        dismiss.Tick += delegate { dismiss.Stop(); popup.Hide(); };
    }

    void Click(object? sender, MouseEventArgs e) {
        if (e.Button == MouseButtons.Left && e.Clicks <= 1) {
            QueueStatus();
        } else if (e.Button == MouseButtons.Right) single.Stop();
    }

    void DoubleClick(object? sender, MouseEventArgs e) {
        if (e.Button != MouseButtons.Left) return;
        var position = Cursor.Position;
        var tolerance = SystemInformation.DoubleClickSize;
        if (!single.Enabled || Math.Abs(position.X - clickLocation.X) > tolerance.Width / 2 ||
            Math.Abs(position.Y - clickLocation.Y) > tolerance.Height / 2) {
            QueueStatus();
            return;
        }
        single.Stop();
        dismiss.Stop();
        popup.Hide();
        open();
    }

    void QueueStatus() {
        single.Stop();
        clickLocation = Cursor.Position;
        single.Start();
    }

    void ShowStatus() {
        label.Text = status();
        Point cursor = clickLocation;
        Rectangle area = Screen.FromPoint(cursor).WorkingArea;
        Size size = label.GetPreferredSize(Size.Empty);
        popup.ClientSize = size;
        int x = Math.Max(area.Left, Math.Min(cursor.X - size.Width / 2, area.Right - size.Width));
        int y = Math.Max(area.Top, Math.Min(cursor.Y - size.Height - 32, area.Bottom - size.Height - 24));
        popup.Location = new Point(x,y);
        if (!popup.Visible) popup.Show();
        dismiss.Stop();
        dismiss.Start();
    }

    sealed class StatusWindow : Form {
        public StatusWindow() {
            ShowInTaskbar = false;
            TopMost = true;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = SystemColors.Info;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams {
            get {
                var parameters = base.CreateParams;
                parameters.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return parameters;
            }
        }
    }

    public void Dispose() {
        icon.MouseClick -= Click;
        icon.MouseDoubleClick -= DoubleClick;
        single.Dispose();
        dismiss.Dispose();
        popup.Dispose();
    }
}
