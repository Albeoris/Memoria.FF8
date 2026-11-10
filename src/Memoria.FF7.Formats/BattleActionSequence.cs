namespace Memoria.FF7.Formats;

public sealed record BattleActionPoseState(int NextInstruction = 0, int ClipIndex = -1, int ClipFrame = 0, byte WaitFrames = 0, bool Completed = false, byte PresetWaitFrames = 15);
public sealed record BattleActionPoseUpdate(BattleActionPoseState State, IReadOnlyList<BattleActionInstruction> Events);

/// <summary>Advances the clip and script owners by one native animation callback.</summary>
public sealed class BattleActionSequence
{
    private readonly IReadOnlyList<BattleActionInstruction> instructions;
    private readonly IReadOnlyList<BattlePackedClip> clips;
    private readonly int alternateRowIndex;

    private BattleActionSequence(IReadOnlyList<BattleActionInstruction> instructions, IReadOnlyList<BattlePackedClip> clips, int alternateRowIndex)
    {
        this.instructions = instructions;
        this.clips = clips;
        this.alternateRowIndex = alternateRowIndex;
    }

    public static BattleActionSequence Read(BattleActionBank bank, BattleModelData model, int actionId)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentNullException.ThrowIfNull(model);
        if ((uint)actionId >= bank.Scripts.Count || bank.Scripts[actionId].IsEmpty)
        {
            throw new InvalidDataException($"FFVII battle action {actionId} is absent from its native bank.");
        }
        ReadOnlyMemory<byte> script = bank.Scripts[actionId];
        IReadOnlyList<BattleActionInstruction> instructions = BattleActionInstructions.ReadUntilIdle(script, model.BodyClips.Count);
        int alternateRowIndex = -1;
        if (instructions.Any(instruction => instruction.Opcode == 0xB3))
        {
            int marker = instructions[^1].Offset + 1;
            if (marker >= script.Length || script.Span[marker] != 0xB2)
            {
                throw new InvalidDataException("FFVII row-change action has no native alternate branch label.");
            }
            alternateRowIndex = instructions.Count;
            instructions = [.. instructions, .. BattleActionInstructions.ReadUntilTransfer(script, model.BodyClips.Count, marker + 1)];
        }
        return new(instructions, model.BodyClips, alternateRowIndex);
    }

    public BattleActionPoseUpdate Advance(BattleActionPoseState state, bool effectReady = false, bool alternateRowBranch = false, int idleClipFrame = 0)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Completed)
        {
            return new(state, []);
        }
        if ((uint)state.NextInstruction >= instructions.Count || state.ClipIndex < -1 || state.ClipIndex >= clips.Count || state.ClipFrame < 0 || state.ClipIndex >= 0 && state.ClipFrame >= clips[state.ClipIndex].FrameCount)
        {
            throw new InvalidDataException("FFVII battle pose continuation is outside its original script or clip.");
        }
        if (state.ClipIndex >= 0 && state.ClipFrame + 1 < clips[state.ClipIndex].FrameCount)
        {
            return new(state with { ClipFrame = state.ClipFrame + 1 }, []);
        }

        List<BattleActionInstruction> events = [];
        for (int index = state.NextInstruction, budget = instructions.Count; index < instructions.Count && budget > 0; index++, budget--)
        {
            BattleActionInstruction instruction = instructions[index];
            byte opcode = instruction.Opcode;
            if (opcode < 0x8E || opcode == 0xB6)
            {
                int clipIndex = opcode == 0xB6 ? instruction.Operands.Span[0] : opcode;
                if ((uint)clipIndex >= clips.Count || clips[clipIndex].PackedData.Length <= 4 || clips[clipIndex].FrameCount == 0)
                {
                    throw new NotSupportedException($"FFVII battle action requests clip {clipIndex} without a packed body pose.");
                }
                int startFrame = opcode == 0xB6 ? idleClipFrame : 0;
                if (startFrame < 0 || startFrame >= clips[clipIndex].FrameCount)
                {
                    throw new InvalidDataException("FFVII continued idle animation frame lies outside its body clip.");
                }
                if (opcode == 0xB6)
                {
                    events.Add(instruction);
                }
                return new(state with { NextInstruction = index + 1, ClipIndex = clipIndex, ClipFrame = startFrame }, events.AsReadOnly());
            }
            if (opcode == 0xC6)
            {
                state = state with { PresetWaitFrames = instruction.Operands.Span[0] };
                continue;
            }
            if (opcode is 0xC5 or 0xF4)
            {
                state = state with { WaitFrames = opcode == 0xC5 ? state.PresetWaitFrames : instruction.Operands.Span[0] };
                continue;
            }
            if (opcode == 0xF3)
            {
                if (state.WaitFrames != 0)
                {
                    return new(state with { NextInstruction = index, WaitFrames = (byte)(state.WaitFrames - 1) }, events.AsReadOnly());
                }
                continue;
            }
            if (opcode is 0x9E or 0xEB or 0xEC && !effectReady)
            {
                return new(state with { NextInstruction = index }, events.AsReadOnly());
            }
            if (opcode == 0xCA && !effectReady)
            {
                int label = 0;
                while (label < index && instructions[label].Opcode != 0xC9)
                {
                    label++;
                }
                if (label == index)
                {
                    throw new InvalidDataException("FFVII battle effect retry has no earlier native label.");
                }
                index = label;
                continue;
            }
            if (opcode == 0xB3 && alternateRowBranch)
            {
                index = alternateRowIndex - 1;
                continue;
            }
            events.Add(instruction);
            if (opcode is 0x9E or 0xEE or 0xFF)
            {
                return new(state with { NextInstruction = index + 1, Completed = true }, events.AsReadOnly());
            }
        }
        throw new InvalidDataException("FFVII battle action exhausted its instructions before transferring to idle.");
    }
}
