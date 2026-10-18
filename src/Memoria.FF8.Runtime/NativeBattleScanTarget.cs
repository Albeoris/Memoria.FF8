using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanTargetFrame(NativeBattleScanFrame Scan, ImmutableArray<NativeBattleGraphicsModel> Models, ImmutableArray<NativeBattleScanByteOwner> PhysicalModels);
public sealed record NativeBattleScanTargetStep(NativeBattleScanTargetFrame Frame, int Result);
public sealed record NativeBattleScanTargetInputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<uint, byte> ReadExternalByte, Func<NativeBattleScanTargetFrame, uint, uint, byte, NativeBattleScanTargetFrame> Sound501330, Func<NativeBattleScanTargetFrame, int, NativeBattleScanTargetFrame> ApplyContact506690)
{
    public Func<int, byte>? ReadResourceSequence { get; init; }
}

public static class NativeBattleScanTarget
{
    public static NativeBattleScanTargetStep Advance84D230(NativeBattleScanTargetFrame frame, uint address, NativeBattleScanTargetInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(inputs.ReadPoseSource);
        ArgumentNullException.ThrowIfNull(inputs.ReadExternalByte);
        ArgumentNullException.ThrowIfNull(inputs.Sound501330);
        ArgumentNullException.ThrowIfNull(inputs.ApplyContact506690);
        NativeBattleEffectParameters parameters = frame.Scan.Effect.Parameters ?? throw new InvalidDataException("Native Scan target requires its actual action parameters.");
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x84D230 || task.Payload.Length != 0x58 - 12)
        {
            throw new InvalidDataException("Native Scan target requires its84D230 fifty-eight-byte task.");
        }
        if (task.ReadWord(0x24) == 0)
        {
            NativeBattleModulePoolTask firstLevel = hierarchy.Read(task.ReadDword(0x14));
            //8DC700 copies these two DWORDs into an unused balanced local before501330.
            _ = firstLevel.ReadDword(0x38);
            _ = firstLevel.ReadDword(0x3C);
            frame = inputs.Sound501330(frame, 0x15D0B3C, 0, 128);
            hierarchy = new(frame.Scan.Effect.Pools);
            task = hierarchy.Read(address);
        }
        if (frame.Models.IsDefault || frame.Models.Length != 7 || frame.Models.Where((model, index) => model is null || model.Index != index).Any())
        {
            throw new InvalidDataException("Native Scan target requires its seven actual graphics models.");
        }
        task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, frame.Models, inputs.ReadPoseSource);
        int target = task.ReadByte(0x2D);
        NativeBattleGraphicsModel model = frame.Models[target];
        NativeBattlePoseSource source = inputs.ReadPoseSource(target);
        NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Scan target bounds require their current posed skeleton.");
        NativeBattleDecodedPose pose = source.Sample(sequence.Pose ?? throw new NotSupportedException("Native Scan target bounds require their selected pose."), sequence.PoseFrame114 ?? throw new NotSupportedException("Native Scan target bounds require their actual pose frame."), sequence.PoseFlags109);
        task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, pose);
        hierarchy = hierarchy.Write(address, task);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                NativeBattleScanProcess process = frame.Scan.Memory.Scan ?? throw new NotSupportedException("Native Scan target requires its retained presentation process.");
                frame = frame with { Scan = frame.Scan with { Memory = frame.Scan.Memory with { Scan = process with { Presentation = process.Presentation with { Result269D938 = 0, Exit269CE2C = 0 } } } } };
                NativeBattleScanModelSelectionResult selection = NativeBattleScanModelSelection.Select84D320(frame.PhysicalModels, task.ReadByte(0x2D), inputs.ReadExternalByte, inputs.ReadResourceSequence);
                process = frame.Scan.Memory.Scan!;
                NativeBattleScanByteOwner selected = process.Presentation.Selection269A1BC;
                for (int index = 0; index < 7; index++)
                {
                    selected = selected.WriteDword(index * 4, selection.SelectionFlags[index]);
                }
                frame = frame with { Scan = frame.Scan with { Memory = frame.Scan.Memory with { Scan = process with { Presentation = process.Presentation with { Selection269A1BC = selected, PrimaryModel269CE30 = selection.SelectedPhysicalModel } } } } };
                int groupContact = checked(FirstContact(parameters.HeaderIndex7C) + unchecked((sbyte)task.ReadByte(0x2A)));
                uint callback = (inputs.Packets.ReadContact(groupContact).Primary.Flags2 & 2) == 0 ? 0x84D4B0u : 0x850B40u;
                hierarchy = hierarchy.Append8DC540(2, callback, 0x78, address, (pointer, group, actor) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, actor)).Hierarchy;
                task = hierarchy.Read(address).WriteByte(0x29, 1);
                break;
            case 1:
                if (task.ReadByte(0x28) == 0)
                {
                    uint rootAddress = task.ReadDword(0x10);
                    hierarchy = hierarchy.Write(rootAddress, hierarchy.Read(rootAddress).WriteByte(0x63, 0));
                    PublishHierarchy();
                    int contact = FirstContact(checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A))));
                    frame = inputs.ApplyContact506690(frame, contact);
                    hierarchy = new(frame.Scan.Effect.Pools);
                    task = hierarchy.Read(address).WriteByte(0x26, (byte)(hierarchy.Read(address).ReadByte(0x26) | 1)).WriteByte(0x29, 2);
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Native Scan target signed phase exceeds its three callbacks.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool completed = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (completed)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        PublishHierarchy();
        return new(frame, completed ? 2 : 0);

        int FirstContact(int header)
        {
            if ((uint)header >= inputs.Packets.Headers.Length)
            {
                throw new NotSupportedException("Native Scan target group leaves its actual header directory.");
            }
            return inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Native Scan target requires its actual header contact pointer.");
        }

        void PublishHierarchy() => frame = frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools } } };
    }
}
