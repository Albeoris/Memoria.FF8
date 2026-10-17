using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLifeResources(NativeBattleEffectHeapAllocation? Animation, ImmutableArray<NativeBattleModuleResource> Sprites, uint Sound);
/// <summary>The instruction-identical Life/Phoenix Down bodies, with their distinct original entry and registration policies.</summary>
public sealed record NativeBattleLifeProfile(uint Entry, uint CodeDelta, uint GlobalDelta, uint StaticDelta, bool RegistersAnimation)
{
    public uint Code(uint address) => checked(address + CodeDelta);
    public uint Global(uint address) => checked(address + GlobalDelta);
    public static NativeBattleLifeProfile ForEntry(uint entry) => entry switch
    {
        0x8649C0 => new(entry, 0, 0, 0, true),
        0x8D4450 => new(entry, 0x6FA80, 0xB5120, 0x57F20, false),
        _ => throw new NotSupportedException($"Native Life-family entry{entry:X} has no proved profile.")
    };
    public static NativeBattleLifeProfile ForDescriptor(uint descriptor) => ForEntry(descriptor switch
    {
        0x26AC868 => 0x8649C0,
        0x2761988 => 0x8D4450,
        _ => throw new InvalidDataException("Native Life-family descriptor does not match an original pool.")
    });
}
public sealed record NativeBattleLifeState([property: JsonRequired] int? DrawCursor);
public sealed record NativeBattleLifeFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleLifeState State);
public sealed record NativeBattleLifeInputs(NativeBattleActionPacketsState Packets, NativeBattleCameraProjection Projection, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, NativeCrtRandomState Random, Func<NativeBattleLifeFrame, NativeBattleModulePacketBatch, NativeBattleLifeFrame> Execute)
{
    public Func<NativeBattleLifeFrame, int, NativeBattleLifeFrame>? ApplyContact { get; init; }
    public short CameraRoll { get; init; }
}
public sealed record NativeBattleLifeStep(NativeBattleLifeFrame Frame, NativeCrtRandomState Random, ImmutableArray<int> Contacts, ImmutableArray<NativeBattleModuleSound> Sounds);

