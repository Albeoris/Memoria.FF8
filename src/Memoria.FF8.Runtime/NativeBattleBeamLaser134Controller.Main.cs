using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBeamLaser134Controller
{
    private static (NativeBattleBeamLaser134Frame Frame, int Result) Main(NativeBattleBeamLaser134Frame frame, uint address, NativeBattleBeamLaser134Resources resources, NativeBattleBeamLaser134Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short age = unchecked((short)task.ReadWord(12));
        int targetActor = Actor(task.ReadDword(16));
        NativeBattleBeamLaser134PointStep casterStep = inputs.ReadBone502170(frame, Actor(frame.State.ReadDword(NativeBattleBeamLaser134Profile.CasterGlobal)), 13, 2048);
        NativeBattleBeamLaser134AnchorStep targetStep = inputs.ReadAnchor571400(casterStep.Frame, targetActor);
        frame = targetStep.Frame;
        NativeBattleVector caster = casterStep.Point;
        NativeBattleVector target = new(targetStep.Anchor.X, targetStep.Anchor.Y, targetStep.Anchor.Z);
        NativeBattleActionMatrix original = NativeBattleFunguarLaser277.Basis50CBA0(target.X - caster.X, 0, caster.Z - target.Z).Matrix;
        NativeBattleActionMatrix flipped = original with { Rotation = original.Rotation.Select((value, index) => index / 3 == 1 ? value : unchecked((short)-value)).ToImmutableArray() };
        if ((uint)age < 14)
        {
            int angle = (age << 11) / 14;
            int sine = unchecked(NativeBattleCameraMath.Sine(angle) * 500) >> 12;
            int cosine = unchecked(NativeBattleCameraMath.Cosine(angle) * 500) >> 12;
            (frame, _) = RotateAct(frame, address, 0x1C, 0x58, resources.Act(0x12038DC), original, new(0, unchecked((short)sine), unchecked((short)cosine)), caster, inputs);
            (frame, _) = RotateAct(frame, address, 0x74, 0x58, resources.Act(0x12038DC), original, new(0, unchecked((short)-sine), unchecked((short)cosine)), caster, inputs);
        }
        int relative = age - 9;
        if ((uint)relative < 25)
        {
            NativeBattleVector local;
            (frame, local) = RotateAct(frame, address, 0xCC, 0x220, resources.Act(0x1205270), original, new(0, 0, -500), caster, inputs);
            int delta = relative - 5;
            int cue;
            short scale = (inputs.ReadModel(targetActor).Transform ?? throw new NotSupportedException("Beam Laser requires its target scaleWORD38.")).Scale38;
            if ((uint)delta < 12)
            {
                cue = unchecked(4096 - NativeBattleCameraMath.Cosine((delta << 10) / 12));
                int offset = unchecked(scale * 4 - (scale * cue >> 9));
                target = target with { Y = unchecked((short)(target.Y + offset)) };
            }
            else if ((uint)delta < 20)
            {
                cue = unchecked(640 - delta * 32);
                int offset = unchecked(scale * 2 - ((delta + 52) * scale * 4 >> 6));
                target = target with { Y = unchecked((short)(target.Y + offset)) };
            }
            else
            {
                cue = 0;
                target = target with { Y = unchecked((short)(target.Y + scale * 2)) };
            }
            NativeBattleActionMatrix world = NativeBattleFunguarLaser277.Basis50CBA0(target.X - caster.X, caster.Y - target.Y, caster.Z - target.Z).Matrix with { X = local.X, Y = local.Y, Z = local.Z };
            frame = WorldAct(frame, address, 0x2EC, 0xA4, resources.Act(0x12065F0), world, 0, inputs);
            if ((uint)delta < 20)
            {
                NativeBattleVector? intersection = null;
                if (target.Y >= 0)
                {
                    int denominator = target.Y - caster.Y;
                    int x = unchecked((caster.X - target.X) * caster.Y);
                    int z = unchecked((caster.Z - target.Z) * caster.Y);
                    intersection = new(unchecked((short)(x / (denominator - 512) + caster.X)), 0, unchecked((short)(z / (denominator - 512) + caster.Z)));
                    target = new(unchecked((short)(x / denominator + caster.X)), 0, unchecked((short)(z / denominator + caster.Z)));
                }
                (NativeBattleActionMatrix basis, int length) = NativeBattleFunguarLaser277.Basis50CBA0(target.X - caster.X, caster.Y - target.Y, caster.Z - target.Z);
                world = basis with { X = local.X, Y = local.Y, Z = local.Z };
                length = unchecked(length + (length >> 3));
                short minimum = inputs.ReadModel(targetActor).MinimumY146 ?? throw new NotSupportedException("Beam Laser requires its target minimumYWORD146.");
                if (target.Y < minimum)
                {
                    length = unchecked(length << 1);
                }
                else if (target.Y < 0)
                {
                    int height = -target.Y;
                    if (height >= (scale >> 1) && height <= scale)
                    {
                        (frame, uint child) = Append(frame, 2, 0x6B0E30);
                        if (child != 0)
                        {
                            frame = Write(frame, child, Vector(Read(frame, child), 12, target).WriteWord(18, 0));
                        }
                    }
                }
                else
                {
                    (frame, uint child) = Append(frame, 4, 0x6B0D30);
                    if (child != 0)
                    {
                        NativeBattleVector point = intersection ?? throw new InvalidDataException("Beam Laser reached an unproduced native ground intersection.");
                        NativeBattleModulePoolTask spawned = Read(frame, child);
                        for (int index = 0; index < 9; index++)
                        {
                            spawned = spawned.WriteWord(12 + index * 2, unchecked((ushort)world.Rotation[index]));
                        }
                        spawned = spawned.WriteDword(32, unchecked((uint)(int)point.X)).WriteDword(36, unchecked((uint)(int)point.Y)).WriteDword(40, unchecked((uint)(int)point.Z));
                        frame = Write(frame, child, spawned);
                        frame = InitializeAct(frame, child, 0x2C, 0x58, resources.Act(0x1207150));
                    }
                }
                short width = unchecked((short)(NativeBattleCameraMath.Sine(cue >> 1) >> 3));
                NativeBattleActionMatrix diagonal = new([width, 0, 0, 0, width, 0, 0, 0, unchecked((short)length)], 0, 0, 0, 4096);
                NativeBattleActionMatrix scaled = NativeBattleGfMatrices.Multiply56C090(world, diagonal) with { X = world.X, Y = world.Y, Z = world.Z };
                frame = inputs.Execute(frame, new NativeBattleBeamLaser134DrawStatic88(NativeBattlePackedMatrix32.FromMatrix(scaled, null), cue) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(134, Read(frame, address)) with { ResourceGeneration = resources.Mesh.Address } });
            }
        }
        relative = age - 22;
        if ((uint)relative < 24)
        {
            if (relative < 4)
            {
                for (int index = 0; index < 4; index++)
                {
                    (frame, uint child) = Append(frame, 3, 0x6B0D90);
                    if (child != 0)
                    {
                        NativeBattleModulePoolTask main = Read(frame, address);
                        NativeBattleModulePoolTask spawned = Read(frame, child).WriteDword(12, main.ReadDword(20)).WriteDword(16, main.ReadDword(24)).WriteWord(18, 0);
                        for (int axis = 0; axis < 3; axis++)
                        {
                            frame = Random(frame, out ushort roll);
                            spawned = spawned.WriteWord(20 + axis * 2, unchecked((ushort)((roll & 0xFF) - 0x80)));
                        }
                        frame = Write(frame, child, spawned);
                    }
                }
            }
            NativeBattleModulePoolTask current = Read(frame, address);
            NativeBattleActionMatrix world = flipped with { X = unchecked((short)current.ReadWord(20)), Y = unchecked((short)current.ReadWord(22)), Z = unchecked((short)current.ReadWord(24)) };
            frame = WorldAct(frame, address, 0x390, 0x1B0, resources.Act(0x1204004), world, -128, inputs);
        }
        if (age == 2)
        {
            frame = inputs.Execute(frame, new NativeBattleBeamLaser134Sound(resources.Sound));
        }
        if (age == 26)
        {
            frame = inputs.Execute(frame, new NativeBattleBeamLaser134Contact(FirstContact(frame, inputs)));
        }
        if (age >= 46)
        {
            return (frame, 2);
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return (frame, 0);
    }

    private static (NativeBattleBeamLaser134Frame Frame, NativeBattleVector Updated) RotateAct(NativeBattleBeamLaser134Frame frame, uint address, int offset, int size, NativeBattleGfAnimationSource source, NativeBattleActionMatrix rotation, NativeBattleVector local, NativeBattleVector anchor, NativeBattleBeamLaser134Inputs inputs)
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(rotation, null);
        packed = packed with { Known = Enumerable.Range(0, 32).Select(index => index < 18).ToImmutableArray() };
        NativeBattleBeamLaser134RotateStep rotated = inputs.RotatePoint(frame, packed, local, null, anchor);
        NativeBattleBeamLaser134ActStep advanced = inputs.AdvanceAct(rotated.Frame, address, offset, size, source, rotated.Context40);
        return (advanced.Frame, rotated.UpdatedLocal);
    }

    private static NativeBattleBeamLaser134Frame WorldAct(NativeBattleBeamLaser134Frame frame, uint address, int offset, int size, NativeBattleGfAnimationSource source, NativeBattleActionMatrix world, int depth, NativeBattleBeamLaser134Inputs inputs)
    {
        NativeBattleBeamLaser134ContextStep context = inputs.PrepareWorldContext(frame, NativeBattlePackedMatrix32.FromMatrix(world, null), depth);
        return inputs.AdvanceAct(context.Frame, address, offset, size, source, context.Context40).Frame;
    }
}
