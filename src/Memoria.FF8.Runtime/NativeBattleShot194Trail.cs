using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>5B5AC0's eight retained232-byte lanes and physical main-task operand at5B534B.</summary>
public static class NativeBattleShot194Trail
{
    public static short SecondRingVelocity(short firstVelocity, uint physicalTaskAddress)
    {
        return unchecked((short)((firstVelocity * 2 / 3) * (int)physicalTaskAddress));
    }

    public static NativeBattleShot194State Initialize5B5AC0(NativeBattleShot194State state, int lane, NativeBattleShot194Anchor anchor, NativeBattleVector target, NativeBattleVector axis)
    {
        state.Validate();
        if ((uint)lane >= 8)
        {
            throw new InvalidDataException("Native Flame Shot requires its physical lane index.");
        }
        byte[] lanes = state.TrailLanes.ToArray();
        int offset = lane * 232;
        Write(offset, anchor.Position);
        Write(offset + 8, target);
        Write(offset + 16, anchor.Position);
        var direction = NativeBattleGfMatrices.RotateLong56C600(anchor.Rotation, axis.X, axis.Y, axis.Z);
        int dx = target.X - anchor.Position.X;
        int dz = target.Z - anchor.Position.Z;
        int squared = unchecked(dx * dx + dz * dz);
        if (squared < 0)
        {
            throw new NotSupportedException("Native Flame Shot reaches an unproved negative56BEC0 conversion.");
        }
        int distance = (int)Math.Sqrt(squared) >> 1;
        Write(offset + 24, new(unchecked((short)(anchor.Position.X + (unchecked(distance * direction.X) >> 12))), unchecked((short)(anchor.Position.Y + (unchecked(distance * direction.Y) >> 12))), unchecked((short)(anchor.Position.Z + (unchecked(distance * direction.Z) >> 12)))));
        Write(offset + 32, target);
        NativeBattleEffectArena points = NativeBattleEffectArena.Clear().Write(0, lanes);
        NativeBattleEffectArena work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(3, points, offset + 16, NativeBattleEffectArena.Clear().MarkUnknown(0, 400));
        for (int sample = 0; sample < 24; sample++)
        {
            points = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(3, work, sample * 4096 / 23, points, offset + 40 + sample * 8);
        }
        return state with { TrailLanes = points.Read(0, lanes.Length).ToImmutableArray() };

        void Write(int position, NativeBattleVector value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(lanes.AsSpan(position), value.X);
            BinaryPrimitives.WriteInt16LittleEndian(lanes.AsSpan(position + 2), value.Y);
            BinaryPrimitives.WriteInt16LittleEndian(lanes.AsSpan(position + 4), value.Z);
        }
    }

    public static NativeBattleVector ReadSample(NativeBattleShot194State state, int lane, int sample)
    {
        if ((uint)lane >= 8 || (uint)sample >= 24)
        {
            throw new InvalidDataException("Native Flame Shot sample escapes its physical lane.");
        }
        ReadOnlySpan<byte> bytes = state.TrailLanes.AsSpan().Slice(lane * 232 + 40 + sample * 8, 6);
        return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(4)));
    }
}
