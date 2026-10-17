using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleSequenceSelections
{
    public static NativeBattleGraphicsModel SelectResource505BC0(NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource, int selected)
    {
        if (model.ModelId4 == 0x8F)
        {
            return model;
        }
        if (!resource.ContainsNativeIndex505850(selected))
        {
            return model with { ResourceFlags44 = (ushort)((model.ResourceFlags44 ?? throw new InvalidDataException("Native505BC0 lacks its resource WORD44.")) | 0x40) };
        }
        return model with { Sequence = unchecked((byte)selected), SequenceFrame = 0, ResourceFlags44 = 1, SequenceState = model.SequenceState is null ? null : model.SequenceState with { Offset46 = 0, Pending3 = 0, Callback42 = 0 } };
    }

    public static (byte Actor, ushort TargetMask) GeometryArguments92(int currentIndex, byte selector, NativeBattleActionGeometry? retained)
    {
        if ((uint)currentIndex >= 7)
        {
            throw new InvalidDataException("Native92 requires its current physical model index.");
        }
        NativeBattleActionGeometry context = retained ?? throw new NotSupportedException("Native92 requires its retained50D300 actor/mask globals.");
        return selector < 7 ? (context.ActorIndex, (ushort)(1 << selector)) : ((byte)currentIndex, context.SourceTargetMask);
    }

    public static ImmutableArray<int> SelectModels93(IReadOnlyList<NativeBattleGraphicsModel> models, int currentIndex, byte selector)
    {
        if (models.Count != 7 || (uint)currentIndex >= 7)
        {
            throw new InvalidDataException("Native93 requires its seven graphics models and current identity.");
        }
        if (selector < 16)
        {
            if (selector >= 7)
            {
                throw new NotSupportedException("Native93 selects an adjacent record outside the seven-model bank.");
            }
            return [selector];
        }
        ImmutableArray<int>.Builder selected = ImmutableArray.CreateBuilder<int>();
        for (int index = 0; index < models.Count; index++)
        {
            NativeBattleGraphicsModel model = models[index];
            if ((model.Flags0 & 2) != 0 && index != currentIndex && (model.ModelId4 ?? throw new NotSupportedException("Native93 requires its loaded model BYTE4 identity.")) == selector)
            {
                selected.Add(index);
            }
        }
        return selected.ToImmutable();
    }

    public static bool CanSelectTargetBF(NativeBattleGraphicsModel target)
    {
        if (target.Index < 3 && ((target.Flags8 ?? throw new NotSupportedException("Native BF requires its target model DWORD8.")) & 0x300000) != 0)
        {
            return false;
        }
        return ((target.ResourceFlags44 ?? throw new NotSupportedException("Native505CE0 requires its target resource WORD44.")) & 0x140) != 0;
    }
}
