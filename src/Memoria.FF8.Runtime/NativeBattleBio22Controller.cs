using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBio22State([property: JsonRequired] int? DrawCursor);
public sealed record NativeBattleBio22Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleBio22State State);
public sealed record NativeBattleBio22ChildStep(NativeBattleBio22Frame Frame, NativeCrtRandomState Random, int Result);
public abstract record NativeBattleBio22Operation;
public sealed record NativeBattleBio22Packets(NativeBattleGfGeometryBatch Batch) : NativeBattleBio22Operation;
public sealed record NativeBattleBio22Sprite(NativeBattleModulePacketBatch Batch) : NativeBattleBio22Operation;
public sealed record NativeBattleBio22Stage(NativeBattleStageState Stage) : NativeBattleBio22Operation;
public sealed record NativeBattleBio22Sound(uint Sound) : NativeBattleBio22Operation;
public sealed record NativeBattleBio22Contact(int Index) : NativeBattleBio22Operation;
public sealed record NativeBattleBio22Inputs(NativeBattleActionPacketsState Packets, NativeBattleCameraProjection Projection, short CameraRoll, short Zsf3, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleBio22Frame, NativeBattleStageState> ReadStage, Func<NativeBattleBio22Frame, int, ImmutableArray<int>> ReadOverlayProxyDepth, Func<NativeBattleBio22Frame, NativeBattleBio22Operation, NativeBattleBio22Frame> Execute, Func<NativeBattleBio22Frame, uint, NativeCrtRandomState, NativeBattleBio22ChildStep> AdvanceFinalActor);

