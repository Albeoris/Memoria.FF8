using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterActorPoseResult(NativeBattleWaterExecutionFrame Frame, ImmutableArray<byte> Skeleton, ImmutableArray<NativeBattleActionMatrix> LocalBones);
public sealed record NativeBattleWaterActorPoseServices(Func<NativeBattleWaterExecutionFrame, NativeBattleGraphicsModel, NativeBattleWaterActorPoseResult> ReadLocal508C90, Func<NativeBattleWaterExecutionFrame, NativeBattleGraphicsModel, ImmutableArray<NativeBattleActionMatrix>, NativeBattleWaterExecutionFrame> PublishBones5095B0, NativeBattleWaterActorAnchorServices ActorAnchor);
public sealed record NativeBattleWaterActorDrawResult(NativeBattleWaterExecutionFrame Frame, ImmutableArray<NativeBattleGraphicsModel> Models, NativeBattleGfGeometryBatch Packets);
public sealed record NativeBattleWaterActorShadowResult(NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsModel Actor, NativeBattleGfGeometryBatch Packets);

/// <summary>Current physical actor placement and its custom floor shadow, without advancing its sequence.</summary>
public static partial class NativeBattleWaterActorDraw
{
    public static NativeBattleWaterActorDrawResult Draw26A9C340(NativeBattleWaterExecutionFrame frame, ImmutableArray<NativeBattleGraphicsModel> models, NativeBattleWaterActorPoseServices services, NativeBattleCameraProjection projection, short dqa, int dqb, uint orderingTablePointer, Func<uint, int> resolveActualOtBucket)
    {
        if (models.IsDefault || models.Length != 7 || models.Where((model, index) => model is null || model.Index != index).Any())
        {
            throw new InvalidDataException("Native actor drawing requires its seven current physical graphics rows.");
        }
        int index = ReadSlot(frame, 0x1B, 1)[0];
        if ((uint)index >= 7)
        {
            throw new NotSupportedException("Native actor drawing leaves its captured graphics-row directory.");
        }
        uint header = CoreDword(frame, 0x279744C);
        if (CoreDword(frame, checked(header + 0x60 + (uint)(4 * index))) != 0x1D972C0 + (uint)(156 * index))
        {
            throw new NotSupportedException("Native actor drawing requires its actual captured physical graphics row.");
        }
        NativeBattleGraphicsModel actor = models[index];
        NativeBattleModelTransform transform = actor.Transform ?? throw new NotSupportedException("Native actor placement requires its current position owner.");
        NativeBattleVector cachedPosition = CoreVector(frame, checked(header + 0x84 + (uint)(8 * index)));
        NativeBattleVector cachedRotation = CoreVector(frame, checked(header + 0xD8 + (uint)(8 * index)));
        if (BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xC4, 4)) == 0)
        {
            frame = SlotDword(frame, 0xC4, uint.MaxValue);
            actor = actor with { Flags0 = (ushort)(actor.Flags0 | 0x1000) };
            NativeBattleActionMatrix world = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native actor placement requires its already published geometry-world matrix.");
            NativeBattleVector difference = new(unchecked((short)(world.X - transform.Position28.X)), unchecked((short)(world.Y - transform.Position28.Y)), unchecked((short)(world.Z - transform.Position28.Z)));
            frame = TemporaryVector(frame, 0xC0, difference);
            NativeBattleVector inverse = new(unchecked((short)-cachedRotation.X), unchecked((short)-cachedRotation.Y), unchecked((short)-cachedRotation.Z));
            frame = TemporaryVector(frame, 0xD0, inverse);
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(inverse);
            frame = TemporaryRotation(frame, rotation);
            NativeBattleActionMatrix initial = rotation with { X = unchecked((int)CoreDword(frame, 0x27971E4 + 20)), Y = unchecked((int)CoreDword(frame, 0x27971E4 + 24)), Z = unchecked((int)CoreDword(frame, 0x27971E4 + 28)) };
            NativeBattleFullMacVertex full = NativeBattleGfMatrices.TransformMac460840(initial, difference);
            NativeBattleVector local = Saturate(full);
            frame = SlotVector(frame, 0xBC, local).WriteTemporaryDword(0xFC, full.Flags);
        }
        NativeBattleVector retained = SlotVector(frame, 0xBC);
        NativeBattleActionMatrix oldBasis = Placement(cachedRotation, retained);
        frame = TemporaryMatrix(frame, oldBasis);
        NativeBattleWaterActorPoseResult localPose = services.ReadLocal508C90(frame, actor);
        frame = localPose.Frame;
        NativeBattleWaterActorAnchorResult old = services.ActorAnchor.EvaluateB66B80(frame, localPose.Skeleton, localPose.LocalBones, 0xF1, 0x1000, oldBasis);
        frame = old.Frame;
        NativeBattleVector angles = new(unchecked((short)(SlotWord(frame, 0x8C) + cachedRotation.X)), unchecked((short)(SlotWord(frame, 0x8E) + cachedRotation.Y)), unchecked((short)(SlotWord(frame, 0x90) + cachedRotation.Z)));
        frame = TemporaryVector(frame, 0xD0, angles);
        actor = actor with { Rotation12 = angles.X, Rotation14 = angles.Y, Rotation16 = angles.Z };
        NativeBattleActionMatrix basis = Placement(angles, retained);
        frame = TemporaryMatrix(frame, basis);
        NativeBattleWaterActorAnchorResult current = services.ActorAnchor.EvaluateB66B80(frame, localPose.Skeleton, localPose.LocalBones, 0xF1, 0x1000, basis);
        frame = current.Frame;
        NativeBattleVector position = new(unchecked((short)(SlotWord(frame, 0x94) + cachedPosition.X + old.Anchor.X - current.Anchor.X)), unchecked((short)(SlotWord(frame, 0x96) + cachedPosition.Y + old.Anchor.Y - current.Anchor.Y)), unchecked((short)(SlotWord(frame, 0x98) + cachedPosition.Z + old.Anchor.Z - current.Anchor.Z)));
        frame = TemporaryVector(frame, 0x50, position);
        actor = actor with { Transform = transform with { Position28 = position } };
        NativeBattleWaterActorShadowResult shadow = FloorShadowA9C670(frame, actor, basis, position, services, projection, dqa, dqb, orderingTablePointer, resolveActualOtBucket);
        return new(shadow.Frame, models.SetItem(index, shadow.Actor), shadow.Packets);
    }

    private static NativeBattleActionMatrix Placement(NativeBattleVector angles, NativeBattleVector offset)
    {
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(angles);
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(rotation, offset);
        return rotation with { X = x, Y = y, Z = z };
    }

    private static NativeBattleWaterExecutionFrame TemporaryRotation(NativeBattleWaterExecutionFrame frame, NativeBattleActionMatrix matrix)
    {
        for (int lane = 0; lane < 9; lane++)
        {
            frame = frame.WriteTemporaryWord(0x60 + lane * 2, unchecked((ushort)matrix.Rotation[lane]));
        }
        return frame with { TemporaryFrame = frame.TemporaryFrame with { Known = frame.TemporaryFrame.Known.SetItem(0x72, false).SetItem(0x73, false) } };
    }

    private static NativeBattleWaterExecutionFrame TemporaryMatrix(NativeBattleWaterExecutionFrame frame, NativeBattleActionMatrix matrix) => TemporaryRotation(frame, matrix).WriteTemporaryDword(0x74, unchecked((uint)matrix.X)).WriteTemporaryDword(0x78, unchecked((uint)matrix.Y)).WriteTemporaryDword(0x7C, unchecked((uint)matrix.Z));
    private static NativeBattleWaterExecutionFrame TemporaryVector(NativeBattleWaterExecutionFrame frame, int offset, NativeBattleVector value) => frame.WriteTemporaryWord(offset, unchecked((ushort)value.X)).WriteTemporaryWord(offset + 2, unchecked((ushort)value.Y)).WriteTemporaryWord(offset + 4, unchecked((ushort)value.Z));

    private static NativeBattleVector Saturate(NativeBattleFullMacVertex value) => new((short)Math.Clamp(value.X, short.MinValue, short.MaxValue), (short)Math.Clamp(value.Y, short.MinValue, short.MaxValue), (short)Math.Clamp(value.Z, short.MinValue, short.MaxValue));

    private static NativeBattleWaterExecutionFrame SlotDword(NativeBattleWaterExecutionFrame frame, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(SlotOffset(frame, offset, 4), bytes) };
    }

    private static NativeBattleWaterExecutionFrame SlotVector(NativeBattleWaterExecutionFrame frame, int offset, NativeBattleVector value)
    {
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, value.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), value.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), value.Z);
        return frame with { Arena = frame.Arena.Write(SlotOffset(frame, offset, 6), bytes) };
    }

    private static NativeBattleVector SlotVector(NativeBattleWaterExecutionFrame frame, int offset)
    {
        byte[] bytes = ReadSlot(frame, offset, 6);
        return Vector(bytes);
    }

    private static NativeBattleVector CoreVector(NativeBattleWaterExecutionFrame frame, uint address) => Vector(frame.Core.Read(address, 6));
    private static NativeBattleVector Vector(ReadOnlySpan<byte> bytes) => new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(4)));

    private static NativeBattleActionMatrix ReadMatrix(NativeBattleWaterExecutionFrame frame, uint address)
    {
        byte[] rotation = frame.Core.Read(address, 18);
        return new(Enumerable.Range(0, 9).Select(lane => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(lane * 2))).ToImmutableArray(), unchecked((int)CoreDword(frame, address + 20)), unchecked((int)CoreDword(frame, address + 24)), unchecked((int)CoreDword(frame, address + 28)), 4096);
    }
}
