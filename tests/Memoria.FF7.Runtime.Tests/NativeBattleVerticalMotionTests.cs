using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Formats;

internal static class NativeBattleVerticalMotionTests
{
    internal static void Run()
    {
        CheckOperandAndScheduling();
        CheckCallbacks();
        CheckSharedPool();
        Console.WriteLine("Native vertical actor motion preserves signed operands, current-height callbacks and the shared pool.");
    }

    private static void CheckOperandAndScheduling()
    {
        byte[] script = [0xD4, 0xD8, 0xDC, 60, 0, 0xEE];
        BattleActionInstruction[] decoded = BattleActionInstructions.ReadUntilIdle(script, 1).ToArray();
        Check(decoded.Length == 3 && decoded[1].Offset == 4 && decoded[0].Operands.Length == 3 && BinaryPrimitives.ReadInt16LittleEndian(decoded[0].Operands.Span) == -9000 && decoded[0].Operands.Span[2] == 60, "D4 did not decode one signed WORD distance and unsigned BYTE duration.");
        BattleModelData model = new(0, [], [new(1, 1, new byte[5])], []);
        BattleActionSequence sequence = BattleActionSequence.Read(new(0, ReadOnlyMemory<byte>.Empty, [script]), model, 0);
        BattleActionPoseUpdate first = sequence.Advance(new());
        Check(first.State.ClipIndex == 0 && first.Events.Count == 1 && first.Events[0].Opcode == 0xD4, "D4 yielded before its following direct clip.");
        Check(sequence.Advance(first.State).Events.All(item => item.Opcode != 0xD4), "A continuing pose scheduled D4 twice.");
        Ff7BattleActorMotionState empty = new();
        string before = JsonSerializer.Serialize(empty);
        Check(Ff7BattleActorMotionEffects.ScheduleVertical(empty, 0, -9000, 60).VerticalSlots[0] is { Step: -150, Remaining: 60 }, "The actual signed distance did not divide to the native step.");
        Check(Ff7BattleActorMotionEffects.ScheduleVertical(empty, 0, -10, 3).VerticalSlots[0]?.Step == -3 && Ff7BattleActorMotionEffects.ScheduleVertical(empty, 0, 10, 3).VerticalSlots[0]?.Step == 3, "Signed motion division did not truncate toward zero.");
        bool rejected = false;
        try
        {
            Ff7BattleActorMotionEffects.ScheduleVertical(empty, 0, 1, 0);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Check(rejected && before == JsonSerializer.Serialize(empty), "Zero-duration motion was admitted or scheduling mutated its source.");
    }

    private static Ff7BattleActorMotionState Advance(Ff7BattleActorMotionState state, Func<int, short> height) => Ff7BattleActorMotionEffects.Advance(state, _ => new(), new(0, new short[15]), height);

    private static void CheckCallbacks()
    {
        Ff7BattleActorMotionState state = Ff7BattleActorMotionEffects.ScheduleVertical(new(), 0, -9000, 60);
        short y = 1000;
        for (int tick = 0; tick < 60; tick++)
        {
            string before = JsonSerializer.Serialize(state);
            Ff7BattleActorMotionState restored = JsonSerializer.Deserialize<Ff7BattleActorMotionState>(before)!;
            Ff7BattleActorMotionState next = Advance(state, _ => y);
            Check(JsonSerializer.Serialize(next) == JsonSerializer.Serialize(Advance(restored, _ => y)), "A cold vertical callback changed its physical output.");
            Check(before == JsonSerializer.Serialize(state), "Advancing a callback mutated a retained source snapshot.");
            Check(next.ActorWrites.Count == 1 && next.ActorWrites[0].Target == 0 && next.ActorWrites[0].WorldY == unchecked((short)(y - 150)), "A vertical callback did not add its signed step to current actor height.");
            y = next.ActorWrites[0].WorldY!.Value;
            state = next;
        }
        Check(y == -8000 && state.VerticalSlots[0] is { Remaining: 0 }, "The final motion step retired before its extra native callback.");
        state = Advance(state, _ => throw new InvalidOperationException("Retirement must not read actor height."));
        Check(state.VerticalSlots[0] is null && state.ActorWrites.Count == 0, "The extra callback did not retire without a height write.");
        Ff7BattleActorMotionState wrapped = Advance(Ff7BattleActorMotionEffects.ScheduleVertical(new(), 2, 10, 1), _ => 32765);
        Check(wrapped.ActorWrites.Single().WorldY == -32761, "Actor height did not wrap as a native signed WORD.");
        Ff7BattleActorMotionState current = Ff7BattleActorMotionEffects.ScheduleVertical(new(), 2, 10, 2);
        current = Advance(current, _ => 100);
        Check(Advance(current, _ => 500).ActorWrites.Single().WorldY == 505, "A callback used a captured origin instead of current actor height.");
    }

    private static void CheckSharedPool()
    {
        Ff7BattleActorMotionState state = Ff7BattleActorMotionEffects.ScheduleDeath(new(), 1, false);
        state = Ff7BattleActorMotionEffects.ScheduleVertical(state, 0, 10, 2);
        Check(state.Slots[0] is not null && state.VerticalSlots[0] is null && state.VerticalSlots[1] is { Target: 0 }, "Death and vertical motion did not share first-free native callback slots.");
        state = Ff7BattleActorMotionEffects.ScheduleVertical(state, 0, 20, 2);
        Ff7BattleActorMotionState advanced = Advance(state, _ => 100);
        Check(advanced.ActorWrites.Where(write => write.WorldY is not null).Select(write => write.WorldY!.Value).SequenceEqual(new short[] { 105, 115 }), "Same-actor callbacks did not accumulate in shared slot order.");
        for (int index = 3; index < Ff7BattleActorMotionEffects.Capacity; index++)
        {
            state = Ff7BattleActorMotionEffects.ScheduleVertical(state, index % 10, 1, 1);
        }
        string full = JsonSerializer.Serialize(state);
        bool rejected = false;
        try
        {
            Ff7BattleActorMotionEffects.ScheduleVertical(state, 0, 1, 1);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Check(rejected && full == JsonSerializer.Serialize(state), "A full mixed callback pool was overwritten or mutated.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
