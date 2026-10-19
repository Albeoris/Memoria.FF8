using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoomtrain191KnockbackMemory([property: System.Text.Json.Serialization.JsonRequired] NativeBattleEffectArena Rows)
{
    public static NativeBattleDoomtrain191KnockbackMemory FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public static NativeBattleDoomtrain191KnockbackMemory Unknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, 252));
    public void Validate()
    {
        Rows.Validate();
        if (Rows.Regions.Any(region => region.Offset > 252 - region.Data.Length) || Rows.UnknownRegions.Any(region => region.Offset > 252 - region.Length))
        {
            throw new InvalidDataException("Native Doomtrain knockback requires its seven physical36-byte rows.");
        }
    }
}

public sealed record NativeBattleDoomtrain191KnockbackServices(Func<uint> ReadFlags, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattleVector> ReadAnchor, Func<NativeBattleDoomtrain191Frame, int, NativeBattleGraphicsModel, NativeBattleDoomtrain191Frame> WriteModel);

public static class NativeBattleDoomtrain191Knockback
{
    public static NativeBattleDoomtrain191Frame Spawn646AA0(NativeBattleDoomtrain191Frame frame, ImmutableArray<int> actors, NativeBattleVector center, NativeBattleDoomtrain191KnockbackServices services)
    {
        var state = frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain knockback requires its retained module state.");
        NativeBattleDoomtrain191KnockbackMemory owner = state.Knockback ?? NativeBattleDoomtrain191KnockbackMemory.Unknown();
        NativeBattleEffectArena rows = owner.Rows;
        for (int index = 0; index < actors.Length; index++)
        {
            int actor = actors[index];
            NativeBattleGraphicsModel model = services.ReadModel(actor);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Doomtrain knockback requires its current actor transform.");
            NativeBattleVector point = services.ReadAnchor(actor);
            NativeBattleEffectTaskPool pool = Pool(frame);
            (pool, int slot) = pool.Append(0x646D30);
            ushort spin = unchecked((ushort)((Random() % 2 + 1) << 12));
            ushort mask = unchecked((ushort)(1u << ((index + 1) & 31)));
            NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0xC, 0).WriteWord(0xE, unchecked((ushort)actor)).WriteWord(0x18, spin).WriteWord(0x1A, mask).WriteWord(0x10, unchecked((ushort)point.X)).WriteWord(0x12, unchecked((ushort)point.Y)).WriteWord(0x14, unchecked((ushort)point.Z));
            frame = PutPool(frame, pool with { Slots = pool.Slots.SetItem(slot, task) });
            int record = checked(actor * 36);
            rows = rows.Write(record, BitConverter.GetBytes((ushort)(model.Flags0 & 0x1020)));
            rows = rows.Write(record + 4, BitConverter.GetBytes(model.Rotation12 ?? throw new NotSupportedException("Native Doomtrain knockback requires its current actor pitch."))).Write(record + 6, BitConverter.GetBytes(model.Rotation14 ?? throw new NotSupportedException("Native Doomtrain knockback requires its current actor yaw."))).Write(record + 8, BitConverter.GetBytes(model.Rotation16 ?? throw new NotSupportedException("Native Doomtrain knockback requires its current actor roll.")));
            rows = model.RotationPadding18 is ushort padding ? rows.Write(record + 10, BitConverter.GetBytes(padding)) : rows.MarkUnknown(record + 10, 2);
            rows = WriteVector(rows, record + 12, transform.Position28).Write(record + 18, BitConverter.GetBytes(transform.Vertical34));
            rows = WriteVector(rows, record + 20, new(0, 0, 0));
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(point.X - center.X, point.Y - center.Y, Math.Abs(point.Z - center.Z));
            short x = Target(direction.X, center.X, point.X);
            short y = Target(direction.Y, center.Y, point.Y);
            short z = Target(direction.Z, center.Z, point.Z);
            rows = WriteVector(rows, record + 28, new(x, y, z));
        }
        return frame with { Memory = frame.Memory with { Doomtrain191 = state with { Knockback = owner with { Rows = rows } } } };

