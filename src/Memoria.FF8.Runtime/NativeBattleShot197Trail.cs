using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>5B0E80's eight retained232-byte three-point curves.</summary>
public static class NativeBattleShot197Trail
{
    public static NativeBattleShot197State Initialize(NativeBattleShot197State state, int lane, NativeBattleVector start, NativeBattleVector middle, NativeBattleVector end)
    {
        state.Validate();
        if ((uint)lane >= 8)
        {
            throw new InvalidDataException("Native Armor Shot requires its original curve lane index.");
        }
        int offset = lane * 232;
        byte[] bytes = state.TrailLanes.ToArray();
        Write(offset, start);
        Write(offset + 8, end);
        Write(offset + 16, start);
        Write(offset + 24, middle);
        Write(offset + 32, end);
        NativeBattleEffectArena points = NativeBattleEffectArena.Clear().Write(0, bytes);
        NativeBattleEffectArena work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(3, points, offset + 16, NativeBattleEffectArena.Clear().MarkUnknown(0, 400));
        for (int sample = 0; sample < 24; sample++)
        {
            points = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(3, work, sample * 4096 / 23, points, offset + 40 + sample * 8);
        }
        return state with { TrailLanes = points.Read(0, bytes.Length).ToImmutableArray() };

        void Write(int cursor, NativeBattleVector point)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(cursor), point.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(cursor + 2), point.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(cursor + 4), point.Z);
        }
    }

    public static NativeBattleVector ReadSample(NativeBattleShot197State state, int lane, int index)
    {
        if ((uint)lane >= 8 || (uint)index >= 24)
        {
            throw new InvalidDataException("Native Armor Shot sample escapes its original curve lane.");
        }
        ReadOnlySpan<byte> bytes = state.TrailLanes.AsSpan().Slice(lane * 232 + 40 + index * 8, 6);
        return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(4)));
    }
}
