using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEnergyBomber217Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEnergyBomber217Controller
{
    public static NativeBattleEnergyBomber217Frame Initialize(NativeBattleEnergyBomber217Frame frame, NativeBattleEnergyBomber217Resources resources, NativeBattleEnergyBomber217Inputs inputs)
    {
        Require(frame);
        resources.Validate();
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Energy Bomber requires its actual native arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Energy Bomber requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Energy Bomber requires its texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        int target = inputs.Packets.ReadContact(FirstContact(frame, inputs)).Primary.ActorIndex;
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, int slot) = root.Append(RootCallback);
        root = root.WriteSlot(slot, root.Slots[slot].WriteBytes(12, new byte[8]));
        return frame with { State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78)).WriteDword(TargetGlobal, ModelAddress(target)), Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
    }

    public static NativeBattleEnergyBomber217Frame Advance(NativeBattleEnergyBomber217Frame frame, NativeBattleEnergyBomber217Resources resources, NativeBattleEnergyBomber217Inputs inputs)
    {
        Require(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 3) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Energy Bomber requires its actual root, main and sprite pools.");
        }
        NativeBattleEnergyBomber217Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.Callback != RootCallback)
            {
                throw new NotSupportedException("Energy Bomber reached an unsupported root callback.");
            }
            bool first = task.ReadDword(16) != 0;
            current = current with { State = current.State.WriteDword(CursorGlobal, checked(Working(current) + (uint)(first ? FirstPacketOffset : SecondPacketOffset))) };
            current = Write(current, RootBank, task.WriteDword(16, first ? 0u : 1u));
            if (task.ReadWord(12) == 1 && task.ReadByte(15) == 0)
            {
                current = Setup(current, resources, inputs);
            }
            bool started = Read(current, RootBank).ReadByte(15) != 0;
            int mainSurvivors = 0;
            if (started)
            {
                for (int index = 1; index < 3; index++)
                {
                    NativeBattleEnergyBomber217Frame scan = current;
                    NativeBattleEffectTaskStep children = current.Effect.Pools[index].Advance((owner, child) =>
                    {
                        scan = WithPool(scan, index, owner);
                        uint address = checked(owner.BankAddress + (uint)(child * owner.Stride));
                        uint expected = index == 1 ? MainCallback : SpriteCallback;
                        if (owner.Slots[child].Callback != expected)
                        {
                            throw new NotSupportedException("Energy Bomber reached an unsupported child callback.");
                        }
                        (scan, int result) = index == 1 ? AdvanceMain(scan, address, resources, inputs) : AdvanceSprite(scan, address, inputs);
                        return new(scan.Effect.Pools[index], result);
                    });
                    current = WithPool(scan, index, children.Pool);
                    if (index == 1)
                    {
                        mainSurvivors = children.Survivors;
                    }
                }
            }
            if (started && mainSurvivors == 0)
            {
                return new(current.Effect.Pools[0], 2);
            }
            NativeBattleModulePoolTask root = Read(current, RootBank);
            current = Write(current, RootBank, root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1))));
            return new(current.Effect.Pools[0], 0);
        });
        current = WithPool(current, 0, roots.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : RootDescriptor } };
    }

    private static NativeBattleEnergyBomber217Frame Setup(NativeBattleEnergyBomber217Frame frame, NativeBattleEnergyBomber217Resources resources, NativeBattleEnergyBomber217Inputs inputs)
    {
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, checked(Working(frame) + MainOffset), MainStride, 1);
        NativeBattleEffectTaskPool sprites = NativeBattleEffectTaskPool.Initialize(SpriteDescriptor, Working(frame), SpriteStride, SpriteCount);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(15, 1));
        frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], main, sprites] }, Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [main, sprites]) };
        (main, int slot) = main.Append(MainCallback);
        NativeBattleModulePoolTask task = main.Slots[slot].WriteBytes(12, new byte[MainStride - 12]);
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[0x398].ToImmutableArray()).Initialize7016B0(resources.MainAct, 0x398);
        task = task.WriteBytes(0x44, workspace.Data.AsSpan());
        frame = WithPool(frame, 1, main.WriteSlot(slot, task));
        NativeBattleEnergyBomber217AnchorStep anchor = inputs.ReadAnchor571400(frame, Actor(frame.State.ReadDword(TargetGlobal)));
        frame = anchor.Frame;
        task = Read(frame, main.BankAddress).WriteWord(0x28, unchecked((ushort)anchor.Anchor.X)).WriteWord(0x2A, unchecked((ushort)anchor.Anchor.Y)).WriteWord(0x2C, unchecked((ushort)anchor.Anchor.Z)).WriteWord(0x2E, unchecked((ushort)anchor.Anchor.W));
        return Write(frame, main.BankAddress, task);
    }

    public static NativeBattleModulePoolTask Read(NativeBattleEnergyBomber217Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            if (address >= pool.BankAddress && address < pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length) && (address - pool.BankAddress) % pool.Stride == 0)
            {
                return pool.Slots[checked((int)((address - pool.BankAddress) / pool.Stride))];
            }
        }
        throw new NotSupportedException("EnergyBomber217 task address has no current physical owner.");
    }

    public static NativeBattleEnergyBomber217Frame Write(NativeBattleEnergyBomber217Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (address >= pool.BankAddress && address < pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length) && (address - pool.BankAddress) % pool.Stride == 0)
            {
                return WithPool(frame, index, pool.WriteSlot(checked((int)((address - pool.BankAddress) / pool.Stride)), task));
            }
        }
        throw new NotSupportedException("EnergyBomber217 task write has no current physical owner.");
    }

    private static NativeBattleModulePoolTask CopyToTask(NativeBattleModulePoolTask task, int offset, NativeBattleEffectArena source, int length)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, length);
        task = task.WriteBytes(offset, bytes);
        for (int index = 0; index < length; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(offset + index, 1);
            }
        }
        return task;
    }

    private static NativeBattleEffectArena TaskView(NativeBattleModulePoolTask task, int offset, int length)
    {
        NativeBattleEffectArena view = NativeBattleEffectArena.Clear().Write(0, task.ReadRawPayloadSpan().Slice(offset - 12, length));
        if (task.UnknownPayloadRanges is { } ranges)
        {
            foreach (NativeBattleEffectUnknownRegion range in ranges)
            {
                int first = Math.Max(offset, range.Offset);
                int end = Math.Min(offset + length, range.Offset + range.Length);
                if (first < end)
                {
                    view = view.MarkUnknown(first - offset, end - first);
                }
            }
        }
        return view;
    }

    private static void Require(NativeBattleEnergyBomber217Frame frame)
    {
        if (frame.Effect.Binding.Effect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("EnergyBomber217 requires its selected original binding.");
        }
        frame.State.Validate();
    }

    private static NativeBattleEnergyBomber217Frame WithPool(NativeBattleEnergyBomber217Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleEnergyBomber217Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("EnergyBomber217 requires its captured native parameters.");
    private static int FirstContact(NativeBattleEnergyBomber217Frame frame, NativeBattleEnergyBomber217Inputs inputs) => inputs.Packets.Headers[Parameters(frame).HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("EnergyBomber217 requires its current physical contacts.");
    private static uint Working(NativeBattleEnergyBomber217Frame frame) => frame.State.ReadDword(WorkingGlobal);
    private static uint ModelAddress(int actor) => actor is >= 0 and < 7 ? 0x1D972C0u + (uint)(actor * 156) : throw new NotSupportedException("EnergyBomber217 selected an absent actor.");
    private static int Actor(uint pointer) => pointer >= 0x1D972C0 && pointer < 0x1D972C0 + 7 * 156 && (pointer - 0x1D972C0) % 156 == 0 ? (int)((pointer - 0x1D972C0) / 156) : throw new NotSupportedException("EnergyBomber217 requires its current physical model pointer.");
}
