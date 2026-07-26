namespace AsusFanProfileSwitcher.Dialogs;

internal static class TextEntryDialog
{
    public static string? Show(
        IWin32Window owner,
        string title,
        string label,
        string initialValue)
    {
        using var form = CreateBaseForm(title, new Size(430, 185));
        var prompt = new Label
        {
            Text = label,
            ForeColor = Color.FromArgb(170, 176, 185),
            Location = new Point(22, 20),
            AutoSize = true
        };
        var input = new TextBox
        {
            Text = initialValue,
            Location = new Point(22, 49),
            Width = 368,
            BackColor = Color.FromArgb(27, 30, 35),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        var save = CreateButton("SAVE", true, new Point(282, 101));
        var cancel = CreateButton("CANCEL", false, new Point(176, 101));
        form.Controls.AddRange([prompt, input, save, cancel]);
        form.AcceptButton = save;
        form.CancelButton = cancel;
        form.Shown += (_, _) =>
        {
            input.SelectAll();
            input.Focus();
        };
        return form.ShowDialog(owner) == DialogResult.OK &&
               !string.IsNullOrWhiteSpace(input.Text)
            ? input.Text.Trim()
            : null;
    }

    public static (string DisplayName, string FileName)? ShowNewProfile(
        IWin32Window owner,
        string initialDisplayName,
        string initialFileName)
    {
        using var form = CreateBaseForm("Create profile", new Size(470, 260));
        var displayLabel = LabelFor("DISPLAY NAME", 22);
        var display = InputFor(initialDisplayName, 49);
        var fileLabel = LabelFor("PROFILE FILE NAME", 92);
        var file = InputFor(initialFileName, 119);
        var hint = new Label
        {
            Text = "The new profile starts as a copy of the selected profile.",
            ForeColor = Color.FromArgb(120, 128, 139),
            Location = new Point(22, 155),
            AutoSize = true
        };
        var create = CreateButton("CREATE", true, new Point(322, 187));
        var cancel = CreateButton("CANCEL", false, new Point(216, 187));
        form.Controls.AddRange([displayLabel, display, fileLabel, file, hint, create, cancel]);
        form.AcceptButton = create;
        form.CancelButton = cancel;
        if (form.ShowDialog(owner) != DialogResult.OK ||
            string.IsNullOrWhiteSpace(display.Text) ||
            string.IsNullOrWhiteSpace(file.Text))
        {
            return null;
        }
        return (display.Text.Trim(), file.Text.Trim());

        Label LabelFor(string text, int y) => new()
        {
            Text = text,
            ForeColor = Color.FromArgb(154, 161, 171),
            Location = new Point(22, y),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 8F)
        };
        TextBox InputFor(string text, int y) => new()
        {
            Text = text,
            Location = new Point(22, y),
            Width = 408,
            BackColor = Color.FromArgb(27, 30, 35),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private static Form CreateBaseForm(string title, Size size) => new()
    {
        Text = title,
        Size = size,
        FormBorderStyle = FormBorderStyle.FixedDialog,
        MaximizeBox = false,
        MinimizeBox = false,
        StartPosition = FormStartPosition.CenterParent,
        BackColor = Color.FromArgb(16, 18, 22),
        ForeColor = Color.White,
        Font = new Font("Segoe UI", 9.5F),
        ShowInTaskbar = false
    };

    private static Button CreateButton(string text, bool primary, Point location) => new()
    {
        Text = text,
        DialogResult = primary ? DialogResult.OK : DialogResult.Cancel,
        Location = location,
        Size = new Size(100, 34),
        FlatStyle = FlatStyle.Flat,
        BackColor = primary ? Color.FromArgb(226, 35, 51) : Color.FromArgb(36, 39, 45),
        ForeColor = Color.White
    };
}
