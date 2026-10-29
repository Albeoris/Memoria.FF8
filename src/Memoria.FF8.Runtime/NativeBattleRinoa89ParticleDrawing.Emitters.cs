using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRinoa89ParticleDrawing
{
    public static NativeBattleRinoa89ParticleDraw DrawEmitters6FC500(NativeBattleRinoa89Frame frame, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Func<uint, int, byte[]> readResource, bool captureVisual = false)
    {
        NativeBattleEffectArena arena = ParticleArena(frame);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 76);
        NativeBattleFileStaging staging = allocation.Staging;
        uint temporary = allocation.Address;
        staging = staging.Write(temporary + 32, new byte[20]);
        staging = Word(Word(Word(staging, temporary + 32, 4096), temporary + 40, 4096), temporary + 48, 4096);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < 128; index++)
        {
            int offset = 16960 + index * 48;
            if (arena.Read(offset, 1)[0] == 0)
            {
                continue;
            }
            byte type = arena.Read(offset + 1, 1)[0];
            byte[] descriptor = readResource(checked(0x14AF510u + type * 16u), 7);
            uint source = BinaryPrimitives.ReadUInt32LittleEndian(descriptor);
            uint faceAddress = checked(source + BinaryPrimitives.ReadUInt32LittleEndian(readResource(source, 4)) + 16);
            byte[] face = readResource(faceAddress, 24);
            short[] rotation = new short[9];
            for (int component = 0; component < 9; component++)
            {
                rotation[component] = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset + 16 + component * 2, 2));
            }
            short sx = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset + 4, 2));
            short sy = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset + 6, 2));
            short sz = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset + 8, 2));
            staging = Word(Word(Word(staging, temporary + 32, unchecked((ushort)sx)), temporary + 40, unchecked((ushort)sy)), temporary + 48, unchecked((ushort)sz));
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Multiply56C090(new(rotation.ToImmutableArray(), 0, 0, 0, 4096), new([sx, 0, 0, 0, sy, 0, 0, 0, sz], 0, 0, 0, 4096));
            int tx = BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset + 36, 4));
            int ty = BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset + 40, 4));
            int tz = BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset + 44, 4));
            (int x, int y, int z) = Transform(view, unchecked((short)tx), unchecked((short)ty), unchecked((short)tz));
            NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.MultiplyGteRotation45E470(view, local) with { X = x, Y = y, Z = z };
            staging = Matrix(staging, temporary, matrix, false).MarkUnknown(temporary + 18, 2);
            NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[4];
            System.Numerics.Vector3[]? world = captureVisual ? new System.Numerics.Vector3[4] : null;
            for (int corner = 0; corner < 3; corner++)
            {
                vertices[corner] = NativeBattleModuleProjection.Project45FE10(matrix, Vertex(corner), projection);
            }
            uint flags = vertices[0].Flags | vertices[1].Flags | vertices[2].Flags;
            staging = Dword(staging, temporary + 64, flags);
            byte[] packet = new byte[40];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[40];
            Store32(0, 0x9000000);
            Store32(4, BinaryPrimitives.ReadUInt32LittleEndian(face));
            Store32(12, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(12)));
            Store32(20, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(16)));
            uint uv = BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(20));
            Store32(28, uv);
            Store32(36, uv >> 16);
            if ((flags & 0x60000) != 0)
            {
                attempts.Add(new(allocated, packet.ToImmutableArray(), mask.ToImmutableArray(), null));
                continue;
            }
            Store32(8, vertices[0].Screen);
            Store32(16, vertices[1].Screen);
            Store32(24, vertices[2].Screen);
            vertices[3] = NativeBattleModuleProjection.Project45FAA0(matrix, Vertex(3), projection, 0, 0);
            Store32(32, vertices[3].Screen);
            int average = vertices.Sum(vertex => (int)vertex.Depth) >> 4;
            short fade = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset + 2, 2));
            staging = Dword(Dword(staging, temporary + 68, unchecked((uint)average)), temporary + 72, unchecked((uint)(int)fade));
            uint farColor = (uint)(descriptor[4] | descriptor[5] << 8 | descriptor[6] << 16);
            Store32(4, colors.Interpolate(BinaryPrimitives.ReadUInt32LittleEndian(face), fade, farColor));
            ImmutableArray<uint> words = Enumerable.Range(0, 10).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(word * 4))).ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(checked(17 + (average >> 2)), words, vertices.Select(vertex => (int)vertex.Depth).ToImmutableArray());
            if (world is not null)
            {
                primitive = primitive with { PilotGeometry = new(NativeBattleRinoa89Visuals.Row(arena, 16960, index), world[0], world[1], world[2], world[3], 4) };
            }
            attempts.Add(new(allocated, packet.ToImmutableArray(), mask.ToImmutableArray(), primitive));
            allocated += 40;

            NativeBattleVector Vertex(int corner)
            {
                ushort selector = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2));
                byte[] bytes = readResource(checked(source + 8u + selector * 4u), 6);
                NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
                if (world is not null)
                {
                    world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, point), projection);
                }
                return point;
            }
            void Store32(int displacement, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(displacement), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, displacement, 4);
            }
        }
        return new(frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 76) }, new(attempts.ToImmutable(), allocated));
    }
}


