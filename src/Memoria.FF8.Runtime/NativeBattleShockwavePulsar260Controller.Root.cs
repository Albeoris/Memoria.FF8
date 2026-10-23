using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleShockwavePulsar260Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleShockwavePulsar260Controller
{
    public static NativeBattleShockwavePulsar260Frame Initialize(NativeBattleShockwavePulsar260Frame frame, NativeBattleShockwavePulsar260Resources resources, NativeBattleShockwavePulsar260Inputs inputs)
    {
        RootRequireBinding(frame);
        resources.Validate();
        NativeBattleEffectParameters parameters = RootParameters(frame);
        _ = RootModelAddress(parameters.Actor78);
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Shockwave Pulsar requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Shockwave Pulsar requires its actual texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(Arena(frame), tim);
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 1);
        (roots, int slot) = roots.Append(RootCallback);
        roots = roots with { Slots = roots.Slots.SetItem(slot, roots.Slots[slot].WriteDword(0xC, 0)) };
        NativeBattleShockwavePulsar260State state = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, RootModelAddress(parameters.Actor78));
        return frame with { State = state, Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [roots], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
    }

    public static NativeBattleShockwavePulsar260Frame Advance(NativeBattleShockwavePulsar260Frame frame, NativeBattleShockwavePulsar260Resources resources, NativeBattleShockwavePulsar260Inputs inputs)
    {
        RootRequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 3) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Shockwave Pulsar requires its original root and working task pools.");
        }
        NativeBattleShockwavePulsar260Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            uint address = checked(pool.BankAddress + (uint)(slot * pool.Stride));
            if (Read(current, address).Callback != RootCallback)
            {
                throw new NotSupportedException("Shockwave Pulsar reached an unsupported root callback.");
            }
            NativeBattleShockwavePulsar260TaskStep step = RootAdvance(current, address, resources, inputs);
            current = step.Frame;
            return new(current.Effect.Pools[0], step.Result);
        });
        current = WithPool(current, 0, roots.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : RootDescriptor } };
    }

    private static NativeBattleShockwavePulsar260TaskStep RootAdvance(NativeBattleShockwavePulsar260Frame frame, uint address, NativeBattleShockwavePulsar260Resources resources, NativeBattleShockwavePulsar260Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint working = frame.State.ReadDword(WorkingGlobal);
        bool firstBank = task.ReadByte(0xE) != 0;
        frame = frame with { State = frame.State.WriteDword(PrimaryCursorGlobal, checked(working + (uint)(firstBank ? FirstPacketOffset : SecondPacketOffset))).WriteDword(SecondaryCursorGlobal, checked(working + (uint)(firstBank ? SecondPacketOffset : SecondPacketOffset + PacketBankBytes))).WriteDword(0x25086A8, 0).WriteDword(0x25086B0, 0) };
        frame = Write(frame, address, task.WriteByte(0xE, firstBank ? (byte)0 : (byte)1));
        if (task.ReadWord(0xC) == 1 && task.ReadByte(0xF) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(0xF, 1));
            frame = RootStart(frame, resources, inputs);
        }
        int mainSurvivors = 0;
        bool started = Read(frame, address).ReadByte(0xF) != 0;
        if (started)
        {
            NativeBattlePackedMatrix32 camera = inputs.ReadView(frame);
            NativeBattlePackedMatrix32 normalized = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Normalize67AAE0(camera.ReadMatrix()), null);
            NativeBattleEffectArena copied = frame.State.ReadView(CopiedViewAddress, 32);
            copied = copied.Write(0, normalized.Bytes.AsSpan().Slice(0, 18)).Write(20, normalized.Bytes.AsSpan().Slice(20, 12));
            frame = frame with { State = frame.State.WriteView(CopiedViewAddress, copied, 32) };
            NativeBattleShockwavePulsar260Frame current = frame;
            NativeBattleEffectTaskStep mains = current.Effect.Pools[1].Advance((pool, slot) =>
            {
                current = WithPool(current, 1, pool);
                uint mainAddress = checked(pool.BankAddress + (uint)(slot * pool.Stride));
                if (Read(current, mainAddress).Callback != MainCallback)
                {
                    throw new NotSupportedException("Shockwave Pulsar reached an unsupported main callback.");
                }
                NativeBattleShockwavePulsar260TaskStep step = AdvanceMain(current, mainAddress, resources, inputs);
                current = step.Frame;
                return new(current.Effect.Pools[1], step.Result);
            });
            current = WithPool(current, 1, mains.Pool);
            mainSurvivors = mains.Survivors;
            NativeBattleEffectTaskStep children = current.Effect.Pools[2].Advance((pool, slot) =>
            {
                current = WithPool(current, 2, pool);
                NativeBattleShockwavePulsar260TaskStep step = AdvanceTarget(current, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
                current = step.Frame;
                return new(current.Effect.Pools[2], step.Result);
            });
            frame = WithPool(current, 2, children.Pool);
        }
        frame = RootApplyStageFactor(frame, inputs, 0x25086A8, 0x25086AC, 7);
        frame = RootApplyStageFactor(frame, inputs, 0x25086B0, 0x25086B4, 8);
        if (started && mainSurvivors == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260StageWrite(15, 0, 0));
            return new(frame, 2);
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0xC, unchecked((ushort)(Read(frame, address).ReadWord(0xC) + 1))));
        return new(frame, 0);
    }

    private static NativeBattleShockwavePulsar260Frame RootStart(NativeBattleShockwavePulsar260Frame frame, NativeBattleShockwavePulsar260Resources resources, NativeBattleShockwavePulsar260Inputs inputs)
    {
        uint working = frame.State.ReadDword(WorkingGlobal);
        for (int actor = 0; actor < 7; actor++)
        {
            frame = frame with { State = frame.State.WriteDword(SavedFlagsAddress + (uint)actor * 4, inputs.ReadModel(actor).Flags0) };
        }
        NativeBattleEffectArena arena = Arena(frame);
        int workingOffset = Offset(working, 1);
        for (int index = 0; index < PuffsCount; index++)
        {
            arena = arena.Write(workingOffset + PuffsOffset + index * 16 + 15, [0]);
        }
        for (int index = 0; index < SecondaryParticlesCount; index++)
        {
            arena = arena.Write(workingOffset + SecondaryParticlesOffset + index * 16, [0, 0]);
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena }, State = frame.State.WriteDword(0x2508614, working + PuffsOffset).WriteDword(0x2508618, working + SecondaryParticlesOffset) };
        frame = frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, workingOffset) };
        NativeBattleEffectTaskPool mains = NativeBattleEffectTaskPool.Initialize(MainDescriptor, working + MainOffset, MainStride, 1);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, working + ChildrenOffset, ChildStride, ChildCount);
        (mains, int mainSlot) = mains.Append(MainCallback);
        mains = mains with { Slots = mains.Slots.SetItem(mainSlot, mains.Slots[mainSlot].WriteBytes(0xC, new byte[0x4A3 * 4])) };
        frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], mains, children] }, Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [mains, children]) };
        uint mainAddress = checked(mains.BankAddress + (uint)(mainSlot * mains.Stride));
        byte caster = checked((byte)Actor(frame.State.ReadDword(CasterGlobal)));
        frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260BindCaster(caster, mainAddress + 0x178, 0x111B3CC));
        frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260RingSequence(caster, 1));
        NativeBattleEffectParameters parameters = RootParameters(frame);
        if ((uint)parameters.HeaderIndex7C >= inputs.Packets.Headers.Length)
        {
            throw new InvalidDataException("Shockwave Pulsar requires its selected physical action header.");
        }
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[parameters.HeaderIndex7C];
        int count = inputs.FormationId == 796 ? 1 : header.ReadByte(16);
        frame = Write(frame, mainAddress, Read(frame, mainAddress).WriteWord(0xE, (ushort)count));
        List<int> actors = [];
        short[] minimum = [32767, 32767, 32767];
        short[] maximum = [-32767, -32767, -32767];
        for (int index = 0; index < count; index++)
        {
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Shockwave Pulsar requires its physical target-contact pointer.");
            int actor = inputs.Packets.ReadContact(checked(first + index)).Primary.ActorIndex;
            _ = RootModelAddress(actor);
            actors.Add(actor);
            uint record = checked(mainAddress + 0x10 + (uint)(index * 0x24));
            frame = WriteDword(frame, record, RootModelAddress(actor));
            frame = WriteView(frame, record + 4, inputs.ReadPosition8(actor), 8);
            frame = WriteView(frame, record + 0xC, frame.State.ReadView(0x2508690, 8), 8);
            NativeBattleShockwavePulsar260AnchorStep anchor = inputs.ReadAnchor571400(frame, actor);
            frame = anchor.Frame;
            short[] xyz = [anchor.Anchor.X, anchor.Anchor.Y, anchor.Anchor.Z];
            for (int axis = 0; axis < 3; axis++)
            {
                minimum[axis] = Math.Min(minimum[axis], xyz[axis]);
                maximum[axis] = Math.Max(maximum[axis], xyz[axis]);
            }
            short scale = inputs.ReadModel(actor).Transform?.Scale38 ?? throw new NotSupportedException("Shockwave Pulsar requires its current native model scaleWORD38.");
            frame = WriteDword(frame, record + 0x18, unchecked((uint)(0x800000 / (scale + 512))));
        }
        for (int index = 0; index < actors.Count; index++)
        {
            int actor = actors[index];
            bool grouped = true;
            HashSet<int> visited = [actor];
            int? linked = inputs.ReadLinkedActor(actor);
            while (linked is int next && next != actor)
            {
                if (next is < 0 or > 6 || !visited.Add(next))
                {
                    throw new NotSupportedException("Shockwave Pulsar requires its actual terminated or circular linked-model ring.");
                }
                if ((inputs.ReadModel(next).Flags0 & 2) != 0 && actors.Contains(next))
                {
                    grouped = false;
                    break;
                }
                linked = inputs.ReadLinkedActor(next);
            }
            frame = WriteDword(frame, checked(mainAddress + 0x10 + (uint)(index * 0x24) + 0x14), grouped ? 1u : 0u);
        }
        for (int axis = 0; axis < 3; axis++)
        {
            frame = frame with { State = frame.State.WriteWord(0x25086A0u + (uint)axis * 2, unchecked((ushort)((minimum[axis] + maximum[axis]) / 2))) };
        }
        return frame;
    }


    private static NativeBattleShockwavePulsar260Frame RootApplyStageFactor(NativeBattleShockwavePulsar260Frame frame, NativeBattleShockwavePulsar260Inputs inputs, uint currentAddress, uint previousAddress, ushort groups)
    {
        uint current = frame.State.ReadDword(currentAddress);
        uint previous = frame.State.ReadDword(previousAddress);
        if (current == previous)
        {
            return frame;
        }
        frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260StageWrite(groups, unchecked((short)current), 0));
        current = frame.State.ReadDword(currentAddress);
        if (unchecked((int)current) >= 4096)
        {
            frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260StageFlags(groups, 2, 0));
        }
        else if (unchecked((int)previous) >= 4096)
        {
            frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260StageFlags(groups, 0, 2));
        }
        return frame with { State = frame.State.WriteDword(previousAddress, current) };
    }

    private static NativeBattleEffectParameters RootParameters(NativeBattleShockwavePulsar260Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Shockwave Pulsar requires its captured native action parameters.");
    private static uint RootModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)actor * 156) : throw new InvalidDataException("Shockwave Pulsar selected an actor outside its seven-model bank.");
    private static void RootRequireBinding(NativeBattleShockwavePulsar260Frame frame)
    {
        if (frame.Effect.RequestedEffect != 260 || frame.Effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Shockwave Pulsar requires its original selected entry.");
        }
    }
}
