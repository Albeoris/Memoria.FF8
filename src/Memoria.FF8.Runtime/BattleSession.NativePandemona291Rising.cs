using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattlePandemona291Frame Frame, NativeBattleGraphicsState Graphics) DrawNativePandemona291Rising(NativeBattlePandemona291Frame frame, uint address, int age, NativeBattleGraphicsState graphics)
    {
        NativeBattlePandemona291Resources resources = frame.Effect.Binding.Pandemona291Resources ?? throw new NotSupportedException("Native Pandemona rising scene requires its original resources.");
        NativeBattlePandemona291State state = frame.Memory.Pandemona291 ?? throw new NotSupportedException("Native Pandemona rising scene requires its current module state.");
        int elapsed = unchecked(age - 100);
        int center = state.Center25562E8 ?? throw new NotSupportedException("Native Pandemona requires its current center.");
        uint pause = state.Pause2556258 ?? throw new NotSupportedException("Native Pandemona requires its current pause DWORD.");
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona rising scene requires its current arena.");
        if (pause == 0 && elapsed <= 24)
        {
            int modelY = NativeBattlePandemona291Rising.BodyY100(age);
            arena = arena.Write(NativeBattleWaterExecutionFields.ArenaOffset(unchecked(address + 0x1AC), 4), BitConverter.GetBytes(modelY));
            NativeBattlePandemona291TrailAllocation spawned = NativeBattlePandemona291Vortex.Spawn6EDC66(arena, frame.Random, address, resources.TrailTrigonometry);
            frame = frame with { Memory = frame.Memory with { Arena = spawned.Arena }, Random = spawned.Random };
            NativeBattleActionMatrix trailMatrix = NativeBattlePandemona291Layers.Build6F29D0(resources.TrailTrigonometry, unchecked(elapsed << 6), unchecked((elapsed + 64) << 6), 8192) with { Z = center };
            NativeBattleScriptedColorRegisters registers = frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Pandemona requires its actual GTE input owner.");
            arena = NativeBattlePandemona291Vortex.Advance6F1A30(frame.Memory.Arena!, address, trailMatrix, 60, 2048, resources.TrailTrigonometry, (input, transformed) => registers = registers with { NormalInput = input, NormalInputKnown = null, Flags = transformed.Flags, Mac = [transformed.X, transformed.Y, transformed.Z], Ir = [Math.Clamp(transformed.X, short.MinValue, short.MaxValue), Math.Clamp(transformed.Y, short.MinValue, short.MaxValue), Math.Clamp(transformed.Z, short.MinValue, short.MaxValue)] });
            frame = frame with { Memory = frame.Memory with { Arena = arena, ScriptedColorRegisters = registers } };
            if (elapsed == 24)
            {
                NativeBattlePandemona291TrailAllocation transitioned = NativeBattlePandemona291RisingChildren.Transition124(arena, frame.Random, address, center);
                frame = frame with { Memory = frame.Memory with { Arena = transitioned.Arena }, Random = transitioned.Random };
            }
        }
        else if (pause == 0)
        {
            frame = frame with { Memory = frame.Memory with { Arena = NativeBattlePandemona291RisingChildren.Advance6F1B20(arena, address) } };
        }
        if (elapsed == 14 && pause == 0)
        {
            frame = NativeBattlePandemona291RisingChildren.Spawn114(frame);
        }
        int firstFade = elapsed < 24 ? 0 : unchecked((int)((uint)(unchecked((elapsed - 24) << 12)) / 20));
        if (firstFade > 4096)
        {
            return (frame, graphics);
        }
        var layers = DrawNativePandemona291LayerRequests(frame, NativeBattlePandemona291Rising.Layers100(center, age), graphics);
        frame = layers.Frame;
        graphics = layers.Graphics;
        int fade = elapsed < 24 ? 0 : unchecked((int)((uint)(unchecked((elapsed - 24) << 12)) / 30));
        if (fade > 4096)
        {
            return (frame, graphics);
        }
        NativeBattleStaticMeshResource floor = resources.VortexFloorGeometry ?? throw new NotSupportedException("Native Pandemona requires its current floor geometry.");
        NativeBattleStaticMeshResource overlay = resources.VortexOverlayGeometry ?? throw new NotSupportedException("Native Pandemona requires its current overlay geometry.");
        NativeBattleActionMatrix view = NativeCamera?.Projection?.View ?? throw new NotSupportedException("Native Pandemona requires its actual view matrix.");
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleEsunaMeshContext context = new(fade == 0 ? 0x2030u : 0x20F0u, fade == 0 ? null : 0u, fade, 256, 0, 0, full, full, 17, 2);
        NativeBattleActionMatrix first = NativeBattlePandemona291Layers.Build6F29D0(resources.TrailTrigonometry, unchecked(elapsed << 6), 8192, 4096) with { Y = -8192, Z = center };
        var primary = DrawNativePandemona291PreparedMesh(frame, floor, NativeBattleGfMatrices.Compose56C2F0(view, first), context, null, graphics, layers.Ir0, resources.VortexFloorTarget, pilotIdentity: NativePandemona291VisualIdentity(frame, 200));
        NativeBattleActionMatrix second = NativeBattlePandemona291Layers.Build6F29D0(resources.TrailTrigonometry, unchecked(elapsed * 100), 5000, 4096) with { Y = -8192, Z = center };
        var secondary = DrawNativePandemona291PreparedMesh(primary.Frame, floor, NativeBattleGfMatrices.Compose56C2F0(view, second), context with { DepthBias = 128 }, null, primary.Graphics, primary.Ir0, resources.VortexFloorTarget, pilotIdentity: NativePandemona291VisualIdentity(frame, 201));
        NativeBattleActionMatrix translated = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, -8192, center, 4096);
        var late = DrawNativePandemona291PreparedMesh(secondary.Frame, overlay, NativeBattleGfMatrices.Compose56C2F0(view, translated), context with { Flags = fade == 0 ? 0x30u : 0xF0u, DepthBias = 0 }, null, secondary.Graphics, secondary.Ir0, pilotIdentity: NativePandemona291VisualIdentity(frame, 202));
        return (late.Frame, late.Graphics);
    }

    private (NativeBattlePandemona291Frame Frame, NativeBattleGraphicsState Graphics) DrawNativePandemona291Body(NativeBattlePandemona291Frame frame, uint address, NativeBattleGraphicsState graphics)
    {
        NativeBattlePandemona291BodyBounds prepared = NativeBattlePandemona291BodyDrawing.Prepare6F0AE0(frame, address, 8192);
        frame = prepared.Frame;
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Pandemona body requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Pandemona body requires its current view matrix.");
        NativeBattleGraphicsDefinition rules = definition.NativeGraphics ?? throw new NotSupportedException("Native Pandemona body requires its graphics definition.");
        NativeBattlePrimitiveCapabilities capabilities = rules.PrimitiveCapabilities ?? throw new NotSupportedException("Native Pandemona body requires its actual device capabilities.");
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Pandemona body requires its actual packet process.");
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Pandemona body requires its actual ordering table.");
        ushort modelFlags = BinaryPrimitives.ReadUInt16LittleEndian(frame.Memory.Arena!.Read(NativeBattleWaterExecutionFields.ArenaOffset(prepared.ModelAddress, 2), 2));
        if ((modelFlags & 0x20) == 0)
        {
            NativeBattleShadowSource source = rules.Rules.ShadowSource ?? throw new NotSupportedException("Native Pandemona body requires its original shadow source.");
            NativeBattleShadowGeometry shadow = NativeBattlePoseMatrices.ShadowGeometry5088A0(prepared.World, prepared.GroundY, prepared.Bounds, view, source, capabilities.GouraudAlpha);
            NativeBattleModuleProjectedVertex[] points = shadow.Vertices.Select(point => NativeBattleModuleProjection.Project45FE10(shadow.CameraMatrix, point, projection)).ToArray();
            NativeBattleGfGeometryBatch batch = NativeBattleShadowPackets.EmitBatch5088A0(points, prepared.ShadowColor, capabilities.GouraudAlpha, 0x1010, 16, shadow.Vertices.Select(point => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(shadow.CameraMatrix, point), projection)).ToArray(), NativePandemona291TaskVisualIdentity(frame, address, -1));
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, batch);
            process = written.Process;
            primitive = primitive with { Pending = written.Ordering };
            NativeBattleScriptedColorRegisters registers = frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Pandemona shadow requires its canonical GTE input owner.");
            byte[] input = new byte[8];
            NativeBattleVector last = shadow.Vertices[^1];
            BinaryPrimitives.WriteInt16LittleEndian(input, last.X);
            BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(2), last.Y);
            BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(4), last.Z);
            registers = registers with { NormalInput = input.ToImmutableArray(), NormalInputKnown = ImmutableArray.Create(true, true, true, true, true, true, false, false), Mac = [null, null, null], Ir = [null, null, null], Flags = null };
            frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = registers } };
        }
        prepared = prepared with { Frame = frame };
        frame = NativeBattlePandemona291BodyDrawing.Compose5095B0(prepared, view);
        var gate = process.Enter5099D0(capabilities.HardwareEnabled ?? throw new NotSupportedException("Native Pandemona body requires its actual hardware capability."));
        process = gate.Process;
        if (gate.Draw)
        {
            uint color = BinaryPrimitives.ReadUInt32LittleEndian(frame.Memory.Arena!.Read(NativeBattleWaterExecutionFields.ArenaOffset(unchecked(prepared.ModelAddress + 0x28), 4), 4));
            process = process.PublishModelColor(color);
            NativeBattlePandemona291BodyDraw drawn = NativeBattlePandemona291BodyDrawing.Render5099D0(frame, address, projection, rules.Rules.SummonColors ?? throw new NotSupportedException("Native Pandemona body requires its original color arithmetic."), NativePandemona291TaskVisualIdentity(frame, address));
            frame = drawn.Frame;
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, drawn.Packets);
            process = written.Process;
            primitive = primitive with { Pending = written.Ordering };
            if (drawn.SourceCursor is uint cursor)
            {
                process = process.Publish50FDF0(unchecked((uint)process.ByteCursor!.Value), cursor);
            }
        }
        frame = NativeBattlePandemona291BodyDrawing.Restore508C90(frame, prepared.SkeletonOffset, prepared.BoneCount);
        frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process } };
        return (frame, graphics with { PrimitiveFrame = primitive });
    }
}
