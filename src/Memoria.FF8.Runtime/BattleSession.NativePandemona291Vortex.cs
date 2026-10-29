using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattlePandemona291Frame Frame, NativeBattleGraphicsState Graphics) DrawNativePandemona291Vortex(NativeBattlePandemona291Frame frame, uint address, int age, NativeBattleGraphicsState graphics)
    {
        NativeBattlePandemona291Resources resources = frame.Effect.Binding.Pandemona291Resources ?? throw new NotSupportedException("Native Pandemona vortex requires its original resources.");
        NativeBattlePandemona291State state = frame.Memory.Pandemona291 ?? throw new NotSupportedException("Native Pandemona vortex requires its current module state.");
        int elapsed = unchecked(age - 40);
        int growth = unchecked((int)(unchecked((uint)elapsed << 12) / 60));
        int center = state.Center25562E8 ?? throw new NotSupportedException("Native Pandemona vortex requires its current center.");
        uint buffer = state.ModelBuffer25562F4 ?? throw new NotSupportedException("Native Pandemona vortex requires its current work buffer.");
        uint pause = state.Pause2556258 ?? throw new NotSupportedException("Native Pandemona vortex requires its current pause DWORD.");
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona vortex requires its current arena.");
        if (elapsed == 0)
        {
            arena = NativeBattlePandemona291Vortex.Reset6F1520(arena, address);
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        if (pause == 0)
        {
            NativeBattlePandemona291TrailAllocation spawned = NativeBattlePandemona291Vortex.Spawn6EDC66(arena, frame.Random, address, resources.TrailTrigonometry);
            frame = frame with { Memory = frame.Memory with { Arena = spawned.Arena }, Random = spawned.Random };
            frame = NativeBattlePandemona291LayerParticles.Spawn(frame, unchecked((int)((uint)elapsed >> 1)), unchecked(elapsed << 7), center, 8192, 4096, 0);
            NativeBattleActionMatrix trailMatrix = NativeBattlePandemona291Layers.Build6F29D0(resources.TrailTrigonometry, unchecked(elapsed << 6), unchecked((elapsed + 64) << 6), 8192) with { Z = center };
            NativeBattleScriptedColorRegisters registers = frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Pandemona vortex requires its current GTE input registers.");
            arena = NativeBattlePandemona291Vortex.Advance6F1A30(frame.Memory.Arena!, address, trailMatrix, unchecked(elapsed << 7), growth >> 1, resources.TrailTrigonometry, (input, transformed) => registers = registers with { NormalInput = input, NormalInputKnown = null, Flags = transformed.Flags, Mac = [transformed.X, transformed.Y, transformed.Z], Ir = [Math.Clamp(transformed.X, short.MinValue, short.MaxValue), Math.Clamp(transformed.Y, short.MinValue, short.MaxValue), Math.Clamp(transformed.Z, short.MinValue, short.MaxValue)] });
            frame = frame with { Memory = frame.Memory with { Arena = arena, ScriptedColorRegisters = registers } };
        }
        var layers = DrawNativePandemona291LayerRequests(frame, NativeBattlePandemona291Vortex.Layers(center, age), graphics);
        frame = layers.Frame;
        graphics = layers.Graphics;
        short ir0 = layers.Ir0 ?? throw new NotSupportedException("Native Pandemona floor morph requires the actual IR0 from its immediately preceding layer renderer.");
        NativeBattleStaticMeshResource floor = resources.VortexFloorGeometry ?? throw new NotSupportedException("Native Pandemona requires its original vortex floor mesh.");
        ImmutableArray<NativeBattleVector> morphed = NativeBattlePandemona291Vortex.Morph6F2D20(floor.Bytes, resources.VortexFloorTarget, ir0);
        int scratch = checked((int)(buffer - NativeBattleEffectNodeMemory.ArenaAddress) + 0x31D4);
        arena = frame.Memory.Arena!;
        for (int index = 0; index < morphed.Length; index++)
        {
            NativeBattleVector vector = morphed[index];
            byte[] xyz = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(xyz, vector.X);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), vector.Y);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), vector.Z);
            arena = arena.Write(checked(scratch + index * 8), xyz);
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        NativeBattleActionMatrix view = NativeCamera?.Projection?.View ?? throw new NotSupportedException("Native Pandemona vortex requires its current view matrix.");
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleEsunaMeshContext context = new(0x2030, null, null, 256, 0, 0, full, full, 17, 2);
        NativeBattleActionMatrix first = NativeBattlePandemona291Layers.Build6F29D0(resources.TrailTrigonometry, unchecked(elapsed << 6), 8192, 4096) with { Y = -8192, Z = center };
        var primary = DrawNativePandemona291PreparedMesh(frame, floor, NativeBattleGfMatrices.Compose56C2F0(view, first), context, scratch, graphics, ir0, pilotIdentity: NativePandemona291VisualIdentity(frame, 210));
        NativeBattleActionMatrix second = NativeBattlePandemona291Layers.Build6F29D0(resources.TrailTrigonometry, unchecked(elapsed * 100), 5000, 4096) with { Y = -8192, Z = center };
        var secondary = DrawNativePandemona291PreparedMesh(primary.Frame, floor, NativeBattleGfMatrices.Compose56C2F0(view, second), context with { DepthBias = 128 }, scratch, primary.Graphics, primary.Ir0, pilotIdentity: NativePandemona291VisualIdentity(frame, 211));
        if (elapsed < 44)
        {
            return (secondary.Frame, secondary.Graphics);
        }
        NativeBattleStaticMeshResource overlay = resources.VortexOverlayGeometry ?? throw new NotSupportedException("Native Pandemona requires its original late vortex overlay geometry.");
        NativeBattleActionMatrix translated = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, -8192, center, 4096);
        NativeBattleActionMatrix overlayMatrix = NativeBattleGfMatrices.Compose56C2F0(view, translated);
        NativeBattleEsunaMeshContext overlayContext = context with { Flags = 0xF0, FarColor = 0, Fade = unchecked((60 - elapsed) << 8), DepthBias = 0 };
        var late = DrawNativePandemona291PreparedMesh(secondary.Frame, overlay, overlayMatrix, overlayContext, null, secondary.Graphics, secondary.Ir0, pilotIdentity: NativePandemona291VisualIdentity(frame, 212));
        return (late.Frame, late.Graphics);
    }
}
