using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Original camera group/track directory addressed by503520, including shared track tails.</summary>
public sealed record NativeBattleCameraBank(ImmutableArray<byte> Data)
{
    public int? TrackOffset(byte selector)
    {
        ushort ReadWord(int offset)
        {
            if (Data.IsDefault || offset < 0 || offset > Data.Length - 2)
            {
                throw new InvalidDataException("Native camera directory exceeds its original resource.");
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan().Slice(offset));
        }
        int group = selector >> 4 & 15;
        int groups = ReadWord(0);
        if (group >= groups)
        {
            return null;
        }
        int directory = ReadWord(2 + 2 * group);
        int relative = unchecked((short)ReadWord(directory + 2 * (selector & 7)));
        int track = checked(directory + 2 * relative);
        _ = ReadWord(track);
        return track;
    }
}

public sealed partial record NativeBattleCameraTask(byte Channel, NativeBattleCameraBank Bank, int ProgramOffset, ushort Elapsed, ushort Duration);

/// <summary>5041E0/503520/5035E0 two-slot camera callback owner; mask includes the separately written high bits.</summary>
public sealed partial record NativeBattleCameraState(ushort Mask, ImmutableArray<NativeBattleCameraTask?> Tasks, ImmutableArray<int> Order)
{
    public NativeBattleEffectAnimationRegisters? ControlResource { get; init; }
    public ImmutableArray<short> Variables { get; init; } = [0, 0, 0, 0, 0, 0, 0, 0];
    public ImmutableArray<short?> Scratch { get; init; } = ImmutableArray.CreateRange(new short?[8]);
    public int? ControlCursor { get; init; }
    public bool? ForceCamera { get; init; }
    public ushort BlendProgress { get; init; } = 4096;
    public ulong PoseCaptureCount { get; init; }
    public static NativeBattleCameraState Initialize() => new(0, [null, null], []) { Mode = 1, ForceCamera = false };

    public void Validate()
    {
        if (ControlResource is { } control && (control.Source is null || control.Source.Data.IsDefault || control.StreamOffset < 0 || control.StreamOffset >= control.Source.Data.Length || control.ModelTableOffset < 0 || control.ModelTableOffset > control.Source.Data.Length - 2))
        {
            throw new InvalidDataException("Native509610 camera control source leaves its original effect resource.");
        }
        if (Variables.IsDefault || Variables.Length != 8 || Scratch.IsDefault || Scratch.Length != 8 || RetainedPoses.IsDefault || RetainedPoses.Length != 2 || SelectedSlot is < 0 or >= 2 || ControlCursor < 0 || Tasks.IsDefault || Tasks.Length != 2 || Order.IsDefault || Order.Any(slot => slot is < 0 or >= 2 || Tasks[slot] is null) || Order.Distinct().Count() != Order.Length || Order.Length != Tasks.Count(task => task is not null) || Tasks.Any(task => task is not null && (task.Channel >= 8 || task.Bank.Data.IsDefault || task.ProgramOffset < 0 || task.ProgramOffset > task.Bank.Data.Length - 2 || task.Elapsed != 0 && task.Pose is null)))
        {
            throw new InvalidDataException("Native camera task owner requires its two original slots and bounded resource cursors.");
        }
    }

    public NativeBattleCameraState Start(NativeBattleCameraBank? bank, byte selector)
    {
        if (bank is null)
        {
            return this;
        }
        Validate();
        int? track = bank.TrackOffset(selector);
        if (track is null)
        {
            return this;
        }
        NativeBattleCameraState result = (Mask & 0xFF00) == 0 && ForceCamera != true ? this with { ForceCamera = true } : this;
        int slot = Tasks.IndexOf(null);
        if (slot < 0)
        {
            return result;
        }
        byte channel = (byte)(selector & 7);
        return result with { Mask = (ushort)(Mask | 1 << channel), Tasks = Tasks.SetItem(slot, new(channel, bank, track.Value, 0, 0) { Pose = RetainedPoses[slot] }), Order = Order.Add(slot), BlendProgress = 0, SelectedSlot = slot };
    }

    public NativeBattleCameraState Tick(uint graphicsFlags, NativeBattleCameraPoseInputs? inputs = null)
    {
        Validate();
        NativeBattleCameraState result = this;
        inputs ??= new();
        foreach (int slot in Order)
        {
            if (Tasks[slot] is not { } task)
            {
                continue;
            }
            while (task.Elapsed >= task.Duration)
            {
                (int next, ushort? duration, NativeBattleCameraTrackPose? pose) = result.ReadPoseSegment(task, inputs, graphicsFlags);
                if (duration is null)
                {
                    result = result with { Mask = (ushort)(result.Mask & ~(1 << task.Channel)), Tasks = result.Tasks.SetItem(slot, null), Order = result.Order.Remove(slot) };
                    break;
                }
                task = task with { ProgramOffset = next, Elapsed = 0, Duration = duration.Value, Pose = pose };
                if ((pose!.Flags & 0x3E) == 0x1E)
                {
                    result = result with { CapturedPose = result.CurrentPose, PoseCaptureCount = checked(result.PoseCaptureCount + 1) };
                }
            }
            if (result.Tasks[slot] is not null)
            {
                //5035E0 freezes only elapsed increments under graphics bits101; the callback still polls segments.
                task = task with { Pose = result.SampleCameraTrack(task, inputs, graphicsFlags) };
                task = task with { Elapsed = (graphicsFlags & 0x101) == 0 ? unchecked((ushort)(task.Elapsed + 16)) : task.Elapsed };
                int fraction = (unchecked((short)task.Elapsed) << 12) / unchecked((short)task.Duration);
                short? focal = task.Pose.FocalStart is short focalStart && task.Pose.FocalEnd is short focalEnd ? unchecked((short)(focalStart + (unchecked(fraction * (focalEnd - focalStart)) >> 12))) : null;
                short roll = unchecked((short)(task.Pose.RollStart + (unchecked(fraction * (task.Pose.RollEnd - task.Pose.RollStart)) >> 12)));
                result = result with { FocalLength = focal, Roll = roll, RetainedPoses = result.RetainedPoses.SetItem(slot, task.Pose) };
                result = result with { Tasks = result.Tasks.SetItem(slot, task) };
            }
        }
        return result.PublishCameraPose();
    }
}
