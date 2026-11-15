namespace Memoria.FF8.Runtime;

public static class NativeBattleCameraGraphics
{
    /// <summary>50D790 returns a fixed4096 scale for F2, unlike the four bone-pair matricesF5..F8.</summary>
    public static NativeBattleActionMatrix ReadMatrix(NativeBattleActionGeometry context, byte reference)
    {
        if (reference is 0xF2 or >= 0xF5 and <= 0xF8)
        {
            int index = reference == 0xF2 ? 0 : reference - 0xF4;
            NativeBattleActionMatrix matrix = context.Matrices?[index] ?? throw new NotSupportedException("Native camera requires its captured source/recipient matrix bank.");
            return reference == 0xF2 ? matrix with { Scale = 4096 } : matrix;
        }
        if (reference == 0xF9)
        {
            throw new NotSupportedException("Native custom camera references require their process matrix owner rather than an action geometry capture.");
        }
        if (reference >= 0xB0)
        {
            return context.TargetMatrix ?? throw new NotSupportedException("Native camera requires its captured target matrix.");
        }
        throw new ArgumentOutOfRangeException(nameof(reference));
    }

    /// <summary>509640 queries the current action and party graphics rows.</summary>
    public static NativeBattleScriptValue ReadVariable(NativeBattleGraphicsState graphics, byte variable)
    {
        if (variable is 24 or 25)
        {
            NativeBattleActionGeometry context = Action();
            BattleModelOwner owner = variable == 24 ? context.Source : context.Target ?? throw new NotSupportedException("Native camera target variable requires its resolved recipient.");
            return NativeBattleScriptValue.CaptureActor(owner, graphics.ActorModels, graphics.NativeBindings);
        }
        int value = variable switch
        {
            19 => PartyModels().Count(model => (model.Flags0 & 2) != 0 && ((model.Flags8 ?? throw new InvalidDataException("Native camera requires loaded model status flags.")) & 2) == 0),
            21 => graphics.ActorModels.Get(Action().Target ?? throw new NotSupportedException("Native camera BYTE145 query requires its retained physical recipient.")).SequenceByte145 ?? throw new NotSupportedException("Native camera BYTE145 query requires its observed model byte."),
            22 => TargetFlags(graphics),
            26 => (Action().TargetActors ?? throw new NotSupportedException("Native camera target count requires its resolved target set.")).Count,
            27 => PartyModels().Count(model => (model.Flags0 & 2) != 0 && (model.Flags8 ?? throw new InvalidDataException("Native camera requires loaded model status flags.")) == 0),
            _ => throw new NotSupportedException($"Native camera variable{variable} requires its active command descriptor owner.")
        };

        return NativeBattleScriptValue.Scalar(value);

        NativeBattleActionGeometry Action() => graphics.ActionContext ?? throw new NotSupportedException("Native509640 requires its current50D300 action context.");
        IEnumerable<NativeBattleGraphicsModel> PartyModels() => graphics.ActorModels.Party.Entries.Select(entry => entry.State);

    }

    private static int TargetFlags(NativeBattleGraphicsState graphics)
    {
        BattleModelOwner target = graphics.ActionContext?.Target ?? throw new NotSupportedException("Native camera status query requires its resolved physical recipient.");
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(target);
        byte modelId = model.ModelId4 ?? throw new InvalidDataException("Native camera status query requires the loaded model ID BYTE.");
        uint flags = model.Flags8 ?? throw new InvalidDataException("Native camera status query requires the projected status DWORD.");
        return unchecked((int)(modelId >= 16 ? flags & ~1u : (flags & 0x41021) != 0 ? flags | 1u : flags));
    }
}
