using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public enum Ff7FadeQuadBlend
{
    Alpha,
    Additive
}

public sealed record Ff7FadeColorOrigin(int Red, int Green, int Blue)
{
    public void Validate()
    {
        if (Red is < 0 or > 255 || Green is < 0 or > 255 || Blue is < 0 or > 255)
        {
            throw new InvalidDataException("FFVII new-fade color origin is invalid.");
        }
    }
}

public sealed record Ff7FadeProcess(int Type, int Position, int Speed, int Red, int Green, int Blue, bool UsesFieldColor, uint FieldColorArgb = 0x80FFFFFF, uint? QuadColorArgb = null, Ff7FadeQuadBlend QuadBlend = Ff7FadeQuadBlend.Alpha, bool EntryCurve = false)
{
    public bool Completed => Type is 0 or 4 || Type is 1 or 5 or 9 && Position == 0 || Type is 2 or 6 or 10 && Position >= 255 || Type == 12 && Position == Speed;

    public void Validate()
    {
        int minimum = Type == 12 ? short.MinValue : 0;
        int maximumPosition = Type == 12 ? short.MaxValue : 256;
        int maximumSpeed = Type == 12 ? short.MaxValue : 255;
        if (Type is not (0 or 1 or 2 or 4 or 5 or 6 or 9 or 10 or 12) || Position < minimum || Position > maximumPosition || Speed < minimum || Speed > maximumSpeed || Type == 12 && Speed == 0 || Red is < 0 or > 255 || Green is < 0 or > 255 || Blue is < 0 or > 255 || !Enum.IsDefined(QuadBlend))
        {
            throw new InvalidDataException("FFVII fade continuation is invalid.");
        }
    }
}

public static class Ff7NativeFade
{
    public static Ff7FadeProcess Start(FieldFadeCommand command, Ff7FadeProcess? previous, int? fieldId = null, Ff7FadeColorOrigin? newFadeOrigin = null)
    {
        if (command.Type is not (0 or 1 or 2 or 4 or 5 or 6 or 9 or 10 or 12) || command.NewFade && command.Type is not (0 or 4 or 12) || command.Type == 12 && !command.NewFade)
        {
            throw new NotSupportedException($"FFVII fade type {command.Type} requires its separate native fade process.");
        }
        if (command.Type == 12)
        {
            if (command.Speed == 0)
            {
                throw new NotSupportedException("FFVII new-fade duration zero reaches the native division-by-zero boundary.");
            }
            (newFadeOrigin ?? throw new InvalidDataException("FFVII new-fade creation requires its native color origin.")).Validate();
        }
        int type = command.Type;
        // NG-023: the native field handler cancels this exact field-specific request.
        if (fieldId == 139 && type == 2 && command.Red == 250 && command.Green == 250 && command.Blue == 250)
        {
            type = 0;
        }
        bool fieldColor = previous?.UsesFieldColor ?? false;
        if (type is 1 or 2)
        {
            fieldColor |= command.Red is > 20 and < 220 || command.Green is > 20 and < 220 || command.Blue is > 20 and < 220;
            if (!fieldColor && !(command.Red == 0 && command.Green == 0 && command.Blue == 0) && !(type == 1 && previous?.EntryCurve == true))
            {
                throw new NotSupportedException("FFVII fullscreen fade composition requires its native quad output contract.");
            }
        }
        Ff7FadeProcess next = new(type, command.Type == 12 ? 0 : command.Type is 1 or 5 or 9 ? command.Adjustment + 1 : command.Type is 2 or 6 or 10 ? command.Adjustment : previous?.Position ?? 0, command.Speed, command.Red, command.Green, command.Blue, fieldColor, previous?.FieldColorArgb ?? 0x80FFFFFF, QuadBlend: command.Type is 5 or 6 or 9 or 10 ? Ff7FadeQuadBlend.Additive : Ff7FadeQuadBlend.Alpha, EntryCurve: previous?.EntryCurve ?? false);
        next.Validate();
        return next;
    }

    public static Ff7FadeProcess Advance(Ff7FadeProcess process, bool movieActive = false, Ff7FadeColorOrigin? newFadeOrigin = null, IReadOnlyList<byte>? entryAlpha = null)
    {
        process.Validate();
        if (process.Type != 1 && process.EntryCurve)
        {
            process = process with { EntryCurve = false };
        }
        if (process.Type == 12)
        {
            Ff7FadeColorOrigin origin = newFadeOrigin ?? throw new InvalidDataException("FFVII new-fade update requires its saved color origin.");
            origin.Validate();
            int phase = unchecked((short)(process.Position + 1));
            if (phase >= process.Speed)
            {
                phase = process.Speed;
                origin = new(process.Red, process.Green, process.Blue);
            }
            int currentRed = origin.Red + (process.Red - origin.Red) * phase / process.Speed;
            int currentGreen = origin.Green + (process.Green - origin.Green) * phase / process.Speed;
            int currentBlue = origin.Blue + (process.Blue - origin.Blue) * phase / process.Speed;
            uint fieldColor = 0xFF000000u | (uint)(255 - currentRed) << 16 | (uint)(255 - currentGreen) << 8 | (uint)(255 - currentBlue);
            return process with { Position = phase, FieldColorArgb = fieldColor, QuadColorArgb = null };
        }
        if (process.Type == 0)
        {
            return process with { UsesFieldColor = false, FieldColorArgb = 0x80FFFFFF, QuadColorArgb = null, QuadBlend = Ff7FadeQuadBlend.Alpha };
        }
        if (process.Type == 4)
        {
            return process;
        }
        int position = process.Type is 1 or 5 or 9 ? (movieActive ? 0 : Math.Max(0, process.Position - process.Speed)) : Math.Min(255, process.Position + process.Speed);
        if (process.EntryCurve)
        {
            if (entryAlpha is not { Count: 257 })
            {
                throw new NotSupportedException("FFVII field-entry fade requires its original alpha table.");
            }
            bool fieldColor = process.Red == 0 && process.Green == 0 && process.Blue == 0 ? false : process.UsesFieldColor;
            return process with { Type = position == 0 ? 0 : process.Type, Position = position, UsesFieldColor = fieldColor, QuadColorArgb = (uint)entryAlpha[position] << 24 };
        }
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
        if (Process.NewFadeOrigin is null)
        {
            Ff7FadeColorOrigin origin = Process.FadeState is { Type: 12, Completed: true } terminal ? new(terminal.Red, terminal.Green, terminal.Blue) : new(0, 0, 0);
            bool exact = Process.FadeState is { Type: 0 } or { Type: 12, Completed: true } || Process.FadeState is null && (Process.Fade is null || Process.Fade.Type == 0);
            if (!exact)
            {
                Console.Error.WriteLine("FFVII compatibility: legacy new-fade origin uses the original RGB0 default; the first color ramp may approximate presentation while recorded progress is preserved.");
            }
            Process = Process with { NewFadeOrigin = origin };
        }
        if (Process.FadeState is not null || Process.Fade is not { Wait: false } saved)
        {
            return;
        }
        Ff7FadeProcess? baseline = saved.Type is 1 or 2 ? new(0, 0, 0, 0, 0, 0, true) : null;
        Process = Process with { FadeState = Ff7NativeFade.Start(saved, baseline, fieldId, Process.NewFadeOrigin) };
        if (saved.Type is 1 or 2)
        {
            Console.Error.WriteLine("FFVII compatibility: the fade presentation restarted from its saved command; recorded progress and script PCs are preserved.");
        }
    }
}
