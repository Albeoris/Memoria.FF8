using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleUltraWaves184Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleUltraWaves184Controller
{
    public static NativeBattleUltraWaves184Frame Initialize(NativeBattleUltraWaves184Frame frame, NativeBattleUltraWaves184Resources resources, NativeBattleUltraWaves184Inputs inputs)
    {
        Require(frame);
        resources.Validate();
        int header = Parameters(frame).HeaderIndex7C;
        int first = inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Ultra Waves requires its captured first contact pointer.");
        if (inputs.Packets.Headers[header].ReadByte(16) == 0)
        {
            throw new InvalidDataException("Ultra Waves requires its captured first target.");
        }
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Ultra Waves requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Ultra Waves requires its retained texture queue.");
        textures = textures.Register5099A0(resources.Camera);
        (textures, _) = textures.EnqueueArenaTim505E30(frame.Memory.Arena ?? throw new NotSupportedException("Ultra Waves requires its current texture allocation."), tim);
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2);
        (roots, int root) = roots.Append(RootCallback);
        roots = roots.WriteSlot(root, roots.Slots[root].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
        (children, int main) = children.Append(MainCallback);
        children = children.WriteSlot(main, children.Slots[main].WriteWord(12, 0));
        NativeBattleUltraWaves184State state = frame.State.SeedColdSources(resources).WriteDword(0x22BD108, inputs.Packets.ReadContact(first).Primary.ActorIndex).WriteDword(0x22BE8E0, 0x1D99A78).WriteDword(0x22BE8E4, Parameters(frame).Actor78).WriteDword(InitializerGlobal, checked(0x20DFAB8u + (uint)tim));
        for (int index = 0; index < 100; index++)
        {
            state = state with { Particles = state.Particles.Write(index * 24, new byte[2]) };
        }
        return frame with { State = state, Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [roots, children], ReturnedDescriptor = RootDescriptor } };
    }

    public static NativeBattleUltraWaves184Frame Advance(NativeBattleUltraWaves184Frame frame, NativeBattleUltraWaves184Resources resources, NativeBattleUltraWaves184Inputs inputs)
    {
        Require(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor || frame.Effect.Pools[1].DescriptorAddress != ChildDescriptor)
        {
            throw new InvalidDataException("Ultra Waves requires its original two task pools.");
        }
        NativeBattleUltraWaves184Frame current = frame;
        NativeBattleEffectTaskStep advanced = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = Pool(current, 0, pool);
            uint address = RootBank + (uint)(slot * 16);
            NativeBattleModulePoolTask task = Read(current, address);
            int result;
            if (task.Callback == RootCallback)
            {
                current = current with { State = current.State.WriteCursor(PacketAddress + ((task.ReadWord(12) & 1) == 0 ? 0u : PacketBankLength)) };
                NativeBattleEffectTaskStep children = current.Effect.Pools[1].Advance((childPool, childSlot) =>
                {
                    current = Pool(current, 1, childPool);
                    uint childAddress = ChildBank + (uint)(childSlot * 36);
                    NativeBattleModulePoolTask child = Read(current, childAddress);
                    NativeBattleUltraWaves184TaskStep step = child.Callback switch
                    {
                        MainCallback => Main(current, childAddress, resources, inputs),
                        WarpCallback => Warp(current, childAddress, resources, inputs),
                        EmitterCallback => inputs.AdvanceEmitter(current, childAddress, resources),
                        _ => throw new NotSupportedException("Ultra Waves selected an unsupported child callback.")
                    };
                    current = step.Frame;
                    return new(current.Effect.Pools[1], step.Result);
                });
                current = Pool(current, 1, children.Pool);
                if (children.Survivors == 0)
                {
                    (current, uint waiting) = Append(current, 0, NativeBattleUltraWaves184Profile.WaitCallback);
                    current = Write(current, waiting, Read(current, waiting).WriteWord(12, 0).WriteWord(14, 0));
                }
                result = children.Survivors == 0 ? 2 : 0;
            }
            else if (task.Callback == NativeBattleUltraWaves184Profile.WaitCallback)
            {
                if (task.ReadWord(12) == 1)
                {
                    current = inputs.Execute(current, new NativeBattleUltraWaves184RestoreTexture(PacketAddress, address + 14));
                }
                result = Read(current, address).ReadWord(14) != 0 ? 2 : 0;
            }
            else
            {
                throw new NotSupportedException("Ultra Waves selected an unsupported root callback.");
            }
            task = Read(current, address);
            current = Write(current, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
            return new(current.Effect.Pools[0], result);
        });
        current = Pool(current, 0, advanced.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = advanced.Survivors == 0 ? 0u : RootDescriptor } };
    }

    private static NativeBattleUltraWaves184TaskStep Main(NativeBattleUltraWaves184Frame frame, uint address, NativeBattleUltraWaves184Resources resources, NativeBattleUltraWaves184Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        byte caster = checked((byte)frame.State.ReadDword(0x22BE8E4));
        if (age == 0)
        {
            frame = inputs.InitializeGrid(frame);
        }
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleUltraWaves184BindCaster(caster, SavedBindingAddress, CasterDatAddress));
            frame = inputs.Execute(frame, new NativeBattleUltraWaves184RingSequence(caster, 1));
        }
        if (age == 6)
        {
            for (int index = 0; index < 4; index++)
            {
                (frame, uint child) = Append(frame, 1, EmitterCallback);
                NativeBattleUltraWaves184AnchorStep anchor = inputs.ReadAnchor502170(frame, caster, 0xF1, 2048);
                frame = anchor.Frame;
                frame = Write(frame, address, WriteAnchor(Read(frame, address), 16, anchor.Anchor));
                NativeBattleActionMatrix world = inputs.ReadWorld(caster);
                NativeBattleModuleAnchor first = resources.EmitterVectors[index * 2];
                NativeBattleModuleAnchor second = resources.EmitterVectors[index * 2 + 1];
                (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(world, new(first.X, first.Y, first.Z));
                NativeBattleVector position = new(unchecked((short)(anchor.Anchor.X + unchecked((short)x))), unchecked((short)(anchor.Anchor.Y + unchecked((short)y))), unchecked((short)(anchor.Anchor.Z + unchecked((short)z))));
                (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(world, new(second.X, second.Y, second.Z));
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(unchecked((short)(anchor.Anchor.X - position.X + unchecked((short)x))), unchecked((short)(anchor.Anchor.Y - position.Y + unchecked((short)y))), unchecked((short)(anchor.Anchor.Z - position.Z + unchecked((short)z))));
                NativeCrtRandomState random = frame.Random.Draw(out ushort spin);
                frame = frame with { Random = random };
                NativeBattleModulePoolTask task = Read(frame, child).WriteWord(12, 0).WriteWord(14, (ushort)(1 << (index * 2))).WriteWord(16, unchecked((ushort)position.X)).WriteWord(18, unchecked((ushort)position.Y)).WriteWord(20, unchecked((ushort)position.Z)).WriteWord(24, unchecked((ushort)direction.X)).WriteWord(26, unchecked((ushort)direction.Y)).WriteWord(28, unchecked((ushort)direction.Z)).WriteWord(32, (ushort)(spin & 4095)).WriteWord(34, 0);
                frame = Write(frame, child, task);
            }
        }
        if (age == 14)
        {
            (frame, uint child) = Append(frame, 1, WarpCallback);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
        }
        if (age == 41)
        {
            frame = inputs.Execute(frame, new NativeBattleUltraWaves184ContactAll());
        }
        if (age == 6)
        {
            frame = inputs.Execute(frame, new NativeBattleUltraWaves184Sound(resources.Sound, 0, 128));
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        return new(frame, next > 45 ? 2 : 0);
    }

    private static NativeBattleUltraWaves184TaskStep Warp(NativeBattleUltraWaves184Frame frame, uint address, NativeBattleUltraWaves184Resources resources, NativeBattleUltraWaves184Inputs inputs)
    {
        short age = Signed(Read(frame, address), 12);
        if (age < 35)
        {
            frame = inputs.Execute(frame, new NativeBattleUltraWaves184CaptureStage());
            frame = inputs.Execute(frame, new NativeBattleUltraWaves184DrawGrid(age, resources.WarpColors[age]));
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, 0);
        }
        if (age is 0 or 35)
        {
            frame = inputs.Execute(frame, new NativeBattleUltraWaves184StageVisibility(age == 35));
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        return new(frame, next > 35 ? 2 : 0);
    }

    public static NativeBattleModulePoolTask Read(NativeBattleUltraWaves184Frame frame, uint address)
    {
        for (int pool = 0; pool < frame.Effect.Pools.Length; pool++)
        {
            NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
            uint offset = unchecked(address - owner.BankAddress);
            if (offset < owner.Stride * owner.Slots.Length && offset % owner.Stride == 0)
            {
                return owner.Slots[(int)(offset / owner.Stride)];
            }
        }
        throw new InvalidDataException("Ultra Waves task pointer leaves its original pools.");
    }
    public static NativeBattleUltraWaves184Frame Write(NativeBattleUltraWaves184Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int pool = 0; pool < frame.Effect.Pools.Length; pool++)
        {
            NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
            uint offset = unchecked(address - owner.BankAddress);
            if (offset < owner.Stride * owner.Slots.Length && offset % owner.Stride == 0)
            {
                int slot = (int)(offset / owner.Stride);
                return Pool(frame, pool, owner.WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, owner.Slots[slot].Next, task.Callback)));
            }
        }
        throw new InvalidDataException("Ultra Waves task pointer leaves its original pools.");
    }
    public static NativeBattleUltraWaves184Frame WriteCompletionByte(NativeBattleUltraWaves184Frame frame, uint address, byte value)
    {
        uint relative = unchecked(address - RootBank);
        if (relative >= 32 || relative % 16 != 14 || Read(frame, address - 14).Callback != NativeBattleUltraWaves184Profile.WaitCallback)
        {
            throw new InvalidDataException("Ultra Waves completion requires its actual waiting root task.");
        }
        NativeBattleModulePoolTask task = Read(frame, address - 14);
        return Write(frame, address - 14, task.WriteByte(14, value));
    }
    private static NativeBattleUltraWaves184Frame Pool(NativeBattleUltraWaves184Frame frame, int pool, NativeBattleEffectTaskPool value) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(pool, value) } };
    private static (NativeBattleUltraWaves184Frame Frame, uint Address) Append(NativeBattleUltraWaves184Frame frame, int pool, uint callback)
    {
        NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
        (owner, int slot) = owner.Append(callback);
        return (Pool(frame, pool, owner), owner.BankAddress + (uint)(slot * owner.Stride));
    }
    private static NativeBattleEffectParameters Parameters(NativeBattleUltraWaves184Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Ultra Waves requires its captured action parameters.");
    private static void Require(NativeBattleUltraWaves184Frame frame)
    {
        if (frame.Effect.RequestedEffect != 184 || frame.Effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Ultra Waves requires its original selected entry.");
        }
    }
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask WriteAnchor(NativeBattleModulePoolTask task, int offset, NativeBattleModuleAnchor anchor) => task.WriteWord(offset, unchecked((ushort)anchor.X)).WriteWord(offset + 2, unchecked((ushort)anchor.Y)).WriteWord(offset + 4, unchecked((ushort)anchor.Z));
}
