using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicBrothersActor38Services(Func<NativeBattleWaterExecutionFrame, uint, NativeBattleGraphicsModel> ReadActor, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleGraphicsModel, NativeBattleWaterExecutionFrame> WriteActor, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleCinematicBonePose> RestorePose, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleActionMatrix, NativeBattleVector, NativeBattleWaterExecutionFrame> DrawShadow, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> PublishInput, NativeBattleWaterActorAnchorServices ActorAnchor);

public static class NativeBattleCinematicBrothersActor38
{
    public static NativeBattleWaterExecutionFrame DrawAF59D0(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicBrothersActor38Services services)
    {
        uint header = CoreDword(frame, 0x279744C);
        byte actor = ReadSlot(frame, 0x1B, 1)[0];
        uint address = CoreDword(frame, unchecked(header + 0x60u + actor * 4u));
        if (BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xC4, 4)) == 0)
        {
            frame = WriteSlotDword(frame, 0xC4, uint.MaxValue);
            NativeBattleGraphicsModel physical = services.ReadActor(frame, address);
            frame = services.WriteActor(frame, address, physical with { Flags0 = (ushort)(physical.Flags0 | 0x1000) });
            NativeBattleActionMatrix world = physical.GeometryWorldMatrix ?? throw new NotSupportedException("Native Brothers actor capture requires its retained physical world matrix.");
            NativeBattleVector position = physical.Transform?.Position28 ?? throw new NotSupportedException("Native Brothers actor capture requires its current physical position.");
            frame = frame.WriteTemporaryWord(0xC0, unchecked((ushort)(world.X - position.X))).WriteTemporaryWord(0xC2, unchecked((ushort)(world.Y - position.Y))).WriteTemporaryWord(0xC4, unchecked((ushort)(world.Z - position.Z)));
            NativeBattleVector angles = HeaderAngles();
            angles = new(unchecked((short)-angles.X), unchecked((short)-angles.Y), unchecked((short)-angles.Z));
            StoreTemporaryVector(0xD0, angles);
            NativeBattleActionMatrix rotation = StoreRotation(angles);
            rotation = rotation with { X = unchecked((int)CoreDword(frame, 0x27971E4)), Y = unchecked((int)CoreDword(frame, 0x27971E8)), Z = unchecked((int)CoreDword(frame, 0x27971EC)) };
            frame = services.PublishInput(frame, frame.TemporaryFrame.Address + 0xC0);
            NativeBattleFullMacVertex transformed = NativeBattleGfMatrices.TransformMac460840(rotation, TemporaryVector(0xC0));
            frame = WriteSlotWord(frame, 0xBC, unchecked((ushort)(short)Math.Clamp(transformed.X, short.MinValue, short.MaxValue)));
            frame = WriteSlotWord(frame, 0xBE, unchecked((ushort)(short)Math.Clamp(transformed.Y, short.MinValue, short.MaxValue)));
            frame = WriteSlotWord(frame, 0xC0, unchecked((ushort)(short)Math.Clamp(transformed.Z, short.MinValue, short.MaxValue)));
            frame = frame.WriteTemporaryDword(0xFC, transformed.Flags);
        }
        NativeBattleActionMatrix oldBasis = PrepareBasis(HeaderAngles());
        NativeBattleCinematicBonePose pose = services.RestorePose(frame, address);
        frame = pose.Frame;
        NativeBattleWaterActorAnchorResult first = services.ActorAnchor.EvaluateB66B80(frame, pose.Skeleton, pose.Bones, 0xF1, 4096, oldBasis);
        frame = services.PublishInput(first.Frame, first.Frame.TemporaryFrame.Address + 0xC0);
        NativeBattleVector headerAngles = HeaderAngles();
        NativeBattleVector changed = new(unchecked((short)(SlotWord(frame, 0x8C) + headerAngles.X)), unchecked((short)(SlotWord(frame, 0x8E) + headerAngles.Y)), unchecked((short)(SlotWord(frame, 0x90) + headerAngles.Z)));
        StoreTemporaryVector(0xD0, changed);
        NativeBattleGraphicsModel updated = services.ReadActor(frame, address);
        frame = services.WriteActor(frame, address, updated with { Rotation12 = changed.X, Rotation14 = changed.Y, Rotation16 = changed.Z });
        NativeBattleActionMatrix basis = PrepareBasis(changed);
        NativeBattleWaterActorAnchorResult second = services.ActorAnchor.EvaluateB66B80(frame, pose.Skeleton, pose.Bones, 0xF1, 4096, basis);
        frame = services.PublishInput(second.Frame, second.Frame.TemporaryFrame.Address + 0xC0);
        uint cached = unchecked(CoreDword(frame, 0x279744C) + 0x84u + actor * 8u);
        NativeBattleVector positionNext = new(unchecked((short)(SlotWord(frame, 0x94) + CoreWord(frame, cached) - unchecked(second.Anchor.X - first.Anchor.X))), unchecked((short)(SlotWord(frame, 0x96) + CoreWord(frame, cached + 2) - unchecked(second.Anchor.Y - first.Anchor.Y))), unchecked((short)(SlotWord(frame, 0x98) + CoreWord(frame, cached + 4) - unchecked(second.Anchor.Z - first.Anchor.Z))));
        StoreTemporaryVector(0x50, positionNext);
        updated = services.ReadActor(frame, address);
        NativeBattleModelTransform transform = updated.Transform ?? throw new NotSupportedException("Native Brothers actor positioning requires its retained transform owner.");
        frame = services.WriteActor(frame, address, updated with { Transform = transform with { Position28 = positionNext } });
        return services.DrawShadow(frame, address, basis, positionNext);

        NativeBattleVector HeaderAngles()
        {
            uint current = unchecked(CoreDword(frame, 0x279744C) + 0xD8u + actor * 8u);
            return new(unchecked((short)CoreWord(frame, current)), unchecked((short)CoreWord(frame, current + 2)), unchecked((short)CoreWord(frame, current + 4)));
        }

        NativeBattleActionMatrix StoreRotation(NativeBattleVector angles)
        {
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CE30(angles);
            for (int lane = 0; lane < 9; lane++)
            {
                frame = frame.WriteTemporaryWord(0x60 + lane * 2, unchecked((ushort)matrix.Rotation[lane]));
            }
            frame = frame with { TemporaryFrame = frame.TemporaryFrame with { Known = frame.TemporaryFrame.Known.SetItem(0x72, false).SetItem(0x73, false) } };
            return matrix;
        }

        NativeBattleActionMatrix PrepareBasis(NativeBattleVector angles)
        {
            NativeBattleActionMatrix rotation = StoreRotation(angles);
            NativeBattleVector point = new(unchecked((short)SlotWord(frame, 0xBC)), unchecked((short)SlotWord(frame, 0xBE)), unchecked((short)SlotWord(frame, 0xC0)));
            (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(rotation, point);
            frame = frame.WriteTemporaryDword(0x74, unchecked((uint)x)).WriteTemporaryDword(0x78, unchecked((uint)y)).WriteTemporaryDword(0x7C, unchecked((uint)z));
            return rotation with { X = x, Y = y, Z = z };
        }

        NativeBattleVector TemporaryVector(int offset) => new(unchecked((short)frame.ReadTemporaryWord(offset)), unchecked((short)frame.ReadTemporaryWord(offset + 2)), unchecked((short)frame.ReadTemporaryWord(offset + 4)));

        void StoreTemporaryVector(int offset, NativeBattleVector vector)
        {
            frame = frame.WriteTemporaryWord(offset, unchecked((ushort)vector.X)).WriteTemporaryWord(offset + 2, unchecked((ushort)vector.Y)).WriteTemporaryWord(offset + 4, unchecked((ushort)vector.Z));
        }
    }
}
