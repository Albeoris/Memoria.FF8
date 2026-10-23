namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect183Controller
{
    public static NativeBattleEffect183Frame Initialize(NativeBattleEffect183Frame frame, NativeBattleEffect183Resources resources, NativeBattleEffect183Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null || frame.State.ChildBankStorage is null)
        {
            throw new InvalidDataException("Effect183 initialization requires its exclusive inactive task storage.");
        }
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(0x22F33E0, 0x22F33D0, 16, 1);
        (roots, int root) = roots.Append(0x5C9F80);
        roots = roots with { Slots = roots.Slots.SetItem(root, roots.Slots[root].WriteWord(12, 0)) };
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x22F33F0, 0x22F3400, 36, 100);
        (children, int director) = children.Append(0x5C9AE0);
        children = children with { Slots = children.Slots.SetItem(director, children.Slots[director].WriteWord(12, 0)) };
        int first = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Effect183 requires its first captured contact.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x22F4218)?.ArenaOffset ?? throw new NotSupportedException("Effect183 requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Effect183 requires its actual texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(frame.Memory.Arena ?? throw new NotSupportedException("Effect183 requires its loaded texture bytes."), tim);
        NativeBattleEffect183State state = frame.State.WriteDword(0x22F33C8, inputs.Packets.ReadContact(first).Primary.ActorIndex).WriteDword(0x22F4210, 0x1D99A78).WriteDword(0x22F4214, Parameters(frame).Actor78).WriteDword(0x22F4218, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim));
        return frame with { State = state with { RootBankStorage = null, ChildBankStorage = null }, Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [roots, children], ReturnedDescriptor = 0x22F33E0, DrawArenaOffset = null } };
    }

    public static NativeBattleEffect183Frame Advance(NativeBattleEffect183Frame frame, NativeBattleEffect183Resources resources, NativeBattleEffect183Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        RequirePools(frame);
        NativeBattleEffect183Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            NativeBattleModulePoolTask root = pool.Slots[slot];
            if (root.Callback != 0x5C9F80)
            {
                throw new NotSupportedException("Effect183 reached an unsupported root callback.");
            }
            current = current with { State = current.State.WriteDword(0x230421C, (root.ReadWord(12) & 1) == 0 ? 0x22F421Cu : 0x22FC21Cu) };
            NativeBattleEffect183Frame scan = current;
            NativeBattleEffectTaskStep children = current.Effect.Pools[1].Advance((owner, child) =>
            {
                scan = WithPool(scan, 1, owner);
                uint address = checked(owner.BankAddress + (uint)(child * owner.Stride));
                (scan, int result) = owner.Slots[child].Callback switch
                {
                    0x5C9AE0 => AdvanceDirector(scan, address, resources, inputs),
                    0x5C9C40 => NativeBattleEffect183Children.Advance5C9C40(scan, address, resources, inputs),
                    0x5C9DD0 => NativeBattleEffect183Children.Advance5C9DD0(scan, address, resources, inputs),
                    _ => throw new NotSupportedException("Effect183 reached an unsupported child callback.")
                };
                return new(scan.Effect.Pools[1], result);
            });
            current = WithPool(scan, 1, children.Pool);
            uint rootAddress = checked(pool.BankAddress + (uint)(slot * pool.Stride));
            current = Write(current, rootAddress, Read(current, rootAddress).WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1))));
            return new(current.Effect.Pools[0], children.Survivors == 0 ? 2 : 0);
        });
        current = WithPool(current, 0, roots.Pool);
        current = current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : 0x22F33E0u } };
        if (roots.Survivors != 0)
        {
            return current;
        }
        if (current.Effect.Pools.Any(pool => pool.Head is not null || pool.Slots.Any(task => (task.Flags & 1) != 0)))
        {
            throw new InvalidDataException("Effect183 completion requires both physical queues to drain.");
        }
        NativeBattleEffectArena[] storage = current.Effect.Pools.Select(NativeBattleEffectTaskMemory.CaptureBank).ToArray();
        return current with { State = current.State with { RootBankStorage = storage[0], ChildBankStorage = storage[1] }, Effect = current.Effect with { Pools = [] } };
    }

    public static (NativeBattleEffect183Frame Frame, int Result) AdvanceDirector(NativeBattleEffect183Frame frame, uint address, NativeBattleEffect183Resources resources, NativeBattleEffect183Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        short age = unchecked((short)Read(frame, address).ReadWord(12));
        int caster = checked((int)frame.State.ReadDword(0x22F4214));
        if (age == 0)
        {
            NativeBattleEffect183PointStep point = inputs.ReadBone502170(frame, caster, 0xF1, 0);
            frame = Write(point.Frame, address, CopyPoint(Read(point.Frame, address), point.Point8));
        }
        if (age is 1 or 6)
        {
            NativeBattleEffectTaskPool children = frame.Effect.Pools[1];
            (children, int slot) = children.Append(age == 1 ? 0x5C9C40u : 0x5C9DD0u);
            frame = WithPool(frame, 1, children);
            NativeBattleModulePoolTask source = Read(frame, address);
            NativeBattleModulePoolTask child = CopyTaskPoint(children.Slots[slot].WriteWord(12, 0), source).WriteWord(24, unchecked((ushort)inputs.ReadHeading(caster))).WriteWord(28, age == 1 ? (ushort)0x2000 : (ushort)0x700);
            if (age == 1)
            {
                child = child.WriteWord(30, 0x200);
            }
            frame = Write(frame, checked(children.BankAddress + (uint)(slot * children.Stride)), child);
        }
        if (age == 30)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect183ContactFirst(Parameters(frame).HeaderIndex7C));
        }
        if (age == 1)
        {
            NativeBattleEffect183PointStep anchor = inputs.ReadAnchor571400(frame, caster);
            frame = inputs.Execute(anchor.Frame, new NativeBattleEffect183Sound(resources.Sound, anchor.Point8));
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), unchecked((short)task.ReadWord(12)) > 35 ? 2 : 0);
    }

    private static void RequireBinding(NativeBattleEffect183Frame frame)
    {
        if (frame.Effect.RequestedEffect != 183 || frame.Effect.Binding.Entry != 0x5C9A50 || frame.Effect.Binding.Initializer != 0x5C9A30)
        {
            throw new InvalidDataException("Effect183 requires its original selected entry.");
        }
    }

    private static void RequirePools(NativeBattleEffect183Frame frame)
    {
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != 0x22F33E0 || frame.Effect.Pools[0].BankAddress != 0x22F33D0 || frame.Effect.Pools[0].Stride != 16 || frame.Effect.Pools[0].Slots.Length != 1 || frame.Effect.Pools[1].DescriptorAddress != 0x22F33F0 || frame.Effect.Pools[1].BankAddress != 0x22F3400 || frame.Effect.Pools[1].Stride != 36 || frame.Effect.Pools[1].Slots.Length != 100 || frame.State.RootBankStorage is not null || frame.State.ChildBankStorage is not null)
        {
            throw new InvalidDataException("Effect183 requires its exclusive physical task bank owners.");
        }
    }
}
