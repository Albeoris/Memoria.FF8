namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDuel112Target
{
    public static int StageIntensity6CBF90(short phase)
    {
        if (phase >= 4 && phase <= 148)
        {
            return 1024;
        }
        return Math.Max(-9999, (phase < 4 ? phase : 152 - phase) << 8);
    }

    public static void Backstep6CBF90(int actor, NativeBattleDuel112ActorServices services)
    {
        NativeBattleGraphicsModel model = services.ReadModel(actor);
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Duel backstep requires its actual actor transform.");
        short yaw = model.Rotation14 ?? throw new NotSupportedException("Native Duel backstep requires its actual actor yaw.");
        transform = transform with { Position28 = transform.Position28 with { X = unchecked((short)(transform.Position28.X - (950 * NativeBattleCameraMath.Sine(yaw) >> 12))), Z = unchecked((short)(transform.Position28.Z - (950 * NativeBattleCameraMath.Cosine(yaw) >> 12))) } };
        services.WriteModel(actor, model with { Transform = transform });
    }

    public static NativeBattleDuel112Frame Trajectory6CBF90(NativeBattleDuel112Frame frame, int slot, NativeBattleDuel112ActorServices services, Func<int, NativeBattleModuleAnchor> readAnchor571400)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        NativeBattleModulePoolTask task = pool.Slots[slot];
        int phase = unchecked((short)task.ReadWord(12)) - 45;
        if ((uint)phase > 4)
        {
            return frame;
        }
        int actor = frame.Memory.Duel112?.Parameters?.Actor78 ?? throw new NotSupportedException("Native Duel trajectory requires its actual actor.");
        NativeBattleGraphicsModel model = services.ReadModel(actor);
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Duel trajectory requires its actual actor transform.");
        short yaw = model.Rotation14 ?? throw new NotSupportedException("Native Duel trajectory requires its actual actor yaw.");
        if (phase == 0)
        {
            uint target = task.ReadDword(176);
            uint relative = unchecked(target - 0x1D972C0u);
            if (relative % 156 != 0 || relative / 156 >= 7)
            {
                throw new InvalidDataException("Native Duel trajectory requires its actual target model pointer.");
            }
            int index = (int)(relative / 156);
            NativeBattleModelTransform targetTransform = services.ReadModel(index).Transform ?? throw new NotSupportedException("Native Duel trajectory requires its actual target scale.");
            NativeBattleModuleAnchor anchor = readAnchor571400(index);
            task = task.WriteWord(68, unchecked((ushort)(anchor.X + unchecked((targetTransform.Scale38 + 5000) * NativeBattleCameraMath.Sine(yaw)) / 12288)));
            task = task.WriteWord(70, unchecked((ushort)(anchor.Y - ((targetTransform.Scale38 + 10000) >> 5))));
            task = task.WriteWord(72, unchecked((ushort)(anchor.Z + unchecked((targetTransform.Scale38 + 5000) * NativeBattleCameraMath.Cosine(yaw)) / 12288)));
            task = task.WriteWord(74, unchecked((ushort)anchor.W));
        }
        int radius = (4 - phase) << 11;
        NativeBattleVector position = new(unchecked((short)(unchecked((short)task.ReadWord(68)) + (unchecked(radius * NativeBattleCameraMath.Sine(yaw)) >> 12))), unchecked((short)(unchecked((short)task.ReadWord(70)) - radius)), unchecked((short)(unchecked((short)task.ReadWord(72)) + (unchecked(radius * NativeBattleCameraMath.Cosine(yaw)) >> 12))));
        services.WriteModel(actor, model with { Transform = transform with { Position28 = position } });
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
    }
}
