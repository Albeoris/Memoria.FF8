using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleDoomtrain191KnockbackRecord(ushort Flags, NativeBattleVector Rotation, ushort? RotationPadding, NativeBattleVector Position, short Vertical, NativeBattleVector Offset, NativeBattleVector Velocity);

public sealed record NativeBattleDoomtrain191KnockbackServices(Func<uint> ReadFlags, Func<BattleModelOwner, NativeBattleGraphicsModel> ReadModel, Func<BattleModelOwner, NativeBattleVector> ReadAnchor, Func<NativeBattleDoomtrain191Frame, BattleModelOwner, NativeBattleGraphicsModel, NativeBattleDoomtrain191Frame> WriteModel);

public static class NativeBattleDoomtrain191Knockback
{
    public static NativeBattleDoomtrain191Frame Spawn646AA0(NativeBattleDoomtrain191Frame frame, ImmutableArray<BattleModelOwner> actors, NativeBattleVector center, NativeBattleModelAliases aliases, NativeBattleDoomtrain191KnockbackServices services)
    {
        var state = frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain knockback requires its retained module state.");
        ImmutableDictionary<BattleModelOwner, NativeBattleDoomtrain191KnockbackRecord> records = state.Knockback;
        for (int index = 0; index < actors.Length; index++)
        {
            BattleModelOwner actor = actors[index];
            NativeBattleGraphicsModel model = services.ReadModel(actor);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Doomtrain knockback requires its current actor transform.");
            NativeBattleVector point = services.ReadAnchor(actor);
            NativeBattleEffectTaskPool pool = Pool(frame);
            (pool, int slot) = pool.Append(0x646D30);
            ushort spin = unchecked((ushort)((Random() % 2 + 1) << 12));
            ushort mask = unchecked((ushort)(1u << ((index + 1) & 31)));
            NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0xC, 0);
            task.WriteExecutionReference(0xE, 2, new BattleExecutionReference.Model(actor), aliases.EncodeOperand(actor, 2));
            task = task.WriteWord(0x18, spin).WriteWord(0x1A, mask).WriteWord(0x10, unchecked((ushort)point.X)).WriteWord(0x12, unchecked((ushort)point.Y)).WriteWord(0x14, unchecked((ushort)point.Z));
            frame = PutPool(frame, pool.WriteSlot(slot, task));
            NativeBattleVector rotation = new(
                model.Rotation12 ?? throw new NotSupportedException("Native Doomtrain knockback requires its current actor pitch."),
                model.Rotation14 ?? throw new NotSupportedException("Native Doomtrain knockback requires its current actor yaw."),
                model.Rotation16 ?? throw new NotSupportedException("Native Doomtrain knockback requires its current actor roll."));
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(point.X - center.X, point.Y - center.Y, Math.Abs(point.Z - center.Z));
            short x = Target(direction.X, center.X, point.X);
            short y = Target(direction.Y, center.Y, point.Y);
            short z = Target(direction.Z, center.Z, point.Z);
            records = records.SetItem(actor, new((ushort)(model.Flags0 & 0x1020), rotation, model.RotationPadding18, transform.Position28, transform.Vertical34, new(0, 0, 0), new(x, y, z)));
        }
        return frame with { Memory = frame.Memory with { Doomtrain191 = state with { Knockback = records } } };

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
        BattleModelOwner actor = task.ReadExecutionReference<BattleExecutionReference.Model>(0xE, 2).Owner;
        var state = frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain knockback requires its module state.");
        NativeBattleDoomtrain191KnockbackRecord record = state.Knockback.TryGetValue(actor, out NativeBattleDoomtrain191KnockbackRecord? saved) ? saved : throw new NotSupportedException("Native Doomtrain knockback requires its actor's initialized record.");
        NativeBattleGraphicsModel model = services.ReadModel(actor);
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Doomtrain knockback requires its current actor transform.");
        int age = Word(0xC);
        short pitch = unchecked((short)(unchecked(Word(0x18) * age) / 16));
        short yaw = unchecked((short)(unchecked(age << 12) / 16));
        short roll = model.Rotation16 ?? throw new NotSupportedException("Native Doomtrain knockback requires its current actor roll.");
        NativeBattleVector anchor = new(Word(0x10), Word(0x12), Word(0x14));
        NativeBattleVector original = record.Position;
        NativeBattleVector difference = new(unchecked((short)(anchor.X - original.X)), unchecked((short)(anchor.Y - original.Y)), unchecked((short)(anchor.Z - original.Z)));
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleGfMatrices.Euler56CE30(new(pitch, yaw, roll)), difference);
        NativeBattleVector position = new(unchecked((short)(anchor.X - rotated.X)), unchecked((short)(anchor.Y - rotated.Y)), unchecked((short)(anchor.Z - rotated.Z)));
        if (age < 16)
        {
            NativeBattleVector offset = record.Offset;
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
            return new(PutPool(frame, pool.WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, pool.Slots[slot].Next, task.Callback))), 0);
        }
        if (age < 16)
        {
            NativeBattleVector offset = record.Offset;
            NativeBattleVector velocity = record.Velocity;
            record = record with { Offset = new(unchecked((short)(offset.X + velocity.X)), unchecked((short)(offset.Y + velocity.Y)), unchecked((short)(offset.Z + velocity.Z))) };
        }
        else if (age >= 42)
        {
            task = task.WriteWord(0x20, unchecked((ushort)(Word(0x20) + Word(0x22))));
        }
        ushort next = unchecked((ushort)(age + 1));
        pool = Pool(frame);
        frame = PutPool(frame, pool.WriteSlot(slot, task.WriteWord(0xC, next).SetNext(pool.Slots[slot].Next)));
        frame = frame with { Memory = frame.Memory with { Doomtrain191 = frame.Memory.Doomtrain191! with { Knockback = state.Knockback.SetItem(actor, record) } } };
        if (unchecked((short)next) < 50)
        {
            return new(frame, 0);
        }
        model = services.ReadModel(actor);
        transform = model.Transform ?? throw new NotSupportedException("Native Doomtrain knockback restore requires its current actor transform.");
        model = model with { Flags0 = (ushort)((model.Flags0 & ~0x1020) | record.Flags), Rotation12 = record.Rotation.X, Rotation14 = record.Rotation.Y, Rotation16 = record.Rotation.Z, RotationPadding18 = record.RotationPadding, Transform = transform with { Position28 = original, Vertical34 = record.Vertical } };
        frame = services.WriteModel(frame, actor, model);
        pool = Pool(frame);
        (pool, int child) = pool.Append(0x646F70);
        NativeBattleModulePoolTask burst = pool.Slots[child].WriteWord(0xC, 0).WriteWord(0xE, task.ReadWord(0x1A)).WriteDword(0x10, PackWords(original.X, original.Y)).WriteDword(0x14, PackWords(original.Z, record.Vertical)).WriteWord(0x1C, unchecked((ushort)(unchecked(transform.Scale38 * 2000) >> 12)));
        frame = PutPool(frame, pool.WriteSlot(child, burst));
        return new(frame, 2);

        short Word(int offset) => unchecked((short)task.ReadWord(offset));
    }

    private static uint PackWords(short low, short high) => unchecked((ushort)low) | ((uint)unchecked((ushort)high) << 16);
    private static NativeBattleEffectTaskPool Pool(NativeBattleDoomtrain191Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
    private static NativeBattleDoomtrain191Frame PutPool(NativeBattleDoomtrain191Frame frame, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(Pool(frame)), pool) } };
}

