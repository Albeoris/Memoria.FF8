using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuake38WallDraw(ImmutableArray<byte> Entry, NativeBattleGfGeometryBatch Batch, bool SpawnChip, NativeBattleVector First, NativeBattleVector UpperFirst);

public static class NativeBattleQuake38GroundBorders
{
    public static NativeBattleGfGeometryBatch Crack8D9F70(ImmutableArray<byte> entry, NativeBattleVector first, NativeBattleVector second, int edge, int border, short crackTick, ImmutableArray<byte> template, NativeBattleQuake38Resources resources, NativeBattleCameraProjection projection, short zsf4, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        Require(entry, edge, border);
        if (crackTick <= entry[92 + edge])
        {
            return new([], 0);
        }
        short angle = unchecked((short)(NativePrecision53.RoundInteger(Math.Atan2(first.X - second.X, first.Z - second.Z) * (double)BitConverter.Int32BitsToSingle(0x4422F983)) & 0xFFF));
        NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), angle);
        NativeBattleVector width = NativeBattleCureActorPackets.TransformBone4607E0(new(new(-192, 0, 0), 0), [rotation]);
        ImmutableArray<NativeBattleVector> vertices = [Offset(first, width.X, width.Z), Offset(first, -width.X, -width.Z), Offset(second, width.X, width.Z), Offset(second, -width.X, -width.Z)];
        int shade = entry[64 + edge * 3 + border];
        if ((uint)shade >= (uint)resources.StepShade.Length)
        {
            throw new InvalidDataException("Native Quake crack shade leaves its original table.");
        }
        (ImmutableArray<byte> bytes, ImmutableArray<NativeBattleGfPacketByteState> mask) = Template(template, resources.StepShade[shade], 0x3C54);
        return NativeBattleQuake38QuadPackets.Emit(vertices, bytes, mask, projection, zsf4, pilotIdentity: pilotIdentity);
    }

    public static NativeBattleQuake38WallDraw Wall8DA200(ImmutableArray<byte> entry, NativeBattleVector first, NativeBattleVector second, int edge, int border, short rippleTick, short chipCount, ImmutableArray<byte> template, NativeBattleQuake38Resources resources, NativeBattleCameraProjection projection, short zsf4, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        Require(entry, edge, border);
        int slot = 64 + edge * 3 + border;
        byte step = entry[slot];
        if (rippleTick == 0 || step > 12)
        {
            return new(entry, new([], 0), false, first, first);
        }
        if (step >= resources.WallDepth.Length || step + 1 >= resources.StepShade.Length)
        {
            throw new InvalidDataException("Native Quake wall step leaves its original depth or shade table.");
        }
        NativeBattleVector upperFirst = first with { Y = unchecked((short)(first.Y + resources.WallDepth[step])) };
        NativeBattleVector upperSecond = second with { Y = unchecked((short)(second.Y + resources.WallDepth[step])) };
        byte[] updated = entry.ToArray();
        updated[slot] = (byte)(step + 1);
        (ImmutableArray<byte> bytes, ImmutableArray<NativeBattleGfPacketByteState> mask) = Template(template, resources.StepShade[step + 1], null);
        NativeBattleGfGeometryBatch batch = NativeBattleQuake38QuadPackets.Emit([upperFirst, upperSecond, first, second], bytes, mask, projection, zsf4, pilotIdentity: pilotIdentity);
        return new(updated.ToImmutableArray(), batch, batch.AllocationBytes != 0 && chipCount < 90, upperFirst, first);
    }

    public static NativeBattleGfGeometryBatch Side8DA3E0(ImmutableArray<byte> entry, NativeBattleVector first, NativeBattleVector second, int firstSelector, int secondSelector, short height, NativeBattleCameraProjection projection, short zsf4, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (entry.IsDefault || entry.Length != 112 || (uint)firstSelector >= 5 || (uint)secondSelector >= 5)
        {
            throw new InvalidDataException("Native Quake side requires its split entry and actual selectors.");
        }
        NativeBattleVector lowerFirst = first with { Y = unchecked((short)(first.Y + 16 - height)) };
        NativeBattleVector lowerSecond = second with { Y = unchecked((short)(second.Y + 16 - height)) };
        byte[] bytes = new byte[40];
        NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 40).ToArray();
        Write(0, BitConverter.GetBytes(0x09000000u));
        Write(4, entry.AsSpan().Slice(44, 4).ToArray());
        Write(14, entry.AsSpan().Slice(50, 2).ToArray());
        Write(22, entry.AsSpan().Slice(48, 2).ToArray());
        foreach (int offset in new[] { 12, 28 })
        {
            Write(offset, [entry[100 + firstSelector], entry[105 + firstSelector]]);
        }
        foreach (int offset in new[] { 20, 36 })
        {
            Write(offset, [entry[100 + secondSelector], entry[105 + secondSelector]]);
        }
        return NativeBattleQuake38QuadPackets.Emit([first, second, lowerFirst, lowerSecond], bytes.ToImmutableArray(), mask.ToImmutableArray(), projection, zsf4, pilotIdentity: pilotIdentity);

        void Write(int offset, byte[] value)
        {
            value.CopyTo(bytes, offset);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, value.Length);
        }
    }

    private static NativeBattleVector Offset(NativeBattleVector point, int x, int z) => new(unchecked((short)(point.X + x)), point.Y, unchecked((short)(point.Z + z)));

    private static void Require(ImmutableArray<byte> entry, int edge, int border)
    {
        if (entry.IsDefault || entry.Length != 112 || (uint)edge >= 4 || (uint)border >= 3)
        {
            throw new InvalidDataException("Native Quake border requires its original split entry and edge selectors.");
        }
    }

    private static (ImmutableArray<byte> Bytes, ImmutableArray<NativeBattleGfPacketByteState> Mask) Template(ImmutableArray<byte> template, byte shade, ushort? clut)
    {
        if (template.IsDefault || template.Length != 40)
        {
            throw new InvalidDataException("Native Quake requires its actual forty-byte border template.");
        }
        byte[] bytes = template.ToArray();
        NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 40).ToArray();
        foreach ((int offset, int length) in new[] { (0, 8), (12, 4), (20, 4), (28, 4), (36, 4) })
        {
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, length);
        }
        bytes[4] = shade;
        bytes[5] = shade;
        bytes[6] = shade;
        if (clut is { } value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), value);
        }
        return (bytes.ToImmutableArray(), mask.ToImmutableArray());
    }
}
