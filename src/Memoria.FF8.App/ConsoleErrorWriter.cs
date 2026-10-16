using System.Text;

namespace Memoria.FF8;

internal sealed class ConsoleErrorWriter : TextWriter
{
    private readonly TextWriter output;
    private readonly bool colorEnabled;
    private readonly Func<ConsoleColor> getColor;
    private readonly Action<ConsoleColor> setColor;
    private readonly object gate = new();

    public ConsoleErrorWriter(TextWriter output, bool colorEnabled, Func<ConsoleColor>? getColor = null, Action<ConsoleColor>? setColor = null)
    {
        this.output = output;
        this.colorEnabled = colorEnabled;
        this.getColor = getColor ?? (() => Console.ForegroundColor);
        this.setColor = setColor ?? (color => Console.ForegroundColor = color);
    }

    public override Encoding Encoding => output.Encoding;
    public override IFormatProvider FormatProvider => output.FormatProvider;

    public override void Write(char value)
    {
        WriteColored(() => output.Write(value));
    }

    public override void Write(string? value)
    {
        WriteColored(() => output.Write(value));
    }

    public override void Write(char[] buffer, int index, int count)
    {
        WriteColored(() => output.Write(buffer, index, count));
    }

    public override void Write(ReadOnlySpan<char> buffer)
    {
        Write(buffer.ToString());
    }

    public override void WriteLine(string? value)
    {
        WriteColored(() => output.WriteLine(value));
    }

    public override void Flush()
    {
        lock (gate)
        {
            output.Flush();
        }
    }

    private void WriteColored(Action write)
    {
        lock (gate)
        {
            ConsoleColor? previous = null;
            if (colorEnabled)
            {
                try
                {
                    previous = getColor();
                    setColor(ConsoleColor.Red);
                }
                catch (Exception exception) when (IsColorUnavailable(exception))
                {
                    // Color support must not prevent the original error from being reported.
                }
            }
            try
            {
                write();
                output.Flush();
            }
            finally
            {
                if (previous.HasValue)
                {
                    try
                    {
                        setColor(previous.Value);
                    }
                    catch (Exception exception) when (IsColorUnavailable(exception))
                    {
                        // The console may have detached while the error was being written.
                    }
                }
            }
        }
    }

    private static bool IsColorUnavailable(Exception exception)
    {
        return exception is IOException or NotSupportedException or InvalidOperationException;
    }
}
