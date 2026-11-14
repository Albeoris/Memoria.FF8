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

    public static NativeBattleGraphicsModel RetainDecodedPose5022C0(NativeBattleGraphicsModel model, NativeBattleGraphicsResource previous)
    {
        NativeBattleSequenceState state = model.SequenceState ?? throw new NotSupportedException("Native5022C0 requires its actual existing decoded pose owner.");
        NativeBattlePoseSource source = previous.PoseSource ?? throw new NotSupportedException("Native5022C0 requires its actual previous skeleton/animation source.");
        NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(state, source);
        NativeBattleDecodedPose? linked = state.LinkedPose is not null && previous.LinkedPoseSource is { } linkedSource ? NativeBattlePoseMatrices.ReadLinkedDecodedPose(state, linkedSource) : state.LinkedDecodedPose;
        return model with { SequenceState = state with { DecodedPose = decoded, LinkedDecodedPose = linked } };
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

    public static (byte Actor, BattleModelTargets Targets) GeometryArguments92(int currentIndex, byte selector, NativeBattleActionGeometry? retained, BattleModelRoster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);
        if (!roster.ContainsRow(currentIndex))
        {
            throw new InvalidDataException("Hosted Native92 requires its current bound model row.");
        }
        NativeBattleActionGeometry context = retained ?? throw new NotSupportedException("Native92 requires its retained50D300 actor/mask globals.");
        byte actor = selector < NativeBattleSlots.Count ? context.ActorIndex : checked((byte)currentIndex);
        BattleModelTargets targets = selector < NativeBattleSlots.Count
            ? default(BattleModelTargets).Add(selector)
            : context.PresentationSourceTargets ?? BattleModelTargets.FromNative(NativeBattlePhysicalTargets.FromNativeWord(context.SourceTargetMask).Bits);
        NativeBattleActionGeometry.ValidatePresentationRows(roster, actor, targets, default);
        return (actor, targets);
    }

    public static ImmutableArray<int> SelectModels93(IReadOnlyList<NativeBattleGraphicsModel> models, int currentIndex, byte selector, BattleModelRoster? roster = null)
    {
        if (roster is null && (models.Count != 7 || (uint)currentIndex >= 7))
        {
            throw new InvalidDataException("Native93 requires its seven graphics models and current identity.");
        }
        if (roster is not null && (models.Count != roster.ActorCount || !roster.ContainsRow(currentIndex)))
        {
            throw new InvalidDataException("Hosted Native93 requires its complete graphics bank and current bound row.");
        }
        if (selector < 16)
        {
            if (selector >= 7)
            {
                throw new NotSupportedException("Native93 selects an adjacent record outside the seven-model bank.");
            }
            if (roster is not null && !roster.ContainsRow(selector))
            {
                throw new InvalidDataException("Hosted Native93 cannot select an unbound literal row.");
            }
            return [selector];
        }
        ImmutableArray<int>.Builder selected = ImmutableArray.CreateBuilder<int>();
        for (int index = 0; index < models.Count; index++)
        {
            if (roster is not null && !roster.ContainsRow(index))
            {
                continue;
            }
            NativeBattleGraphicsModel model = models[index];
            if ((model.Flags0 & 2) != 0 && index != currentIndex && (model.ModelId4 ?? throw new NotSupportedException("Native93 requires its loaded model BYTE4 identity.")) == selector)
            {
                selected.Add(index);
            }
        }
        return selected.ToImmutable();
    }

    public static bool CanSelectReady505CE0(NativeBattleGraphicsModel target)
    {
        return ((target.ResourceFlags44 ?? throw new NotSupportedException("Native505CE0 requires its target resource WORD44.")) & 0x140) != 0;
    }

    public static bool CanSelectTargetBF(NativeBattleGraphicsModel target, BattleModelRoster? roster = null)
    {
        if (roster is not null && !roster.ContainsRow(target.Index))
        {
            throw new InvalidDataException("Hosted Native BF requires its bound target row.");
        }
        bool isParty = roster is null ? target.Index < 3 : roster[target.Index].IsParty;
        if (isParty && ((target.Flags8 ?? throw new NotSupportedException("Native BF requires its target model DWORD8.")) & 0x300000) != 0)
        {
            return false;
        }
        return CanSelectReady505CE0(target);
    }
}
