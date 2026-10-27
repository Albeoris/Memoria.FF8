using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel212TargetServices(Func<int, NativeBattleGraphicsModel> ReadModel, Action<int, NativeBattleGraphicsModel> WriteModel, Func<int, (short Maximum, short Minimum)> ReadBounds6DFD90, Func<int, int> ReadBoneCount, Func<int, byte, short, NativeBattleVector> ReadBone502170, Func<NativeBattleDuel212Frame, int, NativeBattleVector, NativeBattleVector, NativeBattleDuel212Frame> DrawBody6A2F60);

public static class NativeBattleDuel212Targets
{
    public static NativeBattleDuel212Frame Draw6A1660(NativeBattleDuel212Frame frame, short tick, int count, NativeBattleDuel212TargetServices services)
    {
        NativeBattleEffectTaskPool pool = Target(frame);
        NativeBattleModulePoolTask task = pool.Slots[0];
        int phase = tick - 134;
        if ((uint)(tick - 109) < 25)
        {
            for (int index = 0; index < count; index++)
            {
                int target = Actor(task.ReadDword(64 + index * 24));
                frame = services.DrawBody6A2F60(frame, target, new(0, 0, 0), new(0, 0, 0));
                NativeBattleGraphicsModel model = services.ReadModel(target);
                services.WriteModel(target, model with { Flags0 = (ushort)(model.Flags0 | 4) });
            }
            return frame;
        }
        if ((uint)phase > 40)
        {
            throw new InvalidDataException("Native Burning Rave target drawing requires its actual target interval.");
        }
        int ratio = (phase << 12) / 40;
        int sine = NativeBattleCameraMath.Sine((phase << 12) / 80);
        int vertical;
        if (ratio >= 2048)
        {
            int squared = ((4096 - sine) * (4096 - sine)) >> 12;
            vertical = ((squared * squared) >> 12) - 4096;
        }
        else
        {
            vertical = -sine;
        }
        int actor = frame.Memory.Duel212?.Parameters?.Actor78 ?? throw new NotSupportedException("Native Burning Rave target drawing requires its entered caster.");
        NativeBattleGraphicsModel caster = services.ReadModel(actor);
        short rx = caster.Rotation12 ?? throw new NotSupportedException("Native Burning Rave target drawing requires its actual caster rotationX.");
        short ry = caster.Rotation14 ?? throw new NotSupportedException("Native Burning Rave target drawing requires its actual caster rotationY.");
        if (phase == 0)
        {
            for (int index = 0; index < count; index++)
            {
                int offset = 64 + index * 24;
                int target = Actor(task.ReadDword(offset));
                (short maximum, short minimum) = services.ReadBounds6DFD90(target);
                task = task.WriteWord(offset + 20, unchecked((ushort)maximum)).WriteWord(offset + 22, unchecked((ushort)minimum));
                NativeBattleGraphicsModel model = services.ReadModel(target);
                services.WriteModel(target, model with { Flags0 = (ushort)(model.Flags0 | 8) });
            }
            Store();
        }
        for (int index = 0; index < count; index++)
        {
            int offset = 64 + index * 24;
            int target = Actor(task.ReadDword(offset));
            short xAngle = unchecked((short)(ratio + rx));
            short yAngle = unchecked((short)(2 * ratio + ry));
            task = task.WriteWord(offset + 12, unchecked((ushort)xAngle)).WriteWord(offset + 14, unchecked((ushort)yAngle));
            NativeBattleGraphicsModel model = services.ReadModel(target);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Burning Rave target drawing requires its retained target position.");
            short y = unchecked((short)(unchecked((short)task.ReadWord(offset + 6)) + vertical));
            short pivotY = unchecked((short)((unchecked((short)task.ReadWord(offset + 20)) + unchecked((short)task.ReadWord(offset + 22))) >> 1));
            services.WriteModel(target, model with { Transform = transform with { Position28 = transform.Position28 with { Y = y } } });
            Store();
            frame = services.DrawBody6A2F60(frame, target, new(xAngle, yAngle, unchecked((short)task.ReadWord(offset + 16))), new(0, pivotY, 0));
            Refresh();
            model = services.ReadModel(target);
            services.WriteModel(target, model with { Flags0 = (ushort)(model.Flags0 | 4) });
        }
        if (phase > 15)
        {
            NativeBattleEffectTaskPool sprites = frame.Effect.Pools.Single(bank => bank.DescriptorAddress == NativeBattleDuel212Controller.Sprites);
            for (int index = 0; index < count; index++)
            {
                if (sprites.Slots.All(task => (task.Flags & 1) != 0))
                {
                    break;
                }
                int target = Actor(task.ReadDword(64 + index * 24));
                (sprites, int slot) = sprites.Append(0x6A3800);
                frame = frame with { Memory = frame.Memory.DrawSharedEffectRandom7059E0(out ushort random) };
                int bones = services.ReadBoneCount(target);
                if (bones is < 1 or > 128)
                {
                    throw new InvalidDataException("Native Burning Rave target sparkle requires its actual skeleton byte count.");
                }
                NativeBattleVector point = services.ReadBone502170(target, checked((byte)((bones * random) >> 15)), 0);
                frame = frame with { Random = frame.Random.Draw(out ushort size) };
                NativeBattleModulePoolTask sprite = sprites.Slots[slot].WriteWord(12, unchecked((ushort)point.X)).WriteWord(14, unchecked((ushort)point.Y)).WriteWord(16, unchecked((ushort)point.Z)).WriteWord(18, unchecked((ushort)(((size << 11) >> 15) + 4096)));
                sprite = sprite.WriteByte(20, 0).WriteByte(21, unchecked((byte)-15)).WriteByte(22, 0).WriteByte(23, 16);
                sprites = sprites.WriteSlot(slot, sprite);
            }
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(bank => bank.DescriptorAddress == sprites.DescriptorAddress ? sprites : bank).ToImmutableArray() } };
        }
        return frame;

        void Store()
        {
            pool = pool.WriteSlot(0, task);
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(bank => bank.DescriptorAddress == pool.DescriptorAddress ? pool : bank).ToImmutableArray() } };
        }
        void Refresh()
        {
            pool = Target(frame);
            task = pool.Slots[0];
        }
    }

    public static NativeBattleDuel212Frame Restore6A1660(NativeBattleDuel212Frame frame, int count, NativeBattleDuel212TargetServices services, bool restorePositions)
    {
        NativeBattleModulePoolTask task = Target(frame).Slots[0];
        for (int index = 0; index < count; index++)
        {
            int offset = 64 + index * 24;
            int target = Actor(task.ReadDword(offset));
            NativeBattleGraphicsModel model = services.ReadModel(target);
            if (restorePositions)
            {
                NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Burning Rave restoration requires the actual target transform.");
                model = model with { Flags0 = (ushort)(model.Flags0 & 0xFFF3), Transform = transform with { Position28 = new(Signed(offset + 4), Signed(offset + 6), Signed(offset + 8)), Vertical34 = Signed(offset + 10) } };
            }
            else
            {
                model = model with { Flags0 = (ushort)(model.Flags0 & ~4) };
            }
            services.WriteModel(target, model);
        }
        RestoreEnemyVisibility6A2F20(frame, services);
        if (!restorePositions)
        {
            return frame;
        }
        int actor = frame.Memory.Duel212?.Parameters?.Actor78 ?? throw new NotSupportedException("Native Burning Rave restoration requires its entered caster.");
        NativeBattleGraphicsModel caster = services.ReadModel(actor);
        NativeBattleModelTransform casterTransform = caster.Transform ?? throw new NotSupportedException("Native Burning Rave restoration requires its actual caster transform.");
        bool paddingUnknown = task.UnknownPayloadRanges?.Any(range => range.Offset < 64 && range.Offset + range.Length > 62) == true;
        services.WriteModel(actor, caster with { Transform = casterTransform with { Position28 = new(Signed(48), Signed(50), Signed(52)), Vertical34 = Signed(54) }, Rotation12 = Signed(56), Rotation14 = Signed(58), Rotation16 = Signed(60), RotationPadding18 = paddingUnknown ? null : task.ReadWord(62) });
        NativeBattleEffectMemory memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, 360, 0x20DFAB8 + 7192, 1);
        return frame with { Memory = NativeBattleEffectIo.Queue5341D0(memory, 361, 0x20DFAB8 + 7192, 0) };

        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
    }

    private static void RestoreEnemyVisibility6A2F20(NativeBattleDuel212Frame frame, NativeBattleDuel212TargetServices services)
    {
        for (int index = 3; index < 7; index++)
        {
            NativeBattleGraphicsModel model = services.ReadModel(index);
            if ((model.Flags0 & 2) != 0)
            {
                byte original = frame.Staging.Read(checked(0x25102A0u + (uint)index * 4), 1)[0];
                services.WriteModel(index, model with { Flags0 = (ushort)(model.Flags0 ^ ((model.Flags0 ^ original) & 4)) });
            }
        }
    }

    private static NativeBattleEffectTaskPool Target(NativeBattleDuel212Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDuel212Controller.Targets);

    private static int Actor(uint address)
    {
        uint offset = unchecked(address - 0x1D972C0u);
        if (offset % 156 != 0 || offset / 156 >= 7)
        {
            throw new InvalidDataException("Native Burning Rave retained target pointer leaves the seven original model rows.");
        }
        return checked((int)(offset / 156));
    }
}
