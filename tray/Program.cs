using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace AudioSwitch.Tray;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var instance = new Mutex(true, @"Local\AudioSwitchTray", out bool first);
        if (!first) return;
        ApplicationConfiguration.Initialize();
        using var popup = new Popup();
        if (!args.Contains("--minimized")) _ = popup.ShowPopup();
        Application.Run();
    }
}

internal sealed class Popup : Form
{
    private static readonly Color Ink = Color.FromArgb(23, 42, 58);
    private static readonly Color Canvas = Color.FromArgb(241, 246, 250);
    private static readonly Color Accent = Color.FromArgb(14, 105, 86);
    private static readonly Color Border = Color.FromArgb(193, 211, 224);
    private static readonly Color Hero = Color.FromArgb(22, 48, 66);
    private readonly ToolTip deviceTips = new() { ShowAlways = true };
    private readonly Icon audioIcon = CreateAudioIcon(Hero);
    private readonly Icon bluetoothOnIcon = CreateAudioIcon(Color.FromArgb(23, 93, 197));
    private readonly Icon bluetoothOffIcon = CreateAudioIcon(Color.FromArgb(178, 35, 47));
    private readonly NotifyIcon icon = new() { Text = "AudioSwitch" };
    private readonly TrayClicks trayClicks;
    private string trayStatus = "Reading audio status…";
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 2000 };
    private readonly Label current = new() { AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(355, 0) };
    private readonly Button bluetooth = new() { AutoSize = true };
    private readonly FlowLayoutPanel devices = new() { AutoScroll = true, WrapContents = false, FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Fill };
    private readonly CancellationTokenSource lifetime = new();
    private readonly string visibilityPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioSwitch", "hidden-devices.json");
    private Dictionary<string, string> hidden = new(StringComparer.Ordinal);
    private Snapshot? latest;
    private string? lastSnapshot;
    private bool busy;
    private bool refreshing;
    private int operationVersion;
    private bool exiting;
    private bool loadFailed;

    internal Popup()
    {
        Text = "AudioSwitch";
        Icon = audioIcon;
        icon.Icon = audioIcon;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(468, 560);
        MinimumSize = new Size(340, 330);
        BackColor = Canvas;
        ForeColor = Ink;
        TopMost = true;

        var header = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Padding = new Padding(14, 10, 14, 10), BackColor = Hero };
        header.SizeChanged += (_, _) => current.Width = Math.Max(1, header.ClientSize.Width - header.Padding.Horizontal - current.Margin.Horizontal);
        header.Controls.Add(new Label { Text = "CURRENT OUTPUT", AutoSize = true, ForeColor = Color.FromArgb(159, 222, 210) });
        current.Font = new Font(Font.FontFamily, 13, FontStyle.Bold);
        current.Height = current.Font.Height + 4;
        current.ForeColor = Color.White;
        current.Margin = new Padding(3, 3, 3, 8);
        header.Controls.Add(current);
        StyleButton(bluetooth);
        header.Controls.Add(bluetooth);
        current.Text = "Reading current output…";
        bluetooth.Text = "Bluetooth";
        bluetooth.Enabled = false;

        var footer = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
        footer.Controls.Add(status);
        var links = new FlowLayoutPanel { AutoSize = true };
        footer.SizeChanged += (_, _) => {
            int width = Math.Max(1, footer.ClientSize.Width - links.Margin.Horizontal);
            links.MaximumSize = new Size(width, 0);
            status.MaximumSize = new Size(width, 0);
        };
        var manage = new Button { Text = "Device list", AutoSize = true };
        StyleButton(manage);
        manage.Click += (_, _) => ManageDevices();
        links.Controls.Add(manage);
        var settings = new Button { Text = "Sound settings", AutoSize = true };
        StyleButton(settings);
        settings.Click += (_, _) => OpenSettings();
        var exit = new Button { Text = "Exit", AutoSize = true };
        StyleButton(exit);
        exit.Click += (_, _) => Exit();
        links.Controls.Add(settings);
        var help = new Button { Text = "Help", AutoSize = true };
        StyleButton(help);
        help.Click += (_, _) => OpenReadme();
        links.Controls.Add(help);
        links.Controls.Add(exit);
        footer.Controls.Add(links);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 3, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(devices, 0, 1);
        layout.Controls.Add(footer, 0, 2);
        Controls.Add(layout);
        devices.ClientSizeChanged += (_, _) => {
            foreach (var button in devices.Controls.OfType<Button>()) button.Width = DeviceButtonWidth();
        };

        icon.ContextMenuStrip = new ContextMenuStrip();
        icon.ContextMenuStrip.Items.Add("Open AudioSwitch", null, async (_, _) => await ShowPopup());
        icon.ContextMenuStrip.Items.Add("Sound settings", null, (_, _) => OpenSettings());
        icon.ContextMenuStrip.Items.Add("Help", null, (_, _) => OpenReadme());
        icon.ContextMenuStrip.Items.Add("Exit", null, (_, _) => Exit());
        icon.Visible = true;
        trayClicks = new TrayClicks(icon, () => trayStatus, async () => await ShowPopup());
        timer.Tick += async (_, _) => await RefreshSnapshot();
        FormClosing += (_, e) =>
        {
            if (exiting || e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = true;
            Hide();
        };
        VisibleChanged += (_, _) => timer.Interval = Visible ? 2000 : 5000;
        timer.Interval = 5000;
        timer.Start();
        try
        {
            if (File.Exists(visibilityPath))
                hidden = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(visibilityPath)) ?? hidden;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            status.Text = "Could not load hidden devices: " + ex.Message;
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    private static Icon CreateAudioIcon(Color backgroundColor)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var drawing = Graphics.FromImage(bitmap))
        using (var background = new SolidBrush(backgroundColor))
        using (var band = new Pen(Color.White, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        using (var earpiece = new SolidBrush(Color.White))
        {
            drawing.SmoothingMode = SmoothingMode.AntiAlias;
            drawing.FillEllipse(background, 0, 0, 32, 32);
            drawing.DrawArc(band, 7, 6, 18, 18, 180, 180);
            drawing.FillEllipse(earpiece, 5, 14, 8, 12);
            drawing.FillEllipse(earpiece, 19, 14, 8, 12);
        }
        IntPtr handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    internal async Task ShowPopup()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        // Position on the target monitor before fitting to its work area and DPI.
        Location = new Point(Math.Max(area.Left, area.Right - Width - 8), Math.Max(area.Top, area.Bottom - Height - 8));
        Show();
        Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        Location = new Point(Math.Max(area.Left, area.Right - Width - 8), Math.Max(area.Top, area.Bottom - Height - 8));
        Activate();
        await RefreshSnapshot();
    }

    private async Task RefreshSnapshot()
    {
        if (busy || refreshing || exiting) return;
        refreshing = true;
        try { await ReadSnapshot(operationVersion); }
        finally { refreshing = false; }
    }

    private async Task ReadSnapshot(int version)
    {
        try
        {
            var snapshot = await Task.Run(Inspect);
            if (exiting || version != operationVersion) return;
            latest = snapshot;
            string signature = JsonSerializer.Serialize(snapshot);
            if (loadFailed)
            {
                status.Text = "";
                loadFailed = false;
            }
            if (signature == lastSnapshot) return;
            Render(snapshot);
            lastSnapshot = signature;
        }
        catch (Exception ex) when (!exiting)
        {
            if (version != operationVersion) return;
            lastSnapshot = null;
            loadFailed = true;
            current.Text = "Current output unavailable";
            UpdateTray(null, "Output unavailable");
            bluetooth.Enabled = false;
            ClearDevices();
            status.Text = ex.Message;
        }
        catch (Exception) when (exiting) { }
    }

    private static async Task<Snapshot> Inspect()
    {
        var radioInfo = new List<Radio>();
        foreach (var item in await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(Windows.Devices.Radios.Radio.GetDeviceSelector()))
        {
            var radio = await Windows.Devices.Radios.Radio.FromIdAsync(item.Id);
            if (radio?.Kind == Windows.Devices.Radios.RadioKind.Bluetooth)
                radioInfo.Add(new Radio(item.Id, radio.Name, radio.State.ToString()));
        }
        var endpoints = Audio.Endpoints().Select(ToEndpoint).ToArray();
        var paired = (await Bluetooth.Paired()).Select(p => new Paired(p.Id, p.Name, p.Endpoints.Select(ToEndpoint).ToArray())).ToArray();
        return new Snapshot(radioInfo.ToArray(), Audio.Defaults(), endpoints, paired);
    }

    private static Endpoint ToEndpoint(global::AudioSwitch.Endpoint endpoint) => new(endpoint.Id, endpoint.Name, (int)endpoint.State);

    private void Render(Snapshot snapshot)
    {
        var output = snapshot.Defaults.ElementAtOrDefault(1);
        current.Text = snapshot.Endpoints.FirstOrDefault(e => e.Id == output)?.Name ?? "No default output";
        deviceTips.SetToolTip(current, current.Text);
        var radio = snapshot.Radios.FirstOrDefault(r => r.State == "On") ?? snapshot.Radios.FirstOrDefault();
        bluetooth.Text = radio is null ? "Bluetooth unavailable" : $"Bluetooth: {radio.State}";
        bluetooth.Enabled = radio?.State is "On" or "Off";
        bluetooth.Tag = radio;
        UpdateTray(radio?.State, current.Text);
        bluetooth.Click -= ToggleBluetooth;
        bluetooth.Click += ToggleBluetooth;

        devices.SuspendLayout();
        try
        {
            ClearDevices();
            Heading("PLAYBACK OUTPUTS", section: true);
            devices.Controls.Add(new Label { Text = "Click an available output to use it.", AutoSize = true,
                ForeColor = Color.FromArgb(66, 89, 106), Margin = new Padding(4, 0, 4, 5) });
            foreach (var endpoint in snapshot.Endpoints.Where(e => e.State == 1 && !hidden.ContainsKey(DeviceKey(snapshot, e))))
            {
                bool selected = endpoint.Id == output;
                ActionButton((selected ? "✓  " : "Use  ·  ") + endpoint.Name,
                    () => Act("Playback output selected.", "switch", endpoint.Id), !selected, selected);
            }
            Heading("PAIRED BLUETOOTH", section: true);
            if (snapshot.PairedAudio.Length == 0) Heading("No paired audio devices found.");
            foreach (var paired in snapshot.PairedAudio.Where(p => !hidden.ContainsKey("paired:" + p.Id)))
            {
                var active = paired.Endpoints.Where(e => e.State == 1).ToArray();
                var candidates = paired.Endpoints.Where(e => e.State is 1 or 8).ToArray();
                string state = active.Length > 0 ? "Connected" : candidates.Length > 0 ? "Disconnected" : "Unavailable";
                Heading($"{paired.Name}  ·  {state}");
                if (active.Length > 0)
                    ActionButton("Disconnect " + paired.Name, () => Act("Playback disconnected.", "disconnect", paired.Id), active.Length == 1);
                else
                    foreach (var endpoint in candidates)
                    {
                        string name = candidates.Length == 1 ? paired.Name : endpoint.Name;
                        ActionButton("Connect " + name, () => Connect(radio, paired.Id, endpoint.Id));
                    }
            }
        }
        finally { devices.ResumeLayout(); }
    }

    private void UpdateTray(string? radioState, string output)
    {
        var trayIcon = radioState switch { "On" => bluetoothOnIcon, "Off" => bluetoothOffIcon, _ => audioIcon };
        if (icon.Icon != trayIcon) icon.Icon = trayIcon;
        if (Icon != trayIcon) Icon = trayIcon;
        string text = $"Bluetooth: {radioState ?? "Unavailable"}\nOutput: {output}";
        trayStatus = text;
        if (text.Length > 63) text = text[..62] + "…";
        if (icon.Text != text) icon.Text = text;
    }

    private static string DeviceKey(Snapshot snapshot, Endpoint endpoint)
    {
        var paired = snapshot.PairedAudio.FirstOrDefault(p => p.Endpoints.Any(e => e.Id == endpoint.Id));
        return paired is null ? "output:" + endpoint.Id : "paired:" + paired.Id;
    }

    private void ManageDevices()
    {
        var entries = new Dictionary<string, string>(hidden, StringComparer.Ordinal);
        if (latest is { } snapshot)
        {
            foreach (var endpoint in snapshot.Endpoints.Where(e => e.State == 1)) entries[DeviceKey(snapshot, endpoint)] = endpoint.Name;
            foreach (var paired in snapshot.PairedAudio) entries["paired:" + paired.Id] = paired.Name;
        }
        using var dialog = new Form { Text = "AudioSwitch · Device list", StartPosition = FormStartPosition.CenterScreen,
            ClientSize = new Size(430, 320), MinimizeBox = false, MaximizeBox = false, TopMost = true };
        var hint = new Label { Text = "Checked devices appear in AudioSwitch.", AutoSize = true, Margin = new Padding(4, 4, 4, 8) };
        var list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, HorizontalScrollbar = true };
        var ordered = entries.OrderBy(e => e.Value, StringComparer.CurrentCultureIgnoreCase).ToArray();
        foreach (var item in ordered) list.Items.Add(item.Value, !hidden.ContainsKey(item.Key));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var save = new Button { Text = "Save", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(hint, 0, 0);
        layout.Controls.Add(list, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = save;
        dialog.CancelButton = cancel;
        timer.Stop();
        DialogResult result;
        try { result = dialog.ShowDialog(this); }
        finally { if (!exiting) timer.Start(); }
        if (result != DialogResult.OK) return;

        var updated = ordered.Where((_, index) => !list.GetItemChecked(index)).ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(visibilityPath)!);
            string temporary = visibilityPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(updated));
            File.Move(temporary, visibilityPath, overwrite: true);
            hidden = updated;
            lastSnapshot = null;
            if (latest is { } currentSnapshot) Render(currentSnapshot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Could not save device list", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void ToggleBluetooth(object? sender, EventArgs e)
    {
        if (bluetooth.Tag is not Radio radio) return;
        bool enable = radio.State == "Off";
        await Act(enable ? "Bluetooth is on." : "Bluetooth is off.", enable ? "radio-on" : "radio-off", radio.Id);
    }

    private Task Connect(Radio? radio, string paired, string endpoint)
    {
        if (radio?.State == "Off")
        {
            var answer = MessageBox.Show(this, "Turn on Bluetooth and connect this device?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return Task.CompletedTask;
            return Act("Connected and selected for playback.", ["connect", paired, endpoint], ["radio-on", radio.Id]);
        }
        return Act("Connected and selected for playback.", "connect", paired, endpoint);
    }

    private Task Act(string success, params string[] command) => Act(success, command, null);

    private async Task Act(string success, string[] command, string[]? first)
    {
        if (busy || exiting) return;
        busy = true;
        int version = ++operationVersion;
        devices.Enabled = false;
        bluetooth.Enabled = false;
        status.Text = "Working…";
        string result = success;
        try
        {
            if (first is not null) await RunWorker(first);
            await RunWorker(command);
        }
        catch (Exception ex) when (!exiting) { result = ex.Message + " Try again when the device is ready."; }
        catch (OperationCanceledException) when (exiting) { }
        finally
        {
            if (!exiting)
            {
                lastSnapshot = null;
                await ReadSnapshot(version);
                if (!exiting)
                {
                    if (!loadFailed) status.Text = result;
                    devices.Enabled = true;
                }
            }
            busy = false;
        }
    }

    private async Task<string> RunWorker(params string[] command)
    {
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "AudioSwitch.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string arg in command) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("AudioSwitch device helper could not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(command[0] is "connect" or "disconnect" ? 45 : 12));
        using var terminate = timeout.Token.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        });
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(output, error);
            if (process.ExitCode != 0)
                throw new InvalidOperationException(error.Result.Trim() is { Length: > 0 } message ? message : "The device operation failed.");
            return output.Result;
        }
        catch (OperationCanceledException)
        {
            if (lifetime.IsCancellationRequested) throw;
            throw new TimeoutException("Windows did not respond in time. Check the device status before retrying.");
        }
    }

    private void Heading(string text, bool section = false) => devices.Controls.Add(new Label
    {
        Text = text, AutoSize = true, ForeColor = section ? Accent : Ink,
        Margin = section ? new Padding(4, 12, 4, 5) : new Padding(4, 7, 4, 3),
    });

    private static void StyleButton(Button button, bool selected = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = selected ? Accent : Border;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(225, 241, 239);
        button.BackColor = selected ? Color.FromArgb(225, 241, 239) : Color.White;
        button.ForeColor = Ink;
    }

    private int DeviceButtonWidth() =>
        Math.Max(1, devices.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - LogicalToDeviceUnits(12));

    private void ActionButton(string text, Func<Task> action, bool enabled = true, bool selected = false)
    {
        var button = new Button
        {
            Text = text,
            Width = DeviceButtonWidth(),
            Height = Math.Max(LogicalToDeviceUnits(35), Font.Height + LogicalToDeviceUnits(10)),
            Padding = new Padding(6, 0, 6, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            Enabled = enabled,
            AutoEllipsis = true
        };
        StyleButton(button, selected);
        deviceTips.SetToolTip(button, text);
        button.Click += async (_, _) => await action();
        devices.Controls.Add(button);
    }

    private void ClearDevices()
    {
        while (devices.Controls.Count > 0) devices.Controls[0].Dispose();
    }

    private void OpenSettings()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true })?.Dispose(); }
        catch (Exception ex) { status.Text = ex.Message; }
    }

    private void OpenReadme()
    {
        try { Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "README.txt")) { UseShellExecute = true })?.Dispose(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not open README", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void Exit()
    {
        exiting = true;
        lifetime.Cancel();
        icon.Visible = false;
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            icon.ContextMenuStrip?.Dispose();
            trayClicks.Dispose();
            icon.Dispose();
            audioIcon.Dispose();
            bluetoothOnIcon.Dispose();
            bluetoothOffIcon.Dispose();
            deviceTips.Dispose();
            timer.Dispose();
            lifetime.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed record Snapshot(Radio[] Radios, string?[] Defaults, Endpoint[] Endpoints, Paired[] PairedAudio);
    private sealed record Radio(string Id, string Name, string State);
    private sealed record Endpoint(string Id, string Name, int State);
    private sealed record Paired(string Id, string Name, Endpoint[] Endpoints);
}