/// <summary>8649C0/864AB0/864BC0's five original task banks and native same-pass scheduling.</summary>
public static partial class NativeBattleLifeController
{
    public static NativeBattleLifeFrame Initialize8649C0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleLifeResources resources)
    {
        effect.Validate();
        packets.Validate();
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native8649C0 requires its50AFC0 parameters.");
        NativeBattleLifeProfile profile = NativeBattleLifeProfile.ForEntry(effect.Binding.Entry);
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(profile.Global(0x26AC868), profile.Global(0x26AC7A0), 100, 2),
            NativeBattleEffectTaskPool.Initialize(profile.Global(0x26B4BD0), profile.Global(0x26B6FD0), 100, 4),
            NativeBattleEffectTaskPool.Initialize(profile.Global(0x26B7178), profile.Global(0x26B7188), 48, 300),
            NativeBattleEffectTaskPool.Initialize(profile.Global(0x26B4BB8), profile.Global(0x26AC878), 112, 300),
            NativeBattleEffectTaskPool.Initialize(profile.Global(0x26B7160), profile.Global(0x26B4BE0), 92, 100)
        ]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, profile.Code(0x864AB0), 100);
        hierarchy = root.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native8649C0 requires its physical first-contact pointer.");
        byte target = packets.ReadContact(first).Primary.ActorIndex;
        NativeBattleModulePoolTask task = hierarchy.Read(root.Address).WriteDword(0x0C, 0x1D99A78).WriteByte(0x29, 0).WriteByte(0x2D, target).WriteWord(0x5A, header.ReadByte(16)).WriteWord(0x58, header.ReadByte(17));
        hierarchy = hierarchy.Write(root.Address, task);
        if (!profile.RegistersAnimation || (parameters.Flags79 & 1) == 0)
        {
            int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == profile.Global(0x26AC780))?.ArenaOffset ?? throw new NotSupportedException("Native Life-family entry requires its actual initializer pointer.");
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native8649C0 requires the5099A0/505E30 texture owner.");
            if (profile.RegistersAnimation)
            {
                textures = textures.Register5099A0(resources.Animation ?? throw new NotSupportedException("Native Life requires its original animation resource."));
            }
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native8649C0 requires its571870 arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = profile.Global(0x26AC868) }, new(null));
    }

    public static NativeBattleLifeStep Advance864AB0(NativeBattleLifeFrame frame, NativeBattleLifeResources resources, NativeBattleLifeInputs inputs)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return new(frame, inputs.Random, [], []);
        }
        NativeBattleLifeProfile profile = NativeBattleLifeProfile.ForEntry(frame.Effect.Binding.Entry);
        if (frame.Effect.ReturnedDescriptor != profile.Global(0x26AC868) || frame.Effect.Pools.Length != 5 || !frame.Effect.Pools.Select(pool => pool.DescriptorAddress).SequenceEqual(new uint[] { 0x26AC868, 0x26B4BD0, 0x26B7178, 0x26B4BB8, 0x26B7160 }.Select(profile.Global)))
        {
            throw new InvalidDataException("Native864AB0 requires its executed five-pool Life entry.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Life lost its50AFC0 parameters.");
        NativeBattleLifeFrame current = frame;
        NativeCrtRandomState random = inputs.Random;
        ImmutableArray<int>.Builder contacts = ImmutableArray.CreateBuilder<int>();
        ImmutableArray<NativeBattleModuleSound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleModuleSound>();
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
        NativeBattleEffectHierarchyStep root = hierarchy.Advance(0, (state, address) =>
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != profile.Code(0x864AB0))
            {
                throw new InvalidDataException("Native Life root callback is not864AB0.");
            }
            //571B50 returns20DFAB8, not the TIM allocation. Even callbacks use the second8KB buffer.
            current = current with { State = current.State with { DrawCursor = (task.ReadByte(0x5C) & 1) != 0 ? 0 : 0x2000 } };
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, Models(inputs), inputs.ReadPoseSource);
            switch (Phase(task))
            {
                case 0:
                    task = Increment(task);
                    break;
                case 1:
                    task = task.WriteByte(0x63, 1);
                    state = state.Write(address, task);
                    state = state.Append8DC540(1, profile.Code(0x864BC0), 100, address, Select).Hierarchy;
                    task = Increment(state.Read(address));
                    break;
                case 2:
                    if (task.ReadByte(0x63) == 0)
                    {
                        sbyte group = unchecked((sbyte)task.ReadByte(0x2A));
                        task = group < Word(task, 0x58) ? task.WriteByte(0x2A, unchecked((byte)(group + 1))).WriteByte(0x29, 1) : Increment(task);
                    }
                    break;
                case 3:
                    if (task.ReadWord(0x5E) == 0)
                    {
                        task = Increment(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    }
                    break;
                case 4:
                    break;
                default:
                    throw new InvalidDataException("Native864AB0 signed phase exceeds its five original callbacks.");
            }
            state = state.Write(address, task);
            int survivors = 0;
            for (int pool = 1; pool < 5; pool++)
            {
                int selected = pool;
                NativeBattleEffectHierarchyStep children = state.Advance(pool, (childState, childAddress) =>
                {
                    if (selected == 1)
                    {
                        return AdvanceTarget(childState, childAddress);
                    }
                    current = current with { Effect = current.Effect with { Pools = childState.Pools } };
                    NativeBattleLifeParticleStep particle = AdvanceParticle(current, childAddress, resources, inputs, random);
                    current = particle.Frame;
                    random = particle.Random;
                    return new(new(current.Effect.Pools), particle.Result);
                });
                state = children.Hierarchy;
                survivors = unchecked((ushort)(survivors + children.Survivors));
            }
            task = state.Read(address).WriteWord(0x5E, (ushort)survivors);
            task = Add(Add(task, 0x5C, 1), 0x24, 1);
            state = state.Write(address, task);
            bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
            return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
        });
        current = current with { Effect = current.Effect with { Pools = root.Hierarchy.Pools, ReturnedDescriptor = root.Survivors == 0 ? 0u : profile.Global(0x26AC868) } };
        return new(current, random, contacts.ToImmutable(), sounds.ToImmutable());

        NativeBattleEffectHierarchyCallback AdvanceTarget(NativeBattleEffectHierarchy state, uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != profile.Code(0x864BC0))
            {
                throw new InvalidDataException("Native Life target callback is not864BC0.");
            }
            task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, Models(inputs), inputs.ReadPoseSource);
            int target = task.ReadByte(0x2D);
            NativeBattleGraphicsModel model = inputs.ReadModel(target);
            NativeBattlePoseSource source = inputs.ReadPoseSource(target);
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native8DC870 requires the current target pose.");
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, source.Sample(sequence.Pose ?? throw new NotSupportedException("Native Life target has no selected pose."), sequence.PoseFrame114 ?? throw new NotSupportedException("Native Life target has no decoded pose frame."), sequence.PoseFlags109));
            switch (Phase(task))
            {
                case 0:
                    task = Increment(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 2)).WriteWord(0x58, 0).WriteWord(0x5A, 0).WriteWord(0x62, 0).WriteWord(0x5C, 1).WriteWord(0x5E, 256).WriteWord(0x60, 4096));
                    break;
                case 1:
                    task = Add(Add(task, 0x5E, 384), 0x58, 102);
                    if (Word(task, 0x24) > 10)
                    {
                        task = Increment(task);
                    }
                    break;
                case 2:
                    task = Add(Add(Add(task, 0x58, -76), 0x5A, 64), 0x62, -38);
                    if (Word(task, 0x24) > 20)
                    {
                        task = Increment(task.WriteWord(0x5C, 1).WriteWord(0x60, 16));
                    }
                    break;
                case 3:
                    task = Add(task, 0x5E, -255);
                    if (Word(task, 0x24) > 30)
                    {
                        uint rootAddress = task.ReadDword(0x10);
                        state = state.Write(rootAddress, state.Read(rootAddress).WriteByte(0x63, 0));
                        int header = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                        int contact = inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Native865520 requires its header's first-contact pointer.");
                        _ = inputs.Packets.ReadContact(contact);
                        current = current with { Effect = current.Effect with { Pools = state.Write(address, task).Pools } };
                        current = inputs.ApplyContact?.Invoke(current, contact) ?? current;
                        state = new(current.Effect.Pools);
                        contacts.Add(contact);
                        task = Increment(task.WriteWord(0x26, (ushort)((task.ReadWord(0x26) & ~2) | 1)));
                    }
                    break;
                case 4:
                    break;
                default:
                    throw new InvalidDataException("Native864BC0 signed phase exceeds its five original callbacks.");
            }
            state = state.Write(address, task);
            if ((task.ReadByte(0x26) & 2) != 0)
            {
                state = Produce(state, address, parameters, inputs.Packets, ref random);
            }
            task = state.Read(address);
            if (task.ReadWord(0x24) == 0)
            {
                sounds.Add(new(resources.Sound, 0, null) { Pan = 128 });
            }
            state = state.Write(address, Add(task, 0x24, 1));
            bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
            return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
        }
    }

    private static NativeBattleGraphicsModel[] Models(NativeBattleLifeInputs inputs) => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static int Phase(NativeBattleModulePoolTask task) => unchecked((sbyte)task.ReadByte(0x29));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(task.ReadWord(offset) + value)));
    private static NativeBattleModulePoolTask Increment(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
}
