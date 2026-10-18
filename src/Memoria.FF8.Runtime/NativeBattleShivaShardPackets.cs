using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleShivaShardPackets
{
    public static NativeBattleGfGeometryAttempt EmitTriangle5C2410(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, bool gouraud, bool animated, NativeBattleGfGeometryContext context, short zsf3, NativeBattleSummonColors? colors = null, int byteOffsetRelative = 0, Action<ushort>? observeDepth = null, Action<int>? observeDepthCue = null)
    {
        if (face.Length != (gouraud ? 28 : 20) || (context.Flags & ~0x33u) != 0 || context.DepthBias != 0 || context.DepthShift > 31 || context.TexturePage is not null || context.Clut is not null)
        {
            throw new InvalidDataException("Native Shiva shard triangles require their original FT3/GT3 records and unchanged material/depth operands.");
        }
        byte[] bytes = new byte[gouraud ? 40 : 32];
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[bytes.Length];
        Store(0, gouraud ? 0x09000000u : 0x07000000u);
        Store(4, BinaryPrimitives.ReadUInt32LittleEndian(face));
        Store(12, BinaryPrimitives.ReadUInt32LittleEndian(face[12..]));
        if (gouraud)
        {
            Store(24, BinaryPrimitives.ReadUInt32LittleEndian(face[16..]));
            Store(36, BinaryPrimitives.ReadUInt16LittleEndian(face[10..]));
            if (!animated)
            {
                Store(16, BinaryPrimitives.ReadUInt32LittleEndian(face[20..]));
                Store(28, BinaryPrimitives.ReadUInt32LittleEndian(face[24..]));
            }
        }
        else
        {
            Store(20, BinaryPrimitives.ReadUInt32LittleEndian(face[16..]));
            Store(28, BinaryPrimitives.ReadUInt16LittleEndian(face[10..]));
        }
        NativeBattleModuleProjectedVertex first = readCorner(0);
        NativeBattleModuleProjectedVertex second = readCorner(1);
        NativeBattleModuleProjectedVertex third = readCorner(2);
        observeDepth?.Invoke(first.Depth);
        observeDepth?.Invoke(second.Depth);
        observeDepth?.Invoke(third.Depth);
        if (((first.Flags | second.Flags | third.Flags) & 0x60000) != 0)
        {
            return Finish(null);
        }
        int nclip = NativeBattleModulePackets.Nclip(first.Screen, second.Screen, third.Screen);
        uint negativeFlag = animated || gouraud ? 0x20u : 0x10u;
        if (nclip == 0 || nclip < 0 && (context.Flags & negativeFlag) == 0)
        {
            return Finish(null);
        }
        Store(8, first.Screen);
        Store(gouraud ? 20 : 16, second.Screen);
        Store(gouraud ? 32 : 24, third.Screen);
        if (animated)
        {
            NativeBattleSummonColors rules = colors ?? throw new NotSupportedException("Native animated Shiva shards require their original GTE depth-cue constants.");
            observeDepthCue?.Invoke(context.DepthCue);
            uint firstColor = BinaryPrimitives.ReadUInt32LittleEndian(face);
            if (gouraud)
            {
                ImmutableArray<uint> shaded = NativeBattleGteColorInterpolation.Gouraud45F4C0([firstColor, BinaryPrimitives.ReadUInt32LittleEndian(face[20..]), BinaryPrimitives.ReadUInt32LittleEndian(face[24..])], false, context.DepthCue, context.FarColor, rules);
                Store(4, shaded[0]);
                Store(16, shaded[1]);
                Store(28, shaded[2]);
            }
            else
            {
                Store(4, rules.Interpolate(firstColor, context.DepthCue, context.FarColor));
            }
        }
        uint average = unchecked((uint)(first.Depth + second.Depth + third.Depth) * (uint)(int)zsf3) >> 12;
        int bucket = unchecked(context.TableBaseBucket + (unchecked((int)average) >> context.DepthShift));
        ImmutableArray<uint>.Builder words = ImmutableArray.CreateBuilder<uint>(bytes.Length / 4);
        for (int offset = 0; offset < bytes.Length; offset += 4)
        {
            words.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)));
        }
        return Finish(new(bucket, words.MoveToImmutable(), [first.Depth, second.Depth, third.Depth, 0]));

        void Store(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
        }

        NativeBattleGfGeometryAttempt Finish(NativeBattleGeometryPrimitive? primitive) => new(byteOffsetRelative, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive);
    }
}
