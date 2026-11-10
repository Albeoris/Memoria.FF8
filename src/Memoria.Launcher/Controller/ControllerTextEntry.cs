using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace Memoria.Launcher.Controller;

internal sealed class ControllerTextEntry : Window
{
    private readonly TextBox editor;

    private ControllerTextEntry(TextBox target)
    {
        Title = string.IsNullOrWhiteSpace(target.Watermark) ? "Edit text" : target.Watermark;
        Width = 580;
        MaxHeight = 580;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        editor = new TextBox
        {
            Text = target.Text,
            Watermark = target.Watermark,
            AcceptsReturn = target.AcceptsReturn,
            TextWrapping = target.TextWrapping,
            NewLine = target.NewLine,
            MaxLength = target.MaxLength,
            ClearSelectionOnLostFocus = false,
            MinHeight = target.AcceptsReturn ? 90 : 36,
            MaxHeight = 150
        };

        StackPanel content = new() { Margin = new Thickness(16), Spacing = 6 };
        content.Children.Add(editor);
        List<(Button Key, char Lower)> letterKeys = [];
        bool uppercase = false;
        foreach (string row in new[] { "0123456789", "qwertyuiop", "asdfghjkl;", "zxcvbnm,./", "\"'{}[]:\\=-", "_+!?@#$%^&", "*()<>~`|" })
        {
            UniformGrid keys = new() { Columns = 10 };
            foreach (char character in row)
            {
                Button key = CreateKey(character.ToString(), () => Insert((uppercase ? char.ToUpperInvariant(character) : character).ToString()));
                keys.Children.Add(key);
                if (char.IsAsciiLetter(character))
                {
                    letterKeys.Add((key, character));
                }
            }
            content.Children.Add(keys);
        }

        UniformGrid commands = new() { Columns = target.AcceptsReturn ? 7 : 6 };
        commands.Children.Add(CreateKey("Shift", () =>
        {
            uppercase = !uppercase;
            foreach ((Button key, char lower) in letterKeys)
            {
                key.Content = (uppercase ? char.ToUpperInvariant(lower) : lower).ToString();
            }
        }));
        commands.Children.Add(CreateKey("Space", () => Insert(" ")));
        commands.Children.Add(CreateKey("←", () => MoveCaret(-1)));
        commands.Children.Add(CreateKey("→", () => MoveCaret(1)));
        commands.Children.Add(CreateKey("Backspace", Backspace));
        commands.Children.Add(CreateKey("Clear", () =>
        {
            editor.SelectAll();
            Insert(string.Empty);
        }));
        if (target.AcceptsReturn)
        {
            commands.Children.Add(CreateKey("New line", () => Insert(editor.NewLine)));
        }
        content.Children.Add(commands);

        StackPanel footer = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6 };
        Button apply = CreateKey("Apply", () => Close(true));
        apply.IsDefault = true;
        footer.Children.Add(apply);
        Button cancel = CreateKey("Cancel", () => Close(false));
        cancel.IsCancel = true;
        footer.Children.Add(cancel);
        content.Children.Add(footer);
        Content = new ScrollViewer { Content = content };

        int selectionStart = target.SelectionStart;
        int selectionEnd = target.SelectionEnd;
        Opened += (_, _) =>
        {
            editor.Focus();
            editor.SelectionStart = selectionStart;
            editor.SelectionEnd = selectionEnd;
        };
    }

    public static async Task EditAsync(Window owner, TextBox target)
    {
        if (owner is ControllerTextEntry || target.IsReadOnly)
        {
            return;
        }

        ControllerTextEntry dialog = new(target);
        if (await dialog.ShowDialog<bool>(owner))
        {
            target.Text = dialog.editor.Text;
            target.SelectionStart = dialog.editor.SelectionStart;
            target.SelectionEnd = dialog.editor.SelectionEnd;
        }
    }

    private static Button CreateKey(string label, Action activate)
    {
        Button key = new() { Content = label, MinHeight = 28, Margin = new Thickness(2, 0), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
        key.Click += (_, _) => activate();
        return key;
    }

    private void Insert(string text)
    {
        editor.SelectedText = text;
    }

    private void Backspace()
    {
        if (editor.SelectionStart == editor.SelectionEnd)
        {
            editor.SelectionStart = GetAdjacentCaret(-1);
        }
        Insert(string.Empty);
    }

    private void MoveCaret(int direction)
    {
        int position = editor.SelectionStart == editor.SelectionEnd
            ? GetAdjacentCaret(direction)
            : direction < 0 ? Math.Min(editor.SelectionStart, editor.SelectionEnd) : Math.Max(editor.SelectionStart, editor.SelectionEnd);
        editor.CaretIndex = position;
        editor.ClearSelection();
    }

    private int GetAdjacentCaret(int direction)
    {
        string text = editor.Text ?? string.Empty;
        int position = editor.CaretIndex;
        int[] starts = StringInfo.ParseCombiningCharacters(text);
        if (direction > 0)
        {
            int next = Array.Find(starts, start => start > position);
            return next > 0 ? next : text.Length;
        }
        return Array.FindLast(starts, start => start < position);
    }
}
