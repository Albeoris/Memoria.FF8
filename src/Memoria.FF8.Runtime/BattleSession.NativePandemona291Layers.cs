using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattlePandemona291Frame Frame, NativeBattleGraphicsState Graphics) DrawNativePandemona291Layers(NativeBattlePandemona291Frame frame, int age, NativeBattleGraphicsState graphics)
    {
        NativeBattlePandemona291Resources resources = frame.Effect.Binding.Pandemona291Resources ?? throw new NotSupportedException("Native Pandemona requires its original layer resources.");
        NativeBattlePandemona291State state = frame.Memory.Pandemona291 ?? throw new NotSupportedException("Native Pandemona requires its current module state.");
        ImmutableArray<NativeBattlePandemona291Animation> animations = state.Animations ?? throw new NotSupportedException("Native Pandemona layers require their current rewritten animation records.");
        uint buffer = state.ModelBuffer25562F4 ?? throw new NotSupportedException("Native Pandemona requires its current geometry workspace.");
        int center = state.Center25562E8 ?? throw new NotSupportedException("Native Pandemona requires its current layer center.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Pandemona requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Pandemona requires its current world/view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Pandemona requires its authenticated PC color arithmetic.");
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Pandemona requires its current projection controls.");
        short zsf4 = controls.Zsf4 ?? throw new NotSupportedException("Native Pandemona requires its current ZSF4.");
        foreach (NativeBattlePandemona291LayerRequest request in NativeBattlePandemona291Layers.Opening(center, age))
        {
            NativeBattlePandemona291Animation source = animations.SingleOrDefault(source => source.Address == request.Source) ?? throw new NotSupportedException("Native Pandemona requires its current selected layer animation.");
            NativeBattleStaticMeshResource geometry = resources.LayerGeometry.SingleOrDefault(model => model.Address == request.Source) ?? throw new NotSupportedException("Native Pandemona requires its original selected layer geometry.");
            NativeBattleStaticMeshResource target = resources.LayerTargets.SingleOrDefault(model => model.Address == request.Target) ?? throw new NotSupportedException("Native Pandemona requires its original selected target shape.");
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
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Pandemona requires its canonical far-color owner.");
            NativeBattleModelRendererRegisters renderer = process.ModelRendererRegisters ?? new(null, null, null, null, null);
            renderer = prepared.Context.FarColor is uint color ? renderer.PublishModelColor(color) : renderer with { FarRed = null, FarGreen = null, FarBlue = null };
            NativeBattleScriptedColorRegisters registers = frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Pandemona requires its canonical GTE input owner.");
            NativeBattlePandemona291LayerVertex last = prepared.Vertices[^1];
            byte[] input = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(input, last.Input.X);
            BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(2), last.Input.Y);
            BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(4), last.Input.Z);
            BinaryPrimitives.WriteUInt16LittleEndian(input.AsSpan(6), last.Padding);
            registers = registers with { NormalInput = input.ToImmutableArray(), NormalInputKnown = null };
            NativeBattleGfGeometryBatch batch = NativeBattlePandemona291Surface.EmitGeometry701DD0(geometry.Bytes, prepared.Matrix, prepared.Context, projection, colors, zsf4, offset =>
            {
                byte[] xyz = arena.Read(checked(scratch + offset), 6);
                return new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
            }, offset =>
            {
                var packed = arena.ReadSnapshot(checked(scratch + offset), 8);
                registers = registers with { NormalInput = packed.Data.ToImmutableArray(), NormalInputKnown = packed.Known.ToImmutableArray() };
            }, flags => registers = registers with { Flags = flags }, point =>
            {
                NativeBattleModuleDepthCueVertex fourth = NativeBattleModuleProjection.ProjectWithCue45FAA0(prepared.Matrix, point, projection, controls.Dqa, controls.Dqb);
                registers = registers with { Flags = fourth.Projected.Flags };
                return fourth.Projected;
            });
            registers = registers with { Mac = [null, null, null], Ir = [null, null, null] };
            if ((prepared.Context.Flags & 0xC0) != 0)
            {
                registers = registers with { Material = null, Rgb = [null, null, null], Flags = null };
            }
            frame = frame with { Memory = frame.Memory with { Arena = arena, PrimitiveProcess = process with { ModelRendererRegisters = renderer }, ScriptedColorRegisters = registers } };
            NativeBattlePandemona291State live = frame.Memory.Pandemona291!;
            uint output = live.PacketCursor25562DC ?? throw new NotSupportedException("Native Pandemona requires its current packet cursor.");
            NativeBattlePrimitiveFrame primitiveFrame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Pandemona requires its current ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, checked((int)(output - NativeBattleEffectNodeMemory.ArenaAddress)), primitiveFrame.Pending, batch);
            frame = frame with { Memory = written.Memory with { Pandemona291 = live with { PacketCursor25562DC = unchecked(output + (uint)batch.AllocationBytes) } } };
            graphics = graphics with { PrimitiveFrame = primitiveFrame with { Pending = written.Ordering } };
        }
        return (frame, graphics);
    }
}
