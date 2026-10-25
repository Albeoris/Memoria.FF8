using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicVertexColors(ImmutableArray<uint> Colors, ImmutableArray<bool> HighKnown);

public static partial class NativeBattleCinematicActorVertexColor20
{
    public static NativeBattleWaterExecutionFrame UpdateColors(NativeBattleWaterExecutionFrame frame, uint state, uint destination, short radius, short intensity, short phase, uint actorColor, NativeBattleVector reference)
    {
        int count = BinaryPrimitives.ReadInt16LittleEndian(Read(state, 2));
        uint distances = BinaryPrimitives.ReadUInt32LittleEndian(Read(state + 4, 4));
        uint palette = BinaryPrimitives.ReadUInt32LittleEndian(Read(state + 12, 4));
        if (count <= 0)
        {
            return frame;
        }
        ImmutableArray<uint> emptyRamp = new uint[256].ToImmutableArray();
        for (int index = 0; index < count; index++)
        {
            ushort distance = BinaryPrimitives.ReadUInt16LittleEndian(Read(distances + 2u * (uint)index, 2));
            uint address = destination + 4u * (uint)index;
            (byte[] bytes, _) = frame.Arena.ReadSnapshot(ArenaOffset(address, 4), 4);
            ImmutableArray<uint> ramp = emptyRamp;
            int signedDistance = unchecked((short)distance);
            if (signedDistance < radius)
            {
                if (radius == 0)
                {
                    throw new InvalidDataException("Native actor glow reached its original zero divisor.");
                }
                int paletteIndex = unchecked((signedDistance << 8) / radius + phase) & 255;
                byte[] rgb = Read(palette + 4u * (uint)paletteIndex, 3);
                uint color = (uint)(rgb[0] | rgb[1] << 8 | rgb[2] << 16);
                ramp = ramp.SetItem(paletteIndex, color);
            }
            int written = WriteGlow(bytes, distance, radius, intensity, phase, actorColor, reference, ramp);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, written), bytes.AsSpan(0, written)) };
        }
        return frame;

        byte[] Read(uint address, int length) => frame.Arena.Read(ArenaOffset(address, length), length);
    }

    public static NativeBattleCinematicVertexColors ReadObjectColors(NativeBattleWaterExecutionFrame frame, uint state, uint colorBase, int objectIndex, int vertexCount)
    {
        if (objectIndex < 0 || vertexCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(objectIndex));
        }
        short offset = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(state + 0x10u + 2u * (uint)objectIndex, 2), 2));
        uint address = unchecked(colorBase + (uint)(offset * 2));
        var colors = ImmutableArray.CreateBuilder<uint>(vertexCount);
        var known = ImmutableArray.CreateBuilder<bool>(vertexCount);
        for (int index = 0; index < vertexCount; index++)
        {
            (byte[] data, bool[] mask) = frame.Arena.ReadSnapshot(ArenaOffset(address + 4u * (uint)index, 4), 4);
            if (mask.Take(3).Any(value => !value))
            {
                throw new NotSupportedException("Native actor glow requires its actual current RGB bytes.");
            }
            colors.Add(BinaryPrimitives.ReadUInt32LittleEndian(data));
            known.Add(mask[3]);
        }
        return new(colors.MoveToImmutable(), known.MoveToImmutable());
    }
}
