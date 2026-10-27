using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleDiablos325Trail([property: JsonRequired] uint Cursor51F8, [property: JsonRequired] uint BlockCursor51FC, [property: JsonRequired] ushort Shift5200, [property: JsonRequired] ushort Step5202, [property: JsonRequired] ushort Parameter5204, [property: JsonRequired] ushort Parameter5206)
{
    public void Validate()
    {
        _ = NativeBattleWaterExecutionFields.ArenaOffset(Cursor51F8, 0);
        _ = NativeBattleWaterExecutionFields.ArenaOffset(BlockCursor51FC, 0);
    }

    public static NativeBattleDiablos325Frame Initialize657E70(NativeBattleDiablos325Frame frame, int parameter, ushort intensity, ushort bias)
    {
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        uint first = checked(state.ModelBuffer5208 + 0x4C5C);
        uint second = checked(state.ModelBuffer5208 + 0x7C5C);
        NativeBattleDiablos325Trail trail = new(first, second, unchecked((ushort)(15 - parameter)), unchecked((ushort)((1 << (parameter & 31)) / 2)), intensity, bias);
        using NativeBattleEffectArenaWriter writer = new(frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos trails require their actual current arena."));
        for (int index = 0; index < 512; index++)
        {
            writer.Write(0x4C5C + index * 24 + 6, [0xFF, 0xFF]);
        }
        for (int index = 0; index < 64; index++)
        {
            writer.Write(0x7C5C + index * 48, [0, 0, 0, 0]);
        }
        return frame with { Memory = frame.Memory with { Arena = writer.Freeze(), Diablos325 = state with { Mode51F0 = 0, Trail = trail } } };
    }
}
