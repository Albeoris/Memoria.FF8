using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicActorPlacement
{
    public static NativeBattleWaterExecutionFrame DrawScaled65(NativeBattleWaterExecutionFrame frame, Func<uint, NativeBattleGraphicsModel> readActor, Action<uint, NativeBattleGraphicsModel> writeActor)
    {
        uint payload = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xB8, 4));
        int factor = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(payload, 2), 2));
        frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)factor));
        uint header = CoreDword(frame, 0x279744C);
        byte index = ReadSlot(frame, 0x1B, 1)[0];
        uint address = CoreDword(frame, unchecked(header + 0x60 + 4u * index));
        NativeBattleGraphicsModel actor = readActor(address);
        actor = actor with { Flags0 = (ushort)(actor.Flags0 | 0x1000) };
        writeActor(address, actor);
        NativeBattleModelTransform transform = actor.Transform ?? throw new NotSupportedException("Native scaled actor placement requires its retained transform.");
        actor = actor with { Transform = transform with { Position28 = new(unchecked((short)SlotWord(frame, 0x94)), unchecked((short)SlotWord(frame, 0x96)), unchecked((short)SlotWord(frame, 0x98))) } };
        writeActor(address, actor);
        uint scale = unchecked(header + 0x120 + 8u * index);
        int zFactor = unchecked((short)SlotWord(frame, 0x8C));
        NativeBattleVector scaled = new(Scale(0, factor), Scale(1, factor), Scale(2, zFactor));
        actor = actor with { Transform = actor.Transform with { Scale148 = scaled }, Rotation12 = 0 };
        writeActor(address, actor);
        uint angles = unchecked(header + 0xD8 + 8u * index);
        actor = actor with { Rotation14 = unchecked((short)(CoreWord(frame, angles + 2) + (SlotWord(frame, 0x8E) << 4))), Rotation16 = unchecked((short)(CoreWord(frame, angles + 4) + (SlotWord(frame, 0x90) << 4))) };
        writeActor(address, actor);
        return frame;

        short Scale(int axis, int multiplier) => unchecked((short)(unchecked((short)CoreWord(frame, scale + (uint)(axis * 2)) * multiplier) / 256));
    }

    public static NativeBattleWaterExecutionFrame DrawRadial64(NativeBattleWaterExecutionFrame frame, Func<uint, NativeBattleGraphicsModel> readActor, Action<uint, NativeBattleGraphicsModel> writeActor)
    {
        uint header = CoreDword(frame, 0x279744C);
        byte index = ReadSlot(frame, 0x1B, 1)[0];
        uint address = CoreDword(frame, unchecked(header + 0x60 + 4u * index));
        NativeBattleGraphicsModel actor = readActor(address);
        actor = actor with { Flags0 = (ushort)(actor.Flags0 | 0x1000) };
        writeActor(address, actor);
        uint payload = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xB8, 4));
        uint captured = unchecked(header + 0x84 + 8u * index);
        int factor = Word(payload + 4);
        int centerX = Word(payload + 6);
        int centerY = Word(payload + 8);
        int centerZ = Word(payload + 10);
        int x = unchecked(centerX + unchecked((unchecked((short)CoreWord(frame, captured)) - centerX) * factor) / 256);
        int y = unchecked(centerY + unchecked((unchecked((short)CoreWord(frame, captured + 2)) - centerY) * factor) / 256);
        int z = unchecked(centerZ + unchecked((unchecked((short)CoreWord(frame, captured + 4)) - centerZ) * factor) / 256);
        int dx = unchecked(x - centerX);
        int dy = unchecked(y - centerY);
        int dz = unchecked(z - centerZ);
        int planar = Sqrt(unchecked(dx * dx + dy * dy));
        int distance = Sqrt(unchecked(planar * planar + dz * dz));
        int radius = Word(payload + 2);
        if (distance < radius)
        {
            int height = unchecked((radius - distance) * Word(payload)) / radius;
            int lift = unchecked(height * unchecked((short)SlotWord(frame, 0x94))) / 256;
            y = unchecked(y + lift);
        }
        NativeBattleModelTransform transform = actor.Transform ?? throw new NotSupportedException("Native radial actor placement requires its retained transform.");
        actor = actor with { Transform = transform with { Position28 = new(unchecked((short)x), unchecked((short)y), unchecked((short)z)) } };
        writeActor(address, actor);
        uint scale = unchecked(header + 0x120 + 8u * index);
        actor = actor with { Transform = actor.Transform with { Scale148 = new(Scale(0), Scale(1), Scale(2)) } };
        writeActor(address, actor);
        return frame;

        short Word(uint source) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(source, 2), 2));
        short Scale(int axis) => unchecked((short)(unchecked((short)CoreWord(frame, scale + (uint)(axis * 2)) * factor) / 256));
        int Sqrt(int value) => unchecked((int)(long)Math.Sqrt(value));
    }
}
