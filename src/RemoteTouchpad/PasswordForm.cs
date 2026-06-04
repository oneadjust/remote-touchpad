namespace RemoteTouchpad;

public sealed class PasswordForm : Form
{
    private readonly TextBox _passwordBox = new();

    public PasswordForm(string currentPassword)
    {
        Text = "\u4fee\u6539\u8fdc\u7a0b\u89e6\u63a7\u677f\u5bc6\u7801";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(360, 140);

        var label = new Label
        {
            Text = "\u65b0\u5bc6\u7801\uff1a",
            AutoSize = true,
            Location = new Point(20, 24)
        };

        _passwordBox.Location = new Point(84, 20);
        _passwordBox.Size = new Size(250, 24);
        _passwordBox.Text = currentPassword;

        var okButton = new Button
        {
            Text = "\u4fdd\u5b58",
            DialogResult = DialogResult.OK,
            Location = new Point(178, 86),
            Size = new Size(75, 28)
        };

        var cancelButton = new Button
        {
            Text = "\u53d6\u6d88",
            DialogResult = DialogResult.Cancel,
            Location = new Point(259, 86),
            Size = new Size(75, 28)
        };

        okButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_passwordBox.Text))
            {
                MessageBox.Show("\u5bc6\u7801\u4e0d\u80fd\u4e3a\u7a7a\u3002", "\u8fdc\u7a0b\u89e6\u63a7\u677f", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };

        Controls.Add(label);
        Controls.Add(_passwordBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    public string Password => _passwordBox.Text.Trim();
}