public static partial class NativeBattleBio22Controller
{
    public static NativeBattleBio22Frame Initialize89A6B0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleBio22Resources resources)
    {
        NativeBattleEnhancementEntryResult initialized = NativeBattleEnhancementEntry.Initialize(memory, effect, packets, resources.Animation, NativeBattleEnhancementProfile.Bio22);
        return new(initialized.Memory, initialized.Effect, new(null));
    }

    public static NativeBattleBio22ChildStep Advance89A7B0(NativeBattleBio22Frame frame, NativeBattleBio22Resources resources, NativeBattleBio22Inputs inputs, NativeCrtRandomState random)
    {
        NativeBattleEnhancementRootInputs rootInputs = new(inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, (child, address, currentRandom) =>
        {
            Execution execution = new(new(child.Memory, child.Effect, new(child.DrawCursor)), resources, inputs, currentRandom);
            int result = execution.Advance(address);
            return new(new(execution.Frame.Memory, execution.Frame.Effect, execution.Frame.State.DrawCursor), execution.Random, result);
        });
        NativeBattleEnhancementRootStep root = NativeBattleEnhancementRoot.Advance(new(frame.Memory, frame.Effect, frame.State.DrawCursor), NativeBattleEnhancementProfile.Bio22, rootInputs, random);
        return new(new(root.Frame.Memory, root.Frame.Effect, new(root.Frame.DrawCursor)), root.Random, 0);
    }

    private sealed partial class Execution(NativeBattleBio22Frame frame, NativeBattleBio22Resources resources, NativeBattleBio22Inputs inputs, NativeCrtRandomState random)
    {
        public NativeBattleBio22Frame Frame { get; private set; } = frame;
        public NativeCrtRandomState Random { get; private set; } = random;
        private NativeBattleEffectHierarchy Hierarchy => new(Frame.Effect.Pools);
        private NativeBattleModulePoolTask Read(uint address) => Hierarchy.Read(address);
        private void Write(uint address, NativeBattleModulePoolTask task) => Frame = Frame with { Effect = Frame.Effect with { Pools = Hierarchy.Write(address, task).Pools } };
        private void Execute(NativeBattleBio22Operation operation) => Frame = inputs.Execute(Frame, operation);
        private NativeBattleModulePoolTask Primary(uint address) => Read(Read(address).ReadDword(0x14));

        private uint Append(uint parent, uint callback, int bytes, int pool, bool required)
        {
            NativeBattleEffectParameters parameters = Frame.Effect.Parameters ?? throw new InvalidDataException("Native Bio append lost its action parameters.");
            NativeBattleEffectHierarchyAppend appended = Hierarchy.Append8DC540(pool, callback, bytes, parent, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target));
            Frame = Frame with { Effect = Frame.Effect with { Pools = appended.Hierarchy.Pools } };
            if (required && appended.Address == 0)
            {
                throw new NotSupportedException("Native Bio dereferenced child allocation failed in its actual task pool.");
            }
            return appended.Address;
        }

        public int Advance(uint address)
        {
            uint callback = Read(address).Callback;
            uint result;
            switch (callback)
            {
                case 0x89A8D0:
                    result = AdvancePrimary(address);
                    break;
                case 0x89B8A0:
                    NativeBattleBio22StageFadeStep stage = NativeBattleBio22StageFade.Advance89B8A0(Read(address), Primary(address), inputs.ReadStage(Frame));
                    Write(address, stage.Task);
                    Execute(new NativeBattleBio22Stage(stage.Stage));
                    result = stage.Result;
                    break;
                case 0x89AAE0:
                    NativeBattleBio22GeometryTaskStep geometry = NativeBattleBio22GeometryTask.Advance89AAE0(Read(address), Primary(address), Frame.Memory, resources, (memory, task, mesh, deformed) =>
                    {
                        Frame = Frame with { Memory = memory };
                        Write(address, task);
                        Execute(new NativeBattleBio22Packets(deformed ? NativeBattleBio22GeometryPackets.Emit89AD30(task, mesh.Bytes, inputs.Projection, inputs.Zsf3) : NativeBattleBio22GeometryPackets.Emit89AB90(task, mesh.Bytes, inputs.Projection, inputs.Zsf3)));
                        return Frame.Memory;
                    });
                    Frame = Frame with { Memory = geometry.Memory };
                    Write(address, geometry.Task);
                    result = geometry.Result;
                    break;
                case 0x89CA70:
                    NativeBattleBio22PaletteStep palette = NativeBattleBio22PaletteTask.Advance89CA70(Read(address), Primary(address), Frame.Memory.Bio22Palette ?? throw new NotSupportedException("Native Bio requires its current process palette."), resources, Random);
                    Write(address, palette.Task);
                    Random = palette.Random;
                    Frame = Frame with { Memory = Frame.Memory with { Bio22Palette = palette.Palette } };
                    if (palette.QueueTim)
                    {
                        Frame = Frame with { Memory = NativeBattleProcessTim.Enqueue505E30(Frame.Memory, 0x16146E8) };
                    }
                    result = palette.Result;
                    break;
                case 0x89C650:
                    NativeBattleBio22OverlayStep overlay = NativeBattleBio22Overlay.Advance89C650(Read(address), resources.OverlayProgram, ordinal => inputs.ReadOverlayProxyDepth(Frame, ordinal));
                    Write(address, overlay.Task);
                    Execute(new NativeBattleBio22Packets(overlay.Packets));
                    result = overlay.Result;
                    break;
                case 0x89BA80:
                case 0x89CD60:
                case 0x89D020:
                    result = AdvanceEmitter(address, callback);
                    break;
                case 0x89BD20:
                case 0x89BF20:
                case 0x89C930:
                case 0x89CE60:
                case 0x89D120:
                    result = AdvanceParticle(address, callback);
                    break;
                case 0x89C270:
                    result = AdvanceSecondGeometry(address);
                    break;
                case 0x89D3D0:
                    NativeBattleBio22ChildStep actor = inputs.AdvanceFinalActor(Frame, address, Random);
                    Frame = actor.Frame;
                    Random = actor.Random;
                    result = unchecked((uint)actor.Result);
                    break;
                default:
                    throw new NotSupportedException($"Native Bio callback{callback:X} has no current implementation.");
            }
            if (result == 2)
            {
                Frame = Frame with { Effect = Frame.Effect with { Pools = Hierarchy.Complete8DC530(address).Pools } };
            }
            return unchecked((int)result);
        }

        private uint AdvancePrimary(uint address)
        {
            NativeBattleBio22PrimaryInputs primaryInputs = new(Update, CenterY, HalfHeight, AppendPrimary, Contact, Sound);
            NativeBattleBio22PrimaryStep step = NativeBattleBio22Primary.Advance89A8D0(Read(address), primaryInputs);
            Write(address, step.Task);
            return step.Result;

            NativeBattleModulePoolTask Update(NativeBattleModulePoolTask task)
            {
                task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray(), inputs.ReadPoseSource);
                int index = task.ReadByte(0x2D);
                NativeBattlePoseSource source = inputs.ReadPoseSource(index);
                NativeBattleSequenceState sequence = inputs.ReadModel(index).SequenceState ?? throw new NotSupportedException("Native Bio bounds require the current target sequence.");
                return NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, source.Sample(sequence.Pose ?? throw new NotSupportedException("Native Bio requires its selected target pose."), sequence.PoseFrame114 ?? throw new NotSupportedException("Native Bio requires its decoded target frame."), sequence.PoseFlags109));
            }

            short CenterY(NativeBattleModulePoolTask task) => unchecked((short)(PositionY(task) + (S(task, 0x52) + S(task, 0x4A)) / 2));
            short HalfHeight(NativeBattleModulePoolTask task) => unchecked((short)(unchecked((short)(task.ReadWord(0x52) - task.ReadWord(0x4A))) / 2));

            NativeBattleModulePoolTask AppendPrimary(NativeBattleModulePoolTask task, uint callback, short? variant)
            {
                Write(address, task);
                bool geometry = callback is 0x89AAE0 or 0x89C270;
                uint child = Append(address, callback, geometry ? 0x98 : 0x38, geometry ? 4 : 2, variant is not null);
                if (variant is short selected)
                {
                    Write(child, Read(child).WriteWord(0x8C, unchecked((ushort)selected)));
                }
                return Read(address);
            }

            NativeBattleModulePoolTask Contact(NativeBattleModulePoolTask task)
            {
                Write(address, task);
                int header = checked((Frame.Effect.Parameters ?? throw new InvalidDataException("Native Bio contact requires its action parameters.")).HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                int contact = inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Native Bio contact requires its actual header pointer.");
                Execute(new NativeBattleBio22Contact(contact));
                return Read(address);
            }

            NativeBattleModulePoolTask Sound(NativeBattleModulePoolTask task)
            {
                Write(address, task);
                Execute(new NativeBattleBio22Sound(resources.Sound));
                return Read(address);
            }
        }

        private short PositionY(NativeBattleModulePoolTask task) => (inputs.ReadModel(task.ReadByte(0x2D)).Transform ?? throw new NotSupportedException("Native Bio requires the target's current physical position.")).Position28.Y;
        private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    }
}
