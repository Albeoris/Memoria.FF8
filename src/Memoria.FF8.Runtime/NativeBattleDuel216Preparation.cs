using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel216PreparationBounds(NativeBattleDuel216Frame Frame, short Maximum, short Minimum);
public sealed record NativeBattleDuel216PreparationServices(Func<int, NativeBattleGraphicsModel> ReadModel, Action<int, NativeBattleGraphicsModel> WriteModel, Func<int, NativeBattleModuleAnchor> ReadAnchor571400, Func<NativeBattleDuel216Frame, NativeBattleModuleAnchor, NativeBattleDuel216Extents, NativeBattleDuel216Frame> PrepareCamera69A790, Func<NativeBattleDuel216Frame, int, uint, NativeBattleDuel216Frame> BindActor5022C0, Func<NativeBattleDuel216Frame, int, byte, NativeBattleDuel216Frame> SelectSequence505C00, Func<NativeBattleDuel216Frame, int, NativeBattleDuel216PreparationBounds> Bounds695F60, Func<NativeBattleDuel216Frame, NativeBattleDuel216Frame> RestoreFocal1D8E038);

public static class NativeBattleDuel216Preparation
{
    public static NativeBattleDuel216Frame Prepare695AB0(NativeBattleDuel216Frame frame, int slot, NativeBattleDuel216PreparationServices services)
    {
        NativeBattleDuel216State state = frame.Memory.Duel216 ?? throw new NotSupportedException("Native Duel216 preparation requires its retained entered owner.");
        NativeBattleDuel216Resources resources = state.Resources ?? throw new NotSupportedException("Native Duel216 preparation requires its original resources.");
        int actor = ModelIndex(frame.Staging, 0x2508880);
        int target = ModelIndex(frame.Staging, 0x2508884);
        if (state.Parameters?.Actor78 != actor)
        {
            throw new InvalidDataException("Native Duel216 actor pointer differs from its entered actor.");
        }
        NativeBattleEffectTaskPool pool = Main(frame);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x6960E0)
        {
            throw new InvalidDataException("Native Duel216 preparation requires its actual main callback.");
        }
        NativeBattleGraphicsModel model = services.ReadModel(actor);
        NativeBattleGraphicsModel targetModel = services.ReadModel(target);
        task = SavePosition(task, 64, model);
        task = SaveRotation(task, 72, model);
        NativeBattleModuleAnchor anchor = services.ReadAnchor571400(target);
        task = task.WriteWord(48, unchecked((ushort)anchor.X)).WriteWord(50, unchecked((ushort)anchor.Y)).WriteWord(52, unchecked((ushort)anchor.Z)).WriteWord(54, unchecked((ushort)anchor.W));
        task = task.WriteBytes(56, task.ReadBytes(48, 8)).WriteWord(50, 0);
        short actorScale = (model.Transform ?? throw new NotSupportedException("Native Duel216 requires its actual actor scale.")).Scale38;
        short targetScale = (targetModel.Transform ?? throw new NotSupportedException("Native Duel216 requires its actual target scale.")).Scale38;
        short scale = unchecked((short)((targetScale >> 1) + 1024));
        task = task.WriteWord(80, unchecked((ushort)scale));
        uint geometry = checked(0x114A6DCu + resources.Dword(0x114A6E8));
        NativeBattleDuel216Extents bounds = NativeBattleDuel216Camera.Scale695AB0(NativeBattleDuel216Camera.Extents69A700(resources.Read(geometry, checked((int)(NativeBattleDuel216Resources.EndAddress - geometry)))), scale);
        frame = Publish(frame, pool, slot, task);
        frame = services.PrepareCamera69A790(frame, new(unchecked((short)task.ReadWord(48)), 0, unchecked((short)task.ReadWord(52)), unchecked((short)task.ReadWord(54))), bounds);
        pool = Main(frame);
        task = pool.Slots[slot].WriteWord(52, unchecked((ushort)(unchecked((short)pool.Slots[slot].ReadWord(52)) + (actorScale >> 1) + (targetScale >> 1))));
        frame = frame with { Staging = frame.Staging.Write(0x25088F8, task.ReadBytes(64, 8)).Write(0x2508900, task.ReadBytes(48, 8)) };
        frame = Publish(frame, pool, slot, task);
        frame = services.BindActor5022C0(frame, actor, checked(pool.BankAddress + (uint)slot * (uint)pool.Stride + 16));
        frame = services.SelectSequence505C00(frame, actor, 1);
        frame = frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, 11408) };
        model = services.ReadModel(actor);
        services.WriteModel(actor, model with { Flags0 = (ushort)(model.Flags0 & ~4) });
        NativeBattleDuel216PreparationBounds projected = services.Bounds695F60(frame, target);
        frame = projected.Frame;
        pool = Main(frame);
        task = pool.Slots[slot].WriteWord(6108, unchecked((ushort)projected.Maximum)).WriteWord(6110, unchecked((ushort)projected.Minimum));
        task = SavePosition(task, 6120, services.ReadModel(target));
        frame = frame with { Staging = frame.Staging.Write(0x2508908, BitConverter.GetBytes(projected.Minimum - projected.Maximum + 1249)) };
        targetModel = services.ReadModel(target);
        NativeBattleModelTransform transform = targetModel.Transform ?? throw new NotSupportedException("Native Duel216 requires the target's actual raw position.");
        services.WriteModel(target, targetModel with { Transform = transform with { Position28 = transform.Position28 with { Y = unchecked((short)(transform.Position28.Y - projected.Maximum)) } } });
        Ring(services, target, true);
        task = NativeBattleDuel216Echo.Initialize695AB0(task);
        return Publish(frame, pool, slot, task);
    }

    public static NativeBattleDuel216Frame Restore695AB0(NativeBattleDuel216Frame frame, NativeBattleDuel216PreparationServices services)
    {
        NativeBattleModulePoolTask task = Main(frame).Slots[0];
        int actor = ModelIndex(frame.Staging, 0x2508880);
        int target = ModelIndex(frame.Staging, 0x2508884);
        RestorePosition(target, 6120);
        Ring(services, target, false);
        RestorePosition(actor, 64);
        NativeBattleGraphicsModel model = services.ReadModel(actor);
        bool unknownPadding = task.UnknownPayloadRanges?.Any(range => range.Offset < 80 && range.Offset + range.Length > 78) == true;
        services.WriteModel(actor, model with { Rotation12 = Signed(72), Rotation14 = Signed(74), Rotation16 = Signed(76), RotationPadding18 = unknownPadding ? null : task.ReadWord(78) });
        return services.RestoreFocal1D8E038(frame);

        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
        void RestorePosition(int index, int offset)
        {
            NativeBattleGraphicsModel current = services.ReadModel(index);
            NativeBattleModelTransform transform = current.Transform ?? throw new NotSupportedException("Native Duel216 restoration requires the actual raw model transform.");
            services.WriteModel(index, current with { Transform = transform with { Position28 = new(Signed(offset), Signed(offset + 2), Signed(offset + 4)), Vertical34 = Signed(offset + 6) } });
        }
    }

    private static void Ring(NativeBattleDuel216PreparationServices services, int source, bool hidden)
    {
        HashSet<int> visited = [];
        int? current = source;
        while (current is int index)
        {
            if (!visited.Add(index))
            {
                if (index != source)
                {
                    throw new InvalidDataException("Native Duel216 target ring does not return to its actual source.");
                }
                break;
            }
            NativeBattleGraphicsModel model = services.ReadModel(index);
            if ((model.Flags0 & 2) != 0)
            {
                services.WriteModel(index, model with { Flags0 = hidden ? (ushort)(model.Flags0 | 8) : (ushort)(model.Flags0 & ~8) });
            }
            current = model.ReadLinkedModelIndex140();
        }
    }
    private static NativeBattleModulePoolTask SavePosition(NativeBattleModulePoolTask task, int offset, NativeBattleGraphicsModel model)
    {
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Duel216 requires actual raw model position WORDs.");
        return task.WriteWord(offset, unchecked((ushort)transform.Position28.X)).WriteWord(offset + 2, unchecked((ushort)transform.Position28.Y)).WriteWord(offset + 4, unchecked((ushort)transform.Position28.Z)).WriteWord(offset + 6, unchecked((ushort)transform.Vertical34));
    }
    private static NativeBattleModulePoolTask SaveRotation(NativeBattleModulePoolTask task, int offset, NativeBattleGraphicsModel model)
    {
        task = task.WriteWord(offset, unchecked((ushort)(model.Rotation12 ?? throw new NotSupportedException("Native Duel216 requires actual rotationX.")))).WriteWord(offset + 2, unchecked((ushort)(model.Rotation14 ?? throw new NotSupportedException("Native Duel216 requires actual rotationY.")))).WriteWord(offset + 4, unchecked((ushort)(model.Rotation16 ?? throw new NotSupportedException("Native Duel216 requires actual rotationZ."))));
        return model.RotationPadding18 is ushort padding ? task.WriteWord(offset + 6, padding) : task.MarkPayloadUnknown(offset + 6, 2);
    }
    private static NativeBattleEffectTaskPool Main(NativeBattleDuel216Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDuel216Controller.Main && pool.BankAddress == NativeBattleDuel216Controller.Arena && pool.Stride == 6128 && pool.Slots.Length == 1);
    private static NativeBattleDuel216Frame Publish(NativeBattleDuel216Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(candidate => candidate.DescriptorAddress == pool.DescriptorAddress ? pool with { Slots = pool.Slots.SetItem(slot, task) } : candidate).ToImmutableArray() } };
    private static int ModelIndex(NativeBattleFileStaging staging, uint global)
    {
        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(global, 4));
        if (pointer < 0x1D972C0 || (pointer - 0x1D972C0) % 156 != 0 || (pointer - 0x1D972C0) / 156 >= 7)
        {
            throw new InvalidDataException("Native Duel216 requires its actual retained actor or minimum-target pointer.");
        }
        return checked((int)((pointer - 0x1D972C0) / 156));
    }
}
