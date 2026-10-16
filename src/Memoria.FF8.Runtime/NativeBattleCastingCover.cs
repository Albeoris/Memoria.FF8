using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>50AFC0's cover recipient and50A9A0's phase4/6 position WORD stores.</summary>
public static class NativeBattleCastingCover
{
    public static byte Recipient(ushort targetMask)
    {
        byte? recipient = null;
        for (byte index = 0; index < 3; index++)
        {
            if ((targetMask & 1 << index) != 0)
            {
                recipient = index;
            }
        }
        return recipient ?? throw new NotSupportedException("Native50AFC0 cover requires the retained1D99AAA recipient when no player target bit is present.");
    }

    public static ImmutableArray<NativeBattleGraphicsModel> Enter(ImmutableArray<NativeBattleGraphicsModel> models, NativeBattleActionGraphicsState action)
    {
        byte source = Source(action);
        byte recipient = Recipient(action.TargetMask);
        NativeBattleModelTransform sourceTransform = Transform(models, source);
        NativeBattleGraphicsModel target = models[recipient];
        NativeBattleModelTransform targetTransform = Transform(models, recipient);
        models = models.SetItem(recipient, target with { Transform = targetTransform with { Position28 = targetTransform.Position28 with { X = sourceTransform.Initial20.X, Z = sourceTransform.Initial20.Z } } });
        NativeBattleGraphicsModel cover = models[source];
        NativeBattleModelTransform coverTransform = Transform(models, source);
        return models.SetItem(source, cover with { Transform = coverTransform with { Position28 = coverTransform.Position28 with { Z = unchecked((short)(sourceTransform.Initial20.Z + 800)) } } });
    }

    public static ImmutableArray<NativeBattleGraphicsModel> Complete(ImmutableArray<NativeBattleGraphicsModel> models, NativeBattleActionGraphicsState action)
    {
        byte source = Source(action);
        byte recipient = Recipient(action.TargetMask);
        NativeBattleGraphicsModel target = models[recipient];
        NativeBattleModelTransform targetTransform = Transform(models, recipient);
        ushort flags = target.ResourceFlags44 ?? throw new NotSupportedException("Native50A9A0 cover completion requires the recipient's resource WORD44.");
        models = models.SetItem(recipient, target with { ResourceFlags44 = (ushort)(flags | 0x200), Transform = targetTransform with { Position28 = targetTransform.Position28 with { X = targetTransform.Initial20.X, Z = targetTransform.Initial20.Z } } });
        NativeBattleGraphicsModel cover = models[source];
        NativeBattleModelTransform coverTransform = Transform(models, source);
        return models.SetItem(source, cover with { Transform = coverTransform with { Position28 = coverTransform.Position28 with { Z = coverTransform.Initial20.Z } } });
    }

    private static byte Source(NativeBattleActionGraphicsState action) => action.CoverMask != 0 && action.CoverSource is byte source && source < 7 ? source : throw new InvalidDataException("Native50A9A0 cover requires the actual50BF90 source BYTE and nonempty cover mask.");

    private static NativeBattleModelTransform Transform(ImmutableArray<NativeBattleGraphicsModel> models, byte index) => models[index].Transform ?? throw new NotSupportedException("Native50A9A0 cover requires the original initial/current position WORDs.");
}
