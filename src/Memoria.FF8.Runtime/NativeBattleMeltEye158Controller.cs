using static Memoria.FF8.Runtime.NativeBattleMeltEye158Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMeltEye158Controller
{
    public static NativeBattleMeltEye158Frame Initialize(NativeBattleMeltEye158Frame frame, NativeBattleMeltEye158Resources resources, NativeBattleMeltEye158Inputs inputs)
    {
        Require(frame);
        resources.Validate();
        NativeBattleActionHeaderSlot header = Header(frame, inputs);
        int firstContact = header.FirstContactIndex ?? throw new NotSupportedException("Melt-Eye requires its captured first contact.");
        int target = inputs.Packets.ReadContact(firstContact).Primary.ActorIndex;
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, RootCount);
        (root, int rootSlot) = root.Append(RootCallback);
        root = root.WriteSlot(rootSlot, root.Slots[rootSlot].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (children, int childSlot) = children.Append(SpawnerCallback);
        children = children.WriteSlot(childSlot, children.Slots[childSlot].WriteWord(12, 0).WriteWord(14, 0));
        NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Melt-Eye requires its original texture state.")).Register5099A0(resources.Camera);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Melt-Eye requires its executed texture allocation.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Melt-Eye requires its executed texture initializer.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        return frame with { State = frame.State.WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, Parameters(frame).Actor78).WriteDword(TargetGlobal, checked((uint)target)).WriteDword(InitializerGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim)).ClearSlotHeads(), Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [root, children], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
    }

    public static NativeBattleMeltEye158Frame Advance(NativeBattleMeltEye158Frame frame, NativeBattleMeltEye158Resources resources, NativeBattleMeltEye158Inputs inputs)
    {
        Require(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor || frame.Effect.Pools[1].DescriptorAddress != ChildDescriptor)
        {
            throw new InvalidDataException("Melt-Eye requires its actual root and shared child descriptors.");
        }
        NativeBattleMeltEye158Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.Callback != RootCallback)
            {
                throw new NotSupportedException("Melt-Eye reached an unsupported root callback.");
            }
            current = current with { State = current.State.WriteDword(CursorGlobal, checked(PacketBase + ((task.ReadByte(12) & 1) == 0 ? 0u : (uint)PacketBankBytes))) };
            NativeBattleMeltEye158Frame scan = current;
            NativeBattleEffectTaskStep children = current.Effect.Pools[1].Advance((owner, child) =>
            {
                scan = WithPool(scan, 1, owner);
                uint address = checked(owner.BankAddress + (uint)(child * owner.Stride));
                (scan, int result) = owner.Slots[child].Callback switch
                {
                    SpawnerCallback => AdvanceSpawner(scan, address, resources, inputs),
                    PathCallback => AdvancePath(scan, address, resources, inputs),
                    HeadMeshCallback => AdvanceHeadMesh(scan, address, resources, inputs),
                    StreamCallback => AdvanceStream(scan, address, resources, inputs),
                    ImpactSpriteCallback => AdvanceImpactSprite(scan, address, resources, inputs),
                    ImpactMeshCallback => AdvanceImpactMesh(scan, address, resources, inputs),
                    ForkEmitterCallback => AdvanceForkEmitter(scan, address, resources, inputs),
                    _ => throw new NotSupportedException("Melt-Eye reached an unsupported child callback.")
                };
                return new(scan.Effect.Pools[1], result);
            });
            current = WithPool(scan, 1, children.Pool);
            current = Write(current, RootBank, Read(current, RootBank).WriteWord(12, unchecked((ushort)(Read(current, RootBank).ReadWord(12) + 1))));
            return new(current.Effect.Pools[0], children.Survivors == 0 ? 2 : 0);
        });
        current = WithPool(current, 0, roots.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : RootDescriptor } };
    }

    public static (NativeBattleMeltEye158Frame Frame, int Result) AdvanceSpawner(NativeBattleMeltEye158Frame frame, uint address, NativeBattleMeltEye158Resources resources, NativeBattleMeltEye158Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        int age = unchecked((short)Read(frame, address).ReadWord(12));
        if (age is >= 1 and <= 15)
        {
            (frame, uint child) = Append(frame, PathCallback);
            ushort ordinal = Read(frame, address).ReadWord(14);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, ordinal));
            frame = Random(frame, out ushort roll);
            frame = Write(frame, address, Read(frame, address).WriteWord(14, unchecked((ushort)(ordinal + 1))));
            frame = Write(frame, child, Read(frame, child).WriteWord(24, unchecked((ushort)(roll % 6 + 1))));
        }
        if (age == 1)
        {
            NativeBattleMeltEye158PointStep anchor = inputs.ReadBone502170(frame, Caster(frame), 0xF0, 0);
            frame = anchor.Frame;
            frame = Write(frame, address, PutTaskVector(Read(frame, address), 16, anchor.Position));
            for (int index = 0; index < 2; index++)
            {
                (frame, uint child) = Append(frame, ForkEmitterCallback);
                NativeBattleModulePoolTask task = Read(frame, child).WriteWord(12, 0).WriteWord(14, unchecked((ushort)(index + 1))).WriteWord(24, unchecked((ushort)(7 + index * 2)));
                frame = Write(frame, child, CopyToTask(task, 16, TaskView(Read(frame, address), 16, 8), 8));
            }
        }
        if (age == 40)
        {
            NativeBattleActionHeaderSlot header = Header(frame, inputs);
            frame = inputs.Execute(frame, new NativeBattleMeltEye158BulkContact(header.FirstContactIndex ?? throw new NotSupportedException("Melt-Eye requires its captured contacts."), header.ReadByte(16)));
        }
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleMeltEye158Sound(resources.Sound));
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        return (frame, next > 42 ? 2 : 0);
    }

    private static void Require(NativeBattleMeltEye158Frame frame)
    {
        if (frame.Effect.Binding.Effect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Melt-Eye requires its selected original binding.");
        }
        frame.State.Validate();
    }

    private static NativeBattleActionHeaderSlot Header(NativeBattleMeltEye158Frame frame, NativeBattleMeltEye158Inputs inputs) => inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
}
