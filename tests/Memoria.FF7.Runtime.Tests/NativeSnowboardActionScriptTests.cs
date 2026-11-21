using System.Text.Json;
using Memoria.FF7.Runtime;

internal static class NativeSnowboardActionScriptTests
{
    private static readonly ushort[] Masks = [16, 64, 128, 32, 4, 1, 8, 2];

    internal static void Run()
    {
        CheckFrameTiming();
        CheckLoopsAndEvents();
        CheckPredicatesAndReset();
        CheckUnsupportedAndMalformed();
        Console.WriteLine("Native snowboard action dispatch, timing, loops, events and input predicates passed.");
    }

    private static byte[] Script(params byte[] commands)
    {
        byte[] scripts = new byte[64 * 256];
        commands.CopyTo(scripts, 0);
        return scripts;
    }

    private static Ff7SnowboardActionScriptState Start => new() { ActionWord = 0x8000 };
    private static Ff7SnowboardActionScriptStep Tick(Ff7SnowboardActionScriptState state, byte[] scripts, ushort held = 0, int speed = 300) => Ff7SnowboardActionScript.Advance(state, scripts, Masks, new(held, speed));

    private static void CheckFrameTiming()
    {
        byte[] scripts = Script(1, 3, 2, 1, 8, 2, 0, 0, 12, 7, 0, 0, 6, 0, 0, 0);
        Ff7SnowboardActionScriptState source = Start;
        string before = JsonSerializer.Serialize(source);
        byte[] raw = scripts.ToArray();
        var first = Tick(source, scripts);
        Check(first.Effects is [Ff7SnowboardActionFrame { Model: 3, Duration: 2, Easing: 1 }] && first.State is { ProgramCounter: 1, Remaining: 1, FrameModel: 3 }, "A native frame command did not yield before its first remaining-time decrement.");
        var second = Tick(first.State, scripts);
        Check(second.Effects.Length == 0 && second.State is { ProgramCounter: 1, Remaining: 0 }, "The next command ran on the callback which only expired the current frame.");
        var third = Tick(second.State, scripts);
        Check(third.Effects is [Ff7SnowboardActionJump { VelocityY: -1024, Gravity: 4096, AirFrames: 1 }] && third.State is { CanPush: 7, ProgramCounter: 3 }, "Native jump/CanPush effects or wait-command timing changed.");
        Check(before == JsonSerializer.Serialize(source) && raw.SequenceEqual(scripts), "Action execution mutated its source snapshot or native scripts.");
        var zero = Tick(Start, Script(1, 1, 0, 0, 8, 2, 0, 0));
        Check(zero.Effects is [Ff7SnowboardActionFrame] && zero.State.ProgramCounter == 1, "A zero-duration frame bypassed its required callback yield.");
    }

    private static void CheckLoopsAndEvents()
    {
        byte[] loop = Script(2, 1, 0, 0, 1, 2, 1, 0, 3, 0, 0, 0, 6, 0, 0, 0);
        var first = Tick(Start, loop);
        var second = Tick(first.State, loop);
        var third = Tick(second.State, loop);
        Check(first.State.LoopDepth == 1 && first.State.Loops[1].Count == 1 && second.Effects is [Ff7SnowboardActionFrame] && second.State.Loops[1].Count == 0 && third.State is { ProgramCounter: 3, LoopDepth: 0 }, "Native byte loop count/start/depth semantics changed.");
        byte[] script = Script(5, 1, 3, 0, 1, 2, 10, 0, 0, 0, 0, 0, 8, 3, 0, 0, 11, 1, 0, 0, 6, 0, 0, 0);
        var registered = Tick(Start, script);
        Check(registered.State.Triggers[1] is { Enabled: true, Pending: false, Target: 3 }, "A native event registration lost its target or initial state.");
        var signaled = Ff7SnowboardActionScript.SignalEvent(registered.State, 1);
        var fired = Tick(signaled, script);
        Check(fired.Effects is [Ff7SnowboardActionJump { VelocityY: -1536 }] && fired.State is { Remaining: 0, ProgramCounter: 5 } && !fired.State.Triggers[1].Enabled && !fired.State.Triggers[1].Pending, "A triggered native event did not cancel the active frame and consume its registration before dispatch.");
        Check(registered.State.Triggers[1].Enabled && !registered.State.Triggers[1].Pending, "Event signaling or dispatch mutated an older state snapshot.");
        var both = registered.State with { Triggers = [new(true, true, 5), new(true, true, 3), new(), new()] };
        var priority = Tick(both, script);
        Check(priority.Effects.Length == 0 && priority.State.ProgramCounter == 5 && priority.State.Triggers[1].Pending, "More than the first enabled native event slot was consumed in one callback.");
    }

