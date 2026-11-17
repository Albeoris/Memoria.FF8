using System.Diagnostics.CodeAnalysis;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleActionPacketsState nativeActionPackets;
    private NativeBattlePartyStatusMirrors nativePartyStatusMirrors = NativeBattlePartyStatusMirrors.Unknown();
    public NativeBattleActionPacketsState NativeActionPackets => nativeActionPackets;
    public NativeBattlePartyStatusMirrors NativePartyStatusMirrors => nativePartyStatusMirrors;
    public int? NativeEffectIoResult => Presentation?.NativeEffectIoResult;
    public NativeBattleEffectMemory? NativeEffectMemory => Presentation?.NativeEffectMemory;
    public NativeBattleModelTextureCache? NativeModelTextureCache => Presentation?.NativeModelTextureCache;

    [MemberNotNull(nameof(nativeActionPackets))]
    private void InitializeNativeRetainedMemory()
    {
        BattleModelRoster empty = new([]);
        NativeBattleModelAliases aliases = new(empty, []);
        if (definition.NativeRetainedMemory is not { } memory)
        {
            nativeActionPackets = NativeBattleActionPacketsState.Create(empty, NativeBattleRetainedActionPackets.Fresh(), aliases);
            return;
        }
        memory.Validate();
        memory = NativeBattleStorageSnapshots.RestoreForNewBattle(memory);
        nativeActionPackets = NativeBattleActionPacketsState.Create(empty, memory.ActionPackets, aliases);
        nativePartyStatusMirrors = memory.PartyStatusMirrors;
        RestoreNativePresentationStatusMemory(memory.PresentationStatusMemory);
        nativeActionScratch = memory.ActionScratch with { PublishedTargetCount = memory.ActionScratch.LastPublishedTargetCountE05 };
        RestoreNativeElementAttack(memory.ElementAttack);
        RestoreNativeActionMemory(memory.ActionMemory);
        nativeScriptState = nativeScriptState with { AllocationControl = memory.AllocationControl };
    }

    private void PublishNativeSingleActionHeader(int targetCount)
    {
        (nativeActionPackets, BattleActionHeaderReference header) = nativeActionPackets.Publish(targetCount);
        // The native arena retains BYTE17 on Prepare. Every session publisher starts with one
        // dispatchable header; an explicit group may extend it after preparing further casts.
        nativeActionPackets = nativeActionPackets.WritePlayerRepeatMode(header, 1);
    }

    private void RefreshNativePacketBindings()
    {
        var (roster, aliases) = ReadBattlePresentationPacketBindings();
        nativeActionPackets = nativeActionPackets.WithRoster(roster, aliases);
    }
}
