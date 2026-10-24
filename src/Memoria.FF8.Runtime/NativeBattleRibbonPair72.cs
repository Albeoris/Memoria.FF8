using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRibbonPair72Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary64, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleRibbonPair72
{
    public static NativeBattleRibbonPair72Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleEffectArena firstSamples, NativeBattleEffectArena secondSamples, NativeBattleEffectArena temporary64, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint initialColor)
    {
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Trail drawing requires its current primitive owner.");
        NativeBattleModelRendererRegisters far = (process.ModelRendererRegisters ?? throw new NotSupportedException("Trail drawing requires its current color controls.")) with { FarRed = 0, FarGreen = 0, FarBlue = 0 };
        memory = memory with { PrimitiveProcess = process with { ModelRendererRegisters = far } };
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Trail drawing requires current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Trail drawing requires current arithmetic registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Trail drawing requires current projection controls.");
        temporary64 = LowerPut(LowerPut(temporary64, 0x14, initialColor), 0x1C, initialColor);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int segment = 0; segment < 30; segment++)
        {
            temporary64 = LowerPut(temporary64, 0x10, LowerDword(temporary64, 0x14));
            SkinWrites first = new(36, allocated);
            first.Word(0, 0x08000000);
            registers = LoadTrailPoint(registers, 0, firstSamples, segment * 8);
            registers = LoadTrailPoint(registers, 1, firstSamples, segment * 8 + 8);
            registers = LoadTrailPoint(registers, 2, secondSamples, segment * 8);
            registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
            uint flags = registers.Colors.Flags ?? throw new NotSupportedException("Trail drawing requires its current projection flags.");
            temporary64 = LowerPut(temporary64, 12, flags);
            if ((flags & 0x60000) != 0)
            {
                attempts.Add(first.Attempt(null));
                continue;
            }
            uint[] screens = registers.Geometry.Sxy.Select(value => value ?? throw new NotSupportedException("Trail drawing requires its current screen FIFO.")).ToArray();
            registers = LoadTrailPoint(registers, 0, secondSamples, segment * 8 + 8);
            registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
            uint fourth = registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Trail drawing requires its current fourth projection.");
            registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Trail drawing requires its current ZSF4."));
            uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("Trail drawing requires its current depth.");
            temporary64 = LowerPut(temporary64, 8, depth);
            NativeBattleScriptedDpcsResult tint = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, LowerDword(temporary64, 0x1C), (uint)(segment * 4096 / 30), far, colors);
            registers = registers with { Colors = tint.Registers };
            temporary64 = LowerPut(temporary64, 0x14, tint.PackedRgb);
            uint previous = LowerDword(temporary64, 0x10);
            first.Word(4, previous);
            first.Word(12, tint.PackedRgb);
            first.Word(20, previous);
            first.Word(28, tint.PackedRgb);
            for (int corner = 0; corner < 3; corner++)
            {
                first.Word(8 + corner * 8, screens[corner]);
            }
            first.Word(32, fourth);
            SkinWrites second = new(36, allocated + 36);
            second.Word(0, 0x08000000);
            second.Word(4, 0x3A000000);
            second.Word(12, 0x3A000000);
            second.Word(20, previous);
            second.Word(28, tint.PackedRgb);
            for (int corner = 0; corner < 3; corner++)
            {
                second.Word(8 + corner * 8, screens[corner]);
            }
            second.Word(32, fourth);
            ImmutableArray<int> payload = registers.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Trail drawing requires all four current depths.")))).ToImmutableArray();
            int bucket = unchecked(17 + ((int)depth >> 2));
            attempts.Add(first.Attempt(new(bucket, first.Words(), payload)));
            attempts.Add(second.Attempt(new(bucket, second.Words(), payload)));
            allocated += 72;
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary64, new(attempts.ToImmutable(), allocated + 36) { NativeUnlinkedAllocation = true });
    }

    private static NativeBattleGeometryRegisterStep LoadTrailPoint(NativeBattleGeometryRegisterStep registers, int slot, NativeBattleEffectArena points, int offset)
    {
        (byte[] bytes, bool[] known) = points.ReadSnapshot(offset + 6, 2);
        short? w = known.All(value => value) ? System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        return NativeBattleGeometry.LoadVector(registers, slot, LowerVector(points, offset), w);
    }
    private static uint LowerDword(NativeBattleEffectArena arena, int offset) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena LowerPut(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleVector LowerVector(NativeBattleEffectArena arena, int offset) => new(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2)), System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset + 2, 2)), System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset + 4, 2)));
}
