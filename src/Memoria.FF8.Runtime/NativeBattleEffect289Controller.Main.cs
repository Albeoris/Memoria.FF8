using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect289Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect289Controller
{
    public static (NativeBattleEffect289Frame Frame, int Result) AdvanceMain(NativeBattleEffect289Frame frame, uint address, NativeBattleEffect289Resources resources, NativeBattleEffect289Inputs inputs)
    {
        int age = Signed(Read(frame, address), 0xC);
        NativeBattleEffectArena? casterPoint = null;
        NativeBattleEffectArena? targetPoint = null;
        NativeBattlePackedMatrix32? basis = null;
        if (age < 22)
        {
            int progress = age * 4096 / 22;
            int half = progress >> 1;
            int delta = (progress - 2048) >> 1;
            NativeBattleModulePoolTask task = Read(frame, address);
            uint target = task.ReadDword(0x10);
            short x = unchecked((short)(Signed(task, 0x14) + delta));
            short z = unchecked((short)(Signed(task, 0x18) - delta));
            short angle = (inputs.ReadModel(frame, target).Transform ?? throw new NotSupportedException("Effect289 requires its current target Angle36.")).Angle36;
            targetPoint = PointView(new(x, angle, z));
            NativeBattleEffect289PointStep point = inputs.ReadCasterPoint678390(frame, NativeBattleEffectArena.Clear().Write(0, resources.CasterPoint8.AsSpan()), NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
            frame = point.Frame;
            casterPoint = point.Point8;
            NativeBattleVector caster = Vector289(casterPoint);
            (NativeBattleActionMatrix rotation, int length) = NativeBattleFunguarLaser277.Basis50CBA0(x - caster.X, caster.Y - angle, caster.Z - z);
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, length, length, length) with { X = caster.X, Y = caster.Y, Z = caster.Z };
            basis = NativeBattlePackedMatrix32.FromMatrix(rotation, null);
            NativeBattlePackedMatrix32 view = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), rotation), null);
            int firstReference = age < 12 ? 558 * age - 4648 : age > 14 ? 877 * age - 10230 : 2048;
            int firstScale = age > 10 ? (22 - age) * 372 : 4096;
            int sineHalf = NativeBattleCameraMath.Sine(half);
            frame = DrawCustom678510(frame, view, 0x106EDE4, age * 16, sineHalf / 4 + 250, -(progress / 8), firstReference, -age * 8, firstScale, resources, inputs);
            int reference = 558 * age - 4648;
            if (reference > 0)
            {
                reference *= 2;
            }
            int scale = age > 2 ? (22 - age) * 372 : 4096;
            frame = DrawCustom678510(frame, view, 0x106FAF4, age * 16, sineHalf / 4 + 350, sineHalf / 8, reference, -age * 8, scale, resources, inputs);
            frame = DrawCustom678510(frame, view, 0x106FAF4, -age * 16, sineHalf / 4 + 150, -(NativeBattleCameraMath.Sine(progress) / 8), reference, -age * 8, scale, resources, inputs);
        }
        if ((uint)(age - 10) < 20)
        {
            if ((uint)(age - 14) < 8)
            {
                frame = LoadMatrix(frame, basis ?? throw new InvalidDataException("Effect289 lost its current bridge matrix."), true, false);
                frame = SpawnTargetParticles289(frame, address, resources, inputs);
            }
            NativeBattleEffectArena anchor = TaskView(Read(frame, address), 0x14, 8);
            NativeBattleEffectArena caller = ProgrammeCaller289(frame, Vector289(anchor), inputs);
            (frame, _) = DrawProgramme289(frame, address, MainProgrammeWorkspaceOffset, MainProgrammeWorkspaceLength, resources.MainProgramme, caller, inputs);
        }
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect289Sound501330(resources.Sound, 0, 128));
        }
        if ((uint)(age - 2) < 10)
        {
            frame = LoadMatrix(frame, basis ?? throw new InvalidDataException("Effect289 lost its current caster particle basis."), true, false);
            for (int index = 0; index < 4; index++)
            {
                (frame, uint particle) = Append(frame, ParticleDescriptor, 0x6783F0);
                if (particle == 0)
                {
                    continue;
                }
                NativeBattleModulePoolTask task = CopyTask(Read(frame, particle), 0x14, casterPoint ?? throw new InvalidDataException("Effect289 lost its current caster point."), 8).WriteDword(0xC, 0x106EAF4);
                frame = Random289(frame, out int phase);
                frame = Random289(frame, out int radius);
                int magnitude = (radius & 15) + 32;
                task = task.WriteWord(0x1C, unchecked((ushort)(unchecked(NativeBattleCameraMath.Sine(phase) * magnitude) >> 12))).WriteWord(0x1E, unchecked((ushort)(unchecked(NativeBattleCameraMath.Cosine(phase) * magnitude) >> 12)));
                frame = Random289(frame, out int depth);
                task = task.WriteWord(0x20, unchecked((ushort)(-32 - (depth & 16))));
                frame = Write(frame, particle, task);
                frame = TransformParticleVelocity289(frame, particle);
                task = Read(frame, particle).WriteWord(0x22, 12).WriteWord(0x1A, 0);
                frame = Random289(frame, out int scale);
                frame = Write(frame, particle, task.WriteDword(0x10, (uint)((scale & 511) + 1000)));
            }
        }
        if ((uint)(age - 8) < 14)
        {
            (frame, uint programme) = Append(frame, ProgrammeDescriptor, 0x678490);
            NativeBattleEffectArena anchor = targetPoint ?? throw new InvalidDataException("Effect289 lost its current target bridge tuple.");
            if (programme != 0)
            {
                NativeBattleModulePoolTask task = CopyTask(Read(frame, programme), 0xC, anchor, 8);
                frame = Write(frame, programme, task);
                NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[ChildProgrammeWorkspaceLength].ToImmutableArray()).Initialize7016B0(resources.ChildProgramme, ChildProgrammeWorkspaceLength);
                frame = WriteBytes(frame, programme + ChildProgrammeWorkspaceOffset, workspace.CopyTo(NativeBattleEffectArena.Clear(), 0), ChildProgrammeWorkspaceLength);
            }
            frame = LoadMatrix(frame, basis ?? throw new InvalidDataException("Effect289 lost its current target particle basis."), true, false);
            (frame, uint particle) = Append(frame, ParticleDescriptor, 0x6783F0);
            if (particle != 0)
            {
                NativeBattleModulePoolTask task = CopyTask(Read(frame, particle), 0x14, anchor, 8).WriteDword(0xC, 0x106E9B0).WriteWord(0x1C, 0);
                frame = Random289(frame, out int vz);
                task = task.WriteWord(0x20, unchecked((ushort)(-16 - (vz & 15))));
                frame = Random289(frame, out int vy);
                task = task.WriteWord(0x1E, unchecked((ushort)(-32 - (vy & 15))));
                frame = TransformParticleVelocity289(Write(frame, particle, task), particle);
                task = Read(frame, particle).WriteWord(0x22, 12).WriteWord(0x1A, 0);
                frame = Random289(frame, out int scale);
                frame = Write(frame, particle, task.WriteDword(0x10, (uint)((scale & 1023) + 2000)));
            }
        }
        if (age == 15)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect289Contact506690());
        }
        short next = unchecked((short)(age + 1));
        return (Write(frame, address, Read(frame, address).WriteWord(0xC, unchecked((ushort)next))), next >= 30 ? 2 : 0);
    }

    private static NativeBattleEffect289Frame SpawnTargetParticles289(NativeBattleEffect289Frame frame, uint main, NativeBattleEffect289Resources resources, NativeBattleEffect289Inputs inputs)
    {
        NativeBattleModulePoolTask owner = Read(frame, main);
        uint target = owner.ReadDword(0x10);
        (frame, uint first) = Append(frame, ParticleDescriptor, 0x6783F0);
        if (first != 0)
        {
            frame = Random289(frame, out int x);
            frame = Random289(frame, out int z);
            frame = Random289(frame, out int y);
            NativeBattleModulePoolTask task = Read(frame, first).WriteDword(0xC, 0x106EC38).WriteWord(0x14, unchecked((ushort)(Signed(owner, 0x14) + (x & 511) - 256))).WriteWord(0x18, unchecked((ushort)(Signed(owner, 0x18) + (z & 511) - 256)));
            task = task.WriteWord(0x16, unchecked((ushort)TargetParticleY289(target, y))).WriteWord(0x1C, 0).WriteWord(0x1E, 0).WriteWord(0x20, 0).WriteWord(0x22, 16).WriteWord(0x1A, 0);
            frame = Random289(frame, out int scale);
            frame = Write(frame, first, task.WriteDword(0x10, (uint)((scale & 511) + 1500)));
        }
        (frame, uint second) = Append(frame, ParticleDescriptor, 0x6783F0);
        if (second != 0)
        {
            frame = Random289(frame, out int x);
            frame = Random289(frame, out int z);
            frame = Random289(frame, out int y);
            NativeBattleModulePoolTask task = Read(frame, second).WriteDword(0xC, 0x106E9B0).WriteWord(0x14, unchecked((ushort)(Signed(owner, 0x14) + (x & 255) - 128))).WriteWord(0x18, unchecked((ushort)(Signed(owner, 0x18) + (z & 255) - 128))).WriteWord(0x1C, 0);
            task = task.WriteWord(0x16, unchecked((ushort)TargetParticleY289(target, y)));
            frame = Random289(frame, out int vz);
            task = task.WriteWord(0x20, unchecked((ushort)(-32 - (vz & 15))));
            frame = Random289(frame, out int vy);
            task = task.WriteWord(0x1E, unchecked((ushort)(-32 - (vy & 15))));
            frame = TransformParticleVelocity289(Write(frame, second, task), second);
            task = Read(frame, second).WriteWord(0x22, 12).WriteWord(0x1A, 0);
            frame = Random289(frame, out int scale);
            frame = Write(frame, second, task.WriteDword(0x10, (uint)((scale & 1023) + 2000)));
        }
        return frame;

        int TargetParticleY289(uint modelAddress, int random)
        {
            NativeBattleGraphicsModel model = inputs.ReadModel(frame, modelAddress);
            short angle = (model.Transform ?? throw new NotSupportedException("Effect289 requires its current target Angle36.")).Angle36;
            short minimum = model.MinimumY146 ?? throw new NotSupportedException("Effect289 requires its current target MinimumY146.");
            return unchecked((short)((unchecked(random * minimum) >> 15) + angle));
        }
    }
}
