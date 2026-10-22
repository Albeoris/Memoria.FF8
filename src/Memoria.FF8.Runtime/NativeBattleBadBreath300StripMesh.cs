using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBadBreath300StripMeshStep(NativeBattleGfGeometryBatch Batch, NativeBattleEffectArena Context92, NativeBattleGeometryRegisterStep Registers);

public static partial class NativeBattleBadBreath300StripMesh
{
    public const int ContextLength = 92;

    public static NativeBattleBadBreath300StripMeshStep Draw66F710(ImmutableArray<byte> source, uint sourceAddress, NativeBattleEffectArena context92, uint selectedVerticesAddress, Func<int, (NativeBattleVector Vertex, short? W)> readVertex, NativeBattleGeometryRegisterStep registers, NativeBattleCameraProjection projection, short dqa, int dqb, short? zsf4, NativeBattleSummonColors constants, int tableBaseBucket = 17, byte depthShift = 2)
    {
        context92.Validate();
        if (context92.Regions.Any(region => region.Offset < 0 || region.Offset > ContextLength - region.Data.Length) || context92.UnknownRegions.Any(region => region.Offset < 0 || region.Offset > ContextLength - region.Length) || source.IsDefault || source.Length < 8 || U32(context92, 0) != sourceAddress || U32(context92, 0x20) != selectedVerticesAddress || depthShift > 31)
        {
            throw new InvalidDataException("Bad Breath strip drawing requires its actual mutable source,92-byte context and selected vertex owner.");
        }
        registers.Geometry.Validate();
        registers.Colors.Validate();
        int directory = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan()));
        context92 = Dword(context92, 0x24, checked(sourceAddress + (uint)directory + 12));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        Pass(false);
        context92 = Dword(context92, 0x24, checked(U32(context92, 0x24) + 12));
        Pass(true);
        return new(new(attempts.ToImmutable(), allocated), context92, registers);

        void Pass(bool gouraud)
        {
            int cursor = Offset(U32(context92, 0x24));
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(cursor, 4));
            cursor = checked(cursor + 4);
            context92 = Dword(context92, 0x24, checked(sourceAddress + (uint)cursor));
            int sourceStride = gouraud ? 36 : 24;
            int packetLength = gouraud ? 52 : 40;
            int screenStride = gouraud ? 12 : 8;
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                byte[] face = Read(cursor, sourceStride).ToArray();
                for (int corner = 0; corner < 3; corner++)
                {
                    var vertex = Vertex(face, corner);
                    registers = NativeBattleGeometry.LoadVector(registers, corner, vertex.Vertex, vertex.W);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, dqa, dqb);
                SkinWrites packet = new(packetLength, allocated);
                packet.Word(0, gouraud ? 0x0C000000u : 0x09000000u);
                packet.Word(4, BinaryPrimitives.ReadUInt32LittleEndian(face));
                uint firstUv = BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(12));
                uint secondUv = BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(16));
                uint finalUv = BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(20));
                int[] uvOffsets = gouraud ? [12, 24, 36, 48] : [12, 20, 28, 36];
                uint[] uvWords = [firstUv, secondUv, finalUv, finalUv >> 16];
                for (int corner = 0; corner < 4; corner++)
                {
                    packet.Word(uvOffsets[corner], uvWords[corner]);
                }
                uint firstFlags = registers.Colors.Flags ?? throw new NotSupportedException("Bad Breath strip drawing requires its actual RTPT flag result.");
                context92 = Dword(context92, 0x34, firstFlags);
                if ((firstFlags & 0x60000) != 0)
                {
                    attempts.Add(packet.Attempt(null));
                    cursor = checked(cursor + sourceStride);
                    continue;
                }
                uint[] screen = new uint[4];
                for (int corner = 0; corner < 3; corner++)
                {
                    screen[corner] = registers.Geometry.Sxy[corner] ?? throw new NotSupportedException("Bad Breath strip drawing requires its actual screen FIFO.");
                    packet.Word(8 + corner * screenStride, screen[corner]);
                }
                var fourth = Vertex(face, 3);
                registers = NativeBattleGeometry.LoadVector(registers, 0, fourth.Vertex, fourth.W);
                registers = NativeBattleGeometry.ProjectSingle(registers, projection, dqa, dqb);
                screen[3] = registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Bad Breath strip drawing requires its actual fourth projected screen point.");
                packet.Word(8 + 3 * screenStride, screen[3]);
                registers = NativeBattleGeometry.Average45E610(registers, zsf4 ?? throw new NotSupportedException("Bad Breath strip drawing requires its reached ZSF4 owner."));
                if (screen.All(value => Outside(unchecked((short)value), 2560)) || screen.All(value => Outside(unchecked((short)(value >> 16)), 1728)))
                {
                    attempts.Add(packet.Attempt(null));
                    cursor = checked(cursor + sourceStride);
                    continue;
                }
                uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("Bad Breath strip drawing requires its actual averaged depth.");
                context92 = Dword(context92, 0x30, depth);
                int colorCount = gouraud ? 4 : 1;
                for (int corner = 0; corner < colorCount; corner++)
                {
                    byte factor = face[corner == 0 ? 0 : 20 + corner * 4];
                    byte[] color = context92.Read(4, 3);
                    registers = registers with { Colors = registers.Colors with { Ir0 = (uint)factor << 5, Ir = [color[0], color[1], color[2]] } };
                    registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, constants);
                    for (int channel = 0; channel < 3; channel++)
                    {
                        int value = registers.Colors.Ir[channel] ?? throw new NotSupportedException("Bad Breath strip drawing requires its reached color IR output.");
                        packet.Byte(4 + corner * 12 + channel, unchecked((byte)value));
                    }
                }
                ushort uOffset = U16(context92, 0x0C);
                ImmutableArray<byte> adjusted = NativeBattleEffectTextureWindows.OffsetAxis(uvWords.Select(value => unchecked((byte)value)), uOffset, unchecked((byte)U16(context92, 0x1C)));
                for (int corner = 0; corner < 4; corner++)
                {
                    packet.Byte(uvOffsets[corner], adjusted[corner]);
                }
                int bucket = checked(tableBaseBucket + (unchecked((int)depth) >> depthShift));
                ImmutableArray<int> payload = registers.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Bad Breath strip drawing requires its actual depth FIFO.")))).ToImmutableArray();
                NativeBattleGfGeometryAttempt stores = packet.Attempt(null);
                NativeBattleGeometryPrimitive primitive = new(bucket, packet.Words(), payload) { PhysicalWriteMask = stores.WriteMask };
                AddBracket(stores, primitive, Window(context92, 0x10), Window(context92, 0x18), attempts);
                allocated = checked(allocated + packetLength + 32);
                cursor = checked(cursor + sourceStride);
            }
            context92 = Dword(context92, 0x24, checked(sourceAddress + (uint)cursor));
        }

        (NativeBattleVector Vertex, short? W) Vertex(byte[] face, int corner)
        {
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + 2 * corner, 2)) * 4;
            return readVertex(offset);
        }

        int Offset(uint address)
        {
            long offset = (long)address - sourceAddress;
            if (offset < 0 || offset > source.Length)
            {
                throw new InvalidDataException("Bad Breath strip face pointer leaves its actual source owner.");
            }
            return (int)offset;
        }

        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (offset < 0 || length < 0 || offset > source.Length - length)
            {
                throw new InvalidDataException("Bad Breath strip reads outside its actual mutable mesh source.");
            }
            return source.AsSpan(offset, length);
        }
    }

    private static bool Outside(short value, int maximum) => value < 0 || value > maximum;
    private static ushort U16(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleTextureWindow Window(NativeBattleEffectArena arena, int offset) => new(unchecked((short)U16(arena, offset)), unchecked((short)U16(arena, offset + 2)), unchecked((short)U16(arena, offset + 4)), unchecked((short)U16(arena, offset + 6)));
}
