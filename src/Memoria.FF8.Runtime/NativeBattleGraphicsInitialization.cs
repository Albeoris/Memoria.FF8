using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleGraphicsInitialization
{
    public static NativeBattleEffectMemory Initialize500520(NativeBattleEffectMemory memory)
    {
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native500520 requires its publication process.");
        NativeBattleScriptedGraphicsMemory scripted = memory.ScriptedGraphics ?? throw new NotSupportedException("Native500520 requires its retained depth workspace.");
        NativeBattleGeometryRegisters previous = memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown();
        ImmutableArray<byte?> zero8 = Enumerable.Repeat<byte?>(0, 8).ToImmutableArray();
        ImmutableArray<byte?> zero20 = Enumerable.Repeat<byte?>(0, 20).ToImmutableArray();
        NativeBattleGeometryRegisters geometry = new(zero20, [0, 0, 0], zero8, zero8, [0, 0, 0], [0, 0, 0, 0], 0, 0, previous.AnchorScratch8);
        NativeBattleScriptedColorRegisters colors = new([0, 0, 0], [0, 0, 0], [0, 0, 0], 0) { NormalInput = new byte[8].ToImmutableArray(), NormalInputKnown = Enumerable.Repeat(true, 8).ToImmutableArray(), Material = 0, Ir0 = 0 };
        NativeBattleScriptedLightingMatrix matrix = new(new byte[20].ToImmutableArray(), Enumerable.Repeat(true, 20).ToImmutableArray());
        NativeBattleModelRendererRegisters renderer = process.ModelRendererRegisters ?? new(null, null, null, null, null);
        return memory with
        {
            GeometryRegisters = geometry,
            ScriptedColorRegisters = colors,
            ScriptedLighting = new(matrix, matrix, new(0, 0, 0)),
            ScriptedGraphics = scripted.InitializeGeom56BAA0(),
            PrimitiveProcess = process with { PublicationState03A = 0x11, ModelRendererRegisters = renderer with { FarRed = 0, FarGreen = 0, FarBlue = 0 } }
        };
    }
}
