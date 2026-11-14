namespace Memoria.FF7.Runtime;

public sealed record Ff7BackgroundScroll(short VelocityX16 = 0, short VelocityY16 = 0, short OffsetX16 = 0, short OffsetY16 = 0)
{
    public Ff7BackgroundScroll Advance(short width, short height)
    {
        if (this is { VelocityX16: 0, VelocityY16: 0, OffsetX16: 0, OffsetY16: 0 })
        {
            return this;
        }
        return this with { OffsetX16 = AdvanceAxis(OffsetX16, VelocityX16, width), OffsetY16 = AdvanceAxis(OffsetY16, VelocityY16, height) };
    }

    private static short AdvanceAxis(short offset, short velocity, short dimension)
    {
        if (dimension == 0)
        {
            throw new InvalidDataException("FFVII background scroll cannot wrap a zero native layer dimension.");
        }
        short added = unchecked((short)(offset + velocity));
        return unchecked((short)(added % (dimension << 4)));
    }
}

public sealed partial class Ff7FieldHost
{
    private void InitializeBackgroundScroll()
    {
        IReadOnlyList<short> offsets = field.Triggers?.InitialLayerScrollOffsets ?? new short[4];
        if (offsets.Count != 4)
        {
            throw new InvalidDataException("FFVII field background requires its four initial scroll words.");
        }
        Process = Process with
        {
            BackgroundLayer2Scroll = Process.BackgroundLayer2Scroll ?? new(OffsetX16: offsets[0], OffsetY16: offsets[1]),
            BackgroundLayer3Scroll = Process.BackgroundLayer3Scroll ?? new(OffsetX16: offsets[2], OffsetY16: offsets[3])
        };
    }

    private void AdvanceBackgroundScroll()
    {
        Ff7BackgroundScroll layer2 = Process.BackgroundLayer2Scroll!;
        Ff7BackgroundScroll layer3 = Process.BackgroundLayer3Scroll!;
        if (layer2 is { VelocityX16: 0, VelocityY16: 0, OffsetX16: 0, OffsetY16: 0 } && layer3 is { VelocityX16: 0, VelocityY16: 0, OffsetX16: 0, OffsetY16: 0 })
        {
            return;
        }
        IReadOnlyList<short> dimensions = field.Triggers?.LayerDimensions ?? throw new InvalidDataException("FFVII background scrolling requires native field trigger resources.");
        if (dimensions.Count != 4)
        {
            throw new InvalidDataException("FFVII field background requires its four native layer dimensions.");
        }
        Process = Process with
        {
            BackgroundLayer2Scroll = layer2.Advance(dimensions[0], dimensions[1]),
            BackgroundLayer3Scroll = layer3.Advance(dimensions[2], dimensions[3])
        };
    }
}
