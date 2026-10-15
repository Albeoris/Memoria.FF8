using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeLadderAutomaticContract()
    {
        foreach (bool up in new[] { false, true })
        {
            EventEngine live = CreateNativeLadder(up, automatic: true);
            ReachNativeLadderClimb(live);
            ActorState actor = live.Actors[0];
            NativeLadderState climb = actor.NativeLadder!;
            Require(!live.IsUserControlEnabled && actor.NativePartyFlags is { AutoAnimation4: false } && actor.NativeMovementCompletion542 == 1 && actor.NativeTurnAccumulator474 == 0 && climb.Duration == 4, "Automatic mode4 fixture did not execute genuine UCOFF, ordinary entry phase and derived climb duration.");
            int entryAnimation = up ? 3 : 5;
            Require(actor.AnimationId == entryAnimation, "Mode4 entry selected an unexpected animation.");
            JsonElement trace = JsonSerializer.SerializeToElement(live.Scene.PartyTrace);
            EventEngine independent = CreateNativeLadder(up, automatic: true);
            independent.Restore(live.Capture());
            for (int counter = 1; counter <= climb.Duration; counter++)
            {
                //The reverse input group must have no effect on the raw UCOFF branch.
                NativeLadderButtons(live, up ? 0xC000 : 0x3000);
                NativeLadderButtons(independent, up ? 0xC000 : 0x3000);
                live.Step();
                independent.Step();
                NativeLadderState current = actor.NativeLadder!;
                ushort modelFrame = unchecked((ushort)(actor.NativeAnimationHeader!.Frame!.Value + unchecked((ushort)actor.AnimationSpeed)));
                Require(actor.NativeMovementCompletion542 == 1 && actor.NativeTurnAccumulator474 == counter && current.FixedX == NativeJumpMath.Interpolate(climb.Approach.X, climb.ClimbingEnd.X, climb.Duration, (short)counter) && current.FixedY == NativeJumpMath.Interpolate(climb.Approach.Y, climb.ClimbingEnd.Y, climb.Duration, (short)counter) && current.FixedZ == NativeJumpMath.Interpolate(climb.Approach.Z, climb.ClimbingEnd.Z, climb.Duration, (short)counter), "Automatic middle did not increment before interpolation or prematurely enter an exit phase.");
                Require(actor.AnimationId == entryAnimation && actor.AnimationFrame == (modelFrame >= 64 ? 0 : modelFrame) / 16f, "Automatic middle selected595 or failed the current model WORD82/add520/wrap contract.");
                Require(actor.WalkmeshTriangle == 7 && JsonElement.DeepEquals(trace, JsonSerializer.SerializeToElement(live.Scene.PartyTrace)) && JsonElement.DeepEquals(live.Capture(), independent.Capture()), "Automatic middle copied landing triangle, changed the party trace or diverged after independent restore.");
                independent.Restore(live.Capture());
            }
            live.Step();
            independent.Step();
            Require(actor.NativeMovementCompletion542 == 2 && actor.NativeTurnAccumulator474 == climb.Duration && actor.NativeLadder is { Active: true, AutomaticCompletion: true } && actor.WalkmeshTriangle == 7 && actor.NativeMovementGroundContact600 == 0 && !live.Capture().GetProperty("Variables").TryGetProperty("6000", out _) && JsonElement.DeepEquals(live.Capture(), independent.Capture()), "Automatic equality callback failed direct completion with retained climb counter/triangle/contact and held issuing PC.");
            JsonElement completed = live.Capture();
            independent.Restore(completed);
            JsonNode malformed = JsonNode.Parse(completed.GetRawText())!;
            malformed["Actors"]![0]!["NativeLadder"]!["AutomaticCompletion"] = false;
            try
            {
                independent.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Automatic terminal accepted a fabricated controlled-drain completion origin.");
            }
            catch (InvalidDataException)
            {
                Require(JsonElement.DeepEquals(completed, independent.Capture()), "Rejected automatic terminal restore changed the live physical owner.");
            }
            live.Step();
            independent.Step();
            Require(actor.NativeLadder is { Active: false, AutomaticCompletion: true } && live.Capture().GetProperty("Variables").GetProperty("6000").GetInt32() == 73 && actor.WalkmeshTriangle == 7 && JsonElement.DeepEquals(live.Capture(), independent.Capture()), "Automatic completion did not execute the immediate successor with return2 or preserved an unsupported exit/drain.");
        }
        Console.WriteLine("Native mode4 UCOFF: unchanged entry, increment-first automatic middle, current model animation, no trace/triangle/exit/drain, direct delayed completion/return2 and independent restores PASS.");
    }
}
