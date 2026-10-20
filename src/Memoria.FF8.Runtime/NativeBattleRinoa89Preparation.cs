using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa89PreparationServices(Func<int, NativeBattleGraphicsModel> ReadModel, Action<int, NativeBattleGraphicsModel> WriteModel, Func<int, NativeBattleModuleAnchor> ReadAnchor571400, ImmutableArray<byte> Targets, uint FirstContact, NativeBattleEffectOwnedModelResource Body, Func<NativeBattleRinoa89Frame, int, uint, NativeBattleRinoa89Frame> BindActor5022C0, Func<NativeBattleRinoa89Frame, int, byte, NativeBattleRinoa89Frame> SelectSequence505C00, Func<NativeBattleRinoa89Frame, NativeBattleRinoa89Frame> InitializeShards6FA3E0);

public static partial class NativeBattleRinoa89Controller
{
    public static NativeBattleRinoa89Frame SnapshotModels6FA270(NativeBattleRinoa89Frame frame, Func<int, NativeBattleGraphicsModel> readModel)
    {
        NativeBattleFileStaging staging = frame.Staging;
        for (int index = 0; index < 7; index++)
        {
            staging = Dword(staging, checked(0x2556604u + (uint)index * 4), readModel(index).Flags0);
        }
        return frame with { Staging = staging };
    }

    public static NativeBattleRinoa89Frame InitializePools6F9D80(NativeBattleRinoa89Frame frame)
    {
        NativeBattleEffectTaskPool body = NativeBattleEffectTaskPool.Initialize(Body, ArenaAddress + 23104, 1052, 1);
        NativeBattleEffectTaskPool sprites = NativeBattleEffectTaskPool.Initialize(Sprites, ArenaAddress + 3136, 48, 32);
        NativeBattleEffectTaskPool beams = NativeBattleEffectTaskPool.Initialize(Beams, ArenaAddress, 392, 8);
        return frame with { Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [body, sprites, beams]), Effect = frame.Effect with { Pools = frame.Effect.Pools.Where(pool => pool.DescriptorAddress != Body && pool.DescriptorAddress != Sprites && pool.DescriptorAddress != Beams).Concat([body, sprites, beams]).ToImmutableArray() } };
    }

    public static NativeBattleRinoa89Frame PrepareBody6F9D80(NativeBattleRinoa89Frame frame, int slot, NativeBattleRinoa89PreparationServices services)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Wishing Star requires its actual action parameters.");
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(bank => bank.DescriptorAddress == Body);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x6FA780 || services.Targets.IsDefault || services.Targets.Length > 31 || services.Targets.Any(target => target >= 7))
        {
            throw new InvalidDataException("Native6F9D80 requires the original body callback and exact header16>>3 target rows.");
        }
        task = task.WriteBytes(12, new byte[260]);
        short minX = short.MaxValue;
        short minY = short.MaxValue;
        short minZ = short.MaxValue;
        short maxX = -32767;
        short maxY = -32767;
        short maxZ = -32767;
        for (int index = 0; index < services.Targets.Length; index++)
        {
            byte selected = services.Targets[index];
            task = task.WriteDword(20 + index * 8, ModelAddress(selected));
            NativeBattleModuleAnchor point = services.ReadAnchor571400(selected);
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            minZ = Math.Min(minZ, point.Z);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
            maxZ = Math.Max(maxZ, point.Z);
        }
        short centerX = unchecked((short)((minX + maxX) / 2));
        short centerY = unchecked((short)((minY + maxY) / 2));
        task = task.WriteWord(216, unchecked((ushort)centerX)).WriteWord(218, unchecked((ushort)centerY)).WriteWord(220, unchecked((ushort)maxZ));
        int actor = parameters.Actor78;
        NativeBattleGraphicsModel caster = services.ReadModel(actor);
        NativeBattleModelTransform transform = caster.Transform ?? throw new NotSupportedException("Native6F9D80 requires its actual caster transform.");
        task = task.WriteWord(208, unchecked((ushort)transform.Position28.X)).WriteWord(210, unchecked((ushort)transform.Position28.Y)).WriteWord(212, unchecked((ushort)transform.Position28.Z)).WriteWord(214, unchecked((ushort)transform.Vertical34));
        task = task.WriteWord(224, unchecked((ushort)(caster.Rotation12 ?? throw new NotSupportedException("Native6F9D80 requires actor rotationX.")))).WriteWord(226, unchecked((ushort)(caster.Rotation14 ?? throw new NotSupportedException("Native6F9D80 requires actor rotationY.")))).WriteWord(228, unchecked((ushort)(caster.Rotation16 ?? throw new NotSupportedException("Native6F9D80 requires actor rotationZ."))));
        task = caster.RotationPadding18 is ushort padding ? task.WriteWord(230, padding) : task.MarkPayloadUnknown(230, 2);
        services.WriteModel(actor, caster with { Rotation14 = 0 });
        frame = frame with { Staging = Dword(Dword(frame.Staging, 0x2556620, services.FirstContact), 0x25565F8, unchecked((ushort)centerX) | ((uint)unchecked((ushort)centerY) << 16)).Write(0x25565FC, BitConverter.GetBytes(maxZ)) };
        for (int index = 0; index < 3; index++)
        {
            NativeBattleGraphicsModel model = services.ReadModel(index);
            if ((model.Flags0 & 2) != 0)
            {
                services.WriteModel(index, model with { Flags0 = unchecked((ushort)(model.Flags0 | 4)) });
            }
        }
        caster = services.ReadModel(actor);
        services.WriteModel(actor, caster with { Flags0 = unchecked((ushort)(caster.Flags0 & ~4)) });
        Store();
        uint address = checked(pool.BankAddress + (uint)(slot * pool.Stride));
        frame = services.BindActor5022C0(frame, actor, address + 248);
        frame = services.SelectSequence505C00(frame, actor, 1);
        Refresh();
        task = NativeBattleRinoa89Model.Initialize6EC060(task, address, services.Body);
        NativeBattleRinoa89ModelStep pose = NativeBattleRinoa89Model.Select6574D0(frame, task, services.Body, 0);
        frame = pose.Frame;
        task = pose.Task;
        NativeBattleActionMatrix basis = NativeBattlePandemona291Restoration.Rotation701270(0);
        byte[] rotation = new byte[20];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rotation.AsSpan(index * 2), basis.Rotation[index]);
        }
        task = task.WriteBytes(116, rotation).WriteDword(136, unchecked((uint)(int)centerX)).WriteDword(140, 0).WriteDword(144, unchecked((uint)(int)maxZ)).WriteWord(80, unchecked((ushort)centerX)).WriteWord(82, 0).WriteWord(84, unchecked((ushort)maxZ));
        caster = services.ReadModel(actor);
        transform = caster.Transform ?? throw new NotSupportedException("Native6F9D80 lost its caster transform during bind.");
        services.WriteModel(actor, caster with { Transform = transform with { Position28 = new(centerX, 0, maxZ) } });
        Store();
        frame = ClearEmitters6FA2C0(frame);
        frame = ClearSparks6FA2F0(frame);
        frame = InitializeShardRotations6FA320(frame);
        frame = services.InitializeShards6FA3E0(frame);
        frame = frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, 24156) };
        return InitializeStars6F9D80(frame);

        void Store()
        {
            pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(bank => bank.DescriptorAddress == Body ? pool : bank).ToImmutableArray() } };
        }
        void Refresh()
        {
            pool = frame.Effect.Pools.Single(bank => bank.DescriptorAddress == Body);
            task = pool.Slots[slot];
        }
    }
}
