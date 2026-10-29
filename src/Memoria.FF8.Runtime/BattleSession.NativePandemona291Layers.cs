using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattlePandemona291Frame Frame, NativeBattleGraphicsState Graphics) DrawNativePandemona291Layers(NativeBattlePandemona291Frame frame, int age, NativeBattleGraphicsState graphics)
    {
        var drawn = DrawNativePandemona291LayerRequests(frame, NativeBattlePandemona291Layers.Opening(frame.Memory.Pandemona291?.Center25562E8 ?? throw new NotSupportedException("Native Pandemona requires its current layer center."), age), graphics);
        return (drawn.Frame, drawn.Graphics);
    }

    private (NativeBattlePandemona291Frame Frame, NativeBattleGraphicsState Graphics, short? Ir0) DrawNativePandemona291LayerRequests(NativeBattlePandemona291Frame frame, ImmutableArray<NativeBattlePandemona291LayerRequest> requests, NativeBattleGraphicsState graphics)
    {
        short? ir0 = null;
        NativeBattlePandemona291Resources resources = frame.Effect.Binding.Pandemona291Resources ?? throw new NotSupportedException("Native Pandemona requires its original layer resources.");
        NativeBattlePandemona291State state = frame.Memory.Pandemona291 ?? throw new NotSupportedException("Native Pandemona requires its current module state.");
        ImmutableArray<NativeBattlePandemona291Animation> animations = state.Animations ?? throw new NotSupportedException("Native Pandemona layers require their current rewritten animation records.");
        uint buffer = state.ModelBuffer25562F4 ?? throw new NotSupportedException("Native Pandemona requires its current geometry workspace.");
        NativeBattleActionMatrix view = NativeCamera?.Projection?.View ?? throw new NotSupportedException("Native Pandemona requires its current world/view matrix.");
        foreach (NativeBattlePandemona291LayerRequest request in requests)
        {
            NativeBattlePandemona291Animation source = animations.SingleOrDefault(source => source.Address == request.Source) ?? throw new NotSupportedException("Native Pandemona requires its current selected layer animation.");
            NativeBattleStaticMeshResource geometry = resources.LayerGeometry.SingleOrDefault(model => model.Address == request.Source) ?? throw new NotSupportedException("Native Pandemona requires its original selected layer geometry.");
            NativeBattleStaticMeshResource target = resources.LayerTargets.SingleOrDefault(model => model.Address == request.Target) ?? throw new NotSupportedException("Native Pandemona requires its original selected target shape.");
            ir0 = unchecked((short)request.Morph);
            NativeBattlePandemona291PreparedLayer prepared = NativeBattlePandemona291Layers.Prepare6F2A30(source, target.Bytes, resources.TrailTrigonometry, view, request);
            int scratch = checked((int)(buffer - NativeBattleEffectNodeMemory.ArenaAddress) + 0x31D4);
            NativeBattleEffectArena arena = frame.Memory.Arena!;
            for (int index = 0; index < prepared.Vertices.Length; index++)
            {
                NativeBattleVector vector = prepared.Vertices[index].Position;
                byte[] xyz = new byte[6];
                BinaryPrimitives.WriteInt16LittleEndian(xyz, vector.X);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), vector.Y);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), vector.Z);
                arena = arena.Write(checked(scratch + index * 8), xyz);
            }
            NativeBattleScriptedColorRegisters registers = frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Pandemona requires its canonical GTE input owner.");
            NativeBattlePandemona291LayerVertex last = prepared.Vertices[^1];
            byte[] input = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(input, last.Input.X);
            BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(2), last.Input.Y);
            BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(4), last.Input.Z);
            BinaryPrimitives.WriteUInt16LittleEndian(input.AsSpan(6), last.Padding);
            registers = registers with { NormalInput = input.ToImmutableArray(), NormalInputKnown = null };
            frame = frame with { Memory = frame.Memory with { Arena = arena, ScriptedColorRegisters = registers } };
            var drawn = DrawNativePandemona291PreparedMesh(frame, geometry, prepared.Matrix, prepared.Context, scratch, graphics, unchecked((short)request.Morph), pilotIdentity: NativePandemona291VisualIdentity(frame, 100 + resources.LayerGeometry.IndexOf(geometry)));
            frame = drawn.Frame;
            graphics = drawn.Graphics;
            ir0 = drawn.Ir0;
        }
        return (frame, graphics, ir0);
    }

    private (NativeBattlePandemona291Frame Frame, NativeBattleGraphicsState Graphics, short? Ir0) DrawNativePandemona291PreparedMesh(NativeBattlePandemona291Frame frame, NativeBattleStaticMeshResource geometry, NativeBattleActionMatrix matrix, NativeBattleEsunaMeshContext context, int? selectedOffset, NativeBattleGraphicsState graphics, short? ir0, ImmutableArray<byte>? selectedSource = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona requires its current mesh arena.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Pandemona requires its current projection.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Pandemona requires its authenticated PC color arithmetic.");
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Pandemona requires its current projection controls.");
        short? zsf3 = controls.Zsf3;
        short zsf4 = controls.Zsf4 ?? throw new NotSupportedException("Native Pandemona requires its current ZSF4.");
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Pandemona requires its canonical far-color owner.");
        NativeBattleModelRendererRegisters renderer = process.ModelRendererRegisters ?? new(null, null, null, null, null);
        renderer = context.FarColor is uint color ? renderer.PublishModelColor(color) : renderer with { FarRed = null, FarGreen = null, FarBlue = null };
        NativeBattleScriptedColorRegisters registers = frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Pandemona requires its canonical GTE input owner.");
        NativeBattleGfGeometryBatch batch = NativeBattlePandemona291Surface.EmitGeometry701DD0(geometry.Bytes, matrix, context, projection, colors, zsf4, offset =>
        {
            byte[] xyz = selectedOffset is int selected ? arena.Read(checked(selected + offset), 6) : (selectedSource ?? geometry.Bytes).AsSpan().Slice(checked(8 + offset), 6).ToArray();
            return new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
        }, offset =>
        {
            if (selectedOffset is int selected)
            {
                var packed = arena.ReadSnapshot(checked(selected + offset), 8);
                registers = registers with { NormalInput = packed.Data.ToImmutableArray(), NormalInputKnown = packed.Known.ToImmutableArray() };
            }
            else
            {
                registers = registers with { NormalInput = (selectedSource ?? geometry.Bytes).AsSpan().Slice(checked(8 + offset), 8).ToArray().ToImmutableArray(), NormalInputKnown = null };
            }
        }, flags => registers = registers with { Flags = flags }, point =>
        {
            NativeBattleModuleDepthCueVertex fourth = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix, point, projection, controls.Dqa, controls.Dqb);
            ir0 = unchecked((short)fourth.Cue);
            registers = registers with { Flags = fourth.Projected.Flags };
            return fourth.Projected;
        }, zsf3, pilotIdentity: pilotIdentity);
        registers = registers with { Mac = [null, null, null], Ir = [null, null, null] };
        if ((context.Flags & 0xC0) != 0)
        {
            registers = registers with { Material = null, Rgb = [null, null, null], Flags = null };
            ir0 = null;
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena, PrimitiveProcess = process with { ModelRendererRegisters = renderer }, ScriptedColorRegisters = registers } };
        NativeBattlePandemona291State live = frame.Memory.Pandemona291!;
        uint output = live.PacketCursor25562DC ?? throw new NotSupportedException("Native Pandemona requires its current packet cursor.");
        NativeBattlePrimitiveFrame primitiveFrame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Pandemona requires its current ordering table.");
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, checked((int)(output - NativeBattleEffectNodeMemory.ArenaAddress)), primitiveFrame.Pending, batch);
        frame = frame with { Memory = written.Memory with { Pandemona291 = live with { PacketCursor25562DC = unchecked(output + (uint)batch.AllocationBytes) } } };
        graphics = graphics with { PrimitiveFrame = primitiveFrame with { Pending = written.Ordering } };
        return (frame, graphics, ir0);
    }

    private static NativeBattlePilotVisualIdentity NativePandemona291VisualIdentity(NativeBattlePandemona291Frame frame, int primitive)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattlePandemona291Entry.MainDescriptor);
        return NativeBattlePilotVisualLifetimes.Identity(291, pool.Slots[pool.Head ?? throw new NotSupportedException("Native Pandemona presentation requires its live master task.")], primitive);
    }

    private static NativeBattlePilotVisualIdentity NativePandemona291TaskVisualIdentity(NativeBattlePandemona291Frame frame, uint address, int primitive = 0)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => address >= pool.BankAddress && (ulong)address < (ulong)pool.BankAddress + (ulong)pool.Stride * (ulong)pool.Slots.Length && (address - pool.BankAddress) % pool.Stride == 0);
        return NativeBattlePilotVisualLifetimes.Identity(291, pool.Slots[checked((int)((address - pool.BankAddress) / pool.Stride))], primitive);
    }

}
