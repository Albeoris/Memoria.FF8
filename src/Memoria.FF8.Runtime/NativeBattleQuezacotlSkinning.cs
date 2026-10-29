using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum NativeBattleQuezacotlSkinBank
{
    VertexScratch,
    Common,
    Effect
}

public sealed record NativeBattleQuezacotlSkinWrite(NativeBattleQuezacotlSkinBank Bank, NativeBattleGfGeometryAttempt Attempt);
public sealed record NativeBattleQuezacotlSkinBatch(ImmutableArray<NativeBattleQuezacotlSkinWrite> Writes, int CommonAllocationBytes, int EffectAllocationBytes);
public sealed record NativeBattleQuezacotlSkinContext(uint ModelMask, uint ModelRgb, uint OverlayRgb, short ClipLeft, short ClipTop, short ClipRight, short ClipBottom, ushort Clut, ushort TexturePage, int UOffset, int VOffset, byte UPeriod, byte VPeriod, int FadeOriginY, ImmutableArray<uint> WindowBefore3, ImmutableArray<uint> WindowAfter3, int TableBaseBucket, byte DepthShift)
{
    public ushort? PriorDepthFifo { get; init; }
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleQuezacotlSkinnedVertex(byte Flags, uint? Screen, ushort? QuarterDepth, byte? U, byte? V, short? Fade)
{
    public System.Numerics.Vector3? World { get; init; }
}
public sealed record NativeBattleQuezacotlSkinProjection(NativeBattleQuezacotlSkinnedVertex Vertex, ImmutableArray<NativeBattleQuezacotlSkinWrite> Writes);

/// <summary>6C68F0 mesh/bone parsing and 6C74D0's model-space skinning followed by the prior camera projection.</summary>
public static class NativeBattleQuezacotlSkinning
{
    public static NativeBattleQuezacotlSkinBatch Emit6C68F0(ImmutableArray<byte> geometry, Func<int, NativeBattleActionMatrix> readBoneMatrix, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleQuezacotlSkinContext context, NativeBattleSummonColors colors)
    {
        if (geometry.IsDefault || context.DepthShift > 31)
        {
            throw new InvalidDataException("Native6C68F0 requires its current geometry source and actual draw context.");
        }
        int meshes = BinaryPrimitives.ReadInt32LittleEndian(Read(geometry, 0, 4));
        ImmutableArray<NativeBattleQuezacotlSkinWrite>.Builder writes = ImmutableArray.CreateBuilder<NativeBattleQuezacotlSkinWrite>();
        int common = 0;
        int effect = 0;
        for (int mesh = 0; mesh < meshes; mesh++)
        {
            int cursor = BinaryPrimitives.ReadInt32LittleEndian(Read(geometry, checked(4 + mesh * 4), 4));
            // Original SHL uses CL, so object indices32..63 reuse bits0..31.
            if ((context.ModelMask & (1u << mesh)) == 0)
            {
                continue;
            }
            int bones = BinaryPrimitives.ReadInt16LittleEndian(Read(geometry, cursor, 2));
            cursor = checked(cursor + 2);
            ImmutableArray<NativeBattleQuezacotlSkinnedVertex>.Builder vertices = ImmutableArray.CreateBuilder<NativeBattleQuezacotlSkinnedVertex>();
            for (int bone = 0; bone < bones; bone++)
            {
                short selector = BinaryPrimitives.ReadInt16LittleEndian(Read(geometry, cursor, 2));
                int count = BinaryPrimitives.ReadInt16LittleEndian(Read(geometry, checked(cursor + 2), 2));
                cursor = checked(cursor + 4);
                if (count < 0)
                {
                    throw new NotSupportedException("Native6C74D0 negative vertex count reaches an unbounded original loop.");
                }
                NativeBattleActionMatrix matrix = readBoneMatrix(selector);
                List<NativeBattleQuezacotlSkinProjection> group = new(count);
                for (int index = 0; index < count; index++)
                {
                    ReadOnlySpan<byte> xyz = Read(geometry, cursor, 6);
                    NativeBattleVector local = new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz[2..]), BinaryPrimitives.ReadInt16LittleEndian(xyz[4..]));
                    NativeBattleQuezacotlSkinProjection transformed = Project6C74D0(local, matrix, view, projection, context, vertices.Count * 12);
                    vertices.Add(transformed.Vertex);
                    group.Add(transformed);
                    cursor = checked(cursor + 6);
                }
                // 6C74D0 skins the whole bone group before projecting its scratch records.
                writes.AddRange(group.Select(item => item.Writes[0]));
                writes.AddRange(group.Select(item => item.Writes[1]));
            }
            cursor = checked((cursor + 3) & ~3);
            ushort triangles = BinaryPrimitives.ReadUInt16LittleEndian(Read(geometry, cursor, 2));
            ushort quads = BinaryPrimitives.ReadUInt16LittleEndian(Read(geometry, checked(cursor + 2), 2));
            cursor = checked(cursor + 12);
            for (int group = 0; group < 2; group++)
            {
                bool quad = group != 0;
                int count = quad ? quads : triangles;
                int length = quad ? 20 : 16;
                for (int index = 0; index < count; index++)
                {
                    ReadOnlySpan<byte> face = Read(geometry, cursor, length);
                    NativeBattleQuezacotlSkinnedVertex[] corners = new NativeBattleQuezacotlSkinnedVertex[quad ? 4 : 3];
                    for (int corner = 0; corner < corners.Length; corner++)
                    {
                        int vertex = BinaryPrimitives.ReadUInt16LittleEndian(face[(corner * 2)..]) & 0xFFF;
                        if (vertex >= vertices.Count)
                        {
                            throw new NotSupportedException("Native6C6A00 requires a retained vertex outside the current mesh's executed skinning producer.");
                        }
                        corners[corner] = vertices[vertex];
                    }
                    NativeBattleQuezacotlSkinBatch emitted = NativeBattleQuezacotlSkinnedPackets.Emit6C6A00(face, corners, context with { PilotIdentity = context.PilotIdentity is { } identity ? identity with { Primitive = unchecked(identity.Primitive * 65537 + cursor) } : null }, colors, common, effect);
                    writes.AddRange(emitted.Writes);
                    common = checked(common + emitted.CommonAllocationBytes);
                    effect = checked(effect + emitted.EffectAllocationBytes);
                    cursor = checked(cursor + length);
                }
            }
        }
        return new(writes.ToImmutable(), common, effect);
    }

    public static NativeBattleQuezacotlSkinProjection Project6C74D0(NativeBattleVector local, NativeBattleActionMatrix bone, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleQuezacotlSkinContext context, int scratchOffset = 0)
    {
        NativeBattleVector skinned = NativeBattleGfMatrices.Transform460840(bone, local);
        SkinWrites initial = new(12, scratchOffset);
        initial.Word16(0, unchecked((ushort)skinned.X));
        initial.Word16(2, unchecked((ushort)skinned.Y));
        initial.Word16(4, unchecked((ushort)skinned.Z));
        NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(view, skinned, projection);
        ushort quarter = (ushort)(projected.Depth >> 2);
        SkinWrites result = new(12, scratchOffset);
        if (quarter == 0)
        {
            result.Byte(6, 0x10);
            return new(new(0x10, null, null, null, null, null), [new(NativeBattleQuezacotlSkinBank.VertexScratch, initial.Attempt(null)), new(NativeBattleQuezacotlSkinBank.VertexScratch, result.Attempt(null))]);
        }
        short sx = unchecked((short)projected.Screen);
        short sy = unchecked((short)(projected.Screen >> 16));
        byte flags = 0;
        if (sx < context.ClipLeft * 8)
        {
            flags |= 1;
        }
        else if (sx >= context.ClipRight * 8)
        {
            flags |= 2;
        }
        if (sy < context.ClipTop * 8)
        {
            flags |= 4;
        }
        else if (sy >= context.ClipBottom * 8)
        {
            flags |= 8;
        }
        (int irX, int irY, _) = NativeBattleGfMatrices.Rotate56C3D0(view, skinned);
        irX = Math.Clamp(unchecked(irX + view.X), short.MinValue, short.MaxValue);
        irY = Math.Clamp(unchecked(irY + view.Y), short.MinValue, short.MaxValue);
        byte u = unchecked((byte)(irX >> 3));
        byte v = unchecked((byte)(irY >> 3));
        short fade = (short)Math.Clamp(unchecked((skinned.Y - context.FadeOriginY) * 8 + 2048), 0, 4096);
        result.Word(0, projected.Screen);
        result.Word16(4, quarter);
        result.Byte(6, flags);
        result.Byte(7, u);
        result.Byte(8, v);
        //6C74D0 copies contextBYTE25, never initialized by6C6770 and never consumed as vertex padding.
        result.Unknown(9, 1);
        result.Word16(10, unchecked((ushort)fade));
        return new(new(flags, projected.Screen, quarter, u, v, fade) { World = context.PilotIdentity.HasValue ? NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(view, skinned), projection) : null }, [new(NativeBattleQuezacotlSkinBank.VertexScratch, initial.Attempt(null)), new(NativeBattleQuezacotlSkinBank.VertexScratch, result.Attempt(null))]);
    }

    private static ReadOnlySpan<byte> Read(ImmutableArray<byte> source, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > source.Length - length)
        {
            throw new InvalidDataException("Native6C68F0 access exceeds its current loaded model geometry.");
        }
        return source.AsSpan().Slice(offset, length);
    }
}

