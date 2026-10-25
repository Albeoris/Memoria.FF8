using System.Diagnostics;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticBattleRequest([property: JsonRequired] int Encounter, [property: JsonRequired] byte Seed, [property: JsonRequired] ushort Flags, [property: JsonRequired] ushort FieldBattleMode, [property: JsonRequired] uint ElapsedCounter)
{
    public DiagnosticBattleDriverRecipe? Driver { get; init; }
    public byte? BattleMusic { get; init; }
    public int? PrimaryMusicChannel { get; init; }
    public ushort? MusicFieldWord { get; init; }

    internal void Validate()
    {
        Driver?.Validate();
        if (Encounter is < 0 or > ushort.MaxValue)
        {
            throw new InvalidDataException("Diagnostic encounter must be an installed formation WORD.");
        }
        if ((Flags & 0x10) == 0 && (BattleMusic is null || PrimaryMusicChannel is null))
        {
            throw new NotSupportedException("Diagnostic music preparation requires explicit battle music BYTE203 and primary channel DWORD108, or the declared native keep-music flag.");
        }
    }
}

internal sealed record DiagnosticBattleReceipt(Guid BattleInstanceId, string InitializerVersion, string ExecutableSha256, DiagnosticBattleRequest Preparation, int Stage, string[] OrderedStages, string[] PresentOwners, double PreparationMilliseconds);

internal sealed partial class GameWorld
{
    private sealed record DiagnosticBattleOwner(DiagnosticBattleRequest Request, Guid InstanceId);
    private DiagnosticBattleOwner? diagnosticBattle;
    internal DiagnosticBattleReceipt? DiagnosticReceipt { get; private set; }
    internal bool IsDiagnosticBattle => diagnosticBattle is not null;

    internal BattleSession StartDiagnosticBattle(DiagnosticBattleRequest request)
    {
        request.Validate();
        if (battle is not null || diagnosticBattle is not null || Field is not null || worldScene is not null || session.State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame || session.State.EventState is not null || HasPendingBattle)
        {
            throw new InvalidOperationException("Direct diagnostic entry requires a canonical New Game process before exploration allocation.");
        }
        if (session.ExamTimer is null || session.State.CharacterVitals is null || session.State.InputConfiguration is null || session.State.Economy is null)
        {
            throw new NotSupportedException("Diagnostic initialization requires canonical New Game timer, character, input and economy owners before battle allocation.");
        }
        long started = Stopwatch.GetTimestamp();
        NativeBattleRetainedMemory retained = session.State.NativeBattleMemory ?? throw new NotSupportedException("Diagnostic initialization requires installed cold-process retained memory.");
        string executable = retained.ColdExecutableSha256 ?? throw new NotSupportedException("Diagnostic initialization requires an authenticated executable identity.");
        NativeBattleMusicProcessState music = retained.MusicProcess ?? throw new NotSupportedException("Diagnostic initialization requires its actual retained music owner.");
        NativeGlobalSoundProcess sound = session.State.GlobalSoundProcess ?? throw new NotSupportedException("Diagnostic initialization requires its actual global sound owner.");
        NativeBattleMusicEntryPreparation preparedMusic = NativeBattleMusicEntry.Prepare52CE30(checked((ushort)request.Encounter), request.Flags, music.Buffers, request.BattleMusic, request.PrimaryMusicChannel, sound, BattleMusicEntryResources, music.ReturnChannel, request.MusicFieldWord);
        diagnosticBattle = new(request, Guid.NewGuid());
        try
        {
            PreparedBattleEntry prepared = PrepareBattleEntry(request.Encounter, request.Flags, request.Seed, BattleAtbMode.Normal, preparedMusic);
            battle = CommitPreparedBattleEntry(prepared);
            DiagnosticReceipt = new(diagnosticBattle.InstanceId, "direct-new-game-v1", executable, request, battle.Capture().Definition.StageId ?? throw new NotSupportedException("Diagnostic entry requires the actual encounter stage owner."), ["installed-cold-process", "hardcoded-new-game", "resource-derived-new-game", "explicit-diagnostic-field-inputs", "native-music-entry", "canonical-encounter-construction", "lazy-effect-resolver", "diagnostic-publication"], ["retained-memory", "music", "global-sound", "exam-timer", "character-vitals", "input-configuration", "economy"], Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return battle;
        }
        catch
        {
            diagnosticBattle = null;
            throw;
        }
    }

    private int ApplyDiagnosticBattleInput(BattleInput input)
    {
        if (input.Kind is BattleInputKind.Return or BattleInputKind.StartEncounter or BattleInputKind.ConfigureEncounters or BattleInputKind.ConfigurePlaythrough or BattleInputKind.InstantVictory or BattleInputKind.InstantDefeat or BattleInputKind.AdvanceTurn or BattleInputKind.SetMode)
        {
            throw new NotSupportedException("Diagnostic battles preserve the live encounter and normal action admission; this lifecycle or accelerated input is unavailable.");
        }
        BattleSession active = battle ?? throw new InvalidOperationException("Diagnostic battle has no live session.");
        int advanced = ExecuteBattleCommand(active, input);
        PublishBattleSnapshot(active);
        return advanced;
    }

    internal void EndDiagnosticBattle()
    {
        if (diagnosticBattle is null)
        {
            throw new InvalidOperationException("No diagnostic battle lifecycle is active.");
        }
        // Isolated teardown releases the live battle without applying exploration rewards or progress.
        battle = null;
        diagnosticBattle = null;
        session.SetLiveBattle(null);
        session.Restore(session.State with { Scene = SceneKind.Title });
    }
}
