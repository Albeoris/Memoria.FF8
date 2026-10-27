using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHoly175WarpFrame(NativeBattleHoly175WarpState State, NativeBattleEffectArena PrivateArena, int PrivateCursor, NativeBattleLogicalOrdering Ordering);
public sealed record NativeBattleHoly175WarpDraw(NativeBattleHoly175WarpFrame Frame, int AliveFaces, uint WarpAlive);

public static class NativeBattleHoly175WarpDrawing
{
    public const string PacketBank = "23814E0";

    public static NativeBattleHoly175WarpDraw Advance5D2BC0(NativeBattleHoly175WarpFrame frame, int animationFrame, int buffer, uint color, uint graphicsFlags, short signedFocalH, uint warpTextured, uint warpAlive, NativeBattleCameraProjection projection)
    {
        frame.State.Validate();
        frame.PrivateArena.Validate();
        if ((uint)buffer >= 2 || frame.PrivateCursor < 0 || frame.PrivateCursor > NativeBattleEffectArena.Length || (frame.PrivateCursor & 3) != 0)
        {
            throw new InvalidDataException("Native Holy warp requires its actual buffer and aligned private allocation cursor.");
        }
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 8, 4, signedFocalH, 4096);
        using NativeBattleEffectArenaWriter gridA = new(frame.State.GridA);
        using NativeBattleEffectArenaWriter gridB = new(frame.State.GridB);
        int[] depths = new int[4];
        for (int point = 0; point < 315; point++)
        {
            NativeBattleModuleProjectedVertex projected = Project(matrix, frame.State.GridA.Read(point * 16 + 4, 6));
            gridA.Write(point * 16, Dword(projected.Screen));
        }
        for (int point = 0; point < 280; point++)
        {
            NativeBattleModuleProjectedVertex projected = Project(matrix, frame.State.GridB.Read(point * 16 + 4, 6));
            gridB.Write(point * 16, Dword(projected.Screen));
        }
        NativeBattleHoly175WarpState state = frame.State with { GridA = gridA.Freeze(), GridB = gridB.Freeze() };
        using NativeBattleEffectArenaWriter faces = new(state.Faces);
        NativeBattleEffectArena packets = state.PhysicalPackets;
        NativeBattleEffectArena privateArena = frame.PrivateArena;
        int privateCursor = frame.PrivateCursor;
        using NativeBattleLogicalOrdering.Builder ordering = new(frame.Ordering);
        int alive = 0;
        for (int faceIndex = 0; faceIndex < 1120; faceIndex++)
        {
            int faceOffset = faceIndex * 36;
            short delay = S(state.Faces.Read(faceOffset + 0x12, 2), 0);
            if (delay < 0)
            {
                continue;
            }
            int packetOffset = faceIndex * 64 + buffer * 32;
            if (delay >= animationFrame)
            {
                packets = packets.Write(packetOffset + 4, Dword(color | 0x24000000));
                for (int corner = 0; corner < 3; corner++)
                {
                    uint address = PointAddress(faceOffset, corner);
                    packets = packets.Write(packetOffset + 8 + corner * 8, state.ReadPoint(address, 0, 4));
                }
                InsertStatic(packetOffset, 4122);
                alive++;
                continue;
            }
            short centerX = S(state.Faces.Read(faceOffset + 0xC, 2), 0);
            short centerY = S(state.Faces.Read(faceOffset + 0xE, 2), 0);
            short offsetX = S(state.Faces.Read(faceOffset + 0x14, 2), 0);
            short offsetY = S(state.Faces.Read(faceOffset + 0x16, 2), 0);
            int absoluteX = centerX + offsetX;
            int absoluteY = centerY + offsetY;
            if (absoluteX > 168 || absoluteY < -116)
            {
                faces.Write(faceOffset + 0x12, Word(-1));
                continue;
            }
            short scale = S(state.Faces.Read(faceOffset + 0x1A, 2), 0);
            if ((graphicsFlags & 0x201) == 0)
            {
                short velocityX = S(state.Faces.Read(faceOffset + 0x1C, 2), 0);
                short velocityY = S(state.Faces.Read(faceOffset + 0x1E, 2), 0);
                short accelerationY = S(state.Faces.Read(faceOffset + 0x20, 2), 0);
                short decay = S(state.Faces.Read(faceOffset + 0x22, 2), 0);
                faces.Write(faceOffset + 0x14, Word(offsetX + velocityX));
                faces.Write(faceOffset + 0x16, Word(offsetY + velocityY));
                faces.Write(faceOffset + 0x1C, Word(velocityX + (velocityX >> 2)));
                faces.Write(faceOffset + 0x1E, Word(velocityY + (accelerationY >> 1)));
                scale = unchecked((short)(scale - decay));
                if (scale < 384)
                {
                    scale = 384;
                }
                faces.Write(faceOffset + 0x1A, Word(scale));
            }
            matrix = new([scale, 0, 0, 0, scale, 0, 0, 0, 4096], absoluteX + 8, absoluteY + 4, signedFocalH, 4096);
            uint[] screen = new uint[3];
            for (int corner = 0; corner < 3; corner++)
            {
                byte[] point = state.ReadPoint(PointAddress(faceOffset, corner), 4, 6);
                NativeBattleVector relative = new(unchecked((short)(S(point, 0) - centerX)), unchecked((short)(S(point, 2) - centerY)), S(point, 4));
                screen[corner] = ProjectVector(matrix, relative).Screen;
            }
            if (warpTextured != 0)
            {
                if (scale == 384)
                {
                    packets = WriteFlat(packets, packetOffset, screen);
                }
                else
                {
                    packets = packets.Write(packetOffset + 4, Dword(color | 0x26000000));
                    for (int corner = 0; corner < 3; corner++)
                    {
                        packets = packets.Write(packetOffset + 8 + corner * 8, Dword(screen[corner]));
                    }
                }
                InsertStatic(packetOffset, 4121);
            }
            else if (scale == 384)
            {
                if (privateCursor > NativeBattleEffectArena.Length - 32)
                {
                    throw new InvalidDataException("Native Holy warp private F3 allocation leaves its actual arena owner.");
                }
                privateArena = WriteFlat(privateArena, privateCursor, screen);
                InsertPrivate(privateCursor);
                privateCursor = checked(privateCursor + 32);
            }
            alive++;
        }
        state = state with { Faces = faces.Freeze(), PhysicalPackets = packets };
        return new(new(state, privateArena, privateCursor, ordering.ToImmutable()), alive, alive == 0 ? 0 : warpAlive);

