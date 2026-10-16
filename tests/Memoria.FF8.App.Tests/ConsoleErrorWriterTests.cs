using System.Text;

namespace Memoria.FF8.App.Tests;

public sealed class ConsoleErrorWriterTests
{
    [Fact]
    public void Errors_are_red_during_output_and_restore_the_previous_color()
    {
        ConsoleColor color = ConsoleColor.Cyan;
        List<ConsoleColor> changes = [];
        using ColorObservingWriter output = new(() => color);
        using ConsoleErrorWriter writer = new(output, true, () => color, value =>
        {
            color = value;
            changes.Add(value);
        });

        writer.WriteLine(new InvalidOperationException("Battle failed"));

        Assert.Contains("System.InvalidOperationException: Battle failed", output.ToString());
        Assert.All(output.ObservedColors, value => Assert.Equal(ConsoleColor.Red, value));
        Assert.Equal(ConsoleColor.Cyan, color);
        Assert.Equal(ConsoleColor.Red, changes[0]);
        Assert.Equal(ConsoleColor.Cyan, changes[^1]);
    }

    [Fact]
    public void Redirected_errors_preserve_plain_text_without_accessing_console_colors()
    {
        using StringWriter output = new();
        using ConsoleErrorWriter writer = new(output, false, () => throw new InvalidOperationException("Must not read console color"), _ => throw new InvalidOperationException("Must not set console color"));

        writer.Write("error: ");
        writer.Write(new[] { 'b', 'a', 'd' }, 0, 3);
        writer.Write(" input".AsSpan());
        writer.WriteLine();

        Assert.Equal("error: bad input" + Environment.NewLine, output.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Unavailable_console_colors_do_not_hide_errors(bool failOnRead)
    {
        using StringWriter output = new();
        using ConsoleErrorWriter writer = new(output, true, () => failOnRead ? throw new IOException("No console") : ConsoleColor.Gray, _ => throw new IOException("No console"));

        writer.WriteLine("Battle failed");

        Assert.Equal("Battle failed" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void Failed_output_still_restores_the_previous_color()
    {
        ConsoleColor color = ConsoleColor.Green;
        using ThrowingWriter output = new();
        using ConsoleErrorWriter writer = new(output, true, () => color, value => color = value);

        Assert.Throws<IOException>(() => writer.WriteLine("Battle failed"));

        Assert.Equal(ConsoleColor.Green, color);
    }

    private sealed class ColorObservingWriter(Func<ConsoleColor> getColor) : StringWriter
    {
        public List<ConsoleColor> ObservedColors { get; } = [];

        public override void Write(string? value)
        {
            ObservedColors.Add(getColor());
            base.Write(value);
        }

        public override void Write(char[] buffer, int index, int count)
        {
            ObservedColors.Add(getColor());
            base.Write(buffer, index, count);
        }
    }

    private sealed class ThrowingWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string? value)
        {
            throw new IOException("Output failed");
        }
    }
}
