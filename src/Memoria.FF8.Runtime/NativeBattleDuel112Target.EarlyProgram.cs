using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDuel112Target
{
    public static NativeBattleDuel112DrawingStep DrawEarly6CBF90(NativeBattleDuel112Frame frame, int slot, NativeBattleDuel112DrawingInputs inputs, NativeBattleLogicalOrdering ordering)
    {
        NativeBattleModulePoolTask task = frame.Effect.Pools[1].Slots[slot];
        if (frame.Effect.Pools[1].DescriptorAddress != 0x2521950 || task.Callback != 0x6CBF90)
        {
            throw new InvalidDataException("Native Duel opening hierarchy requires its actual target task owner.");
        }
        int elapsed = unchecked((short)task.ReadWord(12)) - 10;
        if ((uint)elapsed > 15)
        {
            return new(frame, ordering);
        }
        int actor = frame.Memory.Duel112?.Parameters?.Actor78 ?? throw new NotSupportedException("Native Duel opening hierarchy requires its actual actor.");
        NativeBattleDuel112Resources resources = frame.Memory.Duel112?.Resources ?? throw new NotSupportedException("Native Duel opening hierarchy requires its actual resource owner.");
        NativeBattleGraphicsModel model = inputs.Actor.ReadModel(actor);
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Duel opening hierarchy requires its actual actor transform.");
        if (elapsed == 0)
        {
            frame = NativeBattleDuel112Workspaces.Initialize7016B0(frame, slot, 180);
            NativeBattleVector start = inputs.ReadBone502170(actor, 241, 0);
            task = frame.Effect.Pools[1].Slots[slot].WriteWord(76, unchecked((ushort)start.X)).WriteWord(78, unchecked((ushort)start.Y)).WriteWord(80, unchecked((ushort)start.Z));
            Store();
        }
        else
        {
            NativeBattleVector anchor = new(Signed(76), transform.Angle36, Signed(80));
            NativeBattleDuel112WorkspaceStep drawn = NativeBattleDuel112Workspaces.Advance701970(frame, slot, 180, new(0x12DB65C, MorphAddress(frame), anchor, null, null), inputs.View, inputs.Projection, inputs.Colors, ordering);
            frame = drawn.Frame;
            ordering = drawn.Ordering;
            if (elapsed <= 7)
            {
                short yaw = model.Rotation14 ?? throw new NotSupportedException("Native Duel approach requires its actual actor yaw.");
                int radius = unchecked((int)task.ReadDword(172)) / 400;
                transform = transform with { Position28 = transform.Position28 with { X = unchecked((short)(transform.Position28.X + unchecked(radius * NativeBattleCameraMath.Sine(yaw)) / 160)), Z = unchecked((short)(transform.Position28.Z + unchecked(radius * NativeBattleCameraMath.Cosine(yaw)) / 160)) } };
                model = model with { Transform = transform };
                inputs.Actor.WriteModel(actor, model);
            }
        }
        NativeBattleVector bone = inputs.ReadBone502170(actor, 241, 0);
        if (elapsed < 2)
        {
            frame = NativeBattleDuel112Births.Early6CBF90(frame, slot, bone, transform);
        }
        if (elapsed != 0)
        {
            task = frame.Effect.Pools[1].Slots[slot];
            short dx = unchecked((short)(bone.X - Signed(76)));
            short dy = unchecked((short)(bone.Y - Signed(78)));
            short dz = unchecked((short)(bone.Z - Signed(80)));
            int magnitude = unchecked((int)(long)Math.Sqrt(unchecked(dx * dx + dz * dz)));
            if (dz > 0)
            {
                magnitude = unchecked(-magnitude);
            }
            NativeBattleActionMatrix deformation = new(NativeBattleDuelSharedPackets.Rotation6D9510(unchecked((short)(elapsed << 6))), 0, 0, 0, 4096);
            NativeBattleStaticMeshResource mesh = resources.Meshes.Single(mesh => mesh.Address == 0x12C5928);
            uint selected = MorphAddress(frame);
            int offset = NativeBattleWaterExecutionFields.ArenaOffset(selected, 0);
            short maximum = BinaryPrimitives.ReadInt16LittleEndian(frame.Staging.Read(0x25218F0, 2));
            short range = BinaryPrimitives.ReadInt16LittleEndian(frame.Staging.Read(0x25218F2, 2));
            NativeBattleEffectArena arena = NativeBattleDuel112Geometry.Deform6CD7F0(mesh.Bytes.AsSpan(), frame.Memory.Arena ?? throw new NotSupportedException("Native Duel sword deformation requires its actual arena."), offset, deformation, maximum, range, unchecked((short)magnitude));
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
            short yaw = model.Rotation14 ?? throw new NotSupportedException("Native Duel sword matrix requires its actual actor yaw.");
            ImmutableArray<short> rotation = NativeBattleDuelSharedPackets.Rotation6D9510(yaw).SetItem(4, unchecked((short)(-4 * dy)));
            NativeBattleActionMatrix local = new(rotation, Signed(76), transform.Angle36, Signed(80), 4096);
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.View, local);
            uint flags = elapsed < 7 ? 0x2033u : 0x20F3u;
            int? fade = elapsed < 7 ? null : ((elapsed << 9) - 3073) & 0x1FFFFE00;
            NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(mesh.Bytes, 0, default, matrix, inputs.Projection, new(flags, elapsed < 7 ? null : 0u, fade, null, null, null, 17, 2), inputs.Colors, vertexOffset =>
            {
                byte[] vertex = frame.Memory.Arena!.Read(checked(offset + vertexOffset), 6);
                return new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(vertex.AsSpan(4)));
            });
            int cursor = frame.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Duel sword packet requires its actual draw cursor.");
            uint physicalCursor = BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x2521914, 4));
            if (physicalCursor != checked(0x20DFAB8u + (uint)cursor))
            {
                throw new InvalidDataException("Native Duel sword draw cursor differs from its process DWORD.");
            }
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, ordering, batch);
            frame = frame with { Memory = written.Memory, Effect = frame.Effect with { DrawArenaOffset = written.Cursor }, Staging = frame.Staging.Write(0x2521914, BitConverter.GetBytes(checked(0x20DFAB8u + (uint)written.Cursor))) };
            ordering = written.Ordering;
        }
        if (elapsed >= 2)
        {
            frame = NativeBattleDuel112Births.Early6CBF90(frame, slot, bone, transform);
        }
        return new(frame, ordering);

        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
        void Store()
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool.WriteSlot(slot, task)) } };
        }
    }
}
