using Memory = Memoria.FF8.Runtime.NativeBattleApocalypse221BreakupMemory;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicBreakupParticleHook
{
    public static NativeBattleWaterExecutionFrame Emit(NativeBattleWaterExecutionFrame frame, int fixedX, int fixedY, int fixedZ, Func<NativeBattleWaterExecutionFrame, short, uint> resolveOwner)
    {
        Memory memory = new(frame);
        uint payload = frame.ReadTemporaryDword(0x90);
        short selector = memory.Signed(payload + 0xE);
        if (selector == 0)
        {
            return frame;
        }
        NativeCrtRandomState random = frame.Random.Draw(out ushort draw);
        memory.Frame = frame with { Random = random };
        if ((draw * 256 >> 15) > memory.Signed(payload + 0x10))
        {
            return memory.Frame;
        }
        uint owner = resolveOwner(memory.Frame, selector);
        uint bank = memory.Dword(owner + 0xB8);
        ushort old = memory.Word(bank + 2);
        memory.Word(bank + 2, old + 1);
        int capacity = memory.Word(bank);
        int remainder = old - Memory.Divide(old, capacity) * capacity;
        uint record = unchecked(bank + 0x10u + (uint)(remainder * 0x50));
        memory.Word(bank + 8, 1);
        memory.Word(record, 1);
        memory.Word(record + 2, 0);
        memory.Dword(record + 4, memory.Dword(frame.ReadTemporaryDword(0x84) + 0x10));
        memory.Dword(record + 0x14, unchecked((uint)fixedX));
        memory.Dword(record + 0x18, unchecked((uint)fixedY));
        memory.Dword(record + 0x1C, unchecked((uint)fixedZ));
        for (uint offset = 0x20; offset <= 0x2C; offset += 4)
        {
            memory.Dword(record + offset, 0);
        }
        memory.Word(record + 0x40, memory.Word(owner + 0x92));
        memory.Word(record + 0x48, memory.Word(owner + 0x9A));
        memory.Dword(record + 0x3C, 0);
        memory.Dword(record + 0x38, memory.Dword(owner + 0xCC) & 0x02FFFFFF);
        memory.Byte(record + 0x37, 1);
        return memory.Frame;
    }
}