internal sealed class SkinWrites(int length, int offset)
{
    private readonly byte[] _data = new byte[length];
    private readonly NativeBattleGfPacketByteState[] _mask = new NativeBattleGfPacketByteState[length];

    public void Word(int position, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_data.AsSpan(position), value);
        _mask.AsSpan(position, 4).Fill(NativeBattleGfPacketByteState.Known);
    }

    public void Word16(int position, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(_data.AsSpan(position), value);
        _mask.AsSpan(position, 2).Fill(NativeBattleGfPacketByteState.Known);
    }

    public void Byte(int position, byte value)
    {
        _data[position] = value;
        _mask[position] = NativeBattleGfPacketByteState.Known;
    }

    public void Unknown(int position, int count)
    {
        _mask.AsSpan(position, count).Fill(NativeBattleGfPacketByteState.Unknown);
    }

    public ImmutableArray<uint> Words()
    {
        ImmutableArray<uint>.Builder result = ImmutableArray.CreateBuilder<uint>(length / 4);
        for (int index = 0; index < length; index += 4)
        {
            result.Add(BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(index)));
        }
        return result.ToImmutable();
    }

    public NativeBattleGfGeometryAttempt Attempt(NativeBattleGeometryPrimitive? primitive)
    {
        return new(offset, _data.ToImmutableArray(), _mask.ToImmutableArray(), primitive);
    }
}
