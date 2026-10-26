using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect187Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect187Controller
{
    public static NativeBattleEffect187Step AdvanceModel649D00(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Resources resources, NativeBattleEffect187Inputs inputs)
    {
        NativeBattleEffect187ModelInputs services = new(inputs.ReadFlags(frame), () => (frame.Memory.GuardianCameraProcess ?? throw new NotSupportedException("Effect187 model requires its actual camera sign.")).Sign, Execute);
        NativeBattleEffect187ModelDirectorStep step = NativeBattleEffect187ModelDirector.Advance649D00(Read(frame, address), services);
        return new(Write(frame, address, Read(frame, address).WriteWord(12, step.Counter)), step.Result);

        void Execute(NativeBattleEffect187ModelOperation operation)
        {
            switch (operation.Service)
            {
                case NativeBattleEffect187ModelService.Initialize:
                    NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
                    frame = StoreMatrix(frame, NativeBattleEffect187Model.Address + 0x40, identity with { Rotation = identity.Rotation.Select(value => unchecked((short)(value * 2))).ToImmutableArray(), X = 0, Y = 0, Z = 1800 });
                    frame = SetModel(frame, NativeBattleEffect187Model.Initialize64A1D0(RequireModel(frame), resources.OwnedModel, frame.State.ReadDword(ArenaGlobal)));
                    frame = frame with { State = frame.State.WriteWord(NativeBattleEffect187Model.Address, (ushort)(frame.State.ReadWord(NativeBattleEffect187Model.Address) | 1)) };
                    (frame, _) = Spawn647900(frame, 0x64C060);
                    break;
                case NativeBattleEffect187ModelService.Scale:
                    frame = frame with { State = frame.State.WriteWord(0x24FD918, checked((ushort)operation.First)).WriteWord(0x24FD920, checked((ushort)operation.First)).WriteWord(0x24FD928, checked((ushort)operation.First)) };
                    break;
                case NativeBattleEffect187ModelService.Select:
                    frame = SetModel(frame, NativeBattleEffect187Model.Select64A240(RequireModel(frame), resources.OwnedModel, checked((short)operation.First)));
                    frame = PublishModelPose(frame, resources);
                    break;
                case NativeBattleEffect187ModelService.Advance:
                    frame = SetModel(frame, NativeBattleEffect187Model.Advance64A260(RequireModel(frame), resources.OwnedModel).State);
                    frame = PublishModelPose(frame, resources);
                    break;
                case NativeBattleEffect187ModelService.Draw:
                    frame = DrawOwnedModel64A2A0(frame, resources, inputs);
                    break;
                case NativeBattleEffect187ModelService.InitializeChild:
                    frame = operation.First switch
                    {
                        0x64A690 => InitializeRings64A690(frame),
                        0x64B040 => InitializeBodyBurst64B040(frame),
                        0x64B610 => InitializeBoneParticles64B610(frame),
                        0x64BB20 => InitializePairedBoneParticles64BB20(frame),
                        0x64C300 => InitializeRibbons64C300(frame),
                        0x64C710 => InitializeMeshTrails64C710(frame, resources),
                        0x64D850 => InitializeMeshTrails64D850(frame, resources, operation.Second),
                        _ => throw new NotSupportedException("Effect187 model selected an unsupported initializer.")
                    };
                    break;
                case NativeBattleEffect187ModelService.Append:
                    (frame, _) = Spawn647900(frame, unchecked((uint)operation.First));
                    break;
                case NativeBattleEffect187ModelService.CaptureBone:
                    NativeBattleEffect187PointStep point = ReadOwnedBone502170(frame, resources, operation.First, operation.Second, TaskView(Read(frame, address), 0x10, 8));
                    frame = Write(point.Frame, address, CopyTask(Read(point.Frame, address), 0x10, point.Point8, 8));
                    break;
                case NativeBattleEffect187ModelService.Translate:
                    frame = frame with { State = frame.State.WriteDword(0x24FD92C, unchecked((uint)operation.First)).WriteDword(0x24FD930, unchecked((uint)operation.Second)).WriteDword(0x24FD934, unchecked((uint)operation.Third)) };
                    break;
                case NativeBattleEffect187ModelService.AddZ:
                    frame = frame with { State = frame.State.WriteDword(0x24FD934, unchecked(frame.State.ReadDword(0x24FD934) + (uint)operation.First)) };
                    break;
                case NativeBattleEffect187ModelService.Flash:
                    frame = inputs.Execute(frame, new NativeBattleEffect187Flash5712C0(operation.First, operation.Second, operation.Third, operation.Fourth));
                    break;
                case NativeBattleEffect187ModelService.RestoreActorFlags:
                    frame = RestoreActorFlags64DC70(frame, checked((ushort)operation.First), inputs);
                    break;
                case NativeBattleEffect187ModelService.Complete:
                    frame = frame with { State = frame.State.WriteDword(0x24FD50C, frame.State.ReadDword(0x24FD50C) | 1) };
                    break;
                default:
                    throw new NotSupportedException("Effect187 model selected an unsupported timeline service.");
            }
        }
    }

    public static NativeBattleEffect187PointStep ReadOwnedBone502170(NativeBattleEffect187Frame frame, NativeBattleEffect187Resources resources, int bone, int fraction, NativeBattleEffectArena retained)
    {
        if ((uint)bone > byte.MaxValue || fraction < short.MinValue || fraction > short.MaxValue)
        {
            throw new InvalidDataException("Effect187 owned bone query leaves its actual skeleton.");
        }
        NativeBattleCactuarRawModelState model = RequireModel(frame);
        uint skeleton = checked((model.WorkingBase ?? throw new NotSupportedException("Effect187 bone query requires its actual working model.")) + (uint)resources.OwnedModel.Section(0));
        if (bone >= 240)
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(model.Model.AsSpan(4, 2)) == 0x8F)
            {
                if (resources.OwnedModel.PoseSource.Model8FBoneAliases.IsDefaultOrEmpty)
                {
                    throw new NotSupportedException("Effect187 model8F bone selectors require their actual source aliases.");
                }
                bone = resources.OwnedModel.PoseSource.Model8FBoneAliases[bone - 240];
            }
            else
            {
                int offset = bone - 236;
                if (offset >= 16)
                {
                    throw new NotSupportedException("Effect187 bone selector exceeds its current skeleton header.");
                }
                bone = ReadBytes(frame, checked(skeleton + (uint)offset), 1).Read(0, 1)[0];
            }
            if ((bone & 0x80) != 0)
            {
                fraction = 4096;
                bone &= 0x7F;
            }
        }
        if ((uint)bone >= resources.OwnedModel.PoseSource.BoneCount)
        {
            throw new InvalidDataException("Effect187 bone alias leaves its actual skeleton.");
        }
        uint record = checked(skeleton + 16u + (uint)(bone * 48));
        short parent = BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(frame, record, 2).Read(0, 2));
        short length = BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(frame, record + 2, 2).Read(0, 2));
        short distance = unchecked((short)(parent < 0 ? 0 : fraction * length >> 12));
        NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(ReadPacked(frame, NativeBattleEffect187Model.Address + 0x40).ReadMatrix(), ReadPacked(frame, record + 16).ReadMatrix()), null);
        (frame, uint temporary) = Allocate(frame, 52);
        NativeBattleEffectArena context = Context(frame, temporary, 52);
        context = CopyBytes(PackedArena(matrix), 0, context, 0, 32).MarkUnknown(18, 2);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix.ReadMatrix(), null));
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadAnchorScratchZ(Registers(frame).Geometry, distance);
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadAnchorVector(new(geometry, Registers(frame).Colors));
        registers = NativeBattleGeometry.Transform(registers);
        frame = Publish(frame, registers);
        for (int axis = 0; axis < 3; axis++)
        {
            int value = registers.Colors.Mac[axis] ?? throw new InvalidDataException("Effect187 bone query did not publish its actual MAC output.");
            context = context.Write(32 + axis * 4, BitConverter.GetBytes(value));
            retained = retained.Write(axis * 2, BitConverter.GetBytes(unchecked((short)value)));
        }
        context = context.Write(48, BitConverter.GetBytes(registers.Colors.Flags ?? throw new InvalidDataException("Effect187 bone query did not publish its actual flags.")));
        frame = Release(PublishContext(frame, temporary, context, 52), 52);
        return new(frame, retained);
    }

    private static NativeBattleCactuarRawModelState RequireModel(NativeBattleEffect187Frame frame) => frame.State.Model ?? throw new NotSupportedException("Effect187 requires its retained raw model owner.");

    private static NativeBattleEffect187Frame SetModel(NativeBattleEffect187Frame frame, NativeBattleCactuarRawModelState model) => frame with { State = frame.State with { Model = model } };

    private static NativeBattleEffect187Frame PublishModelPose(NativeBattleEffect187Frame frame, NativeBattleEffect187Resources resources)
    {
        NativeBattleCactuarRawModelState model = RequireModel(frame);
        NativeBattleDecodedPose pose = model.DecodedPose ?? throw new NotSupportedException("Effect187 requires its current decoded model pose.");
        frame = frame with { Memory = NativeBattleEffect187Model.PublishPose508C90(frame.Memory, model, resources.OwnedModel) };
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = NativeBattleBodyBoneTemporary.Prepare508C90(resources.OwnedModel.PoseSource, pose, NativeBattlePoseMatrices.StageBoneMatrices(resources.OwnedModel.PoseSource, pose), Context(frame, temporary, 88));
        return Release(PublishContext(frame, temporary, context, 88), 88);
    }
}
