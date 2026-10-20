using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTheEnd85NodeDrawingStep(NativeBattleFileStaging Staging, NativeBattleGfGeometryBatch Packets, bool PublishedFarColor, uint? FarRed, uint? FarGreen, uint? FarBlue);

/// <summary>The node callbacks consume caller-owned downward scratch views; their caller retains allocation and packet-bank ownership.</summary>
public static class NativeBattleTheEnd85NodeDrawing
{
    public static NativeBattleTheEnd85NodeDrawingStep Draw7897F0(NativeBattleFileStaging staging, uint context, uint node, uint template, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleTheEnd85DrawServices services)
    {
        void Store(int offset, uint value) => staging = staging.Write(context + (uint)offset, BitConverter.GetBytes(value));
        Store(0, Dword(services, node + 368));
        uint flags = Word(services, template + 58) == 0 ? 0x30u : 0;
        short fade = unchecked((short)Word(services, node + 462));
        if (fade != 0)
        {
            Store(8, Dword(services, node + 364));
            Store(12, unchecked((uint)(int)fade));
            flags |= 0xC0;
        }
        Store(20, flags);
        Store(16, 0);
        SetWindows(ref staging, context);
        int count = unchecked((sbyte)Read(services, node + 472, 1)[0]);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        NativeBattleTheEnd85NodeDrawingStep result = Empty(staging);
        for (int instance = 0; instance < count; instance++)
        {
            if (count != 1)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    short position = unchecked((short)Word(services, checked(node + 412u + (uint)instance * 8 + (uint)axis * 2)));
                    services.Write(node + 192u + (uint)axis * 4, BitConverter.GetBytes((int)position).ToImmutableArray());
                }
            }
            NativeBattleTheEnd85MeshStep emitted = NativeBattleTheEnd85GeometryPackets.Emit785A90(result.Staging, context, Matrix(services, node + 172), projection, colors, services.Read);
            attempts.AddRange(emitted.Packets.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + allocated) }));
            allocated = checked(allocated + emitted.Packets.AllocationBytes);
            result = new(emitted.Staging, emitted.Packets, true, emitted.FarRed, emitted.FarGreen, emitted.FarBlue);
        }
        return result with { Packets = new(attempts.ToImmutable(), allocated) };
    }

    public static NativeBattleTheEnd85NodeDrawingStep Draw789A10(NativeBattleFileStaging staging, uint context, uint node, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleTheEnd85DrawServices services)
    {
        uint source = Dword(services, node + 368);
        ushort phase = unchecked((ushort)(short)(sbyte)Read(services, node + 464, 1)[0]);
        NativeBattleActionMatrix matrix = Matrix(services, node + 172);
        void Store(int offset, uint value) => staging = staging.Write(context + (uint)offset, BitConverter.GetBytes(value));
        void StoreWord(int offset, ushort value) => staging = staging.Write(context + (uint)offset, BitConverter.GetBytes(value));
        Store(0, source);
        StoreWord(4, phase);
        StoreWord(36, 0);
        StoreWord(38, Word(services, source + 8));
        uint directory = checked(source + 10 + (uint)phase * 2);
        ushort frame = Word(services, directory);
        StoreWord(40, Word(services, directory + 2));
        uint descriptor = Dword(services, checked(source + frame));
        int count = checked((int)(descriptor & 0x7FFFFFFF));
        Store(44, checked(source + frame + 4));
        Store(48, unchecked((uint)count));
        if ((descriptor & 0x80000000) != 0)
        {
            staging = staging.Write(context + 37, [1]);
        }
        foreach (int offset in new[] { 72, 80, 88, 96 })
        {
            StoreWord(offset, 0);
        }
        Store(28, 0x808080);
        staging = staging.Write(context + 32, Read(services, source + 4, 4));
        Store(176, 0);
        Store(164, 4096);
        Store(168, 0);
        Store(172, 0);
        Store(60, uint.MaxValue);
        int extent = checked(Math.Max(14 + phase * 2, frame + 4 + count * 20));
        ImmutableArray<byte> resource = Read(services, source, extent).ToImmutableArray();
        int quadIndex = 0;
        bool depthStored = false;
        NativeBattleActionMatrix? rolled = null;
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(resource, phase, matrix, projection, cameraRoll, 17, 2, observeRolledMatrix: current =>
        {
            rolled = current;
            StoreMatrix(ref staging, context + 132, current);
            staging = staging.MarkUnknown(context + 150, 2);
        }, observeQuadMatrix: current =>
        {
            ReadOnlySpan<byte> quad = resource.AsSpan().Slice(checked(frame + 4 + quadIndex * 20), 20);
            quadIndex++;
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(quad[14..]);
            if ((flags & 0xC00) != 0)
            {
                short angle = BinaryPrimitives.ReadInt16LittleEndian(quad[12..]);
                if (angle == 0)
                {
                    Store(164, 4096);
                    Store(168, 0);
                    Store(172, 0);
                }
                else if (unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(context + 172, 4))) != angle)
                {
                    Store(164, unchecked((uint)NativeBattleCameraMath.Cosine(angle)));
                    Store(168, unchecked((uint)NativeBattleCameraMath.Sine(angle)));
                    Store(172, unchecked((uint)(int)angle));
                }
            }
            short width = (short)(quad[0] * 8);
            short height = (short)(quad[2] * 8);
            NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[4];
            for (int corner = 0; corner < 4; corner++)
            {
                short x = (corner & 1) == 0 ? unchecked((short)-width) : width;
                short y = corner < 2 ? unchecked((short)-height) : height;
                StoreWord(68 + corner * 8, unchecked((ushort)x));
                StoreWord(70 + corner * 8, unchecked((ushort)y));
                if (corner < 3)
                {
                    projected[corner] = NativeBattleModuleProjection.Project45FE10(current, new(x, y, 0), projection);
                }
            }
            StoreMatrix(ref staging, context + 100, current);
            uint firstFlags = projected[0].Flags | projected[1].Flags | projected[2].Flags;
            Store(64, firstFlags);
            if ((firstFlags & 0x60000) == 0 && !depthStored)
            {
                projected[3] = NativeBattleModuleProjection.Project45FE10(current, new(width, height, 0), projection);
                Store(60, (uint)projected.Sum(point => (int)point.Depth) >> 4);
                depthStored = true;
            }
        });
        if (rolled is null)
        {
            throw new InvalidDataException("Native The End sprite lost its executed rotation-copy owner.");
        }
        return Empty(staging) with { Packets = batch };
    }

    private static NativeBattleActionMatrix Matrix(NativeBattleTheEnd85DrawServices services, uint address)
    {
        byte[] rotation = Read(services, address, 18);
        byte[] translation = Read(services, address + 20, 12);
        return new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(translation), BinaryPrimitives.ReadInt32LittleEndian(translation.AsSpan(4)), BinaryPrimitives.ReadInt32LittleEndian(translation.AsSpan(8)), 4096);
    }

    private static void StoreMatrix(ref NativeBattleFileStaging staging, uint address, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            staging = staging.Write(address + (uint)index * 2, BitConverter.GetBytes(matrix.Rotation[index]));
        }
        staging = staging.Write(address + 20, BitConverter.GetBytes(matrix.X)).Write(address + 24, BitConverter.GetBytes(matrix.Y)).Write(address + 28, BitConverter.GetBytes(matrix.Z));
    }

    private static void SetWindows(ref NativeBattleFileStaging staging, uint context)
    {
        foreach (int offset in new[] { 24, 26, 28, 30, 36, 38 })
        {
            staging = staging.Write(context + (uint)offset, BitConverter.GetBytes((ushort)0));
        }
        foreach (int offset in new[] { 32, 34, 40, 42 })
        {
            staging = staging.Write(context + (uint)offset, BitConverter.GetBytes((ushort)256));
        }
    }

    private static byte[] Read(NativeBattleTheEnd85DrawServices services, uint address, int count)
    {
        byte[] bytes = services.Read(address, count);
        return bytes.Length == count ? bytes : throw new InvalidDataException("Native The End node requires its exact addressed source extent.");
    }

    private static ushort Word(NativeBattleTheEnd85DrawServices services, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(services, address, 2));
    private static uint Dword(NativeBattleTheEnd85DrawServices services, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(services, address, 4));
    private static NativeBattleTheEnd85NodeDrawingStep Empty(NativeBattleFileStaging staging) => new(staging, new([], 0), false, null, null, null);
}
