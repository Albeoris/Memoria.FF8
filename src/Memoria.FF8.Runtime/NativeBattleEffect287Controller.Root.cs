using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect287Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect287Controller
{
    public static NativeBattleEffect287Step AdvanceRoot6F2F20(NativeBattleEffect287Frame frame, uint address, NativeBattleEffect287Resources resources, NativeBattleEffect287Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        bool side = Read(frame, address).ReadDword(16) != 0;
        uint arena = frame.State.ReadDword(ArenaGlobal);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, arena + (uint)(side ? FirstPacketOffset : SecondPacketOffset)).WriteDword(IntensityGlobal, 0) };
        frame = Write(frame, address, Read(frame, address).WriteDword(16, side ? 0u : 1u));
        if (age == 1 && Read(frame, address).ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            foreach (NativeBattleCureFamilyPool shape in CommonPools)
            {
                NativeBattleEffectTaskPool pool = CreatePool(frame, shape, true);
                frame = frame with { Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [pool]), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) } };
                if (shape.Descriptor == MeshDescriptor)
                {
                    frame = frame with { State = frame.State.DetachMeshQueue() };
                }
            }
            (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
            if (main == 0)
            {
                throw new InvalidDataException("Effect287 requires its mandatory main task.");
            }
            frame = ClearDwords701200(frame, main + 12, 0x267);
            frame = NativeBattleEffect287Programme.Initialize(frame, main + 0x2C, FirstProgrammeAddress, 0x8C4, resources);
            frame = NativeBattleEffect287Programme.Initialize(frame, main + 0x8F0, SecondProgrammeAddress, 0xB8, resources);
            for (int index = 0; index < TaperCount; index++)
            {
                frame = WriteBytes(frame, arena + TaperOffset + (uint)(index * 2), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((short)NativeBattleCameraMath.Sine(index * 2048 / 7)))), 2);
            }
            frame = CaptureTargets(frame, main, inputs);
            frame = inputs.Execute(frame, new NativeBattleEffect287Initialize534110(arena + IoOffset));
        }
        bool initialized = Read(frame, address).ReadByte(15) != 0;
        int mains = 0;
        int targets = 0;
        if (initialized)
        {
            BattleModelOwner caster = frame.State.ReadModelAddress(CasterGlobal);
            frame = inputs.Execute(frame, new NativeBattleEffect287Compose5095B0(caster));
            (frame, mains) = AdvanceDescriptor(frame, MainDescriptor, resources, inputs);
            // Root age is one callback ahead of the main target birth wheel.
            const int firstExpandedBirth = 53;
            if (age >= firstExpandedBirth && (age - firstExpandedBirth) % 4 == 0 && frame.State.NextExpandedTarget < frame.State.ExpandedTargets.Length)
            {
                (frame, uint child) = Append(frame, TargetDescriptor, 0x6F3D00);
                if (child != 0)
                {
                    int next = frame.State.NextExpandedTarget;
                    frame = InitializeTarget(frame, child, frame.State.ExpandedTargets[next], resources, inputs);
                    frame = frame with { State = frame.State with { NextExpandedTarget = next + 1 } };
                }
            }
            (frame, _) = AdvanceDescriptor(frame, MeshDescriptor, resources, inputs);
            frame = inputs.Execute(frame, new NativeBattleEffect287Restore508C90(caster));
            (frame, targets) = AdvanceDescriptor(frame, TargetDescriptor, resources, inputs);
            (frame, _) = AdvanceDescriptor(frame, SmallDescriptor, resources, inputs);
            (frame, _) = AdvanceDescriptor(frame, SpriteDescriptor, resources, inputs);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect287Stage5713E0(unchecked((int)frame.State.ReadDword(IntensityGlobal)), 0));
        bool expandedPending = !frame.State.ExpandedTargets.IsEmpty && (frame.State.NextExpandedTarget < frame.State.ExpandedTargets.Length || targets != 0);
        if (initialized && mains == 0 && !expandedPending)
        {
            NativeBattleEffect287PollStep poll = inputs.IoSelect5342C0(frame, 0);
            frame = poll.Frame;
            if (poll.Complete)
            {
                return new(frame with { State = frame.State with { ExpandedTargets = [], NextExpandedTarget = 0 } }, 2);
            }
            return new(Write(frame, address, Read(frame, address).WriteWord(12, 2)), 0);
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    public static NativeBattleEffect287Frame CaptureTargets(NativeBattleEffect287Frame frame, uint main, NativeBattleEffect287Inputs inputs)
    {
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleActionHeaderSlot header = CurrentHeader(frame, packets);
        int count = header.ReadContactCount();
        int physicalCount = Math.Min(count, 3);
        frame = Write(frame, main, Read(frame, main).WriteWord(14, (ushort)physicalCount));
        BattleContactCursor first = header.FirstContact;
        for (int index = 0; index < physicalCount; index++)
        {
            BattleModelOwner target = packets.ReadContact(first.Advance(index)).Primary.Recipient;
            NativeBattleModulePoolTask recipientTask = Read(frame, main);
            recipientTask.WriteExecutionReference(0x20 + index * 4, 4, new BattleExecutionReference.Model(target), packets.ModelAliases.EncodeAddress(target));
            frame = Write(frame, main, recipientTask);
        }
        if (count > physicalCount)
        {
            ImmutableArray<BattleModelOwner>.Builder expanded = ImmutableArray.CreateBuilder<BattleModelOwner>(count - physicalCount);
            for (int index = physicalCount; index < count; index++)
            {
                expanded.Add(packets.ReadContact(first.Advance(index)).Primary.Recipient);
            }
            frame = frame with { State = frame.State with { ExpandedTargets = expanded.MoveToImmutable(), NextExpandedTarget = 0 } };
        }
        return frame;
    }

    private static NativeBattleActionHeaderSlot CurrentHeader(NativeBattleEffect287Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect287 requires its current action header.");
        return packets.ReadHeader(parameters.Header);
    }
}
