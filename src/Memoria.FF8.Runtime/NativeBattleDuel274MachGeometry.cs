using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel274MachGeometry
{
    public static NativeBattleDuel274Frame Backup6D67B0(NativeBattleDuel274Frame frame, uint destination)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Mach Kick requires its actual geometry backup arena.");
        return frame with { Memory = frame.Memory with { Arena = arena.Write(NativeBattleWaterExecutionFields.ArenaOffset(destination, 616), frame.Staging.Read(0x10E9538, 616)) } };
    }

    public static NativeBattleDuel274Frame Restore6D67B0(NativeBattleDuel274Frame frame, uint source)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Mach Kick requires its actual saved geometry bytes.");
        return frame with { Staging = frame.Staging.Write(0x10E9538, arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(source, 616), 616)) };
    }

    public static NativeBattleDuel274Frame Deform68BCD0(NativeBattleDuel274Frame frame, NativeBattleDuel274Resources resources, uint source, int amplitude, short angle)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(frame.Staging.Read(0x10E9534, 4));
        if (count <= 0 || count > 77)
        {
            throw new NotSupportedException("Native Mach Kick geometry count leaves its actual154-DWORD backup.");
        }
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler701310(new(0, 0, angle));
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Mach Kick requires its actual original vertex backup.");
        for (int vertex = 0; vertex < count; vertex++)
        {
            byte[] saved = arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(checked(source + (uint)vertex * 8), 6), 6);
            NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(saved), BinaryPrimitives.ReadInt16LittleEndian(saved.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(saved.AsSpan(4)));
            NativeBattleVector transformed = NativeBattleGfMatrices.Transform460840(rotation, point);
            uint lookup = unchecked(0x110D3B0u + (uint)((transformed.Z >> 1) * 2));
            short curve = BinaryPrimitives.ReadInt16LittleEndian(resources.Read(lookup, 2).AsSpan());
            transformed = transformed with { X = unchecked((short)(transformed.X - (unchecked(amplitude * curve) >> 13))), Z = unchecked((short)(unchecked(amplitude * transformed.Z) >> 10)) };
            byte[] xyz = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(xyz, transformed.X);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), transformed.Y);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), transformed.Z);
            frame = frame with { Staging = frame.Staging.Write(checked(0x10E9538u + (uint)vertex * 8), xyz) };
        }
        return frame;
    }
}
