using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaRetainedVector([property: JsonRequired] short X, [property: JsonRequired] short Y, [property: JsonRequired] short Z, [property: JsonRequired] short W);
public sealed record NativeBattleShivaGeometryState([property: JsonRequired] NativeBattleShivaRetainedVector Current, [property: JsonRequired] NativeBattleShivaRetainedVector Previous);
public sealed record NativeBattleShivaGeometryStep(NativeBattleEffectArena Arena, NativeBattleShivaGeometryState State);

public static class NativeBattleShivaEarlyGeometry
{
    public const int VertexArenaOffset = 0x33000;

    public static NativeBattleShivaGeometryStep Regenerate5C1470(NativeBattleEffectArena arena, NativeBattleShivaGeometryState state, ImmutableArray<byte> sourcePairs, int count)
    {
        arena.Validate();
        if (state is null || state.Current is null || state.Previous is null || sourcePairs.IsDefault || sourcePairs.Length != 74 * 8 || count is < 0 or > 36)
        {
            throw new InvalidDataException("Native Shiva geometry requires its retained current/previous vectors, original37 source pairs and admitted0..36 count.");
        }
        for (int index = 0; index < 74; index++)
        {
            arena = arena.Write(VertexArenaOffset + index * 8 + 6, new byte[2]);
        }
        int startVertex = 2 * (36 - count);
        for (int index = 0; index <= count; index++)
        {
            NativeBattleShivaRetainedVector prefixFirst = Source(index * 2);
            NativeBattleShivaRetainedVector prefixSecond = Source(index * 2 + 1);
            int dx = prefixSecond.X - prefixFirst.X;
            int dy = prefixSecond.Y - prefixFirst.Y;
            int dz = prefixSecond.Z - prefixFirst.Z;
            int square = unchecked(dx * dx + dy * dy + dz * dz);
            if (square < 0)
            {
                throw new NotSupportedException("Native Shiva radius exceeds the admitted signed32 square-root input.");
            }
            int radius = (int)Math.Sqrt(square) >> 1;
            int vertex = startVertex + index * 2;
            NativeBattleShivaRetainedVector first = Source(vertex);
            NativeBattleShivaRetainedVector second = Source(vertex + 1);
            NativeBattleShivaRetainedVector middle = new((short)((first.X + second.X) >> 1), (short)((first.Y + second.Y) >> 1), (short)((first.Z + second.Z) >> 1), 0);
            if (index == 0)
            {
                NativeBattleShivaRetainedVector previous = state.Current;
                NativeBattleShivaRetainedVector current = middle;
                if (current.Y < previous.Y)
                {
                    short saved = current.Y;
                    current = current with { Y = previous.Y };
                    previous = previous with { Y = saved };
                }
                state = new(current, previous);
            }
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(second.X - first.X, second.Y - first.Y, second.Z - first.Z);
            Write(vertex, new(unchecked((short)(middle.X + (radius * direction.X >> 12))), unchecked((short)(middle.Y + (radius * direction.Y >> 12))), unchecked((short)(middle.Z + (radius * direction.Z >> 12))), 1));
            Write(vertex + 1, new(unchecked((short)(middle.X + (-radius * direction.X >> 12))), unchecked((short)(middle.Y + (-radius * direction.Y >> 12))), unchecked((short)(middle.Z + (-radius * direction.Z >> 12))), 1));
        }
        return new(arena, state with { Current = state.Current with { W = (short)startVertex } });

        NativeBattleShivaRetainedVector Source(int index)
        {
            ReadOnlySpan<byte> bytes = sourcePairs.AsSpan().Slice(index * 8, 8);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[6..]));
        }

        void Write(int index, NativeBattleShivaRetainedVector vector)
        {
            Span<byte> bytes = stackalloc byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, vector.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes[2..], vector.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes[4..], vector.Z);
            BinaryPrimitives.WriteInt16LittleEndian(bytes[6..], vector.W);
            arena = arena.Write(VertexArenaOffset + index * 8, bytes);
        }
    }
}
