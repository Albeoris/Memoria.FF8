using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoubleResources(NativeBattleEffectHeapAllocation Animation, ImmutableArray<NativeBattleModuleResource> Sprites, ImmutableArray<NativeBattleStaticMeshResource> Meshes, ImmutableArray<byte> MorphFirst, ImmutableArray<byte> MorphSecond, uint Sound);
public sealed record NativeBattleDoubleState([property: JsonRequired] int? DrawCursor);
public sealed record NativeBattleDoubleFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleDoubleState State);
public abstract record NativeBattleDoubleOperation;
public sealed record NativeBattleDoubleFlash : NativeBattleDoubleOperation;
public sealed record NativeBattleDoublePackets(NativeBattleGfGeometryBatch Batch, ushort Flags20 = 0) : NativeBattleDoubleOperation;
public sealed record NativeBattleDoubleSprite(NativeBattleModuleDraw Draw) : NativeBattleDoubleOperation;
public sealed record NativeBattleDoubleInputs(NativeBattleActionPacketsState Packets, NativeBattleCameraProjection Projection, short CameraRoll, NativeBattleSummonColors Colors, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleDoubleFrame, NativeBattleDoubleOperation, NativeBattleDoubleFrame> Execute)
{
    public Func<NativeBattleDoubleFrame, int, NativeBattleDoubleFrame>? ApplyContact { get; init; }
}
public sealed record NativeBattleDoubleStep(NativeBattleDoubleFrame Frame, NativeCrtRandomState Random, ImmutableArray<int> Contacts, ImmutableArray<NativeBattleModuleSound> Sounds);
public sealed record NativeBattleDoubleParticleStep(NativeBattleDoubleFrame Frame, NativeCrtRandomState Random, int Result);