    private static void CheckPredicatesAndReset()
    {
        byte[] key = Script(9, 0, 0, 0, 12, 9, 0, 0, 6, 0, 0, 0);
        Check(Tick(Start, key).State.ProgramCounter == 0 && Tick(Start, key, 16).State.CanPush == 9, "Held input masking changed native wait/continue semantics.");
        key[2] = 1;
        Check(Tick(Start, key, 16).State.ProgramCounter == 0 && Tick(Start, key).State.CanPush == 9, "The native ushort complement input predicate was not applied.");
        byte[] speed = Script(10, 2, 0, 0, 1, 4, 0, 0, 1, 5, 0, 0);
        Check(Tick(Start, speed, speed: 255).State.FrameModel == 5 && Tick(Start, speed, speed: 256).State.FrameModel == 4, "The exact signed speed<256 branch boundary changed.");
        Check(Tick(new(), speed, speed: 0).State.FrameModel == 4, "Native high-bit-clear reset did not clear the speed-condition latch before dispatch.");
        byte[] branches = Script(10, 2, 3, 4, 1, 4, 0, 0, 1, 5, 0, 0, 1, 6, 0, 0, 1, 7, 0, 0);
        Check(Ff7SnowboardActionScript.Advance(Start, branches, Masks, new(0, 300, 2)).State.FrameModel == 6 && Ff7SnowboardActionScript.Advance(Start, branches, Masks, new(0, 300, 3)).State.FrameModel == 7, "Native reaction branch selectors did not index their distinct command operands.");
        Check(Ff7SnowboardActionScript.Advance(new(), branches, Masks, new(0, 300, 3)).State.FrameModel == 4, "An action reset retained a caller's earlier branch selector.");
        var switched = Tick(Start, Script(7, 1, 0, 0));
        Check(switched.State.ActionWord == 1 && switched.State.ProgramCounter == 0, "The native action command did not clear the continuation bit and yield without advancing its PC.");
        byte[] other = Script();
        other[256] = 1;
        other[257] = 7;
        Check(Tick(switched.State, other).State.FrameModel == 7, "The action selector did not use the low-byte script block after reset.");
        var reset = Ff7SnowboardActionScript.Reset(Start with { ProgramCounter = 5, Remaining = 8, LoopDepth = 2, CanPush = 7, Triggers = [new(true, true, 9), new(), new(), new()] });
        Check(reset is { ProgramCounter: 0, Remaining: 0, LoopDepth: 0, CanPush: 0 } && reset.Triggers[0] is { Enabled: false, Pending: false, Target: 9 }, "Action reset changed native retained event targets or failed to clear execution latches.");
    }

    private static void CheckUnsupportedAndMalformed()
    {
        var unknown = Tick(Start, Script(14, 1, 2, 3));
        Check(unknown.UnsupportedCommand == 14 && unknown.State.ProgramCounter == 0 && unknown.Effects.Length == 0, "An unsupported native command silently executed or became a return.");
        foreach (byte[] script in new[] { Script(4, 0, 0, 0), Script(5, 4, 0, 0), Script(9, 8, 0, 0), Script(1, 0, 1, 4) })
        {
            bool rejected = false;
            try
            {
                Tick(Start, script);
            }
            catch (Exception error) when (error is InvalidDataException or NotSupportedException)
            {
                rejected = true;
            }
            Check(rejected, "A malformed selector or exact non-yielding cycle was admitted.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
