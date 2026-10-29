using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoomtrain191SkyResources(NativeBattleStaticMeshResource Source, NativeBattleEffectHeapAllocation TargetVertices, ImmutableArray<uint> Palette)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<uint>? RainColors { get; init; }
}
public sealed record NativeBattleDoomtrain191SkyDraw(NativeBattleActionMatrix Matrix, uint Tint, int Fade, uint Flags);
public sealed record NativeBattleDoomtrain191SkyServices(Func<uint> ReadFlags, Func<NativeBattleDoomtrain191Frame, NativeBattleDoomtrain191SkyDraw, NativeBattleDoomtrain191Frame> Draw);
public sealed record NativeBattleDoomtrain191SkyPackets(NativeBattleGfGeometryBatch Batch, NativeBattleEffectNodeScratch Scratch);

public static class NativeBattleDoomtrain191Sky
{
    public static NativeBattleDoomtrain191Frame Spawn640390(NativeBattleDoomtrain191Frame frame)
    {
        for (int actor = 0; actor < 2; actor++)
        {
            NativeBattleEffectTaskPool pool = Pool(frame);
            (pool, int slot) = pool.Append(0x640580);
            NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0xC, 0);
            ushort yaw = unchecked((ushort)(Random() % 4096));
            task = task.WriteWord(0x10, yaw);
            int yawStep = Random() % 8 + 4;
            task = task.WriteWord(0x12, unchecked((ushort)((yaw & 1) != 0 ? -yawStep : yawStep)));
            ushort roll = unchecked((ushort)(Random() % 4096));
            task = task.WriteWord(0x14, roll);
            int rollStep = Random() % 4 + 2;
            task = task.WriteWord(0x16, unchecked((ushort)((roll & 1) != 0 ? -rollStep : rollStep)));
            task = task.WriteWord(0x18, unchecked((ushort)(Random() % 4096))).WriteWord(0x1A, 1).WriteWord(0x1C, actor == 0 ? (ushort)0xB80 : (ushort)0xC00);
            task = task.WriteWord(0x1E, unchecked((ushort)(Random() % 32)));
            task = task.WriteWord(0x20, unchecked((ushort)(Random() % (actor == 0 ? 4096 : 2048))));
            task = task.WriteWord(0x22, unchecked((ushort)(Random() % (actor == 0 ? 24 : 40) + (actor == 0 ? 36 : 60))));
            frame = PutPool(frame, pool.WriteSlot(slot, task));
        }
        return frame;

