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
            return context.CustomMatrix ?? throw new NotSupportedException("Native camera requires its command-produced custom matrix.");
        }
        if (reference >= 0xB0)
        {
            return context.TargetMatrix ?? throw new NotSupportedException("Native camera requires its captured target matrix.");
        }
        throw new ArgumentOutOfRangeException(nameof(reference));
    }

    /// <summary>509640 queries the current action and exactly three party graphics rows.</summary>
    public static int ReadVariable(NativeBattleGraphicsState graphics, byte variable)
    {
        return variable switch
        {
            19 => graphics.Models.Take(3).Count(model => (model.Flags0 & 2) != 0 && ((model.Flags8 ?? throw new InvalidDataException("Native camera requires loaded model status flags.")) & 2) == 0),
            22 => TargetFlags(graphics),
            24 => Action().ActorIndex,
            25 => Action().TargetIndex ?? throw new NotSupportedException("Native camera target variable requires its resolved physical recipient."),
            26 => System.Numerics.BitOperations.PopCount(Action().PhysicalTargetMask ?? throw new NotSupportedException("Native camera target count requires its resolved physical target mask.")),
            27 => graphics.Models.Take(3).Count(model => (model.Flags0 & 2) != 0 && (model.Flags8 ?? throw new InvalidDataException("Native camera requires loaded model status flags.")) == 0),
            _ => throw new NotSupportedException($"Native camera variable{variable} requires its active command descriptor owner.")
        };

        NativeBattleActionGeometry Action() => graphics.ActionContext ?? throw new NotSupportedException("Native509640 requires its current50D300 action context.");
    }

    private static int TargetFlags(NativeBattleGraphicsState graphics)
    {
        int target = graphics.ActionContext?.TargetIndex ?? throw new NotSupportedException("Native camera status query requires its resolved physical recipient.");
        NativeBattleGraphicsModel model = graphics.Models[target];
        byte modelId = model.ModelId4 ?? throw new InvalidDataException("Native camera status query requires the loaded model ID BYTE.");
        uint flags = model.Flags8 ?? throw new InvalidDataException("Native camera status query requires the projected status DWORD.");
        return unchecked((int)(modelId >= 16 ? flags & ~1u : (flags & 0x41021) != 0 ? flags | 1u : flags));
    }
}
