using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel215PreparationServices(Func<int, NativeBattleGraphicsModel> ReadModel, Action<int, NativeBattleGraphicsModel> WriteModel, Func<int, NativeBattleModuleAnchor> ReadAnchor571400, ImmutableArray<byte> Targets, Func<NativeBattleDuel215Frame, int, uint, NativeBattleDuel215Frame> BindActor5022C0, Func<NativeBattleDuel215Frame, int, byte, NativeBattleDuel215Frame> SelectSequence505C00);

public static class NativeBattleDuel215Preparation
{
    public static NativeBattleDuel215Frame RestoreModels69AC00(NativeBattleDuel215Frame frame, NativeBattleDuel215PreparationServices services)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(bank => bank.DescriptorAddress == NativeBattleDuel215Controller.Scene);
        NativeBattleModulePoolTask task = pool.Slots[0];
        for (int index = 0; index < services.Targets.Length; index++)
        {
            int offset = 64 + index * 32;
            int target = services.Targets[index];
            if (task.ReadDword(offset) != 0x1D972C0u + (uint)target * 156)
            {
                throw new InvalidDataException("Native Duel215 restoration lost its actual target model pointer.");
            }
            Restore(target, offset + 4, offset + 12, false);
        }
        int actor = frame.Memory.Duel215?.Parameters?.Actor78 ?? throw new NotSupportedException("Native Duel215 restoration requires its entered actor.");
        frame = services.SelectSequence505C00(frame, actor, 2);
        pool = frame.Effect.Pools.Single(bank => bank.DescriptorAddress == NativeBattleDuel215Controller.Scene);
        task = pool.Slots[0];
        Restore(actor, 48, 56, true);
        return frame;

