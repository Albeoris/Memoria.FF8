namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel112ActorServices(Func<int, NativeBattleGraphicsModel> ReadModel, Action<int, NativeBattleGraphicsModel> WriteModel, Func<int, byte> ReadTargetActor, Func<NativeBattleDuel112Frame, int, uint, NativeBattleDuel112Frame> BindActor5022C0, Func<NativeBattleDuel112Frame, int, byte, NativeBattleDuel112Frame> SelectSequence505C00);

public static partial class NativeBattleDuel112Target
{
    public static NativeBattleDuel112Frame Initialize6CBF90(NativeBattleDuel112Frame frame, int slot, int sequence, NativeBattleDuel112ActorServices services)
    {
        NativeBattleEffectParameters parameters = frame.Memory.Duel112?.Parameters ?? throw new NotSupportedException("Native Duel target requires its actual action parameters.");
        int actor = parameters.Actor78;
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (pool.DescriptorAddress != 0x2521950 || task.Callback != 0x6CBF90 || (uint)sequence > byte.MaxValue)
        {
            throw new InvalidDataException("Native Duel target requires its actual physical queue and sequence byte.");
        }
        NativeBattleGraphicsModel model = services.ReadModel(actor);
        services.WriteModel(actor, model with { Flags0 = (ushort)(model.Flags0 | 0x1000) });
        byte target = services.ReadTargetActor(sequence);
        if (target >= 7)
        {
            throw new InvalidDataException("Native Duel target leaves the seven physical graphics rows.");
        }
        task = task.WriteWord(14, (ushort)sequence).WriteDword(176, 0x1D972C0u + 156u * target);
        Store();
        uint row = checked(pool.BankAddress + (uint)(slot * pool.Stride) + 16);
        frame = services.BindActor5022C0(frame, actor, row);
        frame = services.SelectSequence505C00(frame, actor, 1);
        model = services.ReadModel(actor);
        NativeBattleGraphicsModel targetModel = services.ReadModel(target);
        NativeBattleModelTransform actorTransform = model.Transform ?? throw new NotSupportedException("Native Duel target requires the actor's actual transform fields.");
        NativeBattleModelTransform targetTransform = targetModel.Transform ?? throw new NotSupportedException("Native Duel target requires its actual target transform fields.");
        int dx = actorTransform.Position28.X - targetTransform.Position28.X;
        int dz = actorTransform.Position28.Z - targetTransform.Position28.Z;
        short yaw = unchecked((short)NativeWorldScriptedPathMath.NativeAngle(dx, dz));
        pool = frame.Effect.Pools[1];
        task = pool.Slots[slot];
        task = task.WriteDword(172, unchecked((uint)Math.Max(0, targetTransform.Scale38 - 400)));
        task = task.WriteDword(60, Pack(actorTransform.Position28.X, actorTransform.Position28.Y));
        task = task.WriteDword(64, Pack(actorTransform.Position28.Z, actorTransform.Vertical34));
        model = model with { Rotation14 = yaw, Transform = actorTransform with { Position28 = actorTransform.Position28 with { X = 0, Z = 1000 } } };
        services.WriteModel(actor, model);
        task = task.WriteDword(48, model.Color40 ?? throw new NotSupportedException("Native Duel requires the actual actor color DWORD40."));
        task = task.WriteDword(52, model.Color44 ?? throw new NotSupportedException("Native Duel requires the actual actor color DWORD44."));
        task = task.WriteByte(55, model.Alpha7 ?? throw new NotSupportedException("Native Duel requires the actual actor alpha BYTE7."));
        Store();
        return frame;

        void Store()
        {
            pool = pool.WriteSlot(slot, task);
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        }
    }

    private static uint Pack(short first, short second) => unchecked((ushort)first) | unchecked((uint)(ushort)second << 16);
}
