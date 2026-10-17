using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTripleResources(NativeBattleEffectHeapAllocation Animation, ImmutableArray<NativeBattleModuleResource> Sprites, ImmutableArray<NativeBattleStaticMeshResource> Meshes, ImmutableArray<byte> MorphFirst, ImmutableArray<byte> MorphSecond, ImmutableArray<ushort> Curve89E5E0, ImmutableArray<ushort> Curve89E620, uint Sound);
public sealed record NativeBattleTripleState([property: JsonRequired] int? DrawCursor);
public sealed record NativeBattleTripleFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleTripleState State);
public abstract record NativeBattleTripleOperation;
public sealed record NativeBattleTripleFlash : NativeBattleTripleOperation;
public sealed record NativeBattleTriplePackets(NativeBattleGfGeometryBatch Batch, ushort Flags20 = 0) : NativeBattleTripleOperation;
public sealed record NativeBattleTripleSprite(NativeBattleModuleDraw Draw) : NativeBattleTripleOperation;
public sealed record NativeBattleTripleInputs(NativeBattleActionPacketsState Packets, NativeBattleCameraProjection Projection, short CameraRoll, NativeBattleSummonColors Colors, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleTripleFrame, NativeBattleTripleOperation, NativeBattleTripleFrame> Execute)
{
    public Func<NativeBattleTripleFrame, int, NativeBattleTripleFrame>? ApplyContact { get; init; }
}
public sealed record NativeBattleTripleStep(NativeBattleTripleFrame Frame, NativeCrtRandomState Random, ImmutableArray<int> Contacts, ImmutableArray<NativeBattleModuleSound> Sounds);
public sealed record NativeBattleTripleParticleStep(NativeBattleTripleFrame Frame, NativeCrtRandomState Random, int Result);

public static partial class NativeBattleTripleController
{
    public static NativeBattleTripleFrame Initialize89DEF0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleTripleResources resources)
    {
        NativeBattleEnhancementEntryResult initialized = NativeBattleEnhancementEntry.Initialize(memory, effect, packets, resources.Animation, NativeBattleEnhancementProfile.Triple);
        return new(initialized.Memory, initialized.Effect, new(null));
    }

    public static NativeBattleTripleStep Advance89DFF0(NativeBattleTripleFrame frame, NativeBattleTripleResources resources, NativeBattleTripleInputs inputs, NativeCrtRandomState random)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return new(frame, random, [], []);
        }
        NativeBattleTripleFrame current = frame;
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Triple lost its50AFC0 parameters.");
        ImmutableArray<int>.Builder contacts = ImmutableArray.CreateBuilder<int>();
        ImmutableArray<NativeBattleModuleSound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleModuleSound>();
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
        NativeBattleEnhancementRootInputs sharedInputs = new(inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, (childFrame, childAddress, childRandom) =>
        {
            current = new(childFrame.Memory, childFrame.Effect, new(childFrame.DrawCursor));
            random = childRandom;
            NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
            if (hierarchy.Read(childAddress).Callback == 0x89E110)
            {
                NativeBattleEffectHierarchyCallback target = AdvanceTarget(hierarchy, childAddress);
                current = current with { Effect = current.Effect with { Pools = target.Hierarchy.Pools } };
                return new(new(current.Memory, current.Effect, current.State.DrawCursor), random, target.Result);
            }
            NativeBattleTripleParticleStep particle = AdvancePrivate(current, childAddress, resources, inputs, random);
            current = particle.Frame;
            return new(new(current.Memory, current.Effect, current.State.DrawCursor), particle.Random, particle.Result);
        });
        NativeBattleEnhancementRootStep root = NativeBattleEnhancementRoot.Advance(new(frame.Memory, frame.Effect, frame.State.DrawCursor), NativeBattleEnhancementProfile.Triple, sharedInputs, random);
        return new(new(root.Frame.Memory, root.Frame.Effect, new(root.Frame.DrawCursor)), root.Random, contacts.ToImmutable(), sounds.ToImmutable());

        NativeBattleEffectHierarchyCallback AdvanceTarget(NativeBattleEffectHierarchy state, uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != 0x89E110)
            {
                throw new InvalidDataException("Native Triple target callback is not89E110.");
            }
            task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, Models(inputs), inputs.ReadPoseSource);
            int target = task.ReadByte(0x2D);
            NativeBattleGraphicsModel model = inputs.ReadModel(target);
            NativeBattlePoseSource source = inputs.ReadPoseSource(target);
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Triple bounds require the actual target pose.");
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, source.Sample(sequence.Pose ?? throw new NotSupportedException("Native Triple target lacks its selected pose."), sequence.PoseFrame114 ?? throw new NotSupportedException("Native Triple target lacks its decoded pose frame."), sequence.PoseFlags109));
            state = state.Write(address, task);
            switch (Phase(task))
            {
                case 0:
                    state = state.Append8DC540(2, 0x89EA70, 64, address, Select).Hierarchy;
                    task = Increment(state.Read(address));
                    break;
                case 1:
                    if (Word(task, 0x24) >= 24)
                    {
                        uint rootAddress = task.ReadDword(0x10);
                        state = state.Write(rootAddress, state.Read(rootAddress).WriteByte(0x63, 0));
                        TripleExecution execution = new(current with { Effect = current.Effect with { Pools = state.Write(address, task).Pools } }, address, resources, inputs, random);
                        execution.SpawnOverlay89F8D0();
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
                    throw new InvalidDataException("Native89E110 signed phase exceeds its four callbacks.");
            }
            state = state.Write(address, task);
            if (task.ReadWord(0x24) == 0)
            {
                //8DC700 copies both retained first-level DWORDs into caller-local output before sound.
                NativeBattleModulePoolTask first = state.Read(task.ReadDword(0x14));
                _ = first.ReadDword(0x38);
                _ = first.ReadDword(0x3C);
                sounds.Add(new(resources.Sound, 0, null) { Pan = 128 });
            }
            if (Word(task, 0x58) == 1)
            {
                current = inputs.Execute(current with { Effect = current.Effect with { Pools = state.Pools } }, new NativeBattleTripleFlash());
                TripleExecution execution = new(current, address, resources, inputs, random);
                execution.SpawnPrimary89E1E0();
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

    private static NativeBattleGraphicsModel[] Models(NativeBattleTripleInputs inputs) => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
    internal static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    internal static int Phase(NativeBattleModulePoolTask task) => unchecked((sbyte)task.ReadByte(0x29));
    internal static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(task.ReadWord(offset) + value)));
    internal static NativeBattleModulePoolTask Increment(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
}
