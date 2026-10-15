using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeJumpTurnContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        foreach (Jsm.Opcode turn in new[] { Jsm.Opcode.LTURNR, Jsm.Opcode.Unknown9 })
        {
            (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] lower = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 30), (turn, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_W, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] upper = [(Jsm.Opcode.LBL, 4), (Jsm.Opcode.PSHN_L, 400), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.PSHN_L, 900), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.JUMP3, 23), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] controller = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            byte[] content = SyntheticObjects([5, (16 << 7) | 1], [init, lower, [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 3), (Jsm.Opcode.RET, 8)], upper, [(Jsm.Opcode.LBL, 5), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 8)], controller]);
            (GameSession Session, EventEngine Engine) Create()
            {
                GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
                EventEngine engine = new(session)
                {
                    AnimationFrameCount = (_, _) => 4,
                    ResolveNativeTriangleHeight = (triangle, x, y) => triangle == 23 && x == 400 && y == 5 ? 4 : null
                };
                engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return (session, engine);
            }
            var live = Create();
            ActorState body = live.Engine.Actors[0];
            bool issued = false;
            bool landedWhileTurning = false;
            int jumpCommands = 0;
            live.Engine.CommandExecuted += command =>
            {
                if (command.Name == nameof(Jsm.Opcode.JUMP3) && command.Arguments.Count != 0)
                {
                    jumpCommands++;
                    Require(command.Arguments.SequenceEqual(new[] { 400, 5, 900, 3 }), "Concurrent jump lost its original DWORD operand evidence.");
                }
            };
            for (int callback = 0; callback < 40; callback++)
            {
                var independent = Create();
                independent.Engine.Restore(live.Engine.Capture());
                live.Engine.Step();
                independent.Engine.Step();
                Require(JsonElement.DeepEquals(live.Engine.Capture(), independent.Engine.Capture()), "Concurrent native turn/JUMP3 diverged after independent restoration.");
                if (body.RotationTarget is not null)
                {
                    int elapsed = body.RotationTotalFrames - body.RotationFrames;
                    byte expected = unchecked((byte)(turn == Jsm.Opcode.Unknown9 ? NativeCosineInterpolation.Interpolate(10, 100, 30, elapsed) : NativeJumpMath.Interpolate(10, 100, 30, (short)elapsed)));
                    Require(body.Direction == expected && body.RotationStart == 10 && body.RotationTarget == 100 && body.RotationTotalFrames == 30, "JUMP3 cancelled, restarted or overwrote its independent native turn.");
                }
                if (!issued && body.NativeJump is { Active: true } jump)
                {
                    issued = true;
                    Require(body.RotationTarget == 100 && jump.Duration == 3 && body.NativeMovementCompletion542 == 1 && body.NativeTurnAccumulator474 == 0, "Requested jump did not retain the pending lower turn at its setup sample.");
                }
                if (body.NativeMovementCompletion542 == 2 && body.NativeJump is { Active: true } landed)
                {
                    landedWhileTurning = body.RotationTarget == 100;
                    Require(landed.FixedX == 1638399 && landed.FixedY == 20480 && landed.FixedZ == 16383 && body.WalkmeshTriangle == 23, "Concurrent jump changed its native endpoint residue or delayed triangle update.");
                }
            }
            Require(issued && landedWhileTurning && jumpCommands == 1 && body.RotationTarget is null && live.Session.State.Variables.GetValueOrDefault(5000) == 77, "Concurrent jump failed natural landing, one-time operand consumption or lower-turn completion after RET.");
        }
        Console.WriteLine("Native linear/cosine turn and requested JUMP3: independent phases, original operands, landing/RET, natural turn completion and every-callback restoration passed.");
    }
}
