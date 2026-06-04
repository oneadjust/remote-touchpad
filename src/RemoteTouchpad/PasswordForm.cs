namespace RemoteTouchpad;

public sealed class PasswordForm : Form
{
    private readonly TextBox _passwordBox = new();

    public PasswordForm(string currentPassword)
    {
        Text = "修改 Remote Touchpad 密码";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(360, 140);

        var label = new Label
        {
            Text = "新密码：",
            AutoSize = true,
            Location = new Point(20, 24)
        };

        _passwordBox.Location = new Point(84, 20);
        _passwordBox.Size = new Size(250, 24);
        _passwordBox.Text = currentPassword;

        var okButton = new Button
        {
            Text = "保存",
            DialogResult = DialogResult.OK,
            Location = new Point(178, 86),
            Size = new Size(75, 28)
        };

        var cancelButton = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Location = new Point(259, 86),
            Size = new Size(75, 28)
        };

        okButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_passwordBox.Text))
            {
                MessageBox.Show("密码不能为空。", "Remote Touchpad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
