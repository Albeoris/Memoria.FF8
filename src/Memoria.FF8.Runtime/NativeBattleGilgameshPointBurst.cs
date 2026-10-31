using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshPointResources(NativeBattleEffectHeapAllocation Colors)
{
    public void Validate()
    {
        if (Colors is null || Colors.Identity != "FF8_EN:CD0428" || Colors.Data.IsDefault || Colors.Data.Length != 64)
        {
            throw new InvalidDataException("Native Gilgamesh point burst requires its complete sixteen-DWORD color table.");
        }
        for (int index = 0; index < 16; index++)
        {
            if ((Color(index) >> 24) != 0x6A)
            {
                throw new InvalidDataException("Native Gilgamesh point burst requires its actual one-pixel semitransparent TILE command.");
            }
        }
    }

    public uint Color(int phase)
    {
        if ((uint)phase >= 16)
        {
            throw new InvalidDataException("Native Gilgamesh point phase leaves its supplied color table.");
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(Colors.Data.AsSpan().Slice(phase * 4));
    }
}

public sealed record NativeBattleGilgameshPointStep(NativeBattleModulePoolTask Task, NativeBattleEffectArena Arena, NativeCrtRandomState Random, NativeBattleGfGeometryBatch Packets, int Result);

/// <summary>593E80's raw world particles and ordered twelve-byte point-packet stores.</summary>
public static class NativeBattleGilgameshPointBurst
{
    public static NativeBattleGilgameshPointStep Step593E80(NativeBattleModulePoolTask task, NativeBattleEffectArena arena, NativeCrtRandomState random, NativeBattleGilgameshPointResources resources, uint graphicsFlags, Func<NativeBattleVector, NativeBattleModuleProjectedVertex> project45FAA0)
    {
        return Step593E80WithPresentation(task, arena, random, resources, graphicsFlags, project45FAA0);
    }

    public static NativeBattleGilgameshPointStep Step593E80WithPresentation(NativeBattleModulePoolTask task, NativeBattleEffectArena arena, NativeCrtRandomState random, NativeBattleGilgameshPointResources resources, uint graphicsFlags, Func<NativeBattleVector, NativeBattleModuleProjectedVertex> project45FAA0, NativeBattlePilotVisualIdentity? pilotIdentity = null, Func<NativeBattleVector, System.Numerics.Vector3>? captureWorld = null)
    {
        if (task.Callback != 0x593E80 || task.PayloadLength != 24)
        {
            throw new InvalidDataException("Native593E80 requires its actual raw task.");
        }
        resources.Validate();
        arena.Validate();
        bool paused = (graphicsFlags & 0x201) != 0;
        int counter = unchecked((short)task.ReadWord(12));
        if (!paused && counter <= 40)
        {
            int radius = 1900 - 35 * counter;
            for (int spawn = 0; spawn < 10; spawn++)
            {
                int record = -1;
                for (int index = 0; index < 200; index++)
                {
                    int candidate = 0x2F800 + index * 32;
                    if (Dword(candidate) == 0)
                    {
                        record = candidate;
                        break;
                    }
                }
                if (record < 0)
                {
                    break;
                }
                WriteDword(record, 1);
                WriteWord(record + 4, 0);
                int angle = Draw() % 4096;
                int sin = NativeBattleCameraMath.Sine(angle);
                int cos = NativeBattleCameraMath.Cosine(angle);
                WriteWord(record + 8, unchecked(sin * radius) >> 12);
                WriteWord(record + 10, -(Draw() % 90));
                WriteWord(record + 12, unchecked(cos * radius) >> 12);
                int speed = Draw() % 240 + 90;
                WriteWord(record + 16, unchecked(sin * speed) >> 12);
                WriteWord(record + 18, -10 - Draw() % 50);
                WriteWord(record + 20, unchecked(cos * speed) >> 12);
                if (pilotIdentity.HasValue)
                {
                    NativeBattlePilotVisualLifetimes.Allocated(arena, NativeBattleEffectNodeMemory.ArenaAddress + 0x2F800, (record - 0x2F800) / 32);
                }
            }
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int cursor = 0;
        int survivors = 0;
        for (int index = 0; index < 200; index++)
        {
            int record = 0x2F800 + index * 32;
            if ((Dword(record) & 1) == 0)
            {
                continue;
            }
            NativeBattleVector point = new(Word(record + 8), Word(record + 10), Word(record + 12));
            NativeBattleModuleProjectedVertex projected = project45FAA0(point);
            uint color = resources.Color(Word(record + 4));
            byte[] bytes = new byte[12];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x02000000);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), color);
            if (!paused)
            {
                int phase = unchecked((short)(Word(record + 4) + 1));
                WriteWord(record + 4, phase);
                if (phase >= 16)
                {
                    WriteDword(record, 0);
                }
                else
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int velocity = Word(record + 16 + axis * 2);
                        WriteWord(record + 8 + axis * 2, Word(record + 8 + axis * 2) + velocity);
                        WriteWord(record + 16 + axis * 2, axis == 1 ? velocity + (velocity >> 4) : velocity - (velocity >> 3));
                    }
                    survivors++;
                }
            }
            bool admitted = (projected.Flags & 0x60000) == 0;
            ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, 12).Select(offset => offset < 8 || admitted ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
            NativeBattleGeometryPrimitive? primitive = null;
            if (admitted)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), projected.Screen);
                primitive = new(17 + (projected.Depth >> 4), [0x02000000, color, projected.Screen], [0, 0, 0, 0]) { PhysicalWriteMask = mask };
                if (pilotIdentity is { } parent)
                {
                    System.Numerics.Vector3 world = (captureWorld ?? throw new InvalidDataException("Gilgamesh point capture requires its actual source transform."))(point);
                    NativeBattlePilotVisualIdentity row = NativeBattleGilgameshVisuals.Row(parent, arena, NativeBattleEffectNodeMemory.ArenaAddress + 0x2F800, index, resources.Colors, 0xCD0428)!.Value;
                    primitive = primitive with { PilotGeometry = new(row, world, world, world, world, 4) { ScreenOffsets = [default, new(8, 0), new(0, 8), new(8, 8)], SourceDepths = System.Numerics.Vector4.Zero, UseSourceDepths = true } };
                }
            }
            attempts.Add(new(cursor, bytes.ToImmutableArray(), mask, primitive));
            if (admitted)
            {
                cursor += 12;
            }
        }
        if (!paused)
        {
            task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        }
        return new(task, arena, random, new(attempts.ToImmutable(), cursor), !paused && survivors == 0 ? 2 : 0);

        int Draw()
        {
            random = random.Draw(out ushort value);
            return value;
        }

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
        uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

        void WriteWord(int offset, int value)
        {
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
            arena = arena.Write(offset, bytes);
        }

        void WriteDword(int offset, uint value)
        {
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            arena = arena.Write(offset, bytes);
        }
    }
}
