namespace RemoteTouchpad;

public sealed class MediaBindingsForm : Form
{
    private readonly TextBox _playPauseBox = new();
    private readonly TextBox _previousBox = new();
    private readonly TextBox _nextBox = new();
    private readonly TextBox _volumeDownBox = new();
    private readonly TextBox _volumeUpBox = new();

    private string _playPause;
    private string _previous;
    private string _next;
    private string _volumeDown;
    private string _volumeUp;
    private string? _captureTarget;
    private TextBox? _captureBox;

    public MediaBindingsForm(MediaBindings bindings)
    {
        _playPause = bindings.PlayPause;
        _previous = bindings.Previous;
        _next = bindings.Next;
        _volumeDown = bindings.VolumeDown;
        _volumeUp = bindings.VolumeUp;

        Text = "\u914d\u7f6e\u5a92\u4f53\u952e\u4f4d";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        ClientSize = new Size(450, 318);

        AddBindingRow("\u64ad\u653e / \u6682\u505c", _playPauseBox, "playPause", 22);
        AddBindingRow("\u4e0a\u4e00\u66f2", _previousBox, "previous", 72);
        AddBindingRow("\u4e0b\u4e00\u66f2", _nextBox, "next", 122);
        AddBindingRow("\u97f3\u91cf\u51cf", _volumeDownBox, "volumeDown", 172);
        AddBindingRow("\u97f3\u91cf\u52a0", _volumeUpBox, "volumeUp", 222);

        var defaultsButton = new Button
        {
            Text = "\u6062\u590d\u9ed8\u8ba4",
            Location = new Point(190, 272),
            Size = new Size(82, 28)
        };
        defaultsButton.Click += (_, _) =>
        {
            _playPause = MediaBinding.SystemPlayPause;
            _previous = MediaBinding.SystemPrevious;
            _next = MediaBinding.SystemNext;
            _volumeDown = MediaBinding.SystemVolumeDown;
            _volumeUp = MediaBinding.SystemVolumeUp;
            RefreshDisplay();
        };

        var okButton = new Button
        {
            Text = "\u4fdd\u5b58",
            DialogResult = DialogResult.OK,
            Location = new Point(278, 272),
            Size = new Size(65, 28)
        };

        var cancelButton = new Button
        {
            Text = "\u53d6\u6d88",
            DialogResult = DialogResult.Cancel,
            Location = new Point(350, 272),
            Size = new Size(65, 28)
        };

        Controls.Add(defaultsButton);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        RefreshDisplay();
    }

    public MediaBindings Bindings => new()
    {
        PlayPause = _playPause,
        Previous = _previous,
        Next = _next,
        VolumeDown = _volumeDown,
        VolumeUp = _volumeUp
    };

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_captureTarget is not null)
        {
            e.SuppressKeyPress = true;
            HandleCapturedKey(e.KeyCode, e.Control, e.Shift, e.Alt);
            return;
        }

        base.OnKeyDown(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_captureTarget is null)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        var key = keyData & Keys.KeyCode;
        HandleCapturedKey(
            key,
            keyData.HasFlag(Keys.Control),
            keyData.HasFlag(Keys.Shift),
            keyData.HasFlag(Keys.Alt));
        return true;
    }

    private void AddBindingRow(string labelText, TextBox textBox, string target, int top)
    {
        Controls.Add(new Label
        {
            Text = labelText,
            AutoSize = true,
            Location = new Point(20, top + 5)
        });

        textBox.Location = new Point(118, top);
        textBox.Size = new Size(204, 24);
        textBox.ReadOnly = true;
        Controls.Add(textBox);

        var captureButton = new Button
        {
            Text = "\u6355\u6349",
            Location = new Point(332, top - 1),
            Size = new Size(72, 28)
        };
        captureButton.Click += (_, _) =>
        {
            _captureTarget = target;
            _captureBox = textBox;
            textBox.Text = "\u8bf7\u6309\u4e0b\u952e\u76d8\u952e\u4f4d...";
            textBox.Focus();
        };
        Controls.Add(captureButton);
    }

    private void HandleCapturedKey(Keys key, bool control, bool shift, bool alt)
    {
        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.None)
        {
            if (_captureBox is not null)
            {
                _captureBox.Text = BuildModifierPreview(control, shift, alt);
            }

            return;
        }

        var binding = MediaBinding.FromKeys(
            key,
            control,
            shift,
            alt);

        switch (_captureTarget)
        {
            case "previous":
                _previous = binding;
                break;
            case "next":
                _next = binding;
                break;
            case "volumeDown":
                _volumeDown = binding;
                break;
            case "volumeUp":
                _volumeUp = binding;
                break;
            default:
                _playPause = binding;
                break;
        }

        _captureTarget = null;
        _captureBox = null;
        RefreshDisplay();
    }

    private static string BuildModifierPreview(bool control, bool shift, bool alt)
    {
        var parts = new List<string>();
        if (control)
        {
            parts.Add("Ctrl");
        }

        if (shift)
        {
            parts.Add("Shift");
        }

        if (alt)
        {
            parts.Add("Alt");
        }

        return parts.Count == 0
            ? "\u8bf7\u6309\u4e0b\u952e\u76d8\u952e\u4f4d..."
            : $"{string.Join(" + ", parts)} + ...";
    }

    private void RefreshDisplay()
    {
        _playPauseBox.Text = MediaBinding.GetDisplayName(_playPause);
        _previousBox.Text = MediaBinding.GetDisplayName(_previous);
        _nextBox.Text = MediaBinding.GetDisplayName(_next);
        _volumeDownBox.Text = MediaBinding.GetDisplayName(_volumeDown);
        _volumeUpBox.Text = MediaBinding.GetDisplayName(_volumeUp);
    }
}
