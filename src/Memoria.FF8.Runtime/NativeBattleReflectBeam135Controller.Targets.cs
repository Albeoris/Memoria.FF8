using static Memoria.FF8.Runtime.NativeBattleReflectBeam135Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleReflectBeam135Controller
{
    public static (NativeBattleReflectBeam135Frame Frame, int Result) AdvanceTarget(NativeBattleReflectBeam135Frame frame, uint address, NativeBattleReflectBeam135Resources resources, NativeBattleReflectBeam135Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        if (age < 16)
        {
            if ((age & 1) == 0)
            {
                frame = SpawnTargetParticle(frame, task, ParticleBCallback, 255, 127, 255);
            }
            return TargetTail(frame, address, inputs);
        }
        if (age < 32 && (age & 3) == 0)
        {
            frame = SpawnTargetParticle(frame, task, ParticleACallback, 511, 511, 511);
        }
        if (age < 20)
        {
            for (int index = 0; index < 4; index++)
            {
                (frame, uint childAddress) = Append(frame, 4, ParticleCCallback);
                if (childAddress == 0)
                {
                    continue;
                }
                NativeBattleModulePoolTask child = Vector(Read(frame, childAddress), 12, Vector(task, 20)).WriteWord(18, 0);
                frame = DrawRandom(frame, out ushort first);
                frame = DrawRandom(frame, out ushort second);
                frame = DrawRandom(frame, out ushort third);
                short x = unchecked((short)((first & 511) - 256));
                short y = unchecked((short)((second & 511) - 256));
                short z = unchecked((short)((third & 511) - 256));
                child = Vector(child, 20, new(x, y, z));
                child = Vector(child, 28, new(unchecked((short)(-x >> 4)), unchecked((short)(-y >> 4)), unchecked((short)(-z >> 4))));
                frame = Write(frame, childAddress, child);
            }
        }
        task = Read(frame, address);
        NativeBattleReflectBeam135ActStep advanced = inputs.AdvanceAct(frame, address, resources.TargetAct, 0x1C, 0x310, ActContext(frame, TaskView(task, 0x14, 8), -128));
        if (advanced.Remaining == 0)
        {
            return (advanced.Frame, 2);
        }
        return TargetTail(advanced.Frame, address, inputs);
    }

    private static NativeBattleReflectBeam135Frame SpawnTargetParticle(NativeBattleReflectBeam135Frame frame, NativeBattleModulePoolTask target, uint callback, int xMask, int yMask, int zMask)
    {
        (frame, uint address) = Append(frame, 4, callback);
        if (address == 0)
        {
            return frame;
        }
        NativeBattleModulePoolTask child = Read(frame, address).WriteWord(18, 0);
        frame = DrawRandom(frame, out ushort x);
        frame = DrawRandom(frame, out ushort y);
        frame = DrawRandom(frame, out ushort z);
        child = Vector(child, 12, new(unchecked((short)(Signed(target, 20) + (x & xMask) - (xMask + 1) / 2)), unchecked((short)(Signed(target, 22) + (y & yMask) - (yMask + 1) / 2)), unchecked((short)(Signed(target, 24) + (z & zMask) - (zMask + 1) / 2))));
        return Write(frame, address, child);
    }

    private static (NativeBattleReflectBeam135Frame Frame, int Result) TargetTail(NativeBattleReflectBeam135Frame frame, uint address, NativeBattleReflectBeam135Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (Signed(task, 12) == 25)
        {
            int actor = Actor(task.ReadDword(16));
            int first = FirstContact(frame, inputs);
            int count = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C].ReadByte(16);
            for (int index = 0; index < count; index++)
            {
                if (inputs.Packets.ReadContact(first + index).Primary.ActorIndex == actor)
                {
                    frame = inputs.Execute(frame, new NativeBattleReflectBeam135Contact(index));
                }
            }
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)))), 0);
    }

    public static (NativeBattleReflectBeam135Frame Frame, int Result) AdvanceParticle(NativeBattleReflectBeam135Frame frame, uint address, NativeBattleReflectBeam135Resources resources, NativeBattleReflectBeam135Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (uint Source, short Scale, short Depth, short Lifetime) selected = task.Callback switch
        {
            ParticleACallback => (SpriteCAddress, (short)6000, (short)-1024, (short)16),
            ParticleBCallback => (SpriteAAddress, (short)2048, (short)-1024, (short)7),
            ParticleCCallback => (SpriteBAddress, (short)2048, (short)-512, (short)8),
            _ => throw new NotSupportedException("Reflect Beam selected an unsupported particle callback.")
        };
        (frame, uint temporary) = Allocate(frame, 180);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadCamera(frame), Vector(task, 12), Signed(task, 18), selected.Scale, selected.Depth) };
        NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(RequireStaging(frame), temporary, selected.Source, 0).Write(temporary + 4, BitConverter.GetBytes(task.ReadWord(18)));
        frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleReflectBeam135DrawSprite180(temporary, selected.Source) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(135, Read(frame, address)) with { ResourceGeneration = selected.Source } });
        frame = Release(frame, 180);
        task = Read(frame, address);
        if (task.Callback == ParticleCCallback)
        {
            NativeBattleVector velocity = new(unchecked((short)(Signed(task, 20) + Signed(task, 28))), unchecked((short)(Signed(task, 22) + Signed(task, 30))), unchecked((short)(Signed(task, 24) + Signed(task, 32))));
            task = Vector(task, 20, velocity);
            task = Vector(task, 12, new(unchecked((short)(Signed(task, 12) + velocity.X)), unchecked((short)(Signed(task, 14) + velocity.Y)), unchecked((short)(Signed(task, 16) + velocity.Z))));
        }
        ushort next = unchecked((ushort)(task.ReadWord(18) + 1));
        return (Write(frame, address, task.WriteWord(18, next)), unchecked((short)next) < selected.Lifetime ? 0 : 2);
    }
}
