using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDiablos325ProgrammeStep(NativeBattleDiablos325Frame Frame, NativeBattleModulePoolTask Task);
public sealed record NativeBattleDiablos325Vibration(NativeBattleDiablos325Frame Frame, int Handle);
public sealed record NativeBattleDiablos325Inputs(NativeBattleActionPacketsState Packets, Func<NativeBattleDiablos325Frame, NativeBattleDiablos325Vibration> StartVibration)
{
    public Func<NativeBattleDiablos325Frame, NativeBattleEffectFileRequest, NativeBattleDiablos325Frame>? QueueFile { get; init; }
    public Func<NativeBattleDiablos325Frame, ushort, NativeBattleDiablos325Frame>? PresentTail { get; init; }
    public Func<NativeBattleDiablos325Frame, NativeBattleDiablos325Frame>? AdvanceParticles { get; init; }
    public Func<NativeBattleDiablos325Frame, NativeBattleModulePoolTask, NativeBattleDiablos325ProgrammeStep>? PresentShapes { get; init; }
    public Func<NativeBattleDiablos325Frame, NativeBattleModulePoolTask, NativeBattleDiablos325ProgrammeStep>? PresentBody { get; init; }
    public Func<NativeBattleDiablos325Frame, NativeBattleModulePoolTask, NativeBattleDiablos325ProgrammeStep>? PresentWings { get; init; }
    public Func<NativeBattleDiablos325Frame, ushort, NativeBattleDiablos325Frame>? PresentWell { get; init; }
    public Func<NativeBattleDiablos325Frame, NativeBattleDiablos325Frame>? DrawPuffs { get; init; }
    public Func<NativeBattleDiablos325Frame, NativeBattleModulePoolTask, bool, NativeBattleDiablos325ProgrammeStep>? PresentLateProgramme { get; init; }
    public Func<NativeBattleDiablos325Frame, NativeBattleModulePoolTask, NativeBattleDiablos325ProgrammeStep>? PresentSecondaryBody { get; init; }
    public Func<NativeBattleDiablos325Frame, ushort, NativeBattleDiablos325Frame>? PresentLateStatic { get; init; }
    public Func<NativeBattleDiablos325Frame, uint, NativeBattleDiablos325Frame>? UploadLateTexture { get; init; }
    public Func<NativeBattleDiablos325Frame, NativeBattleDiablos325Frame>? DrawTrails { get; init; }
    public Func<NativeBattleDiablos325Frame, NativeBattleModulePoolTask, NativeBattleDiablos325ProgrammeStep>? PresentRecipients { get; init; }
    public Func<NativeBattleDiablos325Frame, NativeBattleModulePoolTask, NativeBattleDiablos325Frame>? RestoreRecipients { get; init; }
    public Func<NativeBattleDiablos325Frame, ushort, NativeBattleDiablos325Frame>? PresentFlash { get; init; }
    public Func<NativeBattleDiablos325Frame, NativeBattleDiablos325Frame>? ApplyContact { get; init; }
    public Func<NativeBattleDiablos325Frame, int, NativeBattleDiablos325Frame>? StopVibration { get; init; }
    public Action<NativeBattleDiablos325Frame, uint, uint>? PlayStream { get; init; }
    public Action<uint, int, int>? PlaySound { get; init; }
    public Func<NativeBattleDiablos325Frame, NativeBattleModulePoolTask, NativeBattleDiablos325ProgrammeStep>? PresentProgramme { get; init; }
}

public static class NativeBattleDiablos325Construction
{
    public const uint TimelineDescriptor = 0x25051A0;
    public const uint TimelineCallback = 0x6545F0;

    public static NativeBattleDiablos325Frame Initialize6543BA(NativeBattleDiablos325Frame frame, NativeBattleDiablos325Inputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        NativeBattleDiablos325Entry.RequireBinding(frame.Effect);
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        NativeBattleDiablos325Resources resources = frame.Effect.Binding.Diablos325Resources ?? throw new NotSupportedException("Native Diablos requires its original opening resources.");
        resources.Validate();
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos requires its current effect arena.");
        using NativeBattleEffectArenaWriter writer = new(arena);
        for (int index = 0; index < 160; index++)
        {
            writer.Write(0x4C6B + index * 0x10, [0]);
        }
        state = state with { Mode51F0 = 0, ParticleCursor51F4 = checked(state.ModelBuffer5208 + 0x4C5C) };
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(TimelineDescriptor, state.ModelBuffer5208, 0x994, 1);
        (pool, int slot) = pool.Append(TimelineCallback);
        NativeBattleModulePoolTask task = pool.Slots[slot].ClearPayload(0x994 - 12);
        frame = frame with { Memory = frame.Memory with { Diablos325 = state, Arena = writer.Freeze() } };
        NativeBattleDiablos325Vibration vibration = inputs.StartVibration(frame);
        frame = vibration.Frame;
        task = task.WriteDword(0x10, unchecked((uint)vibration.Handle));
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[0xE0].ToImmutableArray()).Initialize7016B0(resources.OpeningProgramme, 0xE0);
        task.WriteRetainedBytes(0x264, workspace.Data.AsSpan());
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Diablos timeline requires its current action header.");
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[parameters.HeaderIndex7C];
        int count = header.ReadByte(0x10);
        for (int index = 0; index < count; index++)
        {
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Diablos timeline requires its current contact pointer.");
            byte actor = inputs.Packets.ReadContact(first + index).Primary.ActorIndex;
            int offset = checked(0x94 + index * 12 - 12);
            task.WriteDword(12 + offset, checked(0x1D972C0u + actor * 0x9Cu));
            task.WriteWord(12 + offset + 8, 0);
            task.WriteWord(12 + offset + 10, 4096);
        }
        pool = pool.WriteSlot(slot, task);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(12, task.ReadRawPayloadSpan()) }, Effect = frame.Effect with { Pools = frame.Effect.Pools.Where(item => item.DescriptorAddress != TimelineDescriptor).Append(pool).ToImmutableArray() } };
        frame = frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, 0x4C14) };
        byte[] angles = new byte[0x400];
        for (int index = 0; index < 256; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(angles.AsSpan(index * 4), unchecked((short)NativeBattleCameraMath.Sine(index * 16)));
            BinaryPrimitives.WriteInt16LittleEndian(angles.AsSpan(index * 4 + 2), unchecked((short)NativeBattleCameraMath.Cosine(index * 16)));
        }
        return frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x994, angles) } };
    }
}