        short Target(int direction, short origin, short current)
        {
            short target = unchecked((short)((unchecked(direction * 15000) >> 12) + origin + Random() % 6000 - 3000));
            return unchecked((short)((target - current) / 16));
        }
        int Random()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    public static NativeBattleDoomtrain191LeafStep Advance646D30(NativeBattleDoomtrain191Frame frame, int slot, NativeBattleDoomtrain191KnockbackServices services)
    {
        NativeBattleEffectTaskPool pool = Pool(frame);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x646D30)
        {
            throw new InvalidDataException("Native Doomtrain knockback requires its actual task.");
        }
        int actor = unchecked((short)task.ReadWord(0xE));
        var state = frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain knockback requires its module state.");
        var owner = state.Knockback ?? throw new NotSupportedException("Native Doomtrain knockback requires its actual initialized rows.");
        NativeBattleEffectArena rows = owner.Rows;
        int record = checked(actor * 36);
        NativeBattleGraphicsModel model = services.ReadModel(actor);
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Doomtrain knockback requires its current actor transform.");
        int age = Word(0xC);
        short pitch = unchecked((short)(unchecked(Word(0x18) * age) / 16));
        short yaw = unchecked((short)(unchecked(age << 12) / 16));
        short roll = model.Rotation16 ?? throw new NotSupportedException("Native Doomtrain knockback requires its current actor roll.");
        NativeBattleVector anchor = new(Word(0x10), Word(0x12), Word(0x14));
        NativeBattleVector original = ReadVector(rows, record + 12);
        NativeBattleVector difference = new(unchecked((short)(anchor.X - original.X)), unchecked((short)(anchor.Y - original.Y)), unchecked((short)(anchor.Z - original.Z)));
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleGfMatrices.Euler56CE30(new(pitch, yaw, roll)), difference);
        NativeBattleVector position = new(unchecked((short)(anchor.X - rotated.X)), unchecked((short)(anchor.Y - rotated.Y)), unchecked((short)(anchor.Z - rotated.Z)));
        if (age < 16)
        {
            NativeBattleVector offset = ReadVector(rows, record + 20);
            position = new(unchecked((short)(position.X + offset.X)), unchecked((short)(position.Y + offset.Y)), unchecked((short)(position.Z + offset.Z)));
        }
        else if (age < 50)
        {
            if (age == 16)
            {
                task = task.WriteWord(0x20, 0xDCD8).WriteWord(0x22, 1125);
            }
            position = position with { Y = unchecked((short)(position.Y + Word(0x20))) };
        }
        model = model with { Flags0 = (ushort)(model.Flags0 | 0x1020), Rotation12 = pitch, Rotation14 = yaw, Transform = transform with { Position28 = position } };
        frame = services.WriteModel(frame, actor, model);
        if ((services.ReadFlags() & 1) != 0)
        {
            pool = Pool(frame);
            return new(PutPool(frame, pool with { Slots = pool.Slots.SetItem(slot, task with { Next = pool.Slots[slot].Next }) }), 0);
        }
        if (age < 16)
        {
            NativeBattleVector offset = ReadVector(rows, record + 20);
            NativeBattleVector velocity = ReadVector(rows, record + 28);
            rows = WriteVector(rows, record + 20, new(unchecked((short)(offset.X + velocity.X)), unchecked((short)(offset.Y + velocity.Y)), unchecked((short)(offset.Z + velocity.Z))));
        }
        else if (age >= 42)
        {
            task = task.WriteWord(0x20, unchecked((ushort)(Word(0x20) + Word(0x22))));
        }
        ushort next = unchecked((ushort)(age + 1));
        pool = Pool(frame);
        frame = PutPool(frame, pool with { Slots = pool.Slots.SetItem(slot, task.WriteWord(0xC, next) with { Next = pool.Slots[slot].Next }) });
        frame = frame with { Memory = frame.Memory with { Doomtrain191 = frame.Memory.Doomtrain191! with { Knockback = owner with { Rows = rows } } } };
        if (unchecked((short)next) < 50)
        {
            return new(frame, 0);
        }
        model = services.ReadModel(actor);
        transform = model.Transform ?? throw new NotSupportedException("Native Doomtrain knockback restore requires its current actor transform.");
        var pad = rows.ReadSnapshot(record + 10, 2);
        ushort? padding = pad.Known.All(value => value) ? BinaryPrimitives.ReadUInt16LittleEndian(pad.Data.AsSpan()) : null;
        model = model with { Flags0 = (ushort)((model.Flags0 & ~0x1020) | BinaryPrimitives.ReadUInt16LittleEndian(rows.Read(record, 2))), Rotation12 = BinaryPrimitives.ReadInt16LittleEndian(rows.Read(record + 4, 2)), Rotation14 = BinaryPrimitives.ReadInt16LittleEndian(rows.Read(record + 6, 2)), Rotation16 = BinaryPrimitives.ReadInt16LittleEndian(rows.Read(record + 8, 2)), RotationPadding18 = padding, Transform = transform with { Position28 = original, Vertical34 = BinaryPrimitives.ReadInt16LittleEndian(rows.Read(record + 18, 2)) } };
        frame = services.WriteModel(frame, actor, model);
        pool = Pool(frame);
        (pool, int child) = pool.Append(0x646F70);
        NativeBattleModulePoolTask burst = pool.Slots[child].WriteWord(0xC, 0).WriteWord(0xE, task.ReadWord(0x1A)).WriteDword(0x10, BinaryPrimitives.ReadUInt32LittleEndian(rows.Read(record + 12, 4))).WriteDword(0x14, BinaryPrimitives.ReadUInt32LittleEndian(rows.Read(record + 16, 4))).WriteWord(0x1C, unchecked((ushort)(unchecked(transform.Scale38 * 2000) >> 12)));
        frame = PutPool(frame, pool with { Slots = pool.Slots.SetItem(child, burst) });
        return new(frame, 2);

        short Word(int offset) => unchecked((short)task.ReadWord(offset));
    }

    private static NativeBattleVector ReadVector(NativeBattleEffectArena rows, int at) => new(BinaryPrimitives.ReadInt16LittleEndian(rows.Read(at, 2)), BinaryPrimitives.ReadInt16LittleEndian(rows.Read(at + 2, 2)), BinaryPrimitives.ReadInt16LittleEndian(rows.Read(at + 4, 2)));
    private static NativeBattleEffectArena WriteVector(NativeBattleEffectArena rows, int at, NativeBattleVector vector) => rows.Write(at, BitConverter.GetBytes(vector.X)).Write(at + 2, BitConverter.GetBytes(vector.Y)).Write(at + 4, BitConverter.GetBytes(vector.Z));
    private static NativeBattleEffectTaskPool Pool(NativeBattleDoomtrain191Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
    private static NativeBattleDoomtrain191Frame PutPool(NativeBattleDoomtrain191Frame frame, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(Pool(frame)), pool) } };
}

