using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBlastingZone164Tiles(NativeBattleModulePacketBatch Batch) : NativeBattleBlastingZone164Operation;
public sealed record NativeBattleBlastingZone164Shake(short Offset) : NativeBattleBlastingZone164Operation;

public static partial class NativeBattleBlastingZone164Controller
{
    public static NativeBattleBlastingZone164ChildStep Advance5F4330(NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleBlastingZone164Inputs inputs)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        if (!paused && task.ReadWord(12) == 0)
        {
            for (int index = 0; index < 160; index++)
            {
                uint row = 0x23C7C38;
                while (row < 0x23C8EF8 && ReadDword(frame.Staging, row) != 0)
                {
                    row += 24;
                }
                if (row == 0x23C8EF8)
                {
                    break;
                }
                NativeBattleSquallFinisherVisualIdentity.Allocated(164, task, row);
                NativeBattleFileStaging staging = WriteWord(WriteDword(frame.Staging, row, 1), row + 4, 0);
                staging = WriteWord(staging, row + 8, (ushort)(Rand() % 320));
                staging = WriteWord(staging, row + 10, (ushort)(Rand() % 216));
                uint gray = (uint)(Rand() % 255);
                staging = WriteDword(staging, row + 16, 0x68000000 | gray | gray << 8 | gray << 16);
                frame = frame with { Staging = staging };
            }
        }
        ImmutableArray<NativeBattleAllocatedPrimitive>.Builder packets = ImmutableArray.CreateBuilder<NativeBattleAllocatedPrimitive>();
        for (uint row = 0x23C7C38; row < 0x23C8EF8; row += 24)
        {
            if ((ReadDword(frame.Staging, row) & 1) != 0)
            {
                uint color = unchecked((uint)ReadDword(frame.Staging, row + 16));
                uint screen = unchecked((uint)ReadDword(frame.Staging, row + 8));
                short x = unchecked((short)screen);
                short y = unchecked((short)(screen >> 16));
                NativeBattlePilotVisualGeometry geometry = new(NativeBattleSquallFinisherVisualIdentity.Particle(164, task, row, 0), new(x, y, 0), new(x + 8, y, 0), new(x, y + 8, 0), new(x + 8, y + 8, 0), 4, false) { ScreenSpace = true, UseSourceDepths = true, SourceDepths = System.Numerics.Vector4.Zero };
                NativeBattleGeometryPrimitive primitive = new(4382, [0x02000000, color, screen], []) { PilotGeometry = geometry };
                packets.Add(new(primitive, packets.Count * 12, 0));
            }
        }
        frame = inputs.Execute(frame, new NativeBattleBlastingZone164Tiles(new(packets.ToImmutable(), packets.Count * 12)));
        if (paused)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        bool released = Word(task, 12) >= 60;
        if (released)
        {
            for (uint row = 0x23C7C38; row < 0x23C8EF8; row += 24)
            {
                if ((ReadDword(frame.Staging, row) & 1) != 0)
                {
                    frame = frame with { Staging = WriteDword(frame.Staging, row, 0) };
                }
            }
        }
        return new(frame, pool.WriteSlot(slot, task), released ? 2 : 0);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    public static NativeBattleBlastingZone164ChildStep Advance5F5A50(NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleBlastingZone164Inputs inputs)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        int value = (140 * NativeBattleCameraMath.Cosine((counter << 10) / 14)) >> 12;
        frame = inputs.Execute(frame, new NativeBattleBlastingZone164Shake(unchecked((short)((counter & 1) == 0 ? -value : value))));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool.WriteSlot(slot, task), Word(task, 12) >= 14 ? 2 : 0);
    }

    public static NativeBattleBlastingZone164ChildStep Advance5F5AD0(NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool, int slot, Func<NativeBattleBlastingZone164Frame, uint, NativeBattleBlastingZone164Frame> beginTextureLoad)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.ReadWord(12) == 1)
        {
            uint completion = checked(pool.BankAddress + (uint)(slot * pool.Stride) + 14);
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
            frame = beginTextureLoad(frame, completion);
            pool = frame.Effect.Pools[1];
            task = pool.Slots[slot];
        }
        bool released = task.ReadWord(14) != 0;
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return new(frame, pool.WriteSlot(slot, task), released ? 2 : 0);
    }
}
