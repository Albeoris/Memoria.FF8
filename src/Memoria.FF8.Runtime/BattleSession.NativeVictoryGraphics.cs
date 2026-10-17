using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) PollNativeVictory503040(NativeBattleGraphicsState graphics, int taskSlot, byte phase)
    {
        byte completion = graphics.Descriptors.Tasks[taskSlot].Payload[2];
        NativeBattleVictoryMusicStep step = NativeBattleVictoryMusicLoader.Advance503040(phase, NativeEffectIoResult, completion, NativeFileStaging, NativeMusicProcess);
        NativeMusicProcess = step.Music;
        if (step.FileRequest is { } request)
        {
            NativeBattleArchiveFile file = definition.NativeVictoryFile ?? throw new NotSupportedException("Native503040 requires its actual original B84CCC file766 resource.");
            if (file.FileId != request.FileId || file.Bytes.IsDefaultOrEmpty)
            {
                throw new InvalidDataException("Native503040 requires its selected original victory file bytes.");
            }
            NativeFileStaging = (NativeFileStaging ?? NativeBattleFileStaging.Unknown()).Write(request.DestinationAddress, file.Bytes.AsSpan());
            NativeEffectIoResult = -1;
            QueueNativeFileIo482610(file.Bytes.Length, request.CompletionCallback);
        }
        if (step.StartWorker)
        {
            uint address = NativeBattleDescriptorState.CompletionAddress503040(taskSlot);
            NativeBattleAuxiliaryState auxiliary = graphics.Auxiliary.AppendMusicLoad501A20(address, (target, value) => graphics = graphics with { Descriptors = graphics.Descriptors.WriteCompletion501A70(target, value) });
            graphics = graphics with { Auxiliary = auxiliary };
        }
        if (step.ResourceRows is not uint row)
        {
            return (graphics, step.Phase, false);
        }
        for (byte index = 0; index < 3; index++)
        {
            NativeBattleGraphicsModel model = graphics.Models[index];
            if ((model.Flags0 & 2) == 0)
            {
                continue;
            }
            uint status = model.Flags8 ?? throw new NotSupportedException("Native503040 requires the loaded party model DWORD8.");
            if ((status & 0x1A) != 0)
            {
                continue;
            }
            if (model.HasLinkedModelRing140 == true)
            {
                throw new NotSupportedException("Native509CD0 requires its actual linked model ring traversal.");
            }
            model = SetNativeGraphicsFlags(model, status & 0x400, graphics);
            graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
            ImmutableArray<NativeBattleVictoryModelSection> mappings = definition.NativeGraphics!.Rules.VictoryModelSections ?? throw new NotSupportedException("Native503040 requires its six original B8B7E0 model/section pairs.");
            NativeBattleVictoryModelSection? selected = mappings.FirstOrDefault(candidate => candidate.Model == model.ModelId4);
            if (selected is null)
            {
                graphics = SelectNativeHeaderSequence(graphics, index, 18);
                continue;
            }
            NativeBattleGraphicsResource previous = NativeGraphicsPoseResource(graphics, index);
            model = RetainNativeDecodedPoseBeforeResourceChange(model, previous);
            NativeBattleFileStaging staging = NativeFileStaging ?? throw new InvalidDataException("Native503040 lost its current shared staging bytes.");
            uint directory = checked(NativeBattleFileStaging.Address + BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(checked(NativeBattleFileStaging.Address + (uint)(selected.Section + 1) * 4), 4)));
            NativeBattleVictoryResourceBinding binding = NativeBattleVictoryResources.Bind5022C0(staging, NativeModelCpuResources ?? throw new NotSupportedException("Native503040 requires its current physical CPU resource owners."), model, row, directory);
            NativeFileStaging = binding.Staging;
            row = binding.NextRow;
            graphics = SelectNativeHeaderSequence(graphics with { Models = graphics.Models.SetItem(index, binding.Model) }, index, 1);
        }
        graphics = StartNativeVictoryCamera5064F0(graphics);
        return (graphics, step.Phase, true);
    }

    private static NativeBattleGraphicsModel RetainNativeDecodedPoseBeforeResourceChange(NativeBattleGraphicsModel model, NativeBattleGraphicsResource previous)
    {
        NativeBattleSequenceState state = model.SequenceState ?? throw new NotSupportedException("Native5022C0 requires its actual existing decoded pose owner.");
        NativeBattlePoseSource source = previous.PoseSource ?? throw new NotSupportedException("Native5022C0 requires its actual previous skeleton/animation source.");
        NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(state, source);
        NativeBattleDecodedPose? linked = state.LinkedPose is not null && previous.LinkedPoseSource is { } linkedSource ? NativeBattlePoseMatrices.ReadLinkedDecodedPose(state, linkedSource) : state.LinkedDecodedPose;
        return model with { SequenceState = state with { DecodedPose = decoded, LinkedDecodedPose = linked } };
    }
}
