using System.Buffers.Binary;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private ushort highwayHeld;
    private ushort highwayPressed;

    public Ff7HighwayRuntime? Highway { get; private set; }
    internal Ff7HighwayStart? HighwayStart { get; private set; }

    public void SetHighwayInput(ushort held, ushort pressed)
    {
        highwayHeld = held;
        highwayPressed = pressed;
    }

    private void AdvanceMinigame(FieldHostRequest request, bool advanceClock)
    {
        FieldMinigameCommand command = (FieldMinigameCommand)request.Command;
        try
        {
            if (command.Game != 0)
            {
                throw new NotSupportedException($"FFVII minigame {command.Game} requires its native gameplay module before returning to map {command.Return.Map}.");
            }
            FieldVariables variables = Machine.Variables;
            byte mode = variables.ReadByte(FieldBank.Persistent1Byte, 0x73);
            ushort highScore = unchecked((ushort)variables.ReadWord(FieldBank.Persistent2Word, 0x6D));
            if (mode > 1)
            {
                throw new NotSupportedException($"FFVII highway mode {mode} is unsupported.");
            }
            if (HighwayStart is { } prepared)
            {
                if (prepared.Request != request || prepared.Arcade != (mode == 1) || prepared.HighScore != highScore)
                {
                    throw new InvalidDataException("The prepared FFVII highway no longer matches its native departure owner.");
                }
            }
            else
            {
                HighwayStart = Ff7HighwayStart.Prepare(request, mode == 1, highScore, Ff7HighwayResources.Load(installation, mode == 1));
            }
            if (mode == 1)
            {
                throw new NotSupportedException("FFVII arcade highway requires its native score and results lifecycle.");
            }
            if (Highway is null)
            {
                Highway = new(HighwayStart, Ff7PcExecutableData.LoadBattleCameraMath(installation), Progress.BattleRng?.CrtSeed ?? 1);
                Highway.Sound += (channel, sample) => Host.SubmitNativeAudio(new(0x27 + channel, 64, sample));
                Host.SubmitNativeAudio(new(0xC0, 127));
                Host.SubmitNativeAudio(new(0x10, 0x26));
                Host.SubmitNativeAudio(new(0x2B, 64, 0xB3));
                Host.SubmitNativeAudio(new(0x2A, 64, 0x12B));
                return;
            }
            if (Highway.Request != request)
            {
                throw new InvalidDataException("The FFVII highway lost its field request owner.");
            }
            if (advanceClock && !Highway.Finished)
            {
                AdvanceNominalClockCallbacks();
            }
            if (!Highway.Finished)
            {
                Highway.Advance(highwayHeld, highwayPressed);
                Host.AdvanceNativeMusicClock();
                highwayPressed = 0;
            }
            if (Highway.Finished)
            {
                CompleteHighwayReturn(Highway, command.Return);
            }
        }
        catch (Exception error) when (error is NotSupportedException or InvalidDataException or IOException)
        {
            throw new FieldExecutionException(new(Machine.ProgramIdentity, Machine.Tick, request.Entity, request.Priority, request.ProgramCounter, 0x20), error.Message, error);
        }
    }

    private void CompleteHighwayReturn(Ff7HighwayRuntime highway, FieldTransitionCommand entry)
    {
        if (entry.Map <= 64)
        {
            throw new NotSupportedException("FFVII highway return requires a field destination.");
        }

        FieldVariables variables = new(Machine.Variables.CopyPersistent(), new byte[256]);
        int[] gaugeOrder = [0, 1, 3, 2, 4];
        for (int character = 0; character < gaugeOrder.Length; character++)
        {
            variables.WriteWord(FieldBank.Persistent1Word, (byte)(0x74 + character * 2), unchecked((short)highway.Gauges[gaugeOrder[character]].Value));
        }

        Ff7Progress progress = Progress with
        {
            BattleRng = (Progress.BattleRng ?? new(new byte[8])) with { CrtSeed = highway.RandomSeed }
        };
        long generation = checked(FieldGeneration + 1);
        var original = (Field, MapId, models, encounterTables, Host, Machine, Progress);
        List<Action> effects = [];
        fieldEntryEffects = effects;
        try
        {
            Progress = progress;
            LoadField(entry.Map, variables, prepareHost: host =>
            {
                host.AudioRequested = audio => effects.Add(() => original.Host.AudioRequested?.Invoke(audio));
                host.NativeSoundRequested = sound => effects.Add(() => original.Host.NativeSoundRequested?.Invoke(sound));
                host.NativeSoundLoopAlreadyPlaying = null;
                host.BeginBattleRequest = _ => throw new NotSupportedException("FFVII highway return cannot prepare a field initializer that starts a battle.");
                host.SubmitNativeAudio(new(0xF1));
                host.SubmitNativeAudio(new(0x10, 0));
            });
            fieldLoadIncomplete = true;
            Machine.Initialize();
            Host.InitializePlayerMovement(Field.Scripts.Scale);
            Host.InitializeBackgroundTriggers();
            Host.PlacePlayerAtEntry(entry);
            Host.Process.Validate();
            Progress.Validate();
        }
        catch
        {
            (Field, MapId, models, encounterTables, Host, Machine, Progress) = original;
            throw;
        }
        finally
        {
            fieldEntryEffects = null;
            fieldLoadIncomplete = false;
        }
        FieldGeneration = generation;
        dialogueTokens.Clear();
        Host.AudioRequested = original.Host.AudioRequested;
        Host.NativeSoundRequested = original.Host.NativeSoundRequested;
        Host.NativeSoundLoopAlreadyPlaying = original.Host.NativeSoundLoopAlreadyPlaying;
        Host.BeginBattleRequest = BeginFieldBattle;
        Highway = null;
        HighwayStart = null;
        highwayHeld = highwayPressed = 0;
        foreach (Action effect in effects)
        {
            effect();
        }
    }

    private void ApplyHighwayHealth()
    {
        Ff7Progress before = Progress;
        FieldVariables variables = Machine.Variables;
        Ff7Progress after = before with { Characters = before.Characters.Select(character => ApplyHighwayHealth(character, variables)).ToArray() };
        if (GuestFieldVitals is { } guest)
        {
            foreach (Ff7GuestFieldVitalsView actor in guest.ReadAll().Where(actor => actor.StoryCharacter is >= 0 and <= 4))
            {
                uint gauge = unchecked((ushort)variables.ReadWord(FieldBank.Persistent1Word, (byte)(0x74 + actor.StoryCharacter * 2)));
                short difference = checked((short)((int)Math.Max(1, actor.Hp * gauge / ushort.MaxValue) - actor.Hp));
                PublishFieldEntryEffect(() => guest.AddHitPoints(actor.StoryCharacter, difference));
            }
        }
        Progress = PreserveGuestFieldCharacters(before, after);
    }

    internal static Ff7CharacterState ApplyHighwayHealth(Ff7CharacterState character, FieldVariables variables)
    {
        if (character.Id > 4)
        {
            return character;
        }
        uint gauge = unchecked((ushort)variables.ReadWord(FieldBank.Persistent1Word, (byte)(0x74 + character.Id * 2)));
        int hp = (int)Math.Max(1, character.HitPoints * gauge / ushort.MaxValue);
        byte[] record = character.NativeRecord.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(44), (ushort)hp);
        return character with { HitPoints = hp, NativeRecord = record };
    }
}
