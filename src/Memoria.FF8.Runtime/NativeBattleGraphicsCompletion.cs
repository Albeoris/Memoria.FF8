namespace Memoria.FF8.Runtime;

/// <summary>Raw graphics model fields consumed by508540, separate from battle actor records.</summary>
public sealed record NativeBattleGraphicsModelCompletion(ushort Flags0, byte Kind5, uint Flags8, ushort? ResourceFlags44);

public static class NativeBattleGraphicsCompletion
{
    public static int PollTask112(byte effectTaskCount, IReadOnlyList<NativeBattleGraphicsModelCompletion>? models, byte cameraTaskMask, ref byte completionByte, NativeBattleGraphicsRoster? roster = null)
    {
        return PollTask112Core(effectTaskCount, models, static model => model, () => cameraTaskMask, ref completionByte, roster);
    }

    internal static int PollHostedTask112(byte effectTaskCount, IReadOnlyList<NativeBattleGraphicsModel>? models, Func<byte> readCameraTaskMask, ref byte completionByte, NativeBattleGraphicsRoster roster)
    {
        return PollTask112Core(effectTaskCount, models, static model => model.CompletionInputs(), readCameraTaskMask, ref completionByte, roster);
    }

    private static int PollTask112Core<TModel>(byte effectTaskCount, IReadOnlyList<TModel>? models, Func<TModel, NativeBattleGraphicsModelCompletion> readModel, Func<byte> readCameraTaskMask, ref byte completionByte, NativeBattleGraphicsRoster? roster)
    {
        //5085F0 preserves short-circuit order and returns the508420 unlink bit only after all three owners clear.
        if (effectTaskCount != 0)
        {
            return 0;
        }
        roster?.Validate();
        if (models is null || models.Count != (roster?.ActorCount ?? NativeBattleGraphicsModelAddress.NativeRowCount))
        {
            throw new NotSupportedException("Native graphics task112 requires its owned graphics model records.");
        }
        for (int index = 0; index < models.Count; index++)
        {
            if (roster is not null && roster.Rows[index] is null)
            {
                continue;
            }
            NativeBattleGraphicsModelCompletion model = readModel(models[index]);
            if ((model.Flags0 & 2) == 0 || (model.Flags0 & 8) != 0 || (model.Flags8 & 0x18) != 0)
            {
                continue;
            }
            if (model.Kind5 != 0)
            {
                return 0;
            }
            ushort resourceFlags = model.ResourceFlags44 ?? throw new NotSupportedException("Native graphics task112 requires the reached model resource WORD44.");
            if ((resourceFlags & 0x40) == 0)
            {
                return 0;
            }
        }
        if (readCameraTaskMask() != 0)
        {
            return 0;
        }
        completionByte = 255;
        return 2;
    }
}
