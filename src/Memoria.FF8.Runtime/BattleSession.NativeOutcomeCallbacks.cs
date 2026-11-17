using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleOutcomeState
{
    public byte? ResultByte6E7 { get; init; }
    public byte? FadeStyleE01 { get; init; }
    public byte? VictoryQueuedDFE { get; init; }
    public byte? ExitModeDE8 { get; init; }
    public byte? AdmissionDFD { get; init; }
    public byte? ActionOwnerB0D { get; init; }
    public byte? FadeDurationB0C { get; init; }
}

public sealed partial record NativeBattleGraphicsState
{
    public NativeBattleOutcomeState? NativeOutcome { get; init; }
}

public static class NativeBattleOutcomeCallbacks
{
    internal const short OutcomeFadeDuration501E10 = 13;
    internal const int FadeLeadFrames47E030 = 15;

    internal static byte InterpolateFade501D10(short elapsed, short duration, byte start, byte target)
    {
        int weight = (elapsed << 12) / duration;
        return unchecked((byte)(start + ((target - start) * weight >> 12)));
    }

    public static ImmutableArray<NativeBattleGeometryPrimitive> FadePackets501D10(byte fade)
    {
        uint shade = unchecked((byte)(128 - fade));
        uint color = 0x2A000000u | shade | shade << 8 | shade << 16;
        // 45C8E0 submits full screen coordinates without projected vertex depths.
        NativeBattleGeometryPrimitive quad = new(1, [0x05000000, color, 0, 320, 216u << 16, (216u << 16) | 320], []) { ScreenCoverage = NativeBattleScreenCoverage.Full };
        NativeBattleGeometryPrimitive mode = new(1, [0x01000000, 0xE1000040], [0, 0, 0, 0]);
        return [quad, mode];
    }

    public static NativeBattlePreparedPrimitiveFrame PrepareFade501D10(byte fade, NativeBattlePrimitiveTables tables)
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        using NativeBattleLogicalOrdering.Builder packets = new(ordering);
        int offset = 0;
        foreach (NativeBattleGeometryPrimitive primitive in FadePackets501D10(fade))
        {
            packets.Prepend(primitive.Bucket, new("outcome-fade", 0, offset), primitive.Words, primitive.VertexPayload4.IsEmpty ? (ushort)1 : (ushort)0, primitive.VertexPayload4, screenCoverage: primitive.ScreenCoverage);
            offset += primitive.Words.Length * sizeof(uint);
        }
        var executed = NativeBattleLogicalPasses.Execute(packets.ToImmutable(), tables, new(0) { TextureWindowBits = 0 }, 1);
        return new(executed.Commands, [])
        {
            LastDescriptorPerspective = true,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
    }

    public static byte AdvanceCountdown47CCB0(byte remaining)
    {
        return remaining is 0 or 255 ? remaining : (byte)(remaining - 1);
    }

    public static NativeBattleScriptTask Advance47E030(NativeBattleScriptTask task, Action queueFade)
    {
        ushort counter = task.ReadWord(8);
        if (counter == 0)
        {
            queueFade();
            task = task.WriteByte(15, 1);
        }
        return task.WriteWord(8, unchecked((ushort)(counter - 1)));
    }

    public static byte Duration47DFC0(byte? style)
    {
        return style switch
        {
            0 or 3 => 60,
            1 => 30,
            2 => 40,
            _ => throw new NotSupportedException("Native47DFC0 requires one of its four established fade style BYTE values.")
        };
    }
}
