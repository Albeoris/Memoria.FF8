using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePandemona291SurfaceInputs(NativeBattleGfAnimationSource Source, NativeBattleActionMatrix Parent, ImmutableArray<byte>? TextureKinds, byte Phase, short DepthBias, int WorkspaceLength, int WorkspaceOffset = 0x1F0);

public sealed partial class BattleSession
{
    private (NativeBattlePandemona291Frame Frame, NativeBattleGraphicsState Graphics, int Remaining) DrawNativePandemona291Surface(NativeBattlePandemona291Frame frame, uint taskAddress, int age, NativeBattleGraphicsState graphics, NativeBattlePandemona291SurfaceInputs? inputs = null)
    {
        NativeBattlePandemona291Resources resources = frame.Effect.Binding.Pandemona291Resources ?? throw new NotSupportedException("Native Pandemona requires its current surface resources.");
        NativeBattleGfAnimationSource source = inputs?.Source ?? (age >= 144 ? resources.RisingProgram : resources.SurfaceProgram) ?? throw new NotSupportedException("Native Pandemona requires its selected original surface programme.");
        if (inputs is null && age < 144 && resources.SurfaceTextureKinds.IsDefaultOrEmpty)
        {
            throw new NotSupportedException("Native Pandemona requires its original surface texture selector table.");
        }
        NativeBattlePandemona291State state = frame.Memory.Pandemona291 ?? throw new NotSupportedException("Native Pandemona requires its current module state.");
        uint buffer = state.ModelBuffer25562F4 ?? throw new NotSupportedException("Native Pandemona requires its current model buffer.");
        int center = state.Center25562E8 ?? throw new NotSupportedException("Native Pandemona requires its current surface center.");
        uint pause = state.Pause2556258 ?? throw new NotSupportedException("Native Pandemona requires its current pause DWORD.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Pandemona requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Pandemona requires its current world/view matrix.");
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler701310(new(0, 2048, 0)) with { Z = center };
        NativeBattleActionMatrix parent = inputs?.Parent ?? NativeBattleGfMatrices.Compose56C2F0(view, local);
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Pandemona requires its authenticated PC color arithmetic.");
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Pandemona requires its current projection controls.");
        short zsf4 = controls.Zsf4 ?? throw new NotSupportedException("Native Pandemona requires its current ZSF4.");
        int workspaceOffset = checked((int)(taskAddress - NativeBattleEffectNodeMemory.ArenaAddress) + (inputs?.WorkspaceOffset ?? 0x1F0));
        int workspaceLength = inputs?.WorkspaceLength ?? (age >= 144 ? 0xE0 : 0x12C);
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(frame.Memory.Arena!, workspaceOffset, workspaceLength);
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, unchecked((int)pause), (current, node) =>
        {
            if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
            {
                return current;
            }
            using NativeBattleGfSourceReader sourceBytes = source.OpenReader();
            ImmutableArray<byte>? kinds = inputs is null ? age >= 144 ? null : resources.SurfaceTextureKinds : inputs.TextureKinds;
            byte kind = kinds is { } table ? table[unchecked((short)node.GeometryIndex)] : (byte)4;
            NativeBattlePandemona291PreparedSurface prepared = NativeBattlePandemona291Surface.Prepare6F06F0(source, node, parent, kind, inputs?.Phase ?? (age >= 144 ? (byte)0 : unchecked((byte)((age - 10) * 8))), inputs?.DepthBias ?? (age >= 144 ? (short)-128 : (short)0));
            int scratch = checked((int)(buffer - NativeBattleEffectNodeMemory.ArenaAddress) + 0x31D4);
            NativeBattleEffectArena arena = frame.Memory.Arena!;
            for (int index = 0; index < prepared.Morph.Length; index++)
            {
                NativeBattleVector vector = prepared.Morph[index];
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
            NativeBattleGfGeometryBatch batch = NativeBattlePandemona291Surface.Emit701DD0(source, prepared, projection, colors, zsf4, offset =>
            {
                Span<byte> xyz = stackalloc byte[6];
                if (prepared.Morph.IsEmpty)
                {
                    sourceBytes.ReadInto(checked(prepared.SelectedOffset + offset), xyz);
                }
                else
                {
                    arena.ReadInto(checked(scratch + offset), xyz);
                }
                return new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz[2..]), BinaryPrimitives.ReadInt16LittleEndian(xyz[4..]));
            }, offset =>
            {
                if (prepared.Morph.IsEmpty)
                {
                    registers = registers with { NormalInput = System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(sourceBytes.ReadBytes(checked(prepared.SelectedOffset + offset), 8)), NormalInputKnown = null };
                }
                else
                {
                    var packed = arena.ReadSnapshot(checked(scratch + offset), 8);
                    registers = registers with { NormalInput = packed.Data.ToImmutableArray(), NormalInputKnown = packed.Known.ToImmutableArray() };
                }
            }, flags => registers = registers with { Flags = flags }, point =>
            {
                NativeBattleModuleDepthCueVertex fourth = NativeBattleModuleProjection.ProjectWithCue45FAA0(prepared.Matrix, point, projection, controls.Dqa, controls.Dqb);
                registers = registers with { Flags = fourth.Projected.Flags };
                return fourth.Projected;
            }, controls.Zsf3);
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
            return current;
        });
        frame = frame with { Memory = frame.Memory with { Arena = advanced.Workspace.CopyTo(frame.Memory.Arena!, workspaceOffset) } };
        return (frame, graphics, advanced.Remaining);
    }
}
