namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void PublishNativeGraphics500590()
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native500590 requires its retained primitive process.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native500590 requires its publication BYTE1D8E03A.");
        NativeEffectMemory = memory with { PrimitiveProcess = process with { PublicationState03A = 2 } };
    }

    private (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) PollNativeAfterBattle502F90(NativeBattleGraphicsState graphics, byte phase)
    {
        if (phase == 0)
        {
            if (graphics.Models.Any(model => (model.Flags0 & 2) != 0 && NativeBattleActionGraphics.IsModelBusy(model, 0x40, 0x101A)))
            {
                return (graphics, phase, false);
            }
            phase = 1;
        }
        if (phase == 1)
        {
            return (graphics with { Flags = graphics.Flags | 0x80 }, 2, false);
        }
        if (phase != 2)
        {
            return (graphics, phase, false);
        }
        PublishNativeModuleSound(new(0x15, 0, null) { Pan = 128 });
        for (byte index = 0; index < 3; index++)
        {
            NativeBattleGraphicsModel model = graphics.Models[index];
            if ((model.Flags0 & 2) == 0)
            {
                continue;
            }
            uint flags = model.Flags8 ?? throw new NotSupportedException("Native502F90 requires its loaded party model DWORD8.");
            if ((flags & 0x101A) == 0)
            {
                graphics = ApplyNativeGraphicsStatus509CD0(graphics, index, flags & 0x400);
                graphics = SelectNativeHeaderSequence(graphics, index, 17);
            }
        }
        return (graphics, phase, true);
    }
}
