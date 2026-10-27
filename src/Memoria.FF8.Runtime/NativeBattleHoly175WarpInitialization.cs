using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHoly175WarpInitializationResult(NativeBattleHoly175WarpState State, NativeCrtRandomState Random);

public static class NativeBattleHoly175WarpInitialization
{
    public static NativeBattleHoly175WarpInitializationResult InitializeGrid5D2210(NativeBattleHoly175WarpState state, NativeCrtRandomState random)
    {
        state.Validate();
        using NativeBattleEffectArenaWriter gridA = new(state.GridA);
        using NativeBattleEffectArenaWriter gridB = new(state.GridB);
        for (int row = 0; row < 15; row++)
        {
            for (int column = 0; column < 21; column++)
            {
                byte[] point = new byte[12];
                int x = -168 + 16 * column;
                int y = -120 + 16 * row;
                int jitterY = 0;
                int jitterX = 0;
                int flags = 0;
                if (row is not (0 or 14))
                {
                    random = random.Draw(out ushort value);
                    jitterY = value % 12 - 6;
                    y += jitterY;
                }
                else
                {
                    flags |= 1;
                }
                if (column is not (0 or 20))
                {
                    random = random.Draw(out ushort value);
                    jitterX = value % 14 - 7;
                    x += jitterX;
                }
                else
                {
                    flags |= 2;
                }
                W(point, 0, x);
                W(point, 2, y);
                W(point, 6, flags);
                W(point, 8, jitterX + 16 * column);
                W(point, 10, jitterY + 16 * row);
                gridA.Write((row * 21 + column) * 16 + 4, point);
            }
        }
        for (int row = 0; row < 14; row++)
        {
            for (int column = 0; column < 20; column++)
            {
                random = random.Draw(out ushort valueY);
                int jitterY = valueY % 6 - 3;
                random = random.Draw(out ushort valueX);
                int jitterX = valueX % 6 - 3;
                byte[] point = new byte[12];
                W(point, 0, -160 + 16 * column + jitterX);
                W(point, 2, -112 + 16 * row + jitterY);
                W(point, 8, 8 + 16 * column + jitterX);
                W(point, 10, 8 + 16 * row + jitterY);
                gridB.Write((row * 20 + column) * 16 + 4, point);
            }
        }
        state = state with { GridA = gridA.Freeze(), GridB = gridB.Freeze() };
        using NativeBattleEffectArenaWriter faces = new(state.Faces);
        using NativeBattleEffectArenaWriter packets = new(state.PhysicalPackets);
        ImmutableArray<uint>? pointerCache = state.PointPointerCache;
        for (int row = 0; row < 14; row++)
        {
            for (int column = 0; column < 20; column++)
            {
                int cell = row * 20 + column;
                uint a = NativeBattleHoly175WarpState.GridAAddress + (uint)((row * 21 + column) * 16);
                uint b = a + 16;
                uint c = a + 21 * 16;
                uint d = c + 16;
                uint o = NativeBattleHoly175WarpState.GridBAddress + (uint)(cell * 16);
                pointerCache = ImmutableArray.Create(a, b, c, d, o);
                uint[][] triangles = [[a, b, o], [a, o, c], [b, d, o], [o, d, c]];
                bool rightPage = S(state.ReadPoint(a, 12, 2), 0) > 160;
                int page = rightPage ? 0x13B : 0x139;
                int uOffset = rightPage ? -128 : 0;
                for (int triangle = 0; triangle < 4; triangle++)
                {
                    byte[] pointers = new byte[12];
                    for (int corner = 0; corner < 3; corner++)
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(pointers.AsSpan(corner * 4), triangles[triangle][corner]);
                    }
                    faces.Write((cell * 4 + triangle) * 36, pointers);
                    for (int buffer = 0; buffer < 2; buffer++)
                    {
                        int packet = NativeBattleHoly175WarpState.PacketOffset(cell, triangle, buffer);
                        byte[] header = new byte[8];
                        BinaryPrimitives.WriteUInt32LittleEndian(header, 0x07000000);
                        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), 0x26808080);
                        packets.Write(packet, header);
                        for (int corner = 0; corner < 3; corner++)
                        {
                            byte[] pointUv = state.ReadPoint(triangles[triangle][corner], 12, 4);
                            uint uv = unchecked((byte)(S(pointUv, 0) + uOffset)) | (uint)unchecked((byte)S(pointUv, 2)) << 8 | (uint)page << 16;
                            byte[] bytes = new byte[4];
                            BinaryPrimitives.WriteUInt32LittleEndian(bytes, uv);
                            packets.Write(packet + 12 + corner * 8, bytes);
                        }
                    }
                }
            }
        }
        return new(state with { Faces = faces.Freeze(), PhysicalPackets = packets.Freeze(), PointPointerCache = pointerCache }, random);
    }

    public static NativeBattleHoly175WarpInitializationResult InitializeFaces5D25D0(NativeBattleHoly175WarpState state, NativeCrtRandomState random)
    {
        state.Validate();
        using NativeBattleEffectArenaWriter faces = new(state.Faces);
        for (int face = 0; face < 1120; face++)
        {
            byte[] pointers = state.Faces.Read(face * 36, 12);
            int x = 0;
            int y = 0;
            for (int corner = 0; corner < 3; corner++)
            {
                uint address = BinaryPrimitives.ReadUInt32LittleEndian(pointers.AsSpan(corner * 4));
                byte[] coordinates = state.ReadPoint(address, 4, 4);
                x += S(coordinates, 0);
                y += S(coordinates, 2);
            }
            uint first = BinaryPrimitives.ReadUInt32LittleEndian(pointers);
            byte[] uv = state.ReadPoint(first, 12, 4);
            int u = S(uv, 0);
            int v = S(uv, 2);
            int square = unchecked(u * u + v * v);
            if (square < 0)
            {
                throw new NotSupportedException("Native Holy warp distance requires the nonnegative signed squared-distance domain.");
            }
            int distance = unchecked((int)(long)Math.Sqrt(square));
            byte[] fields = new byte[24];
            W(fields, 0, x / 3);
            W(fields, 2, y / 3);
            W(fields, 6, unchecked(distance * 10) / 350);
            W(fields, 14, 4096);
            random = random.Draw(out ushort firstRandom);
            W(fields, 16, firstRandom % 6 + 1);
            random = random.Draw(out ushort secondRandom);
            W(fields, 18, secondRandom % 25 + 4);
            random = random.Draw(out ushort thirdRandom);
            W(fields, 20, -2 - thirdRandom % 8);
            random = random.Draw(out ushort fourthRandom);
            W(fields, 22, fourthRandom % 352 + 320);
            faces.Write(face * 36 + 12, fields);
        }
        return new(state with { Faces = faces.Freeze() }, random);
    }

    private static short S(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
    private static void W(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), unchecked((short)value));
}
