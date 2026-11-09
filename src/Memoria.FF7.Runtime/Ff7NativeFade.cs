using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public enum Ff7FadeQuadBlend
{
    Alpha,
    Additive
}

public sealed record Ff7FadeProcess(int Type, int Position, int Speed, int Red, int Green, int Blue, bool UsesFieldColor, uint FieldColorArgb = 0x80FFFFFF, uint? QuadColorArgb = null, Ff7FadeQuadBlend QuadBlend = Ff7FadeQuadBlend.Alpha)
{
    public bool Completed => Type is 0 or 4 || Type is 1 or 5 or 9 && Position == 0 || Type is 2 or 6 or 10 && Position >= 255;

    public void Validate()
    {
        if (Type is not (0 or 1 or 2 or 4 or 5 or 6 or 9 or 10) || Position is < 0 or > 256 || Speed is < 0 or > 255 || Red is < 0 or > 255 || Green is < 0 or > 255 || Blue is < 0 or > 255 || !Enum.IsDefined(QuadBlend))
        {
            throw new InvalidDataException("FFVII fade continuation is invalid.");
        }
    }
}

public static class Ff7NativeFade
{
    public static Ff7FadeProcess Start(FieldFadeCommand command, Ff7FadeProcess? previous)
    {
        if (command.Type is not (0 or 1 or 2 or 4 or 5 or 6 or 9 or 10) || command.NewFade && command.Type is not (0 or 4))
        {
            throw new NotSupportedException($"FFVII fade type {command.Type} requires its separate native fade process.");
        }
        bool fieldColor = previous?.UsesFieldColor ?? false;
        if (command.Type is 1 or 2)
        {
            fieldColor |= command.Red is > 20 and < 220 || command.Green is > 20 and < 220 || command.Blue is > 20 and < 220;
            if (!fieldColor && !(command.Red == 0 && command.Green == 0 && command.Blue == 0))
            {
                throw new NotSupportedException("FFVII fullscreen fade composition requires its native quad output contract.");
            }
        }
        Ff7FadeProcess next = new(command.Type, command.Type is 1 or 5 or 9 ? command.Adjustment + 1 : command.Type is 2 or 6 or 10 ? command.Adjustment : previous?.Position ?? 0, command.Speed, command.Red, command.Green, command.Blue, fieldColor, previous?.FieldColorArgb ?? 0x80FFFFFF, QuadBlend: command.Type is 5 or 6 or 9 or 10 ? Ff7FadeQuadBlend.Additive : Ff7FadeQuadBlend.Alpha);
        next.Validate();
        return next;
    }

    public static Ff7FadeProcess Advance(Ff7FadeProcess process, bool movieActive = false)
    {
        process.Validate();
        if (process.Type == 0)
        {
            return process with { UsesFieldColor = false, FieldColorArgb = 0x80FFFFFF, QuadColorArgb = null, QuadBlend = Ff7FadeQuadBlend.Alpha };
        }
        if (process.Type == 4)
        {
            return process;
        }
        int position = process.Type is 1 or 5 or 9 ? (movieActive ? 0 : Math.Max(0, process.Position - process.Speed)) : Math.Min(255, process.Position + process.Speed);
        if (process.Type is 9 or 10)
        {
            uint quad = 0x80000000u | (uint)process.Red << 16 | (uint)process.Green << 8 | (uint)process.Blue;
            return process with { Type = process.Type == 9 && position == 0 ? 0 : process.Type, Position = position, QuadColorArgb = quad, QuadBlend = Ff7FadeQuadBlend.Additive };
        }
        if (process.Type is 5 or 6)
        {
            uint quad = 0x80000000u | (uint)(position * process.Red / 256) << 16 | (uint)(position * process.Green / 256) << 8 | (uint)(position * process.Blue / 256);
            return process with { Type = process.Type == 5 && position == 0 ? 0 : process.Type, Position = position, QuadColorArgb = quad, QuadBlend = Ff7FadeQuadBlend.Additive };
        }
        if (process.Red == 0 && process.Green == 0 && process.Blue == 0)
        {
            return process with { Type = process.Type == 1 && position == 0 ? 0 : process.Type, Position = position, UsesFieldColor = false, QuadColorArgb = (uint)Math.Min(position, 255) << 24 };
        }
        int red = unchecked((byte)((256 - position / 2) * process.Red / 256));
        int green = unchecked((byte)((272 - position) * process.Green / 256));
        int blue = unchecked((byte)((272 - position) * process.Blue / 256));
        uint color = 0x80000000u | (uint)red << 16 | (uint)green << 8 | (uint)blue;
        return process with { Type = process.Type == 1 && position == 0 ? 0 : process.Type, Position = position, FieldColorArgb = color, QuadColorArgb = null };
    }
}

public sealed partial class Ff7FieldHost
{
    private void InitializeFadeState()
    {
        if (Process.FadeState is not null || Process.Fade is not { Wait: false } saved)
        {
            return;
        }
        Ff7FadeProcess? baseline = saved.Type is 1 or 2 ? new(0, 0, 0, 0, 0, 0, true) : null;
        Process = Process with { FadeState = Ff7NativeFade.Start(saved, baseline) };
        if (saved.Type is 1 or 2)
        {
            Console.Error.WriteLine("FFVII compatibility: the fade presentation restarted from its saved command; recorded progress and script PCs are preserved.");
        }
    }
}
