using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect338Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect338Controller
{
    public static (NativeBattleEffect338Frame Frame, int Result) AdvanceMain(NativeBattleEffect338Frame frame, uint address, NativeBattleEffect338Resources resources, NativeBattleEffect338Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        int intensity = age < 8 ? unchecked(age * 256) : age > 68 ? unchecked((76 - age) * 256) : 2048;
        frame = inputs.Execute(frame, new NativeBattleEffect338Stage5713E0(intensity));
        uint target = frame.State.ReadDword(TargetGlobal);
        short height = Signed(Read(frame, address), 22);
        if (age == 7)
        {
            frame = SpawnProgramme(frame, 0x1376678, 0x1372E60, height, resources);
            frame = inputs.Execute(frame, new NativeBattleEffect338SelectSequence505C00(target, 4));
        }
        if ((uint)(age - 8) < 48)
        {
            bool hit = true;
            switch (age)
            {
                case 12:
                    frame = SpawnProgramme(frame, 0x1376688, 0x1372E60, height, resources);
                    break;
                case 15:
                    frame = SpawnProgramme(frame, 0x1376680, 0x137420C, height, resources);
                    break;
                case 18:
                    frame = SpawnProgramme(frame, 0x1376690, 0x137420C, height, resources);
                    break;
                case 23:
                case 29:
                    frame = SpawnImpactParticles(frame, address, 0x1376670, resources);
                    break;
                case 26:
                case 32:
                    frame = SpawnImpactParticles(frame, address, 0x1376668, resources);
                    break;
                case 39:
                    frame = inputs.Execute(frame, new NativeBattleEffect338SelectSequence505C00(target, 4));
                    frame = SpawnFinalProgramme(frame, address, 0x1376660, resources);
                    hit = false;
                    break;
                case 42:
                    int contact = inputs.ReadPackets(frame).Headers[Parameters(frame).HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Effect338 contact requires its actual physical header.");
                    frame = inputs.Execute(frame, new NativeBattleEffect338Contact506690(contact));
                    frame = SpawnFinalProgramme(frame, address, 0x1376658, resources);
                    hit = false;
                    break;
                case 45:
                    frame = SpawnFinalProgramme(frame, address, 0x1376658, resources);
                    hit = false;
                    break;
                case 48:
                    frame = SpawnFinalProgramme(frame, address, 0x1376660, resources);
                    hit = false;
                    break;
                default:
                    hit = false;
                    break;
            }
            if (hit)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect338SelectSequence505C00(target, 4));
            }
            if (age is >= 40 and <= 50)
            {
                for (int index = 0; index < 2; index++)
                {
                    (frame, uint particle) = AllocateParticle(frame);
                    if (particle == 0)
                    {
                        return (frame, 0);
                    }
                    frame = WriteBytes(frame, particle + 10, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)12)), 2);
                    frame = Roll(frame, out ushort boneRandom);
                    int bone = unchecked(boneRandom * inputs.ReadBoneCount(frame, target)) >> 15;
                    frame = inputs.Execute(frame, new NativeBattleEffect338ReadBone502170(target, bone, particle + 4));
                    frame = WriteBytes(frame, particle + 12, NativeBattleEffectArena.Clear().Write(0, new byte[4]), 4);
                    frame = WriteBytes(frame, particle + 16, NativeBattleEffectArena.Clear().Write(0, new byte[2]), 2);
                    frame = WriteBytes(frame, particle + 20, NativeBattleEffectArena.Clear().Write(0, new byte[4]), 4);
                    frame = Roll(frame, out ushort scaleRandom);
                    frame = WriteBytes(frame, particle, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0x137130Cu)), 4);
                    frame = WriteBytes(frame, particle + 18, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((ushort)((scaleRandom & 4095) + 2048)))), 2);
                }
            }
            if ((uint)(age - 32) < 5)
            {
                int ratio = NativeBattleCameraMath.Sine(1024 - (age - 32) * 256);
                NativeBattleVector first = new(Word(frame, 0x2556240), Word(frame, 0x2556242), Word(frame, 0x2556244));
                NativeBattleVector second = new(Word(frame, 0x2556248), Word(frame, 0x255624A), Word(frame, 0x255624C));
                NativeBattleWeightedPointsStep blended = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, first, second, unchecked((short)(4096 - ratio)), unchecked((short)ratio), inputs.Colors);
                frame = Write(frame with { Memory = blended.Memory }, address, Read(frame, address).WriteDword(0x7C, unchecked((uint)(int)blended.Position.Y)).WriteDword(0x80, unchecked((uint)(int)blended.Position.Z)));
            }
        }
        if ((uint)age < 8)
        {
            int progress = (int)((uint)(age << 10) / 7);
            int remaining = 1024 - progress;
            int y = unchecked(Word(frame, 0x255624A) - NativeBattleCameraMath.Sine(remaining) * 2000 / 4096);
            int z = unchecked(Word(frame, 0x255624C) + remaining * 4000 / 1024);
            frame = Write(frame, address, Read(frame, address).WriteDword(0x7C, unchecked((uint)y)).WriteDword(0x80, unchecked((uint)z)));
            int cue = 4096 - NativeBattleCameraMath.Cosine((4096 - NativeBattleCameraMath.Cosine(progress)) >> 2);
            frame = DrawShimmer(frame, address, age << 8, cue, resources, inputs);
        }
        if ((uint)(age - 56) < 4)
        {
            int progress = (int)((uint)((age - 56) << 10) / 3);
            for (int count = 8; count >= 0; count--)
            {
                (frame, uint particle) = AllocateParticle(frame);
                if (particle == 0)
                {
                    break;
                }
                frame = Roll(frame, out ushort angle);
                frame = Roll(frame, out ushort radiusRandom);
                int radius = (radiusRandom & 63) + 32;
                short vx = unchecked((short)(NativeBattleCameraMath.Sine(angle) * radius / 4096));
                short vz = unchecked((short)(NativeBattleCameraMath.Cosine(angle) * radius / 4096));
                NativeBattleEffectArena row = ReadBytes(frame, particle, 24).Write(0, BitConverter.GetBytes(0x137130Cu)).Write(4, BitConverter.GetBytes(unchecked((short)(Word(frame, 0x2556240) + vx)))).Write(6, BitConverter.GetBytes(Word(frame, 0x2556242))).Write(8, BitConverter.GetBytes(unchecked((short)(Word(frame, 0x2556244) + vz)))).Write(10, BitConverter.GetBytes((ushort)12)).Write(12, BitConverter.GetBytes(vx)).Write(14, new byte[2]).Write(16, BitConverter.GetBytes(vz)).Write(20, new byte[3]);
                frame = Roll(frame, out ushort scaleRandom);
                row = row.Write(18, BitConverter.GetBytes(unchecked((ushort)((scaleRandom & 4095) + 3000))));
                frame = WriteBytes(frame, particle, row, 24);
            }
            int y = unchecked(Word(frame, 0x2556242) - NativeBattleCameraMath.Sine(progress) * 2000 / 4096);
            int z = unchecked(Word(frame, 0x2556244) + progress * 1000 / 1024);
            frame = Write(frame, address, Read(frame, address).WriteDword(0x7C, unchecked((uint)y)).WriteDword(0x80, unchecked((uint)z)));
        }
        if ((uint)(age - 60) < 8)
        {
            int progress = (age - 59) * 128;
            int y = unchecked(Word(frame, 0x2556242) - NativeBattleCameraMath.Sine(progress) * 4000 / 4096 - 2000);
            int z = unchecked(Word(frame, 0x2556244) + progress * 4000 / 1024 + 1000);
            frame = Write(frame, address, Read(frame, address).WriteDword(0x7C, unchecked((uint)y)).WriteDword(0x80, unchecked((uint)z)));
        }
        if ((uint)(age - 56) < 12)
        {
            int angle = (int)((uint)((age - 56) << 12) / 12) >> 2;
            int cue = 4096 - NativeBattleCameraMath.Sine(NativeBattleCameraMath.Sine(angle) >> 2);
            frame = DrawShimmer(frame, address, (age - 56) << 8, cue, resources, inputs);
        }
        if (unchecked((ushort)age) < 68)
        {
            NativeBattleEffect338PoseStep advanced = NativeBattleEffect338Model.Advance6FBDB0(frame.State.Skeleton!, Read(frame, address), resources.Model);
            frame = Write(frame with { State = frame.State with { Skeleton = advanced.Skeleton } }, address, advanced.Task);
            frame = inputs.Execute(frame, new NativeBattleEffect338DrawModel6A79B0(address));
        }
        NativeBattleModulePoolTask main = Read(frame, address);
        NativeBattleActionMatrix particleWorld = NativeBattlePandemona291Restoration.Rotation701270(0) with { X = unchecked((int)main.ReadDword(0x78)), Y = unchecked((int)main.ReadDword(0x7C)), Z = unchecked((int)main.ReadDword(0x80)) };
        frame = frame with { Memory = frame.Memory.PublishEffectLight1D99AB0(NativeBattlePackedMatrix32.FromMatrix(particleWorld, 0)) };
        frame = DrawParticles(frame, resources, inputs);
        frame = AdvanceParticleBank(frame);
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect338Sound501330(resources.Sound, 0, 128));
        }
        byte? nextPose = age switch { 8 => 1, 56 => 2, 60 => 3, _ => null };
        if (nextPose is byte selector)
        {
            NativeBattleEffect338PoseStep selected = NativeBattleEffect338Model.Select6574D0(frame.State.Skeleton!, Read(frame, address), resources.Model, selector);
            frame = Write(frame with { State = frame.State with { Skeleton = selected.Skeleton } }, address, selected.Task);
        }
        if (age == 72)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect338StopVibration4A2940(unchecked((int)Read(frame, address).ReadDword(16))));
        }
        short nextAge = unchecked((short)(Read(frame, address).ReadWord(12) + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)nextAge)));
        if (nextAge < 76)
        {
            return (frame, 0);
        }
        frame = Dword(frame, 0x255623C, 0);
        return (inputs.Execute(frame, new NativeBattleEffect338QueueFile5341D0(362, unchecked(frame.State.ReadDword(ArenaGlobal) + 0x5CD8), 0)), 2);
    }

    private static NativeBattleEffect338Frame Roll(NativeBattleEffect338Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
}
