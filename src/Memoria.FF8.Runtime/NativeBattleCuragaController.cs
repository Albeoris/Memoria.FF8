using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuragaResources(NativeBattleEffectHeapAllocation Animation, uint Sound)
{
    public NativeBattleCuragaExecutableSource? Executable { get; init; }
    public ImmutableArray<uint> FlashProgram { get; init; } = [];
}
public sealed record NativeBattleCuragaState([property: JsonRequired] int SourceArenaOffset, [property: JsonRequired] int? PrimaryDrawCursor, [property: JsonRequired] int? SecondaryDrawCursor, [property: JsonRequired] int AuxiliaryCursor, [property: JsonRequired] NativeBattleActionMatrix? CopiedCamera)
{
    public int Counter26D82F8 { get; init; }
    public int Counter26D76C4 { get; init; }
    public int? Auxiliary25F8 { get; init; }
    public int? Auxiliary16F80 { get; init; }
    public ushort? Counter26D76C0 { get; init; }
    public ushort? Counter26D82E8 { get; init; }
    public ushort? Counter26D82D8 { get; init; }
    public ushort? Counter26D75C4 { get; init; }
    public ushort? PassCount26D82EA { get; init; }
    public ushort? PassCount26D82DA { get; init; }
}
public sealed record NativeBattleCuragaFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleCuragaState State);
public sealed record NativeBattleCuragaControllerInputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource)
{
    public Func<NativeBattleCuragaFrame, int, NativeBattleCuragaFrame>? ApplyContact { get; init; }
}
public sealed record NativeBattleCuragaLeafStep(NativeBattleCuragaFrame Frame, int Result);
public sealed record NativeBattleCuragaControllerStep(NativeBattleCuragaFrame Frame, ImmutableArray<int> Contacts, ImmutableArray<NativeBattleModuleSound> Sounds);

