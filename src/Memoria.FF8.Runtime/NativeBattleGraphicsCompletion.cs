namespace Memoria.FF8.Runtime;

/// <summary>Raw graphics model fields consumed by508540, separate from battle actor records.</summary>
public sealed record NativeBattleGraphicsModelCompletion(ushort Flags0, byte Kind5, uint Flags8, ushort? ResourceFlags44);

public static class NativeBattleGraphicsCompletion
{
    public static int PollTask112(byte effectTaskCount, IReadOnlyList<NativeBattleGraphicsModelCompletion>? models, byte cameraTaskMask, ref byte completionByte)
    {
        //5085F0 preserves short-circuit order and returns the508420 unlink bit only after all three owners clear.
        if (effectTaskCount != 0)
        {
            return 0;
        }
        if (models is null || models.Count != 7)
        {
            throw new NotSupportedException("Native graphics task112 requires its seven original graphics model records.");
        }
        foreach (NativeBattleGraphicsModelCompletion model in models)
        {
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
        if (cameraTaskMask != 0)
        {
            return 0;
        }
        completionByte = 255;
        return 2;
    }
}
