using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeFootstepAudioContract(string gameDirectory)
    {
        EventEngine Create(params (Jsm.Opcode Opcode, int Parameter)[] commands)
        {
            GameSession session = GameSession.CreateColdProcess();
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 32 };
            List<(Jsm.Opcode Opcode, int Parameter)> initialization = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0)];
            initialization.AddRange(commands);
            initialization.Add((Jsm.Opcode.RET, 0));
            engine.LoadField(600, SyntheticObjects([1], [initialization.ToArray(), [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)]]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return engine;
        }
        EventEngine live = Create((Jsm.Opcode.FOOTSTEPON, 0));
        ActorState actor = live.Actors[0];
        List<NativeFootstepSound> sounds = [];
        List<bool> cameraPhases = [];
        live.ResolveFootstepSound = (source, mode, beforeCameraSelection) =>
        {
            cameraPhases.Add(beforeCameraSelection);
            return new NativeFootstepSound(source.ObjectId, mode, (uint)(100 + mode), 128, 119, 0x400000);
        };
        live.AudioCommandExecuted += command =>
        {
            if (command is NativeFootstepSound sound)
            {
                sounds.Add(sound);
            }
        };
        MethodInfo contacts = typeof(EventEngine).GetMethod("StepNativeFootstepCrossings", BindingFlags.Instance | BindingFlags.NonPublic)!;
        void Check(ushort previous, ushort next, ushort total, ushort increment, bool narrow, params int[] modes)
        {
            sounds.Clear();
            cameraPhases.Clear();
            contacts.Invoke(live, [actor, previous, next, total, increment, narrow]);
            Require(sounds.Select(sound => sound.Mode).SequenceEqual(modes), $"Native contact crossing lost callback order or WORD domain for {previous}->{next}, total {total}, increment {increment}, narrow {narrow}.");
            Require(cameraPhases.All(beforeCameraSelection => beforeCameraSelection == !narrow), "Native script/body contact used the wrong side of4767B0 camera selection.");
        }
        foreach (bool narrow in new[] { true, false })
        {
            Check(112, 128, 512, 16, narrow, 1);
            Check(128, 144, 512, 16, narrow);
            Check(128, 112, 512, 16, narrow, 1);
            Check(368, 384, 512, 16, narrow, 0);
            Check(384, 368, 512, 16, narrow, 0);
            Check(0, 512, 512, 16, narrow, 1, 0);
            Check(496, 512, 512, 16, narrow);
            Check(0, 65520, 512, 16, narrow);
        }
        Check(33152, 33168, 512, 65520, false);
        Check(32760, 32776, 512, 16, true);
        Check(32760, 32776, 512, 16, false, 1, 0);
        Check(32888, 32904, 512, 16, true, 1);
        Check(32888, 32904, 512, 16, false);
        JsonElement beforeTransient = live.Capture();
        live.ResolveFootstepSound = (source, mode, _) => new NativeFootstepSound(source.ObjectId, mode, 102, 128, 119, 0x400000);
        Check(112, 128, 512, 16, true, 1);
        JsonElement afterTransient = live.Capture();
        JsonElement recorded = afterTransient.GetProperty("GlobalSoundProcess");
        Require(sounds[0] is { ObjectId: 0, EffectId: 102, Pan: 128, Volume: 119, Flags: 0x400000 } && recorded.GetProperty("OriginalEffectIds")[23].GetUInt32() == 102 && recorded.GetProperty("Rotation").GetByte() == 1 && JsonElement.DeepEquals(beforeTransient.GetProperty("Fibers"), afterTransient.GetProperty("Fibers")) && JsonElement.DeepEquals(beforeTransient.GetProperty("Scene"), afterTransient.GetProperty("Scene")), "Native contact lost its persistent original effect word, resolver output or changed VM/presentation receipts.");
        live.ResolveFootstepSound = null;
        Check(0, 512, 512, 16, true);

        EventEngine disabled = Create();
        Require(!disabled.Actors[0].FootstepsEnabled && disabled.Actors[0].NativeFootstepSpecial == false, "Native allocation enabled footstep output or invented a special-character flag.");
        disabled.ResolveFootstepSound = (_, _, _) => throw new InvalidOperationException("Disabled footstep reached its resolver.");
        contacts.Invoke(disabled, [disabled.Actors[0], (ushort)0, (ushort)512, (ushort)512, (ushort)16, true]);
        EventEngine ordinary = Create((Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0));
        Require(ordinary.Actors[0].FootstepsEnabled && ordinary.Actors[0].NativeFootstepSpecial == false, "SETPC did not enable ordinary native footsteps.");
        ordinary.ResolveFootstepSound = (source, mode, _) => new NativeFootstepSound(source.ObjectId, mode, 77, 128, 119, 0x800000);
        contacts.Invoke(ordinary, [ordinary.Actors[0], (ushort)112, (ushort)128, (ushort)512, (ushort)16, true]);
        JsonElement headless = ordinary.Capture().GetProperty("GlobalSoundProcess");
        Require(headless.GetProperty("Rotation").GetByte() == 1 && headless.GetProperty("OriginalEffectIds")[24].GetUInt32() == 77, "Headless contact skipped the native original effect word without a presentation listener.");
        EventEngine sticky = Create((Jsm.Opcode.PSHN_L, 259), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 256), (Jsm.Opcode.SETPC, 0));
        Require(sticky.Actors[0].FootstepsEnabled && sticky.Actors[0].NativeFootstepSpecial == true, "SETPC cleared its sticky special-character fallback flag.");
        EventEngine off = Create((Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.FOOTSTEPOFF, 0));
        Require(!off.Actors[0].FootstepsEnabled && off.Actors[0].NativeFootstepSpecial == true, "FOOTSTEPOFF changed the independent sticky special-character state.");

        // Exercise the reached animation helper itself: its contact callback must
        // inspect the advanced WORD even when the stored renderer frame wraps.
        MethodInfo ladder = typeof(EventEngine).GetMethod("StepLadderAnimation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        live.ResolveFootstepSound = (source, mode, _) => new NativeFootstepSound(source.ObjectId, mode, 3, 128, 119, 0x400000);
        sounds.Clear();
        for (int frame = 0; frame < 32; frame++)
        {
            ladder.Invoke(live, [actor, 0, 1, true]);
        }
        Require(sounds.Select(sound => sound.Mode).SequenceEqual([1, 0]) && actor.AnimationFrame == 0, "Native player animation lost its two real contacts or emitted extra contacts at frame wrap.");
        sounds.Clear();
        for (int frame = 0; frame < 32; frame++)
        {
            ladder.Invoke(live, [actor, 0, 1, false]);
        }
        Require(sounds.Count == 0, "Native follower/history animation enabled a caller-suppressed contact callback.");

        byte[] movementContent = SyntheticObjects([1], [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.FOOTSTEPON, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 4), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 2000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.RMOVE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        EventEngine Moving()
        {
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 32, CanUseNativeScriptMove = _ => true };
            engine.ResolveNativeScriptDirection = query => new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, query.TurnAccumulator);
            engine.ResolveNativeScriptMovement = query =>
            {
                NativeScriptGroundProbe[] probes = [new(query.FixedX, query.FixedY, 0), new(query.FixedX, query.FixedY, 0), new(query.FixedX, query.FixedY, 0)];
                return new(NativeScriptMovementStatus.Blocked, query.FixedX, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(query.FixedX, query.FixedY, query.Direction, probes)]);
            };
            engine.ResolveFootstepSound = (source, mode, _) => new NativeFootstepSound(source.ObjectId, mode, (uint)(3 - mode), 128, 119, 0x400000);
            engine.LoadField(600, movementContent, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            engine.Step();
            return engine;
        }
        EventEngine moving = Moving();
        List<int> contactsBeforeRestore = [];
        moving.AudioCommandExecuted += command =>
        {
            if (command is NativeFootstepSound sound)
            {
                contactsBeforeRestore.Add(sound.Mode);
            }
        };
        for (int frame = 0; frame < 16; frame++)
        {
            moving.Step();
        }
        Require(contactsBeforeRestore.SequenceEqual([1]) && moving.Actors[0].NativeScriptMovement is { Active: true }, "Blocked native RMOVE lost its animation-owned first contact.");
        EventEngine resumed = Moving();
        resumed.Restore(moving.Capture());
        List<int> resumedContacts = [];
        resumed.AudioCommandExecuted += command =>
        {
            if (command is NativeFootstepSound sound)
            {
                resumedContacts.Add(sound.Mode);
            }
        };
        contactsBeforeRestore.Clear();
        for (int frame = 0; frame < 16; frame++)
        {
            moving.Step();
            resumed.Step();
        }
        Require(contactsBeforeRestore.SequenceEqual([0]) && resumedContacts.SequenceEqual([0]) && JsonElement.DeepEquals(moving.Capture(), resumed.Capture()), "Restored mode1 animation repeated an old footstep, missed the next contact or changed authoritative progression.");
        JsonElement valid = resumed.Capture();
        JsonNode malformed = JsonNode.Parse(valid.GetRawText())!;
        malformed["Actors"]![0]!["NativeFootstepSpecial"] = null;
        try
        {
            resumed.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Native allocated actor restored without its special-footstep flag authority.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(valid, resumed.Capture()), "Rejected special-footstep flag authority changed the receiver.");
    }
}
