namespace Memoria.FF7.Formats;

public sealed record BattleActionPoseState(int NextInstruction = 0, int ClipIndex = -1, int ClipFrame = 0, byte WaitFrames = 0, bool Completed = false, byte PresetWaitFrames = 15)
{
    public byte? TerminalOpcode { get; init; }
    public int? ContinuedClipFrame { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? MissingClip { get; init; }
}
public sealed record BattleActionPoseUpdate(BattleActionPoseState State, IReadOnlyList<BattleActionInstruction> Events);

/// <summary>Advances the clip and script owners by one native animation callback.</summary>
public sealed class BattleActionSequence
{
    private readonly IReadOnlyList<BattleActionInstruction> instructions;
    private readonly IReadOnlyList<BattlePackedClip> clips;
    private readonly int alternateRowIndex;
    private readonly IReadOnlyDictionary<int, int> enemyBranches;

    private BattleActionSequence(IReadOnlyList<BattleActionInstruction> instructions, IReadOnlyList<BattlePackedClip> clips, int alternateRowIndex, IReadOnlyDictionary<int, int> enemyBranches)
    {
        this.instructions = instructions;
        this.clips = clips;
        this.alternateRowIndex = alternateRowIndex;
        this.enemyBranches = enemyBranches;
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
        List<BattleActionInstruction> branches = [.. instructions];
        Dictionary<int, int> enemyBranches = [];
        for (int index = 0; index < branches.Count; index++)
        {
            BattleActionInstruction instruction = branches[index];
            if (instruction.Opcode != 0xCE || enemyBranches.ContainsKey(instruction.Offset))
            {
                continue;
            }
            byte cursor = unchecked((byte)(instruction.Offset + 1));
            int scanned = 0;
            while (scanned < 256 && cursor < script.Length && script.Span[cursor] != 0xCD)
            {
                cursor = unchecked((byte)(cursor + 1));
                scanned++;
            }
            if (scanned == 256 || cursor >= script.Length)
            {
                enemyBranches.Add(instruction.Offset, -1);
                continue;
            }
            enemyBranches.Add(instruction.Offset, branches.Count);
            branches.AddRange(BattleActionInstructions.ReadUntilTransfer(script, model.BodyClips.Count, unchecked((byte)(cursor + 1))));
        }
        return new(branches, model.BodyClips, alternateRowIndex, enemyBranches);
    }

    public BattleActionPoseUpdate Advance(BattleActionPoseState state, bool effectReady = false, bool alternateRowBranch = false, int idleClipFrame = 0, bool primaryCallbacksReady = true, bool linkedActorReady = true, bool enemy = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Completed)
        {
            return new(state, []);
        }
        if (state.ContinuedClipFrame < 0 || state.MissingClip is < 0 or >= 0x8E || (uint)state.NextInstruction >= instructions.Count || state.ClipIndex < -1 || state.ClipIndex >= clips.Count || state.ClipFrame < 0 || state.ClipIndex >= 0 && state.ClipFrame >= clips[state.ClipIndex].FrameCount)
        {
            throw new InvalidDataException("FFVII battle pose continuation is outside its original script or clip.");
        }
        if (state.MissingClip is null && state.ClipIndex >= 0 && state.ClipFrame + 1 < clips[state.ClipIndex].FrameCount)
        {
            return new(state with { ClipFrame = state.ClipFrame + 1 }, []);
        }

        List<BattleActionInstruction> events = [];
        for (int index = state.NextInstruction, budget = instructions.Count; index < instructions.Count && budget > 0; index++, budget--)
        {
            BattleActionInstruction instruction = instructions[index];
            byte opcode = instruction.Opcode;
            if (opcode == 0xCE)
            {
                if (enemy)
                {
                    if (enemyBranches[instruction.Offset] < 0)
                    {
                        throw new InvalidDataException("FFVII actor branch has no native CD byte marker.");
                    }
                    index = enemyBranches[instruction.Offset] - 1;
                }
                continue;
            }
            if (opcode < 0x8E || opcode == 0xB6)
            {
                int clipIndex = opcode == 0xB6 ? instruction.Operands.Span[0] : opcode;
                if ((uint)clipIndex >= clips.Count || clips[clipIndex].PackedData.Length <= 4 || clips[clipIndex].FrameCount == 0)
                {
                    if (opcode == 0xB6)
                    {
                        throw new NotSupportedException($"FFVII battle action requests continued clip {clipIndex} without a packed body pose.");
                    }
                    // The model callback retains the last submitted pose and makes an absent direct clip ready immediately.
                    return new(state with { NextInstruction = index + 1, MissingClip = clipIndex }, events.AsReadOnly());
                }
                int startFrame = opcode == 0xB6 ? (int)((state.ContinuedClipFrame ?? idleClipFrame) % clips[clipIndex].FrameCount) : 0;
                if (startFrame < 0 || startFrame >= clips[clipIndex].FrameCount)
                {
                    throw new InvalidDataException("FFVII continued idle animation frame lies outside its body clip.");
                }
                if (opcode == 0xB6)
                {
                    events.Add(instruction);
                }
                return new(state with { NextInstruction = index + 1, ClipIndex = clipIndex, ClipFrame = startFrame, ContinuedClipFrame = null, MissingClip = null }, events.AsReadOnly());
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
            if (opcode == 0x9E && (!primaryCallbacksReady || !linkedActorReady))
            {
                return new(state with { NextInstruction = index, TerminalOpcode = 0x9E }, events.AsReadOnly());
            }
            if (opcode is 0xEB or 0xEC && !effectReady)
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
            if (opcode == 0xFE)
            {
                if (state.WaitFrames == 0 && index + 1 < instructions.Count && instructions[index + 1].Opcode == 0xC0)
                {
                    events.Add(instruction);
                    return new(state with { NextInstruction = 0, ClipIndex = instructions[0].Opcode, ClipFrame = 0, WaitFrames = 0, Completed = true, TerminalOpcode = opcode, MissingClip = null }, events.AsReadOnly());
                }
                continue;
            }
            events.Add(instruction);
            if (opcode == 0xC0)
            {
                return new(state with { NextInstruction = index + 1 }, events.AsReadOnly());
            }
            if (opcode == 0xF1)
            {
                return new(state with { NextInstruction = index, Completed = true, TerminalOpcode = opcode, MissingClip = null }, events.AsReadOnly());
            }
            if (opcode is 0x9E or 0xEE or 0xFF)
            {
                return new(state with { NextInstruction = index + 1, Completed = true, TerminalOpcode = opcode, MissingClip = null }, events.AsReadOnly());
            }
        }
        throw new InvalidDataException("FFVII battle action exhausted its instructions before transferring to idle.");
    }
}