public static partial class NativeBattleDoubleController
{
    public static NativeBattleDoubleFrame Initialize8D5080(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleDoubleResources resources)
    {
        effect.Validate();
        packets.Validate();
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native8D5080 requires its50AFC0 parameters.");
        if (effect.Binding.Entry != 0x8D5080)
        {
            throw new InvalidDataException("Native Double requires its original8D5080 entry.");
        }
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(0x2775058, 0x2774F90, 100, 2),
            NativeBattleEffectTaskPool.Initialize(0x277A1C0, 0x277A1D0, 92, 4),
            NativeBattleEffectTaskPool.Initialize(0x277A348, 0x277A358, 64, 40),
            NativeBattleEffectTaskPool.Initialize(0x277A1A8, 0x2775068, 104, 200),
            NativeBattleEffectTaskPool.Initialize(0x2774F68, 0x276FB08, 108, 200),
            NativeBattleEffectTaskPool.Initialize(0x277AE98, 0x277AD58, 80, 4)
        ]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x8D5180, 100);
        hierarchy = root.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native8D5080 requires its physical first-contact pointer.");
        NativeBattleModulePoolTask task = hierarchy.Read(root.Address).WriteDword(0x0C, 0x1D99A78).WriteByte(0x2D, packets.ReadContact(first).Primary.ActorIndex).WriteWord(0x5A, header.ReadByte(16)).WriteWord(0x58, header.ReadByte(17));
        hierarchy = hierarchy.Write(root.Address, task);
        if ((parameters.Flags79 & 1) == 0)
        {
            int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x276FB00)?.ArenaOffset ?? throw new NotSupportedException("Native8D5080 requires its executed276FB00 initializer pointer.");
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native8D5080 requires its texture queue.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native8D5080 requires its571870 arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = 0x2775058 }, new(null));
    }

    public static NativeBattleDoubleStep Advance8D5180(NativeBattleDoubleFrame frame, NativeBattleDoubleResources resources, NativeBattleDoubleInputs inputs, NativeCrtRandomState random)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return new(frame, random, [], []);
        }
        if (frame.Effect.Binding.Entry != 0x8D5080 || frame.Effect.ReturnedDescriptor != 0x2775058 || frame.Effect.Pools.Length != 6 || !frame.Effect.Pools.Select(pool => pool.DescriptorAddress).SequenceEqual(new uint[] { 0x2775058, 0x277A1C0, 0x277A348, 0x277A1A8, 0x2774F68, 0x277AE98 }))
        {
            throw new InvalidDataException("Native8D5180 requires its six original Double descriptors.");
        }
        NativeBattleDoubleFrame current = frame;
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Double lost its50AFC0 parameters.");
        ImmutableArray<int>.Builder contacts = ImmutableArray.CreateBuilder<int>();
        ImmutableArray<NativeBattleModuleSound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleModuleSound>();
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyStep root = hierarchy.Advance(0, (state, address) =>
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != 0x8D5180)
            {
                throw new InvalidDataException("Native Double root callback is not8D5180.");
            }
            current = current with { State = current.State with { DrawCursor = (task.ReadByte(0x5C) & 1) != 0 ? 0 : 0x6000 } };
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, Models(inputs), inputs.ReadPoseSource);
            switch (Phase(task))
            {
                case 0:
                    task = Increment(task);
                    break;
                case 1:
                    state = state.Write(address, task.WriteByte(0x63, 1));
                    state = state.Append8DC540(1, 0x8D52A0, 92, address, Select).Hierarchy;
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
                    throw new InvalidDataException("Native8D5180 signed phase exceeds its five callbacks.");
            }
            state = state.Write(address, task);
            int survivors = 0;
            for (int pool = 1; pool < 6; pool++)
            {
                int selected = pool;
                NativeBattleEffectHierarchyStep children = state.Advance(pool, (childState, childAddress) =>
                {
                    if (selected == 1)
                    {
                        return AdvanceTarget(childState, childAddress);
                    }
                    current = current with { Effect = current.Effect with { Pools = childState.Pools } };
                    NativeBattleDoubleParticleStep particle = AdvancePrivate(current, childAddress, resources, inputs, random);
                    current = particle.Frame;
                    random = particle.Random;
                    return new(new(current.Effect.Pools), particle.Result);
                });
                state = children.Hierarchy;
                survivors = unchecked((ushort)(survivors + children.Survivors));
            }
            task = Add(Add(state.Read(address).WriteWord(0x5E, (ushort)survivors), 0x5C, 1), 0x24, 1);
            state = state.Write(address, task);
            bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
            return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
        });
        current = current with { Effect = current.Effect with { Pools = root.Hierarchy.Pools, ReturnedDescriptor = root.Survivors == 0 ? 0u : 0x2775058u } };
        return new(current, random, contacts.ToImmutable(), sounds.ToImmutable());

        NativeBattleEffectHierarchyCallback AdvanceTarget(NativeBattleEffectHierarchy state, uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != 0x8D52A0)
            {
                throw new InvalidDataException("Native Double target callback is not8D52A0.");
            }
            task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, Models(inputs), inputs.ReadPoseSource);
            int target = task.ReadByte(0x2D);
            NativeBattleGraphicsModel model = inputs.ReadModel(target);
            NativeBattlePoseSource source = inputs.ReadPoseSource(target);
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Double bounds require the actual target pose.");
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, source.Sample(sequence.Pose ?? throw new NotSupportedException("Native Double target lacks its selected pose."), sequence.PoseFrame114 ?? throw new NotSupportedException("Native Double target lacks its decoded pose frame."), sequence.PoseFlags109));
            state = state.Write(address, task);
            switch (Phase(task))
            {
                case 0:
                    state = state.Append8DC540(2, 0x8D5B40, 64, address, Select).Hierarchy;
                    task = Increment(state.Read(address));
                    break;
                case 1:
                    if (Word(task, 0x24) >= 19)
                    {
                        uint rootAddress = task.ReadDword(0x10);
                        state = state.Write(rootAddress, state.Read(rootAddress).WriteByte(0x63, 0));
                        DoubleExecution execution = new(current with { Effect = current.Effect with { Pools = state.Write(address, task).Pools } }, address, resources, inputs, random);
                        execution.SpawnOverlay8D6710();
                        current = execution.Frame;
                        state = execution.Hierarchy;
                        random = execution.Random;
                        task = Increment(state.Read(address));
                    }
                    break;
                case 2:
                    int header = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                    int contact = inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Native8D6940 requires its physical header contact pointer.");
                    _ = inputs.Packets.ReadContact(contact);
                    current = current with { Effect = current.Effect with { Pools = state.Write(address, task).Pools } };
                    current = inputs.ApplyContact?.Invoke(current, contact) ?? current;
                    state = new(current.Effect.Pools);
                    contacts.Add(contact);
                    task = Increment(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    break;
                case 3:
                    break;
                default:
                    throw new InvalidDataException("Native8D52A0 signed phase exceeds its four callbacks.");
            }
            state = state.Write(address, task);
            if (task.ReadWord(0x24) == 0)
            {
                sounds.Add(new(resources.Sound, 0, null) { Pan = 128 });
            }
            if (Word(task, 0x58) == 1)
            {
                current = inputs.Execute(current with { Effect = current.Effect with { Pools = state.Pools } }, new NativeBattleDoubleFlash());
                DoubleExecution execution = new(current, address, resources, inputs, random);
                execution.SpawnPrimary8D5360();
                state = execution.Hierarchy;
                current = execution.Frame;
                random = execution.Random;
                task = Add(state.Read(address), 0x58, 1);
            }
            else if (Word(task, 0x58) == 2)
            {
                task = task.WriteWord(0x58, 0);
            }
            task = Add(task, 0x24, 1);
            state = state.Write(address, task);
            bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
            return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
        }
    }

    private static NativeBattleGraphicsModel[] Models(NativeBattleDoubleInputs inputs) => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
    internal static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    internal static int Phase(NativeBattleModulePoolTask task) => unchecked((sbyte)task.ReadByte(0x29));
    internal static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(task.ReadWord(offset) + value)));
    internal static NativeBattleModulePoolTask Increment(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
}
