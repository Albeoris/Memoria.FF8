namespace Memoria.FF8.Runtime;

/// <summary>Retained model-render far-color controls and cursors. COMMON packet cursors use the current bank-relative convention; private cursors are arena addresses.</summary>
public sealed record NativeBattleModelRendererRegisters(uint? FarRed, uint? FarGreen, uint? FarBlue, uint? PacketCursor, uint? SourceCursor)
{
    public NativeBattleModelRendererRegisters PublishFarColor45DDA0(uint red, uint green, uint blue) => this with { FarRed = unchecked(red << 4), FarGreen = unchecked(green << 4), FarBlue = unchecked(blue << 4) };
    public NativeBattleModelRendererRegisters PublishModelColor(uint rgb) => PublishFarColor45DDA0((byte)rgb, (byte)(rgb >> 8), (byte)(rgb >> 16));
    public NativeBattleModelRendererRegisters Publish50FDF0(uint? packetCursor, uint? sourceCursor) => this with { PacketCursor = packetCursor, SourceCursor = sourceCursor };
}

public sealed partial record NativeBattlePrimitiveProcess
{
    public NativeBattlePrimitiveProcess Reset5099C0() => this with { RendererGateB8B9F8 = 0 };

    public (NativeBattlePrimitiveProcess Process, bool Draw) Enter5099D0(bool hardware)
    {
        uint gate = RendererGateB8B9F8 ?? throw new NotSupportedException("Native model rendering requires its retained shared renderer gate; migrate the named standalone snapshot explicitly.");
        if (gate != 0)
        {
            return (this, true);
        }
        uint debug = Software?.Environment.Debug ?? throw new NotSupportedException("Native model renderer query requires its canonical software drawing debug DWORD.");
        return (this with { RendererGateB8B9F8 = 1 }, (debug | (hardware ? 1u : 0u)) != 0);
    }

    public NativeBattlePrimitiveProcess SetDebug45B5D0()
    {
        NativeBattleSoftwarePrimitiveState software = Software ?? throw new NotSupportedException("Native drawing debug publication requires its canonical software drawing owner.");
        uint debug = software.Environment.Debug ?? throw new NotSupportedException("Native drawing debug publication requires its retained full DWORD.");
        return this with { Software = software with { Environment = software.Environment with { Debug = debug | 1 } } };
    }

    public NativeBattlePrimitiveProcess PublishFarColor45DDA0(uint red, uint green, uint blue)
    {
        NativeBattleModelRendererRegisters registers = ModelRendererRegisters ?? new(null, null, null, null, null);
        return this with { ModelRendererRegisters = registers.PublishFarColor45DDA0(red, green, blue) };
    }

    public NativeBattlePrimitiveProcess PublishModelColor(uint rgb) => PublishFarColor45DDA0((byte)rgb, (byte)(rgb >> 8), (byte)(rgb >> 16));

    public NativeBattlePrimitiveProcess Publish50FDF0(uint? packetCursor, uint? sourceCursor)
    {
        NativeBattleModelRendererRegisters registers = ModelRendererRegisters ?? new(null, null, null, null, null);
        return this with { ModelRendererRegisters = registers.Publish50FDF0(packetCursor, sourceCursor) };
    }
}
