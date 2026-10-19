using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeCarbuncle278(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleCarbuncle278Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Carbuncle requires its retained effect memory."), graphics.OriginalEffect ?? throw new InvalidDataException("Native Carbuncle lost its selected effect."), nativeCrtRandom ?? throw new NotSupportedException("Native Carbuncle requires its actual CRT random owner."));
        if (initialize)
        {
            int encounter = definition.EncounterId ?? throw new NotSupportedException("Native Carbuncle requires its installed encounter WORD1CFF6E0.");
            frame = NativeBattleCarbuncle278Entry.Initialize680C50(frame, nativeActionPackets, graphics.Models, checked((ushort)encounter));
        }
        else
        {
            frame = NativeBattleCarbuncle278Controller.Advance680DF0(frame, (depth, flags) =>
            {
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native5713E0 requires its retained four stage groups.");
                stage.Validate();
                NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)depth), ColorFlags = unchecked((uint)flags) }).ToImmutableArray() };
            });
        }
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }
}
