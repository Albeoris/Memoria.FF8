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

    public static (BattleModelOwner Source, BattleModelTargets Targets, bool SourceHasNonActorSelectors) GeometryArguments92(BattleActorModels models, BattleModelOwner current, byte selector, NativeBattleActionGeometry? retained, NativeBattleModelBoundary boundary)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        if (!models.Contains(current))
        {
            throw new InvalidDataException("Native92 requires its current bound model owner.");
        }
        NativeBattleActionGeometry context = retained ?? throw new NotSupportedException("Native92 requires its retained50D300 geometry context.");
        bool literal = selector < NativeBattleModelBoundary.CellCount;
        BattleModelOwner source = literal ? context.Source : current;
        BattleModelTargets targets = literal ? BattleModelTargets.From([boundary.DecodeLiteral(selector, models)]) : context.SourceTargets;
        if (source is null || !models.Contains(source) || targets is null || targets.Enumerate().Any(owner => !models.Contains(owner)))
        {
            throw new InvalidDataException("Native92 requires bound source and target model owners.");
        }
        return (source, targets, !literal && context.SourceHasNonActorSelectors);
    }

    public static BattleModelTargets SelectModels93(BattleActorModels models, BattleModelOwner current, byte selector, NativeBattleModelBoundary boundary)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        if (!models.Contains(current))
        {
            throw new InvalidDataException("Native93 requires its current bound model owner.");
        }
        if (selector < 16)
        {
            if (selector >= NativeBattleModelBoundary.CellCount)
            {
                throw new NotSupportedException("Native93 selects an adjacent record outside the seven-model bank.");
            }
            return BattleModelTargets.From([boundary.DecodeLiteral(selector, models)]);
        }
        List<BattleModelOwner> selected = [];
        foreach (BattleModelEntry<BattleModelOwner> entry in models.Entries)
        {
            NativeBattleGraphicsModel model = entry.State;
            if ((model.Flags0 & 2) != 0 && entry.Owner != current && (model.ModelId4 ?? throw new NotSupportedException("Native93 requires its loaded model BYTE4 identity.")) == selector)
            {
                selected.Add(entry.Owner);
            }
        }
        return BattleModelTargets.From(selected);
    }

    public static bool CanSelectReady505CE0(NativeBattleGraphicsModel target)
    {
        return ((target.ResourceFlags44 ?? throw new NotSupportedException("Native505CE0 requires its target resource WORD44.")) & 0x140) != 0;
    }

    public static bool CanSelectTargetBF(BattleActorModels models, BattleModelOwner target)
    {
        NativeBattleGraphicsModel model = models.Get(target);
        if (target.Side == BattleSide.Allies && ((model.Flags8 ?? throw new NotSupportedException("Native BF requires its target model DWORD8.")) & 0x300000) != 0)
        {
            return false;
        }
        return CanSelectReady505CE0(model);
    }
}