        int Random()
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            return value;
        }
    }

    public static NativeBattleDoomtrain191LeafStep Advance640580(NativeBattleDoomtrain191Frame frame, int slot, NativeBattleDoomtrain191SkyResources resources, NativeBattleDoomtrain191SkyServices services)
    {
        NativeBattleModulePoolTask task = Pool(frame).Slots[slot];
        if (task.Callback != 0x640580 || resources.Palette.IsDefault || resources.Palette.Length != 32)
        {
            throw new InvalidDataException("Native Doomtrain sky requires its actual task and complete palette.");
        }
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.RotateZ56D090(NativeBattleGfMatrices.RotateY56D020(NativeBattleGfMatrices.RotateX56CFB0(identity, Word(0x18)), Word(0x10)), Word(0x14));
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, Word(0x1C), Word(0x1C), Word(0x1C)) with { X = 0, Y = 0, Z = 288 };
        uint flags = task.ReadWord(0x1A) != 0 ? 0x20C3u : 0x20CCu;
        int tintFactor = (NativeBattleCameraMath.Sine(Word(0x20)) + 4096) / 2;
        uint tint = NativeBattleGteColorInterpolation.CodeConstants45F270.Interpolate(resources.Palette[(byte)task.ReadWord(0x1E) & 31], tintFactor, 0);
        frame = frame with { Memory = frame.Memory with { PrimitiveProcess = (frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Doomtrain sky requires its canonical far-color owner.")).PublishModelColor(0) } };
        short age = Word(0xC);
        int fade = age < 32 ? unchecked((32 - age) << 7) : age >= 372 ? unchecked((age - 372) << 8) : 0;
        int factor = (NativeBattleCameraMath.Sine(Word(0x20) >> 2) + 4096) / 2;
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain sky requires its actual morph workspace.");
        using NativeBattleEffectArenaTransaction? owned = arena.Transaction is null ? new(arena) : null;
        NativeBattleEffectArena view = owned?.View ?? arena;
        int count = BinaryPrimitives.ReadInt32LittleEndian(Read(resources.Source.Bytes, 4, 4));
        for (int index = 0; index < count; index++)
        {
            byte[] output = new byte[6];
            for (int axis = 0; axis < 3; axis++)
            {
                short start = BinaryPrimitives.ReadInt16LittleEndian(Read(resources.Source.Bytes, checked(8 + index * 8 + axis * 2), 2));
                short end = BinaryPrimitives.ReadInt16LittleEndian(Read(resources.TargetVertices.Data, checked(index * 8 + axis * 2), 2));
                short value = unchecked((short)(start + (unchecked((end - start) * factor) >> 12)));
                BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(axis * 2), value);
            }
            view.Write(checked(0x37000 + index * 8), output);
        }
        frame = frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? view } };
        frame = services.Draw(frame, new(matrix, tint, fade, flags));
        if ((services.ReadFlags() & 1) != 0)
        {
            return new(frame, 0);
        }
        ushort next = unchecked((ushort)(age + 1));
        task = task.WriteWord(0x10, unchecked((ushort)(Word(0x10) + Word(0x12)))).WriteWord(0x14, unchecked((ushort)(Word(0x14) + Word(0x16)))).WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) + 1))).WriteWord(0x20, unchecked((ushort)(Word(0x20) + Word(0x22)))).WriteWord(0xC, next);
        NativeBattleEffectTaskPool pool = Pool(frame);
        frame = PutPool(frame, pool.WriteSlot(slot, task));
        return new(frame, unchecked((short)next) >= 388 ? 2 : 0);

        short Word(int offset) => unchecked((short)task.ReadWord(offset));
    }

    public static NativeBattleDoomtrain191SkyPackets Emit640830(NativeBattleDoomtrain191SkyResources resources, NativeBattleEffectArena arena, NativeBattleDoomtrain191SkyDraw draw, NativeBattleCameraProjection projection, NativeBattleEffectNodeScratch scratch, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        scratch = scratch.WritePrefix(0, BitConverter.GetBytes(draw.Flags)).WritePrefix(4, BitConverter.GetBytes(draw.Tint));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        int cursor = checked(BinaryPrimitives.ReadInt32LittleEndian(Read(resources.Source.Bytes, 0, 4)) + 8);
        Dictionary<ushort, NativeBattleModuleProjectedVertex> projected = [];
        NativeBattleMeshPacketVariant variant = new(draw.Tint, draw.Tint, true, true)
        {
            ObserveFlags = value => scratch = scratch.WritePrefix(24, BitConverter.GetBytes(value)),
            ObserveArea = value => scratch = scratch.WritePrefix(12, BitConverter.GetBytes(value)),
            ObserveFourthScreen = value => scratch = scratch.WritePrefix(16, BitConverter.GetBytes(value)),
            ObserveAverageDepth = value => scratch = scratch.WritePrefix(20, BitConverter.GetBytes(value))
        };
        EmitGroup(2, 20);
        EmitGroup(3, 24);
        cursor = checked(cursor + 8);
        EmitGroup(6, 28);
        EmitGroup(7, 36);
        return new(new(attempts.ToImmutable(), allocation), scratch);

        void EmitGroup(int group, int stride)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(resources.Source.Bytes, cursor, 4));
            cursor = checked(cursor + 4);
            PublishCursor();
            for (int index = 0; index < count; index++)
            {
                byte[] face = Read(resources.Source.Bytes, cursor, stride).ToArray();
                ushort[] offsets = Enumerable.Range(0, (group & 1) == 0 ? 3 : 4).Select(corner => BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + 2 * corner))).ToArray();
                NativeBattleGfGeometryPackets.ProjectedCorners corners = new(offsets, Project);
                NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt(face, corners, group, new(draw.Flags, 0, draw.Fade, null, null, 0, 0, 4126, 6), colors, allocation, variant: variant);
                if (pilotIdentity is { } identity && attempt.Primitive is { } primitive)
                {
                    System.Numerics.Vector3 World(ushort offset)
                    {
                        byte[] xyz = arena.Read(checked(0x37000 + offset * 4), 6);
                        NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
                        return NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(draw.Matrix, point), projection);
                    }
                    attempt = attempt with { Primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 65537 + cursor) }, World(offsets[0]), World(offsets[1]), World(offsets[2]), offsets.Length == 4 ? World(offsets[3]) : default, (byte)offsets.Length) { FocalLengthOverride = projection.FocalLength } } };
                }
                attempts.Add(attempt);
                if (attempt.Primitive is not null)
                {
                    allocation = checked(allocation + attempt.PacketLength);
                }
                cursor = checked(cursor + stride);
            }
            PublishCursor();
        }
        void PublishCursor() => scratch = scratch.WritePrefix(8, BitConverter.GetBytes(unchecked(resources.Source.Address + (uint)cursor)));
        NativeBattleModuleProjectedVertex Project(ushort offset)
        {
            if (!projected.TryGetValue(offset, out NativeBattleModuleProjectedVertex vertex))
            {
                byte[] xyz = arena.Read(checked(0x37000 + offset * 4), 6);
                vertex = NativeBattleModuleProjection.Project45FE10(draw.Matrix, new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), projection);
                projected.Add(offset, vertex);
            }
            return vertex;
        }
    }

    private static ReadOnlySpan<byte> Read(ImmutableArray<byte> source, int offset, int length)
    {
        if (source.IsDefault || offset < 0 || length < 0 || offset > source.Length - length)
        {
            throw new InvalidDataException("Native Doomtrain sky source access leaves its supplied original extent.");
        }
        return source.AsSpan().Slice(offset, length);
    }
    private static NativeBattleEffectTaskPool Pool(NativeBattleDoomtrain191Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
    private static NativeBattleDoomtrain191Frame PutPool(NativeBattleDoomtrain191Frame frame, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(Pool(frame)), pool) } };
}
