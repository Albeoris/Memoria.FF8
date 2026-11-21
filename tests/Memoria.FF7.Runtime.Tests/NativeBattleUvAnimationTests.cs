using System.Text.Json;
using Memoria.FF7.Formats;

internal static class NativeBattleUvAnimationTests
{
    internal static void Run()
    {
        CheckActionOperand();
        CheckUvCallbacks();
        Console.WriteLine("Native actor UV animation operands, callback timing and cancellation checks passed.");
    }

    private static void CheckActionOperand()
    {
        byte[] script = [0xF8, 255, 0, 0xEE];
        BattleActionInstruction[] instructions = BattleActionInstructions.ReadUntilIdle(script, 1).ToArray();
        Check(instructions.Length == 3 && instructions[0].Opcode == 0xF8 && instructions[0].Operands.Span.SequenceEqual(new byte[] { 255 }) && instructions[1].Offset == 2 && instructions[1].Opcode == 0, "F8 did not consume exactly one unsigned selector byte.");
        BattleModelData model = new(0, [], [new(1, 1, new byte[5])], []);
        BattleActionSequence sequence = BattleActionSequence.Read(new(0, ReadOnlyMemory<byte>.Empty, [script]), model, 0);
        string before = JsonSerializer.Serialize(script);
        BattleActionPoseUpdate first = sequence.Advance(new());
        Check(first.State.ClipIndex == 0 && first.Events.Count == 1 && first.Events[0].Opcode == 0xF8 && first.Events[0].Operands.Span[0] == 255, "F8 yielded before the direct clip or emitted the wrong scheduling event.");
        BattleActionPoseUpdate second = sequence.Advance(first.State);
        Check(second.State.Completed && second.Events.All(item => item.Opcode != 0xF8), "A continuing clip scheduled the same UV animation twice.");
        Check(before == JsonSerializer.Serialize(script), "Action decoding changed its source bytes.");
        bool rejected = false;
        try
        {
            BattleActionInstructions.ReadUntilIdle(new byte[] { 0xF8 }, 1);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Check(rejected, "A truncated unsigned F8 operand was admitted.");
    }

    private static void CheckUvCallbacks()
    {
        byte[] allocation = [0xFD, 120, 0, 7, 0xFE];
        byte[] original = allocation.ToArray();
        BattleActorUvAnimationState state = new(255);
        BattleActorUvAnimationUpdate update = BattleActorUvAnimation.Advance(state, 255, allocation, 0);
        Check(update.PackedUv == 120 && update.State is { Cursor: 4, Delay: 7 }, "FD did not publish unsigned UV and retain its native delay.");
        state = update.State!;
        for (int delay = 6; delay >= 0; delay--)
        {
            update = BattleActorUvAnimation.Advance(state, 255, allocation, 0);
            Check(update.State is { Cursor: 4 } next && next.Delay == delay && update.PackedUv is null, "A positive old delay parsed the next command before the following callback.");
            state = update.State!;
        }
        update = BattleActorUvAnimation.Advance(state, 255, allocation, 0);
        Check(update.PackedUv == 120 && update.State is { Cursor: 4, Delay: 7 }, "FE did not restart and publish FD during the same callback.");
        BattleActorUvAnimationState restored = JsonSerializer.Deserialize<BattleActorUvAnimationState>(JsonSerializer.Serialize(update.State))!;
        Check(BattleActorUvAnimation.Advance(restored, 255, allocation, 0) == BattleActorUvAnimation.Advance(update.State!, 255, allocation, 0), "A cold callback changed native UV timing.");
        Check(BattleActorUvAnimation.Advance(state, 0, allocation, 0) is { State: null, PackedUv: null }, "Byte-generation wrap failed to cancel an older callback without replacing UV.");
        Check(BattleActorUvAnimation.Advance(new(3), 3, new byte[] { 0xFF }, 0) is { State: null, PackedUv: null }, "FF did not retire without clearing the retained UV offset.");
        Check(BattleActorUvAnimation.Advance(new(3), 3, new byte[] { 0x42, 0xFF }, 0) is { State.Cursor: 1, PackedUv: null }, "An unknown byte did not consume exactly one callback byte.");
        Check(allocation.SequenceEqual(original), "UV callbacks modified their source allocation.");

        byte[] wrapped = new byte[512];
        wrapped[382] = 0xFD;
        wrapped[383] = 0xFF;
        wrapped[128] = 0x80;
        wrapped[129] = 2;
        update = BattleActorUvAnimation.Advance(new(1, 126), 1, wrapped, 256);
        Check(update.PackedUv == 0x80FF && update.State is { Cursor: -126, Delay: 2 }, "FD operands did not individually wrap the native signed-byte cursor.");
        wrapped[383] = 0x42;
        Check(BattleActorUvAnimation.Advance(new(1, 127), 1, wrapped, 256).State?.Cursor == -128, "An unknown command did not wrap cursor127 to signed -128.");
        foreach (byte[] malformed in new byte[][] { [0xFE], [0xFD, 1, 2] })
        {
            bool rejected = false;
            try
            {
                BattleActorUvAnimation.Advance(new(1), 1, malformed, 0);
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            Check(rejected, "A nonyielding loop or truncated native UV frame was admitted.");
        }
        Check(BattleActorUvAnimation.Advance(new(1, -1, 1), 1, ReadOnlyMemory<byte>.Empty, 0).State?.Delay == 0, "An old positive delay dereferenced the allocation before yielding.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