        void Restore(int index, int position, int rotation, bool clearFlag8)
        {
            NativeBattleGraphicsModel model = services.ReadModel(index);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Duel215 restoration requires its actual raw model transform.");
            bool paddingUnknown = task.UnknownPayloadRanges?.Any(range => range.Offset < rotation + 8 && range.Offset + range.Length > rotation + 6) == true;
            model = model with
            {
                Transform = transform with { Position28 = new(Signed(position), Signed(position + 2), Signed(position + 4)), Vertical34 = Signed(position + 6) },
                Rotation12 = Signed(rotation),
                Rotation14 = Signed(rotation + 2),
                Rotation16 = Signed(rotation + 4),
                RotationPadding18 = paddingUnknown ? null : task.ReadWord(rotation + 6),
                Flags0 = clearFlag8 ? (ushort)(model.Flags0 & ~8) : model.Flags0
            };
            services.WriteModel(index, model);
        }
        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
    }

    public static NativeBattleDuel215Frame Initialize69AC00(NativeBattleDuel215Frame frame, int slot, NativeBattleDuel215PreparationServices services)
    {
        NativeBattleEffectParameters parameters = frame.Memory.Duel215?.Parameters ?? throw new NotSupportedException("Native Duel215 preparation requires its entered parameters.");
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(bank => bank.DescriptorAddress == NativeBattleDuel215Controller.Scene);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x69B070 || services.Targets.IsDefault || services.Targets.Length > 10 || services.Targets.Any(index => index >= 7))
        {
            throw new InvalidDataException("Native Duel215 preparation requires its actual scene and target rows.");
        }
        int minX = short.MaxValue;
        int maxX = -32767;
        int maxZ = -32767;
        NativeBattleDuel215TargetBounds[] bounds = new NativeBattleDuel215TargetBounds[services.Targets.Length];
        for (int index = 0; index < services.Targets.Length; index++)
        {
            int target = services.Targets[index];
            NativeBattleGraphicsModel model = services.ReadModel(target);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Duel215 preparation requires the target's raw position WORDs.");
            NativeBattleEffectLocalBounds localBounds = model.GeometryBounds52 ?? throw new NotSupportedException("Native Duel215 placement requires the target's actual local bounds.");
            int offset = 64 + index * 32;
            task = task.WriteDword(offset, checked(0x1D972C0u + (uint)target * 156));
            task = SavePosition(task, offset + 4, transform);
            task = SaveRotation(task, offset + 12, model);
            task = task.WriteBytes(offset + 20, frame.Staging.Read(0x2508990, 8));
            NativeBattleModuleAnchor anchor = services.ReadAnchor571400(target);
            minX = Math.Min(minX, anchor.X);
            maxX = Math.Max(maxX, anchor.X);
            maxZ = Math.Max(maxZ, anchor.Z);
            bounds[index] = new(transform.Position28.X, localBounds.Minimum.X, localBounds.Maximum.X, localBounds.Maximum.Y);
        }
        int actor = parameters.Actor78;
        NativeBattleGraphicsModel caster = services.ReadModel(actor);
        NativeBattleModelTransform casterTransform = caster.Transform ?? throw new NotSupportedException("Native Duel215 preparation requires the caster's raw position WORDs.");
        short center = unchecked((short)((minX + maxX) / 2));
        task = task.WriteDword(384, unchecked((ushort)center)).WriteWord(388, unchecked((ushort)maxZ));
        center = unchecked((short)NativeBattleDuel215Controller.PlaceActor69D500(center, casterTransform.Scale38, caster.MinimumY146 ?? throw new NotSupportedException("Native Duel215 placement requires the caster's actual minimum height."), bounds));
        task = task.WriteWord(384, unchecked((ushort)center));
        task = SavePosition(task, 48, casterTransform);
        task = SaveRotation(task, 56, caster);
        casterTransform = casterTransform with { Position28 = casterTransform.Position28 with { X = center, Z = unchecked((short)(maxZ + 4096)) } };
        services.WriteModel(actor, caster with { Transform = casterTransform, Rotation14 = 0 });
        frame = frame with { Staging = frame.Staging.Write(0x2508998, BitConverter.GetBytes(Pack(casterTransform.Position28.X, casterTransform.Position28.Y))).Write(0x250899C, BitConverter.GetBytes(Pack(casterTransform.Position28.Z, casterTransform.Vertical34))).Write(0x25089A0, task.ReadBytes(384, 4)).Write(0x25089A4, task.ReadBytes(388, 4)) };
        task = NativeBattleDuel215Controller.ClearParticles69B040(task, checked(pool.BankAddress + (uint)(slot * pool.Stride)));
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(bank => bank.DescriptorAddress == pool.DescriptorAddress ? pool : bank).ToImmutableArray() } };
        frame = services.BindActor5022C0(frame, actor, checked(pool.BankAddress + (uint)(slot * pool.Stride) + 16));
        frame = services.SelectSequence505C00(frame, actor, 1);
        frame = frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, 28880) };
        for (int index = 0; index < 3; index++)
        {
            NativeBattleGraphicsModel model = services.ReadModel(index);
            services.WriteModel(index, model with { Flags0 = (ushort)(model.Flags0 | 4) });
        }
        caster = services.ReadModel(actor);
        services.WriteModel(actor, caster with { Flags0 = (ushort)(caster.Flags0 & ~4) });
        return frame;
    }

    private static NativeBattleModulePoolTask SavePosition(NativeBattleModulePoolTask task, int offset, NativeBattleModelTransform transform) => task.WriteDword(offset, Pack(transform.Position28.X, transform.Position28.Y)).WriteDword(offset + 4, Pack(transform.Position28.Z, transform.Vertical34));

    private static NativeBattleModulePoolTask SaveRotation(NativeBattleModulePoolTask task, int offset, NativeBattleGraphicsModel model)
    {
        short x = model.Rotation12 ?? throw new NotSupportedException("Native Duel215 requires the actual model rotationX.");
        short y = model.Rotation14 ?? throw new NotSupportedException("Native Duel215 requires the actual model rotationY.");
        short z = model.Rotation16 ?? throw new NotSupportedException("Native Duel215 requires the actual model rotationZ.");
        task = task.WriteDword(offset, Pack(x, y)).WriteWord(offset + 4, unchecked((ushort)z));
        return model.RotationPadding18 is ushort padding ? task.WriteWord(offset + 6, padding) : task.MarkPayloadUnknown(offset + 6, 2);
    }

    private static uint Pack(short low, short high) => unchecked((ushort)low | (uint)(ushort)high << 16);
}