/// <summary>87DB10/87DC50/87DF70's physical pools, ordered callbacks and original header/contact selectors.</summary>
public static class NativeBattleCuragaController
{
    public static NativeBattleCuragaFrame Initialize87DB10(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleCuragaResources resources)
    {
        effect.Validate();
        packets.Validate();
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native87DB10 requires50AFC0 parameters.");
        if (effect.Binding.Entry != 0x87DB10)
        {
            throw new InvalidDataException("Native Curaga requires the actual87DB10 entry.");
        }
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(0x26D76B0, 0x26D75E8, 100, 2),
            NativeBattleEffectTaskPool.Initialize(0x26D8160, 0x26D8170, 88, 4),
            NativeBattleEffectTaskPool.Initialize(0x26D74F8, 0x26D7508, 60, 3),
            NativeBattleEffectTaskPool.Initialize(0x26D7EC0, 0x26D76D0, 676, 3),
            NativeBattleEffectTaskPool.Initialize(0x26D8150, 0x26D7ED0, 64, 10)
        ]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x87DC50, 100);
        hierarchy = root.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int firstContact = header.FirstContactIndex ?? throw new NotSupportedException("Native87DB10 requires its header's actual contact pointer.");
        byte target = packets.ReadContact(firstContact).Primary.ActorIndex;
        byte targets = header.ReadByte(16);
        byte groups = header.ReadByte(17);
        byte maximum = unchecked((byte)(targets - 1));
        if (maximum < groups)
        {
            maximum = groups;
        }
        NativeBattleModulePoolTask task = hierarchy.Read(root.Address).WriteDword(0x0C, 0x1D99A78).WriteByte(0x29, 0).WriteByte(0x2C, header.ReadByte(0)).WriteByte(0x2D, target).WriteWord(0x5A, targets).WriteWord(0x58, groups).WriteByte(0x2F, maximum);
        hierarchy = hierarchy.Write(root.Address, task);
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x26D75BC)?.ArenaOffset ?? throw new NotSupportedException("Native87DB10 requires the actual26D75BC initializer pointer, including texture-skip entries.");
        if (source > NativeBattleEffectArena.Length - 0xC000)
        {
            throw new InvalidDataException("Native87DB10 draw/auxiliary banks leave the571870 arena.");
        }
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native87DB10 requires the retained5099A0 texture owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native87DB10 requires its loaded571870 arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = 0x26D76B0 }, new(source, null, null, checked(source + 0xC000), null));
    }

    public static NativeBattleCuragaControllerStep Advance87DC50(NativeBattleCuragaFrame frame, NativeBattleCuragaControllerInputs inputs, NativeBattleCuragaResources resources, Func<NativeBattleCuragaFrame, uint, NativeBattleCuragaLeafStep> advanceLeaf)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return new(frame, [], []);
        }
        RequireController(frame.Effect);
        NativeBattleEffectParameters parameters = frame.Effect.Parameters!;
        inputs.Camera.Validate();
        NativeBattleCuragaFrame current = frame with { State = frame.State with { CopiedCamera = inputs.Camera } };
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        ImmutableArray<int>.Builder contacts = ImmutableArray.CreateBuilder<int>();
        ImmutableArray<NativeBattleModuleSound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleModuleSound>();
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
        NativeBattleEffectHierarchyStep main = hierarchy.Advance(0, (state, address) =>
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != 0x87DC50)
            {
                throw new NotSupportedException("Native Curaga main pool has an unsupported callback.");
            }
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            current = current with { State = current.State with { PrimaryDrawCursor = checked(current.State.SourceArenaOffset + (odd ? 0 : 0x6000)), SecondaryDrawCursor = checked(current.State.SourceArenaOffset + (odd ? 0x6000 : 0xC000)) } };
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray(), inputs.ReadPoseSource);
            state = state.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                case 1:
                case 7:
                case 8:
                    state = Next(state, address);
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0)
                    {
                        int first = current.State.AuxiliaryCursor;
                        int second = checked(first + 0x25F8);
                        int end = checked(second + 0x16F80);
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Native87DE10 requires its retained effect arena.");
                        arena = arena.Write(first, new byte[0x25F8]);
                        arena = arena.Write(second, new byte[0x16F80]);
                        current = current with { Memory = current.Memory with { Arena = arena }, State = current.State with { Auxiliary25F8 = first, Auxiliary16F80 = second, AuxiliaryCursor = end, Counter26D76C0 = 0, Counter26D82E8 = 0, Counter26D82D8 = 0, Counter26D75C4 = 0 } };
                        state = state.Append8DC540(4, 0x87DE50, 64, address, Select).Hierarchy;
                        state = Next(state, address);
                    }
                    break;
                case 3:
                    state = state.Write(address, task.WriteByte(0x63, 1));
                    state = state.Append8DC540(1, 0x87DF70, 88, address, Select).Hierarchy;
                    state = Next(state, address);
                    break;
                case 4:
                    if (task.ReadByte(0x63) == 0)
                    {
                        if (unchecked((sbyte)task.ReadByte(0x2A)) < unchecked((short)task.ReadWord(0x58)))
                        {
                            task = task.WriteByte(0x2A, unchecked((byte)(task.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) - 1)));
                        }
                        else
                        {
                            task = task.WriteWord(0x60, 10).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
                        }
                        state = state.Write(address, task);
                    }
                    break;
                case 5:
                    task = task.WriteWord(0x60, unchecked((ushort)(task.ReadWord(0x60) - 1)));
                    state = state.Write(address, task);
                    if (unchecked((short)task.ReadWord(0x60)) <= 0)
                    {
                        state = state.Append8DC540(4, 0x882FC0, 64, address, Select).Hierarchy;
                        state = Next(state, address);
                    }
                    break;
                case 6:
                    if (task.ReadWord(0x5E) == 0)
                    {
                        state = Next(state, address);
                    }
                    break;
                case 9:
                    state = state.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    state = Next(state, address);
                    break;
                case 10:
                    break;
                default:
                    throw new InvalidDataException("Native87DC50 signed phase exceeds its eleven original callbacks.");
            }
            state = state.Write(address, state.Read(address).WriteWord(0x5E, 0));
            current = current with { State = current.State with { PassCount26D82EA = 0, PassCount26D82DA = 0 } };
            int survivors = 0;
            for (int pool = 1; pool < 5; pool++)
            {
                int selected = pool;
                NativeBattleEffectHierarchyStep children = state.Advance(pool, (childState, childAddress) =>
                {
                    if (selected == 1)
                    {
                        return AdvanceChild(childState, childAddress);
                    }
                    current = current with { Effect = current.Effect with { Pools = childState.Pools } };
                    NativeBattleCuragaLeafStep leaf = advanceLeaf(current, childAddress);
                    current = leaf.Frame;
                    return new(new(current.Effect.Pools), leaf.Result);
                });
                state = children.Hierarchy;
                survivors = unchecked((ushort)(survivors + children.Survivors));
                state = state.Write(address, state.Read(address).WriteWord(0x5E, (ushort)survivors));
            }
            task = state.Read(address);
            task = task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1))).WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
            state = state.Write(address, task);
            bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
            return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
        });
        NativeBattleOriginalEffectState effect = current.Effect with { Pools = main.Hierarchy.Pools, ReturnedDescriptor = main.Survivors == 0 ? 0u : 0x26D76B0u };
        return new(current with { Effect = effect }, contacts.ToImmutable(), sounds.ToImmutable());

        NativeBattleEffectHierarchyCallback AdvanceChild(NativeBattleEffectHierarchy state, uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != 0x87DF70)
            {
                throw new NotSupportedException("Native Curaga child pool has an unsupported callback.");
            }
            task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray(), inputs.ReadPoseSource);
            NativeBattleGraphicsModel model = inputs.ReadModel(task.ReadByte(0x2D));
            NativeBattleSequenceState pose = model.SequenceState ?? throw new NotSupportedException("Native8DC870 requires the target's current decoded pose.");
            NativeBattlePoseSource source = inputs.ReadPoseSource(task.ReadByte(0x2D));
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, source.Sample(pose.Pose ?? throw new NotSupportedException("Native8DC870 requires its selected pose."), pose.PoseFrame114 ?? throw new NotSupportedException("Native8DC870 requires its decoded frame."), pose.PoseFlags109));
            state = state.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    NativeBattleEffectHierarchyAppend append = state.Append8DC540(3, 0x87E1A0, 676, address, Select);
                    state = append.Hierarchy;
                    if (append.Address == 0)
                    {
                        throw new NotSupportedException("Native87E150 returned null before its original field writes.");
                    }
                    state = state.Write(append.Address, state.Read(append.Address).WriteDword(0x30, 0x17D5F4C).WriteWord(0x298, 0).WriteWord(0x29C, 0).WriteWord(0x29E, 45));
                    state = Next(state, address);
                    break;
                case 1:
                    if (task.ReadWord(0x24) == 30)
                    {
                        uint root = task.ReadDword(0x10);
                        state = state.Write(root, state.Read(root).WriteByte(0x63, 0));
                        state = Next(state, address);
                    }
                    break;
                case 2:
                    if (unchecked((short)task.ReadWord(0x24)) >= 30)
                    {
                        int header = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                        int first = inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Native882F00 requires its selected group's contact pointer.");
                        int contact = checked(first + unchecked((sbyte)task.ReadByte(0x2B)));
                        if (inputs.ApplyContact is { } applyContact)
                        {
                            current = applyContact(current, contact);
                        }
                        contacts.Add(contact);
                        state = state.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                        state = Next(state, address);
                    }
                    break;
                case 3:
                    break;
                default:
                    throw new InvalidDataException("Native87DF70 signed phase exceeds its four original callbacks.");
            }
            task = state.Read(address);
            if (task.ReadWord(0x24) == 0)
            {
                sounds.Add(new(resources.Sound, 0, null) { Pan = 128 });
            }
            if (task.ReadWord(0x24) == 20)
            {
                NativeBattleEffectHierarchyAppend flash = state.Append8DC540(2, 0x87E020, 60, address, Select);
                state = flash.Hierarchy;
                if (flash.Address == 0)
                {
                    throw new NotSupportedException("Native87DF70 returned null before its flash resource write.");
                }
                state = state.Write(flash.Address, state.Read(flash.Address).WriteDword(0x30, 0x160E0A0));
            }
            task = state.Read(address).WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
            state = state.Write(address, task);
            bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
            return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
        }
    }

    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy hierarchy, uint address)
    {
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        return hierarchy.Write(address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
    }

    private static void RequireController(NativeBattleOriginalEffectState effect)
    {
        effect.Validate();
        if (effect.Binding.Entry != 0x87DB10 || effect.Parameters is null || effect.ReturnedDescriptor != 0x26D76B0 || effect.Pools.Length != 5)
        {
            throw new InvalidDataException("Native87DC50 requires its executed87DB10 physical controller.");
        }
        uint[] descriptors = [0x26D76B0, 0x26D8160, 0x26D74F8, 0x26D7EC0, 0x26D8150];
        uint[] banks = [0x26D75E8, 0x26D8170, 0x26D7508, 0x26D76D0, 0x26D7ED0];
        int[] strides = [100, 88, 60, 676, 64];
        int[] counts = [2, 4, 3, 3, 10];
        for (int index = 0; index < 5; index++)
        {
            NativeBattleEffectTaskPool pool = effect.Pools[index];
            if (pool.DescriptorAddress != descriptors[index] || pool.BankAddress != banks[index] || pool.Stride != strides[index] || pool.Slots.Length != counts[index])
            {
                throw new InvalidDataException("Native Curaga physical hierarchy contradicts its original descriptor.");
            }
        }
    }
}
