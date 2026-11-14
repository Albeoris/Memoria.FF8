namespace Memoria.FF8.Runtime;

/// <summary>Raw graphics model fields consumed by508540, separate from battle actor records.</summary>
public sealed record NativeBattleGraphicsModelCompletion(ushort Flags0, byte Kind5, uint Flags8, ushort? ResourceFlags44);

public static class NativeBattleGraphicsCompletion
{
    public static int PollTask112(int effectTaskCount, BattleActorModels? models, byte cameraTaskMask, ref byte completionByte)
    {
        return PollTask112(effectTaskCount, models, () => cameraTaskMask, ref completionByte);
    }

    internal static int PollTask112(int effectTaskCount, BattleActorModels? models, Func<byte> readCameraTaskMask, ref byte completionByte)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(effectTaskCount);
        // 5085F0 returns the 508420 unlink bit only after effects, models, and camera clear.
        if (effectTaskCount != 0)
        {
            return 0;
        }
        if (models is null)
        {
            throw new NotSupportedException("Native graphics task112 requires its owned graphics model records.");
        }
        foreach (BattleModelEntry<BattleModelOwner> entry in models.Entries)
        {
            NativeBattleGraphicsModelCompletion model = entry.State.CompletionInputs();
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
