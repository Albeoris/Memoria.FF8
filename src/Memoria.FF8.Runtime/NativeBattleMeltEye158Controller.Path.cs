using static Memoria.FF8.Runtime.NativeBattleMeltEye158Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMeltEye158Controller
{
    public static (NativeBattleMeltEye158Frame Frame, int Result) AdvancePath(NativeBattleMeltEye158Frame frame, uint address, NativeBattleMeltEye158Resources resources, NativeBattleMeltEye158Inputs inputs)
    {
        int ordinal = unchecked((short)Read(frame, address).ReadWord(14));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        if ((uint)ordinal >= CurveCount)
        {
            throw new InvalidDataException("Melt-Eye path ordinal leaves its actual fixed curve bank.");
        }
        uint curve = checked(CurveBank + (uint)(ordinal * CurveStride));
        int age = unchecked((short)Read(frame, address).ReadWord(12));
        if (age == 0)
        {
            NativeBattleMeltEye158PointStep anchor = inputs.ReadBone502170(frame, Caster(frame), 0xF0, 0);
            frame = WriteVector(anchor.Frame, curve + 0x10, anchor.Position);
            int vector = unchecked((short)Read(frame, address).ReadWord(24));
            NativeBattleModuleAnchor source = resources.Vectors[vector];
            frame = frame with { State = frame.State.WriteWord(curve, unchecked((ushort)source.X)).WriteWord(curve + 2, unchecked((ushort)source.Y)).WriteWord(curve + 4, unchecked((ushort)source.Z)).WriteWord(curve + 6, unchecked((ushort)source.W)) };
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(WorldRotation(frame, Caster(frame), inputs), new(source.X, source.Y, source.Z));
            NativeBattleVector start = new(unchecked((short)(rotated.X + anchor.Position.X)), unchecked((short)(rotated.Y + anchor.Position.Y)), unchecked((short)(rotated.Z + anchor.Position.Z)));
            frame = WriteVector(frame, curve, start);
            NativeBattleMeltEye158PointStep center = inputs.ReadTargetCenter5020A0(frame, Parameters(frame).TargetMask7A);
            frame = WriteVector(center.Frame, curve + 8, center.Position);
            frame = Random(frame, out ushort xRoll);
            frame = Random(frame, out ushort zRoll);
            NativeBattleVector end = new(unchecked((short)(center.Position.X + xRoll % 5200 - 2600)), center.Position.Y, unchecked((short)(center.Position.Z + zRoll % 2000 - 1000)));
            frame = WriteVector(frame, curve + 8, end);
            frame = frame with { State = frame.State.WriteView(curve + 0x18, frame.State.ReadView(curve, 8), 8).WriteView(curve + 0x30, frame.State.ReadView(curve + 8, 8), 8) };
            int dx = (end.X - start.X) / 3;
            int dz = (end.Z - start.Z) / 3;
            frame = Random(frame, out ushort firstY);
            NativeBattleVector firstControl = new(unchecked((short)(start.X + dx)), unchecked((short)(start.Y - firstY % 2800 - 500)), unchecked((short)(start.Z + dz)));
            frame = WriteVector(frame, curve + 0x20, firstControl);
            frame = Random(frame, out ushort secondY);
            NativeBattleVector secondControl = new(unchecked((short)(start.X + dx * 2)), unchecked((short)(firstControl.Y - secondY % 600 - 50)), unchecked((short)(start.Z + dz * 2)));
            frame = WriteVector(frame, curve + 0x28, secondControl);
            frame = PrepareCurve(frame, curve);
        }
        if (age == 1)
        {
            (frame, uint child) = Append(frame, HeadMeshCallback);
            NativeBattleModulePoolTask task = Read(frame, child).WriteWord(12, 0).WriteWord(14, unchecked((ushort)ordinal));
            NativeBattleModuleAnchor source = resources.Vectors[0];
            task = task.WriteWord(16, unchecked((ushort)source.X)).WriteWord(18, unchecked((ushort)source.Y)).WriteWord(20, unchecked((ushort)source.Z)).WriteWord(22, unchecked((ushort)source.W));
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(WorldRotation(frame, Caster(frame), inputs), new(source.X, source.Y, source.Z));
            NativeBattleVector anchor = ReadVector(frame, curve + 0x10);
            task = PutTaskVector(task, 16, new(unchecked((short)(rotated.X + anchor.X)), unchecked((short)(rotated.Y + anchor.Y)), unchecked((short)(rotated.Z + anchor.Z))));
            frame = Random(frame, out ushort roll);
            short scale = unchecked((short)((roll & 0x3FF) + 1536));
            task = task.WriteWord(28, unchecked((ushort)scale)).WriteWord(30, unchecked((ushort)(scale / 7)));
            frame = Write(frame, child, task);
        }
        if (age == 7)
        {
            (frame, uint child) = Append(frame, StreamCallback);
            NativeBattleModulePoolTask task = Read(frame, child).WriteWord(12, 0).WriteWord(14, unchecked((ushort)ordinal)).WriteWord(24, 0).WriteWord(26, 7);
            frame = Random(frame, out ushort roll);
            frame = Write(frame, child, task.WriteWord(28, unchecked((ushort)((roll & 0x1FF) + 2304))));
        }
        if (age == 16)
        {
            (frame, uint sprite) = Append(frame, ImpactSpriteCallback);
            NativeBattleModulePoolTask task = Read(frame, sprite).WriteWord(12, 0).WriteWord(16, frame.State.ReadWord(curve + 8)).WriteWord(20, frame.State.ReadWord(curve + 12));
            frame = Random(frame, out ushort yRoll);
            task = task.WriteWord(18, unchecked((ushort)(-100 - yRoll % 400)));
            frame = Random(frame, out ushort scaleRoll);
            frame = Write(frame, sprite, task.WriteWord(28, unchecked((ushort)(scaleRoll % 1536 + 3584))));
            (frame, uint mesh) = Append(frame, ImpactMeshCallback);
            NativeBattleModulePoolTask impact = CopyToTask(Read(frame, mesh).WriteWord(12, 0), 16, TaskView(Read(frame, sprite), 16, 8), 8);
            frame = Random(frame, out ushort angleRoll);
            short angle = unchecked((short)(angleRoll & 0xFFF));
            impact = impact.WriteWord(24, unchecked((ushort)angle));
            frame = Random(frame, out ushort speedRoll);
            int speed = speedRoll % 100 + 120;
            impact = impact.WriteWord(26, unchecked((ushort)((angle & 1) != 0 ? -speed : speed)));
            frame = Random(frame, out ushort sizeRoll);
            ushort size = unchecked((ushort)(((sizeRoll & 0x7FF) + 8192) / 6));
            frame = Write(frame, mesh, impact.WriteWord(28, size).WriteWord(30, size));
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        return (frame, next >= 30 ? 2 : 0);
    }

    public static NativeBattleMeltEye158Frame PrepareCurve(NativeBattleMeltEye158Frame frame, uint address)
    {
        NativeBattleEffectArena curve = frame.State.ReadView(address, CurveStride);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Melt-Eye requires its actual curve temporary memory."), 400);
        NativeBattleEffectArena work = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 400), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 400), 0, 400);
        work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(4, curve, 0x18, work);
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, work, 400);
        for (int sample = 0; sample < 48; sample++)
        {
            curve = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(4, work, sample * 4096 / 47, curve, 0x38 + sample * 8);
        }
        frame = frame with { State = frame.State.WriteView(address, curve, CurveStride), Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 400) };
        return frame;
    }
}
