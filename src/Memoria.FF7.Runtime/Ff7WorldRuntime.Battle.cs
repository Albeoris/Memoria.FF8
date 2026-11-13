using System.Buffers.Binary;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldBattleRequest(long Id, int Formation, bool Preemptive, bool Scripted, int ScriptPc = -1)
{
    internal const int TimedFlag = 1 << 29;
    internal const int FieldReturnFlag = 1 << 30;
    internal const int PreemptiveFlag = unchecked((int)0x80000000);

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ReturnsToField { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Timed { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Chocobo { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ReturnsToWorldModule { get; init; }

    internal bool ReloadsWorld => Timed || Chocobo || ReturnsToWorldModule;
    internal ushort? FieldReturnSelector => ReturnsToField ? (ushort)0x2100 : null;
    internal ushort Flags => (ushort)((Preemptive ? 4 : 0) | (Timed ? 2 : 0));
}
public sealed record Ff7WorldBattleState(int MovementCounter, int Danger, int PreviousFormation, byte[] Random, int RandomIndex, Ff7WorldBattleRequest? Pending)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool CountdownActive { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Escaped { get; init; }
}

public sealed partial class Ff7WorldRuntime
{
    private Ff7WorldEncounterTable? encounterTable;
    private Ff7WorldEncounterRandom? worldRandom;
    private int encounterMovementCounter = -140;
    private int encounterDanger;
    private int previousWorldFormation = -1;
    private Ff7WorldBattleRequest? pendingWorldBattle;
    private bool countdownActive;
    private bool escapedWorldBattle;

    internal bool HasEncounterCountdown => countdownActive || pendingWorldBattle?.Timed == true;
    internal Action<uint>? SetEncounterCountdown { get; set; }
    internal int EncounterCloudLevel { get; set; }

    internal Ff7WorldBattleRequest? PendingBattle => pendingWorldBattle;
    internal bool RandomBattlesEnabled { get; set; }
    internal uint EncounterSeed { get; set; }
    internal int EncounterPartyCount { get; set; }
    internal Func<Ff7WorldEncounterModifiers>? GetRandomBattleModifiers { get; set; }
    internal Func<long>? AllocateBattleRequestId { get; set; }

    private Ff7WorldBattleState? CaptureWorldBattleState()
    {
        return worldRandom is null && pendingWorldBattle is null && !countdownActive && !escapedWorldBattle
            ? null
            : new(encounterMovementCounter, encounterDanger, previousWorldFormation, WorldRandom.Capture(), WorldRandom.Index, pendingWorldBattle) { CountdownActive = countdownActive, Escaped = escapedWorldBattle };
    }

    private Ff7WorldEncounterRandom WorldRandom => worldRandom ??= new(EncounterSeed);

    private static void ValidateWorldBattleState(Ff7WorldBattleState? state)
    {
        if (state is { } saved && (saved.Random is not { Length: 0x209 } || saved.RandomIndex is < 0 or >= 0x209 || saved.MovementCounter is < -140 or > 16 || saved.Danger < 0 || saved.PreviousFormation is < -1 or > 1023 || saved.Pending is { } request && (request.Id < 1L << 60 || request.Formation is < 0 or > 1023 || request.Scripted != (request.ScriptPc >= 0))))
        {
            throw new InvalidDataException("Invalid FFVII world encounter continuation.");
        }
    }

    private void RestoreWorldBattleState(Ff7WorldBattleState? state)
    {
        worldRandom = state is null ? null : new(state.Random, state.RandomIndex);
        encounterMovementCounter = state?.MovementCounter ?? -140;
        encounterDanger = state?.Danger ?? 0;
        previousWorldFormation = state?.PreviousFormation ?? -1;
        pendingWorldBattle = state?.Pending;
        countdownActive = state?.CountdownActive == true;
        escapedWorldBattle = state?.Escaped == true;
        if (state is null)
        {
            RestorePreviousWorldFormation();
        }
    }

    private void RestorePreviousWorldFormation()
    {
        previousWorldFormation = BinaryPrimitives.ReadUInt16LittleEndian(persistent.AsSpan(0x3B4)) & 1023;
    }

    private void AdvanceRandomWorldBattle()
    {
        if (!RandomBattlesEnabled || mainState != 1 || pendingWorldBattle is not null || fieldDeparture is not null || Resources.World == 2 || !movementEnabled)
        {
            return;
        }
        if (controlledModel is not (0 or 1 or 2 or 6) || encounterRate == 0)
        {
            encounterMovementCounter = 0;
            return;
        }
        Ff7WorldEntity player = RequireEntity(controlledModel);
        if (!player.TerrainResolved || (player.Terrain >> 5 & 7) != 0 || player.X == input.PreviousX && player.Z == input.PreviousZ)
        {
            return;
        }
        if (encounterMovementCounter++ < 16)
        {
            return;
        }
        encounterMovementCounter = 0;
        Ff7WorldEncounterModifiers modifiers = GetRandomBattleModifiers?.Invoke() ?? Ff7WorldEncounterModifiers.Baseline;
        encounterTable ??= new(Resources.ReadRequired("enc_w.bin"));
        int formation = encounterTable.Select(player.Terrain, EncounterPartyCount, (persistent[0x185] & 1) != 0, previousWorldFormation, ref encounterDanger, WorldRandom.Next, out bool preemptive, out byte? chocoboRating, EncounterCloudLevel, modifiers, controlledModel is 0 or 1 or 2);
        if (formation < 0)
        {
            return;
        }
        previousWorldFormation = formation & 1023;
        BinaryPrimitives.WriteUInt16LittleEndian(persistent.AsSpan(0x3B4), (ushort)previousWorldFormation);
        UpdateEntity(player with { X = input.PreviousX, Y = input.PreviousY, Z = input.PreviousZ, PresentationEpoch = player.PresentationEpoch + 1 });
        if (chocoboRating is byte rating)
        {
            persistent[0x3FC] = rating;
        }
        RequestWorldBattle(formation, preemptive, false, tick, -1, chocoboRating.HasValue);
    }

    private void RequestWorldBattle(int formation, bool preemptive, bool scripted, long identity, int pc, bool chocobo = false)
    {
        const int admitted = 1023 | Ff7WorldBattleRequest.TimedFlag | Ff7WorldBattleRequest.FieldReturnFlag | Ff7WorldBattleRequest.PreemptiveFlag;
        if ((formation & ~admitted) != 0)
        {
            throw new NotSupportedException($"FFVII world encounter at word {pc:X4} requires an unsupported dispatcher flag.");
        }
        if (mainState > 2 || pendingWorldBattle is not null)
        {
            return;
        }
        long id = AllocateBattleRequestId?.Invoke() ?? checked((1L << 61) + identity * 2 + (scripted ? 1 : 0));
        pendingWorldBattle = new(id, formation & 1023, preemptive || (formation & Ff7WorldBattleRequest.PreemptiveFlag) != 0, scripted, pc)
        {
            ReturnsToField = (formation & Ff7WorldBattleRequest.FieldReturnFlag) != 0,
            Timed = countdownActive || (formation & Ff7WorldBattleRequest.TimedFlag) != 0,
            Chocobo = chocobo,
            ReturnsToWorldModule = scripted && Resources.World == 2
        };
        if (pendingWorldBattle.FieldReturnSelector is not null || pendingWorldBattle.ReloadsWorld)
        {
            movementEnabled = false;
            controlLock = 0;
        }
        audio = audio with { PlaybackEnabled = true };
        PlayWorldSong(chocobo ? 5 : 4);
    }

    internal FieldVariables PrepareBattleVariables(Ff7WorldBattleRequest request)
    {
        ValidateBattleOwner(request.Id);
        return new(persistent, new byte[256]);
    }

    internal void ValidateBattleOwner(long id)
    {
        if (pendingWorldBattle?.Id != id)
        {
            throw new InvalidOperationException("The FFVII world battle result requires its pending encounter.");
        }
    }

    internal void SetBattleReturnEscape()
    {
        escapedWorldBattle = true;
    }

    internal void CompleteSpecialBattleDeparture(Ff7WorldBattleRequest request)
    {
        ValidateBattleOwner(request.Id);
        if ((request.ReturnsToField || request.ReloadsWorld) && !Machine.IsUnloading)
        {
            Machine.CompleteUnload();
        }
    }

    internal void ResumeWorldBattle(Ff7WorldBattleRequest request, FieldVariables variables, bool escaped = false)
    {
        ValidateBattleOwner(request.Id);
        variables.CopyPersistent().CopyTo(persistent, 0);
        pendingWorldBattle = null;
        countdownActive = false;
        escapedWorldBattle = escaped;
        encounterDanger = 0;
        encounterMovementCounter = -140;
        input = input with { Held = 0, Pressed = 0, Horizontal = 0, Vertical = 0, PreviousControlHeld = 0 };
        HeldConfirm = false;
        PresentationCameraEpoch++;
        audio = audio with { PlaybackEnabled = true };
        PlayWorldSong(1);
    }
}