        NativeBattleModuleProjectedVertex Project(NativeBattleActionMatrix current, byte[] point) => ProjectVector(current, new(S(point, 0), S(point, 2), S(point, 4)));
        NativeBattleModuleProjectedVertex ProjectVector(NativeBattleActionMatrix current, NativeBattleVector point)
        {
            NativeBattleModuleProjectedVertex result = NativeBattleModuleProjection.Project45FE10(current, point, projection);
            depths[0] = depths[1];
            depths[1] = depths[2];
            depths[2] = depths[3];
            depths[3] = result.Depth;
            return result;
        }
        uint PointAddress(int faceOffset, int corner) => BinaryPrimitives.ReadUInt32LittleEndian(state.Faces.Read(faceOffset + corner * 4, 4));
        void InsertStatic(int offset, int bucket)
        {
            if (ordering.ProxyCount >= NativeBattlePrimitiveOrdering.ProxyCapacity)
            {
                return;
            }
            ImmutableArray<uint> words = ReadPacket(packets, offset);
            ordering.Prepend(bucket, new(PacketBank, 0, offset), words, 0, depths.ToImmutableArray());
            packets = packets.Write(offset, NativeBattleLogicalOrdering.LinkBytes(ordering.LastHeaderLink));
        }
        void InsertPrivate(int offset)
        {
            if (ordering.ProxyCount >= NativeBattlePrimitiveOrdering.ProxyCapacity)
            {
                return;
            }
            ordering.Prepend(4121, new("20DFAB8", 0, offset), ReadPacket(privateArena, offset), 0, depths.ToImmutableArray());
            privateArena = privateArena.Write(offset, NativeBattleLogicalOrdering.LinkBytes(ordering.LastHeaderLink));
        }
    }

    private static NativeBattleEffectArena WriteFlat(NativeBattleEffectArena arena, int offset, uint[] screen)
    {
        byte[] packet = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(packet, 0x04000000);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), 0x20FFFFFF);
        for (int corner = 0; corner < 3; corner++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8 + corner * 4), screen[corner]);
        }
        return arena.Write(offset, packet);
    }

    private static ImmutableArray<uint> ReadPacket(NativeBattleEffectArena arena, int offset)
    {
        int length = arena.Read(offset + 3, 1)[0];
        if (length is not (4 or 7))
        {
            throw new NotSupportedException("Native Holy warp requires its retained F3 or FT3 packet length.");
        }
        byte[] body = arena.Read(offset + 4, checked(length * 4));
        ImmutableArray<uint>.Builder words = ImmutableArray.CreateBuilder<uint>(length + 1);
        words.Add((uint)length << 24);
        for (int index = 0; index < length; index++)
        {
            words.Add(BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(index * 4)));
        }
        return words.ToImmutable();
    }

    private static short S(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
    private static byte[] Dword(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }
    private static byte[] Word(int value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, unchecked((short)value));
        return bytes;
    }
}
