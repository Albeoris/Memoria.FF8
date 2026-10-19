using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicModelServices(Func<NativeBattleWaterExecutionFrame, short, uint> ResolveShape, Func<NativeBattleWaterExecutionFrame, uint, byte[]> ReadAnimations);

/// <summary>Sparse embedded model construction and first pose selection over the current arena.</summary>
public static partial class NativeBattleCinematicModel
{
    public static NativeBattleWaterExecutionFrame Register(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleCinematicModelServices services)
    {
        short argument = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        uint address;
        (frame, address) = Allocate(frame, argument, services);
        frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0xBC, 4), BitConverter.GetBytes(address)) };
        frame = NativeBattleWaterParticleCommands.RegisterDrawB65320(frame, 3);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }

    public static (NativeBattleWaterExecutionFrame Frame, uint Address) Allocate(NativeBattleWaterExecutionFrame frame, short argument, NativeBattleCinematicModelServices services)
    {
        uint shape = services.ResolveShape(frame, (short)(argument & 0xFF));
        uint address;
        (frame, address) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, 0x80);
        Word(8, unchecked((ushort)argument));
        Dword(0x14, unchecked(address + 0x30));
        Dword(0x30, unchecked(shape + ReadDword(unchecked(shape + 4))));
        Dword(0x34, unchecked(shape + ReadDword(unchecked(shape + 8))));
        Dword(0x38, unchecked(shape + ReadDword(unchecked(shape + 12))));
        Dword(0x54, 0);
        Dword(0x58, 0x00D00140);
        Dword(0x5C, 0x00808080);
        Dword(0x60, uint.MaxValue);
        Word(0x64, 0);
        Dword(0x68, 0);
        Dword(0x50, uint.MaxValue);
        Word(0, 0);
        Word(2, 0);
        frame = frame with { Arena = frame.Arena.Write(ArenaOffset(unchecked(address + 0x21), 1), new byte[] { 0 }) };
        Dword(0x78, 0x00808080);
        byte animation = frame.Arena.Read(ArenaOffset(unchecked(address + 9), 1), 1)[0];
        frame = Select509440(frame, address, animation, services);
        return (frame, address);

        uint ReadDword(uint pointer) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(pointer, 4), 4));
        void Word(int offset, ushort value)
        {
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(unchecked(address + (uint)offset), 2), bytes) };
        }
        void Dword(int offset, uint value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(unchecked(address + (uint)offset), 4), BitConverter.GetBytes(value)) };
    }

    public static NativeBattleWaterExecutionFrame Select509440(NativeBattleWaterExecutionFrame frame, uint model, byte animation, NativeBattleCinematicModelServices services)
    {
        uint table = ReadDword(unchecked(model + 0x14));
        uint skeleton = ReadDword(table);
        uint animations = ReadDword(unchecked(table + 8));
        byte flags = frame.Arena.Read(ArenaOffset(unchecked(model + 0x21), 1), 1)[0];
        if ((flags & 3) != 0)
        {
            throw new NotSupportedException($"Native embedded model pose speed flags 0x{flags:X2} require their selected cursor contract.");
        }
        byte[] section = services.ReadAnimations(frame, animations) ?? throw new InvalidDataException("Native embedded model requires its current bounded animation section.");
        int skeletonOffset = ArenaOffset(skeleton, 1);
        int count = frame.Arena.Read(skeletonOffset, 1)[0];
        NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(section, count, animation);
        int entry = BinaryPrimitives.ReadInt32LittleEndian(section.AsSpan(4 + 4 * animation, 4));
        byte limit = section[entry];
        byte[] control = new byte[8];
        control[0] = animation;
        control[1] = (byte)((flags & 0xF3) | 4);
        BinaryPrimitives.WriteUInt16LittleEndian(control.AsSpan(2), first.ByteOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(control.AsSpan(4), first.BitOffset);
        control[6] = 1;
        control[7] = limit;
        frame = frame with { Arena = frame.Arena.Write(ArenaOffset(unchecked(model + 0x20), 8), control) };
        NativeBattleEffectArena arena = NativeBattleArenaPose.WritePose(frame.Arena, skeletonOffset, first.Pose);
        arena = NativeBattleArenaPose.RestoreBoneMatrices(arena, skeletonOffset, first.Pose);
        return frame with { Arena = arena };

        uint ReadDword(uint pointer) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(pointer, 4), 4));
    }
}
