using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleShockwavePulsar260Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleShockwavePulsar260Controller
{
    public static NativeBattleShockwavePulsar260TaskStep AdvanceMain(NativeBattleShockwavePulsar260Frame frame, uint address, NativeBattleShockwavePulsar260Resources resources, NativeBattleShockwavePulsar260Inputs inputs)
    {
        int age = Signed(Read(frame, address), 0xC);
        int targets = Signed(Read(frame, address), 0xE);
        int fade = unchecked(2300 * (262 - age)) / 12;
        frame = frame with { State = frame.State.WriteDword(0x25086A8, unchecked((uint)(age < 50 ? unchecked(age << 12) / 50 : age >= 250 ? fade : 4096))).WriteDword(0x25086B0, unchecked((uint)(age < 12 ? unchecked(2300 * age) / 12 : age >= 250 ? fade : 2300))) };
        if (unchecked((uint)(age - 50)) < 30)
        {
            if (age == 50)
            {
                frame = InitializeAct(frame, address, 0x198, 0x218, 0x112A974, resources);
            }
            frame = AdvanceAct(frame, address, 0x198, 0x218, 0x112A974, MainContext(frame, -16384, 0, 0x112D190, -128), resources, inputs).Frame;
        }
        int elapsed = age - 80;
        if (unchecked((uint)elapsed) < 182)
        {
            if (unchecked((uint)(elapsed - 126)) < 8)
            {
                int flashAge = elapsed - 126;
                int factor = flashAge < 7 ? ((flashAge + 1) << 12) / 7 : 4096;
                frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260Flash(255, 255, 255, factor));
            }
            if (elapsed == 0)
            {
                frame = WithPool(frame, 2, NativeBattleEffectTaskPool.Initialize(ChildDescriptor, frame.State.ReadDword(WorkingGlobal) + ChildrenOffset, ChildStride, ChildCount));
                for (int actor = 0; actor < 7; actor++)
                {
                    if ((inputs.ReadModel(actor).Flags0 & 2) != 0)
                    {
                        frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260ActorFlags((byte)actor, 0, 4));
                    }
                }
                frame = InitializeAct(frame, address, 0x198, 0x1100, 0x112015C, resources);
                for (int index = 0; index < targets; index++)
                {
                    uint record = address + 0x10 + (uint)(index * 0x24);
                    (NativeBattleShockwavePulsar260Frame allocated, uint child) = Append(frame, 2, TargetCallback);
                    frame = allocated;
                    int actor = Actor(ReadDword(frame, record));
                    frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260ActorWrite((byte)actor, 0x1E, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)4096)), 2));
                    frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260ActorWrite((byte)actor, 0x58, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(4096)), 4));
                    short angle = unchecked((short)((actor & 1) != 0 ? -7669 : 7669));
                    frame = WriteWord(frame, record + 0xC, unchecked((ushort)angle));
                    frame = WriteWord(frame, record + 0xE, unchecked((ushort)angle));
                    frame = WriteWord(frame, record + 0x10, 0);
                    frame = Write(frame, child, Read(frame, child).WriteWord(0x10, 0));
                    frame = InitializeAct(frame, child, 0x14, 0xBC, 0x111F09C, resources);
                    frame = Write(frame, child, Read(frame, child).WriteDword(0xC, record));
                }
            }
            else if (elapsed is >= 13 and <= 40)
            {
                frame = SpawnMainPuffs(frame);
            }
            else if (elapsed is >= 115 and < 131)
            {
                frame = SpawnSecondaryParticles(frame, address, targets);
            }
            int y;
            short depth;
            int zOffset;
            if (elapsed < 134)
            {
                frame = frame with { State = frame.State.WriteDword(0x25086A8, 4096).WriteDword(0x25086B0, 4096) };
                frame = DrawMainTargets(frame, address, targets, inputs);
                y = 0;
                depth = 2048;
                zOffset = 2048;
            }
            else
            {
                if (elapsed == 134)
                {
                    for (int index = 0; index < targets; index++)
                    {
                        uint record = address + 0x10 + (uint)(index * 0x24);
                        int actor = Actor(ReadDword(frame, record));
                        frame = MainLinkedFlags(frame, actor, ReadDword(frame, record + 0x14) != 0, 12, 0, inputs);
                        frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260ActorWrite((byte)actor, 0x1C, ReadView(frame, record + 4, 8), 8));
                    }
                    frame = RestoreMainFlags(frame, inputs);
                }
                int transition = elapsed - 134;
                int factor = 4096 - unchecked(1796 * transition) / 32;
                int a = unchecked((int)frame.State.ReadDword(0x25086A8));
                if (a >= 2300)
                {
                    frame = frame with { State = frame.State.WriteDword(0x25086A8, unchecked((uint)(transition < 32 ? factor : 2300))) };
                }
                int b = unchecked((int)frame.State.ReadDword(0x25086B0));
                if (b >= 2300 && transition < 32)
                {
                    frame = frame with { State = frame.State.WriteDword(0x25086B0, unchecked((uint)factor)) };
                }
                y = 0;
                depth = -4096;
                zOffset = 0;
            }
            frame = AdvanceAct(frame, address, 0x198, 0x1100, 0x112015C, MainContext(frame, y, zOffset, 0x112D1B4, depth), resources, inputs).Frame;
        }
        frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260DrawPuffs());
        if (age < 213)
        {
            frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260DrawSecondaryParticles());
        }
        frame = UpdatePuffs(frame);
        frame = UpdateSecondaryParticles(frame);
        int sound = age == 0 ? 0 : age == 80 ? 1 : age == 170 ? 2 : -1;
        if (sound >= 0)
        {
            NativeBattleShockwavePulsar260Sound descriptor = resources.Sounds[sound];
            frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260SoundPlay(descriptor.Address, descriptor.Value, 0, 128));
        }
        int late = age - 1;
        if (unchecked((uint)late) < 32 && (late & 7) == 0 && late / 8 < targets)
        {
            uint record = address + 0x10 + (uint)((late / 8) * 0x24);
            (NativeBattleShockwavePulsar260Frame allocated, uint child) = Append(frame, 2, LateTargetCallback);
            frame = allocated;
            int actor = Actor(ReadDword(frame, record));
            frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260CaptureBounds((byte)actor, record + 0x20));
            frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260ActorFlags((byte)actor, 0, 8));
            frame = Write(frame, child, Read(frame, child).WriteWord(0x10, 0).WriteWord(0x12, unchecked((ushort)(50 - late))));
            frame = InitializeAct(frame, child, 0x14, 0x100, 0x111DEBC, resources);
            frame = Write(frame, child, Read(frame, child).WriteDword(0xC, record));
        }
        frame = AdvanceMainCamera(frame, age, inputs);
        if (age == 213)
        {
            uint destination = frame.State.ReadDword(WorkingGlobal) + StreamOffset;
            frame = frame with { Memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, 360, destination, 1) };
            frame = frame with { Memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, 361, destination, 0) };
        }
        frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260AdvanceIo());
        if (age == 252)
        {
            frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260ContactAll());
        }
        if (age >= 262)
        {
            frame = RestoreMainFlags(frame, inputs);
            frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260CameraRestore());
            frame = frame with { State = frame.State.WriteDword(0x25086B0, 0).WriteDword(0x25086A8, 0) };
            NativeBattleEffectTaskPool children = frame.Effect.Pools[2];
            frame = WithPool(frame, 2, children.UpdateLinks(null, null));
            NativeBattleShockwavePulsar260IoStep poll = inputs.PollIo(frame, 1);
            return new(poll.Frame, poll.Busy ? 0 : 2);
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0xC, unchecked((ushort)(age + 1))));
        return new(frame, 0);
    }

    private static NativeBattleEffectArena MainContext(NativeBattleShockwavePulsar260Frame frame, int y, int zOffset, uint dispatch, short depth)
    {
        return NativeBattleEffectArena.Clear().MarkUnknown(0, 20).Write(0, BitConverter.GetBytes(unchecked((short)frame.State.ReadDword(0x25086A0)))).Write(2, BitConverter.GetBytes(unchecked((short)y))).Write(4, BitConverter.GetBytes(unchecked((short)(frame.State.ReadDword(0x25086A4) + zOffset)))).Write(8, BitConverter.GetBytes(dispatch)).Write(12, BitConverter.GetBytes(depth)).Write(16, BitConverter.GetBytes(frame.State.ReadDword(WorkingGlobal) + MorphOffset));
    }

    private static NativeBattleShockwavePulsar260Frame MainLinkedFlags(NativeBattleShockwavePulsar260Frame frame, int actor, bool linked, ushort clear, ushort set, NativeBattleShockwavePulsar260Inputs inputs)
    {
        int current = actor;
        HashSet<int> seen = [];
        do
        {
            if (!seen.Add(current))
            {
                throw new InvalidDataException("Shockwave Pulsar linked actor chain does not return to its selected actor.");
            }
            if (!linked || (inputs.ReadModel(current).Flags0 & 2) != 0)
            {
                frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260ActorFlags((byte)current, clear, set));
            }
            int? next = linked ? inputs.ReadLinkedActor(current) : null;
            if (next is null || next == actor)
            {
                break;
            }
            current = next.Value;
        }
        while (true);
        return frame;
    }

    private static NativeBattleShockwavePulsar260Frame DrawMainTargets(NativeBattleShockwavePulsar260Frame frame, uint address, int targets, NativeBattleShockwavePulsar260Inputs inputs)
    {
        for (int index = 0; index < targets; index++)
        {
            uint record = address + 0x10 + (uint)(index * 0x24);
            int actor = Actor(ReadDword(frame, record));
            bool linked = ReadDword(frame, record + 0x14) != 0;
            short height = unchecked((short)((unchecked((short)ReadWord(frame, record + 0x20)) + unchecked((short)ReadWord(frame, record + 0x22))) >> 1));
            NativeBattleEffectArena pivot = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes((short)0)).Write(2, BitConverter.GetBytes(height)).Write(4, BitConverter.GetBytes((short)0));
            int current = actor;
            HashSet<int> seen = [];
            do
            {
                if (!seen.Add(current))
                {
                    throw new InvalidDataException("Shockwave Pulsar body chain does not return to its selected actor.");
                }
                if (!linked || (inputs.ReadModel(current).Flags0 & 2) != 0)
                {
                    frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260DrawBody((byte)current, ReadView(frame, record + 0xC, 8), pivot, unchecked((short)ReadWord(frame, record + 0x1C))));
                }
                int? next = linked ? inputs.ReadLinkedActor(current) : null;
                if (next is null || next == actor)
                {
                    break;
                }
                current = next.Value;
            }
            while (true);
            frame = MainLinkedFlags(frame, actor, linked, 0, 4, inputs);
        }
        return frame;
    }

    private static NativeBattleShockwavePulsar260Frame RestoreMainFlags(NativeBattleShockwavePulsar260Frame frame, NativeBattleShockwavePulsar260Inputs inputs)
    {
        for (int actor = 0; actor < 7; actor++)
        {
            if ((inputs.ReadModel(actor).Flags0 & 2) != 0)
            {
                byte original = frame.State.ReadView(SavedFlagsAddress + (uint)(actor * 4), 1).Read(0, 1)[0];
                frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260ActorFlags((byte)actor, 4, (ushort)(original & 4)));
            }
        }
        return frame;
    }
}
