using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicPalette47(NativeBattleWaterExecutionFrame frame, NativeBattleGraphicsState graphics)
    {
        uint payload = Dword(0xB8);
        short columns = Word(payload + 4);
        int count = columns * Word(payload + 6);
        uint captured = Dword(0xBC);
        uint palette = Dword(0xC4);
        if (captured == 0)
        {
            (frame, captured) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, checked((uint)(count * 8)));
            frame = WriteSlotDword(frame, 0xBC, captured);
            var rows = NativeBattleCinematicPalette47.CaptureB67710(frame.Arena.Read(ArenaOffset(palette, checked(count * 2)), checked(count * 2)), count);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(captured, rows.Length), rows.AsSpan()) };
        }
        int hue = unchecked((short)SlotWord(frame, 0x94));
        int weight = unchecked((short)SlotWord(frame, 0x8C));
        int saturation = unchecked((short)SlotWord(frame, 0x96));
        int value = unchecked((short)SlotWord(frame, 0x98));
        frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)hue)).WriteTemporaryDword(0xF4, unchecked((uint)weight)).WriteTemporaryDword(0xF8, unchecked((uint)saturation)).WriteTemporaryDword(0xFC, unchecked((uint)value));
        NativeBattleCinematicPalette47Result result = NativeBattleCinematicPalette47.TransformB67810(frame.Arena.Read(ArenaOffset(captured, checked(count * 8)), checked(count * 8)), count, hue, weight, saturation, value);
        frame = frame with { Arena = frame.Arena.Write(ArenaOffset(palette, result.Palette.Length), result.Palette.AsSpan()) };
        NativeBattleEffectMemory memory = RequireWaterMemory();
        NativeBattleModelColorOverrides overrides = memory.ModelColorOverrides ?? throw new NotSupportedException("Native palette tint requires its retained model color slots.");
        foreach (NativeBattleCinematicPalette47ColorWrite write in NativeBattleCinematicPalette47.ActorColorWrites(result.Palette.AsSpan(), columns))
        {
            overrides = overrides with { Slots = overrides.Slots.SetItem(write.ActorIndex - 3, new(checked((byte)write.ActorIndex), write.GlobalColor & 0xFFFFFF)) };
            graphics = graphics with { Models = graphics.Models.SetItem(write.ActorIndex, graphics.Models[write.ActorIndex] with { Color40 = write.ActorColor }) };
        }
        NativeEffectMemory = memory with { ModelColorOverrides = overrides };
        return (frame, graphics);

        uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
    }
}
