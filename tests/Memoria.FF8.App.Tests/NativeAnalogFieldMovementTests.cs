using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeAnalogFieldMovementTests
{
    [Fact]
    public void Native_field_movement_retains_full_circle_heading_and_mapped_walk_run()
    {
        string gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(gameDirectory), "Installed field corpus is required.");
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        (GameSession Session, EventEngine Engine, List<int> Speeds, List<int> Directions) Create(bool forcedWalk = false)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(initial.DeepCopy());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
            session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
            session.SetNativeEventOwners(session.WorldControls!.WithSalaryEnabled(true), seeD: session.SeeD!.WithSalaryUiEnabled(true));
            List<int> speeds = [];
            List<int> directions = [];
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) => 4,
                ResolveWalkmesh = (x, y) => new(new(x, y, 0), 0),
                ResolvePartyTraceTriangle = (_, _, _) => 0,
                CanUseNativePlayerMovement = _ => true,
                ResolveGlobalSoundResource = sample =>
                {
                    GlobalAudioSampleDescriptor descriptor = GlobalAudioFormat.Read(gameDirectory, sample);
                    return new GlobalSoundResource(sample, GlobalSoundBackendStatus.Muted, new GlobalSoundLoopPolicy(descriptor.PlaybackFlags, descriptor.LoopStartPcmByte, descriptor.LoopEndPcmByte, descriptor.WaveFormat.SampleRate, 16, descriptor.WaveFormat.Channels, null));
                }
            };
            engine.ResolveNativePlayerMovement = query =>
            {
                speeds.Add(query.Speed);
                directions.Add(query.Direction);
                int x = query.FixedX + 4096;
                NativePlayerGroundProbe[] fans = [new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)];
                return new NativePlayerMovementResult(x, query.FixedY, query.FixedZ, 0, query.Direction, [new(x, query.FixedY, query.Direction)], [fans]);
            };
            byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.FADENONE, 0), (forcedWalk ? Jsm.Opcode.RUNDISABLE : Jsm.Opcode.RUNENABLE, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]]);
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            engine.Step();
            void Set(string property, object value) => typeof(SceneEventState).GetProperty(property)!.SetValue(engine.Scene, value);
            // Explicit component trace, not a patched research checkpoint.
            PartyTraceSample sample = new(0, 0, 0, 0, 0);
            Set(nameof(SceneEventState.PartyTrace), new PartyTraceState(0, 0, 0, Enumerable.Repeat<PartyTraceSample?>(sample, 64).ToArray(), Enumerable.Repeat<PartyTraceSample?>(sample, 64).ToArray()));
            Set(nameof(SceneEventState.NativePartyTraceVersion), 1);
            Set(nameof(SceneEventState.NativePlayerPosition), new FieldFixedPosition(0, 0, 0));
            Set(nameof(SceneEventState.NativePlayerEarlyRetryState), (byte)0);
            typeof(ActorState).GetProperty(nameof(ActorState.WalkmeshTriangle))!.SetValue(engine.Actors[0], 0);
            return (session, engine, speeds, directions);
        }
        var analogue = Create();
        // Original B80C18 direction lookup resolves these off-octant headings.
        (float X, float Y, int Direction)[] headings = [(0.75f, 0.25f, 77), (0.25f, 0.75f, 115), (-0.25f, 0.75f, 141), (-0.75f, 0.25f, 179), (-0.75f, -0.25f, 205), (-0.25f, -0.75f, 243), (0.25f, -0.75f, 13), (0.75f, -0.25f, 51)];
        foreach (var heading in headings)
        {
            PlayerActionResult result = analogue.Engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: heading.X, Vertical: heading.Y));
            Assert.True(result is { Accepted: true, FramesAdvanced: 1 } && analogue.Directions[^1] == heading.Direction && analogue.Speeds[^1] == 7929, $"Analogue heading ({heading.X},{heading.Y}) snapped or changed native run speed: {analogue.Directions[^1]}/{analogue.Speeds[^1]}.");
        }
        analogue.Engine.Apply(new PlayerAction(PlayerActionKind.FieldButtons, Value: 0x10));
        analogue.Engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 0.375f, Vertical: 0.125f));
        Assert.True(analogue.Directions[^1] == headings[0].Direction && analogue.Speeds[^1] == 2696 && analogue.Engine.Scene.NativeSalaryMovementUnits == 3, "Mapped slow-stick walk changed its off-octant heading or bypassed native walk speed/salary ownership.");
        analogue.Engine.Apply(new PlayerAction(PlayerActionKind.FieldButtons, Value: 0));
        analogue.Engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 0.75f, Vertical: 0.25f));
        Assert.True(analogue.Directions[^1] == headings[0].Direction && analogue.Speeds[^1] == 7929 && analogue.Engine.Scene.NativeSalaryMovementUnits == 5, "Releasing mapped slow-stick walk failed to restore native running at the same heading.");
        var forcedWalk = Create(forcedWalk: true);
        forcedWalk.Engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 0.75f, Vertical: 0.25f));
        Assert.True(forcedWalk.Directions.Single() == headings[0].Direction && forcedWalk.Speeds.Single() == 2696 && forcedWalk.Engine.Scene.NativeSalaryMovementUnits == 3, "Current VM RUNDISABLE lost precedence over full-stick running.");
        Console.WriteLine("Native analogue field movement: eight off-octant headings, mapped slow-stick walk/run and current-VM forced walk passed.");
    }

    private static byte[] SyntheticObjects(ushort[] groups, List<(Jsm.Opcode Opcode, int Parameter)[]> scripts)
    {
        int scriptsOffset = 8 + groups.Length * 2;
        int operationsOffset = scriptsOffset + (scripts.Count + 1) * 2;
        byte[] bytes = new byte[operationsOffset + scripts.Sum(script => script.Length) * 4];
        bytes[3] = checked((byte)groups.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), checked((ushort)scriptsOffset));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), checked((ushort)operationsOffset));
        for (int index = 0; index < groups.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8 + index * 2), groups[index]);
        }
        int operationIndex = 0;
        for (int index = 0; index < scripts.Count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(scriptsOffset + index * 2), checked((ushort)operationIndex));
            foreach ((Jsm.Opcode opcode, int parameter) in scripts[index])
            {
                int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHAC or Jsm.Opcode.PSHI_L or Jsm.Opcode.POPI_L or Jsm.Opcode.PREQ or Jsm.Opcode.PREQSW or Jsm.Opcode.PREQEW or Jsm.Opcode.REQ or Jsm.Opcode.REQSW or Jsm.Opcode.REQEW ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(operationsOffset + operationIndex++ * 4), encoded);
            }
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(scriptsOffset + scripts.Count * 2), checked((ushort)operationIndex));
        return bytes;
    }

}
