using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEntryTransitionConstants(float Half, float WipePhaseScale, float WipeProgress, float InitialProgress, double WipeAmplitude, double WipeFrequency, float SpecialProgress, double SpecialSeparation, double SpecialFrequency, float InitialGray, float GrayProgress, double InitialScale, double ScaleProgress, float WipeTravel, float WipeRandomScale);

public sealed record NativeBattleEntryTransitionResources(ImmutableArray<ushort> SpecialEncounters, NativeBattleEntryTransitionConstants Constants);

/// <summary>A captured-texture rectangle with full0..1 UVs; draw in submission order using ONE/ONE blending.</summary>
public readonly record struct NativeBattleEntryQuad(float X, float Y, float Width, float Height, byte Gray);

/// <summary>One native untextured line: interpolate alpha from192 at X0 to0 at X1 and blend SRCALPHA/INVSRCALPHA.</summary>
public readonly record struct NativeBattleEntryWipeRow(int Y, int X0, int X1);

public sealed record NativeBattleEntryTransitionFrame(int Counter, bool Complete, float ClearGray, bool CopyCapture, ImmutableArray<NativeBattleEntryQuad> Quads, ImmutableArray<NativeBattleEntryWipeRow> WipeRows);

/// <summary>Original559530/559890 entry transition, independent of the host renderer and wall clock.</summary>
public sealed class NativeBattleEntryTransition
{
    public const int CallbackMilliseconds = 33;

    private readonly NativeBattleEntryTransitionConstants constants;
    private readonly int[] randomRows;

    public NativeBattleEntryTransition(NativeBattleEntryTransitionResources resources, ushort encounterId, int captureWidth, int captureHeight, Func<int>? nextNativeCrtRandom = null)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentOutOfRangeException.ThrowIfLessThan(captureWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(captureHeight, 2);
        if (resources.SpecialEncounters.IsDefault || resources.Constants is null)
        {
            throw new ArgumentException("Battle entry transition requires original executable resources.", nameof(resources));
        }
        constants = resources.Constants;
        Width = captureWidth;
        Height = captureHeight;
        IsSpecial = resources.SpecialEncounters.Contains(encounterId);
        randomRows = new int[IsSpecial ? 0 : captureHeight];
        if (!IsSpecial)
        {
            ArgumentNullException.ThrowIfNull(nextNativeCrtRandom);
            randomRows[0] = Draw() % 120;
            randomRows[^1] = Draw() % 120;
            FillRows(0, captureHeight - 1);
        }

        int Draw()
        {
            int value = nextNativeCrtRandom!();
            if ((uint)value > 32767)
            {
                throw new InvalidDataException("Battle entry transition requires a native15-bit CRT random value.");
            }
            return value;
        }

        void FillRows(int first, int last)
        {
            int spread = captureHeight / 30 + ((last - first) >> 2);
            int middle = (first + last) >> 1;
            randomRows[middle] = ((randomRows[last] + randomRows[first]) >> 1) + spread * Draw() / 32767;
            if (first + 1 < middle)
            {
                FillRows(first, middle);
            }
            if (middle + 1 < last)
            {
                FillRows(middle, last);
            }
        }
    }

    public int Width { get; }
    public int Height { get; }
    public bool IsSpecial { get; }
    public int CompletionCounter => IsSpecial ? 82 : 72;
    public int Counter { get; private set; }
    public NativeBattleEntryTransitionFrame Current => Sample(Counter);

    public NativeBattleEntryTransitionFrame Advance()
    {
        Counter = Math.Min(Counter + 1, CompletionCounter);
        return Current;
    }

    public NativeBattleEntryTransitionFrame Sample(int counter)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(counter);
        if (counter > (IsSpecial ? 80 : 70))
        {
            return new(counter, counter >= CompletionCounter, 0, false, [], []);
        }
        if (IsSpecial)
        {
            return SpecialFrame(counter);
        }
        if (counter <= 20)
        {
            float progress = (float)((double)counter * constants.InitialProgress);
            int right = (int)((Width / 8) * (double)progress * 2 + Width);
            double vertical = (Height / 9) * (double)progress;
            int top = (int)-vertical;
            int bottom = (int)(Height + vertical);
            return new(counter, false, 0, true, [new(0, top, right, bottom - top, 64)], []);
        }
        float wipeProgress = (float)((double)(counter - 20) * constants.WipeProgress);
        int travel = (int)(Width * (double)constants.WipeTravel * wipeProgress);
        double phase = wipeProgress * (double)constants.WipePhaseScale;
        ImmutableArray<NativeBattleEntryWipeRow>.Builder rows = ImmutableArray.CreateBuilder<NativeBattleEntryWipeRow>(Height);
        for (int row = 0; row < Height; row++)
        {
            int wave = (int)(Math.Sin(row * constants.WipeFrequency / Height + phase) * constants.WipeAmplitude / Width);
            int right = (int)(travel - wave - randomRows[row] * (double)constants.WipeRandomScale);
            rows.Add(new(row, 0, right));
        }
        return new(counter, false, 0, true, [], rows.MoveToImmutable());
    }

    private NativeBattleEntryTransitionFrame SpecialFrame(int counter)
    {
        float progress = Math.Min((float)((double)counter * constants.SpecialProgress), 1);
        byte gray = unchecked((byte)(int)(progress * (double)constants.GrayProgress + constants.InitialGray));
        double scale = progress * constants.ScaleProgress + constants.InitialScale;
        float width = (float)(Width * scale);
        double height = Height * scale;
        double sine = Math.Sin(progress * constants.SpecialFrequency);
        float separationX = (float)(progress * sine * Width * constants.SpecialSeparation);
        double separationY = progress * sine * Height * constants.SpecialSeparation;
        double y = -separationY * constants.Half - (height - Height) * constants.Half;
        ImmutableArray<NativeBattleEntryQuad>.Builder quads = ImmutableArray.CreateBuilder<NativeBattleEntryQuad>(4);
        for (int row = 0; row < 2; row++)
        {
            double x = -separationX * constants.Half - (width - Width) * constants.Half;
            for (int column = 0; column < 2; column++)
            {
                quads.Add(new((float)x, (float)y, width, (float)height, gray));
                x += separationX;
            }
            y += separationY;
        }
        float clear = counter < 60 ? 0 : (float)((double)(counter - 60) * constants.InitialProgress);
        return new(counter, false, Math.Clamp(clear, 0, 1), false, quads.MoveToImmutable(), []);
    }
}
