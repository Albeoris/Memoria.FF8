using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static class DiagnosticInvocationAftercareValidation
{
    public static void ValidatePlan(BattleDiagnosticInvocationRelease release, BattleDiagnosticEffectDispatch focal, Guid lifecycle)
    {
        BattleDiagnosticInvocationAftercarePlan plan = release.Aftercare ?? throw new InvalidDataException("Scoped aftercare requires its actual focal completion plan.");
        ArgumentNullException.ThrowIfNull(plan.Callback);
        if (release.Boundary != BattleDiagnosticInvocationRelease.AftercareBoundary || plan.LifecycleId != lifecycle || plan.FocalSerial != focal.Serial || plan.MainCallback != release.Callback || plan.MainFrame != release.SimulationFrame || plan.MainTimestamp != release.Timestamp || plan.Callback.SourceHeaderIndex != focal.NativeHeaderIndex || plan.Callback.SourceHeaderGeneration != focal.NativeHeaderGeneration || focal.NativeTargetMask is not ushort targets || (targets & (1 << plan.Callback.ActorIndex)) == 0)
        {
            throw new InvalidDataException("Aftercare plan changed its exact focal lifecycle, header generation or sealed main boundary.");
        }
        ValidateCallback(plan.Callback);
        ValidateRandom(plan.MainRandom);
        ValidateCommandBanks(plan.CommandBanks, plan.Callback);
        ValidateOwners(release.Owners, true, plan.TerminalTextures);
        string[] expected = plan.TerminalTextures is null ? ["action-or-reaction"] : ["effect-or-auxiliary-task", "action-or-reaction"];
        if (release.Blockers.IsDefault || release.Blockers.Length != expected.Length || !release.Blockers.ToHashSet(StringComparer.Ordinal).SetEquals(expected))
        {
            throw new InvalidDataException("Aftercare may retain only its proved terminal texture and actual mandatory callback blockers.");
        }
    }

    public static void Validate(BattleDiagnosticInvocationRelease release, BattleDiagnosticInvocationAftercareReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(receipt);
        DiagnosticInvocationReleaseValidation.Validate(release, release.Dispatch, release.LifecycleId);
        BattleDiagnosticInvocationAftercarePlan plan = release.Aftercare ?? throw new InvalidDataException("Aftercare receipt is undeclared by its sealed main window.");
        if (receipt.LifecycleId != plan.LifecycleId || receipt.FocalSerial != plan.FocalSerial || receipt.MainCallback != plan.MainCallback || !SameCallback(plan.Callback, receipt.Callback) || receipt.DispatchCallback < plan.MainCallback || receipt.HandlerCompletedCallback < receipt.DispatchCallback || receipt.BankReleasedCallback < receipt.HandlerCompletedCallback || receipt.CompletedCallback < receipt.BankReleasedCallback || receipt.CompletedFrame < plan.MainFrame || receipt.CompletedTimestamp < plan.MainTimestamp || !receipt.NativeEventCompleted || !receipt.BankReleased || !receipt.PacketRemoved || receipt.ProducedActions != 0 || receipt.ProducedHeaders != 0 || receipt.ProducedContacts != 0 || receipt.DispatchSerialBefore != plan.FocalSerial || receipt.DispatchSerialAfter != receipt.DispatchSerialBefore)
        {
            throw new InvalidDataException("Aftercare lacks its exact real callback, normal bank retirement and output-free completed child event.");
        }
        ValidateRandom(receipt.HandlerRandomBefore);
        ValidateRandom(receipt.HandlerRandomAfter);
        if (!SameRandom(receipt.HandlerRandomBefore, receipt.HandlerRandomAfter))
        {
            throw new InvalidDataException("The proved no-op player handler consumed a native random stream.");
        }
        NativeBattleCommandPacket packet = receipt.ReleasedBankPacket;
        ArgumentNullException.ThrowIfNull(packet);
        byte[] released = plan.Callback.Packet.Bytes.ToArray();
        released[0] = 255;
        released.AsSpan(6, 6).Clear();
        NativeBattleCommandLink expectedLink = plan.Callback.Link with { Previous = 0, Next = 0, State = 0 };
        if (packet.Bytes.IsDefault || !packet.Bytes.AsSpan().SequenceEqual(released) || packet.UnknownByteMask != (plan.Callback.Packet.UnknownByteMask & 0x3E) || receipt.ReleasedBankLink != expectedLink)
        {
            throw new InvalidDataException("Aftercare did not retain the exact native callback bank release writer.");
        }
        NativeBattleDescriptorPacket completion = receipt.CompletionPacket;
        NativeBattleDescriptorPacket removed = receipt.RemovedCompletionPacket;
        ArgumentNullException.ThrowIfNull(completion);
        ArgumentNullException.ThrowIfNull(removed);
        if (receipt.CompletionPacketSlot is < 0 or >= 30 || completion.Bytes.IsDefault || completion.Bytes.Length != 12 || (completion.UnknownByteMask & 255) != 0 || completion.Command != 10 || completion.Argument != 0x47DDA0 || completion.PriorityState == 0 || (completion.PriorityState & 15) != 0 || removed.Bytes.IsDefault || removed.UnknownByteMask != completion.UnknownByteMask || !removed.Bytes.AsSpan().SequenceEqual(completion.Bytes.SetItem(1, 0).AsSpan()))
        {
            throw new InvalidDataException("Aftercare requires its actual47DDA0 child10 and exact native byte1 retirement.");
        }
        ValidateCommandBanks(receipt.CommandBanks, null);
        ValidateTextureContinuity(plan.TerminalTextures, receipt.CleanupTextures);
        ValidateOwners(receipt.CleanupOwners, false, receipt.CleanupTextures);
    }

    private static void ValidateCommandBanks(ImmutableArray<BattleDiagnosticCommandBankClosure> banks, BattleDiagnosticNoOpPlayerCallback? callback)
    {
        if (banks.IsDefault || banks.Length != 3)
        {
            throw new InvalidDataException("Aftercare requires all three actual native command-bank closures.");
        }
        for (int bankIndex = 0; bankIndex < 3; bankIndex++)
        {
            BattleDiagnosticCommandBankClosure bank = banks[bankIndex];
            byte expectedTail = callback is not null && bankIndex == 0 ? callback.Slot : (byte)255;
            if (bank is null || bank.Bank != bankIndex || bank.Tail != expectedTail || bank.Links.IsDefault || bank.Links.Length != 11)
            {
                throw new InvalidDataException("Aftercare command-bank snapshot is incomplete or retains an undeclared physical queue.");
            }
            for (int slot = 0; slot < 11; slot++)
            {
                NativeBattleCommandLink link = bank.Links[slot];
                if (callback is not null && bankIndex == 0 && slot == callback.Slot)
                {
                    if (link != callback.Link)
                    {
                        throw new InvalidDataException("The actual mandatory callback differs from its bank closure.");
                    }
                }
                else if (link is null || link.Previous != 0 || link.Next != 0 || link.State != 0)
                {
                    throw new InvalidDataException("Aftercare retains another allocated callback or action, including an unbound physical row.");
                }
            }
        }
    }

    private static void ValidateTextureContinuity(BattleDiagnosticTextureQuiescence? main, BattleDiagnosticTextureQuiescence? cleanup)
    {
        if (cleanup is null)
        {
            return;
        }
        DiagnosticTextureQuiescenceValidation.Validate(cleanup);
        if (main is null || main.Head != cleanup.Head || main.Tail != cleanup.Tail || main.PhysicalActiveCount != cleanup.PhysicalActiveCount || main.Tasks.Length != cleanup.Tasks.Length)
        {
            throw new InvalidDataException("Aftercare cannot introduce or replace a terminal texture chain.");
        }
        for (int index = 0; index < main.Tasks.Length; index++)
        {
            BattleDiagnosticTextureTask current = cleanup.Tasks[index];
            BattleDiagnosticTextureTask original = main.Tasks[index] with { ModelFlags0 = current.ModelFlags0, ResourceFlags44 = current.ResourceFlags44, ModelSequence = current.ModelSequence };
            if (original != current)
            {
                throw new InvalidDataException("Aftercare changed a retained texture task's actual stable source/slot/programme ownership.");
            }
        }
    }

    private static void ValidateCallback(BattleDiagnosticNoOpPlayerCallback callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(callback.Packet);
        ArgumentNullException.ThrowIfNull(callback.Link);
        if (callback.Bank != 0 || callback.Slot >= 11 || callback.Subcommand != 0 || string.IsNullOrWhiteSpace(callback.ActorId) || callback.ActorIndex >= 3 || callback.ModelId < 0 || callback.ModelId == 4 || callback.PassiveFlags != 0 || callback.Stage != 2 || callback.SourceHeaderIndex is < 0 or >= 32 || callback.SourceHeaderGeneration <= 0 || callback.Link.Previous != 255 || callback.Link.Next != 255 || callback.Link.State != 0 || callback.Packet.Bytes.IsDefault || callback.Packet.Bytes.Length != 12 || (callback.Packet.UnknownByteMask & 0xFF7) != 0 || callback.Packet.ReadByte(0) != callback.ActorIndex || callback.Packet.ReadByte(1) != 255 || callback.Packet.ReadByte(2) != 0 || callback.Packet.ReadWord(4) != 2 || callback.Packet.Bytes.AsSpan(6, 6).IndexOfAnyExcept((byte)0) >= 0)
        {
            throw new InvalidDataException("Aftercare is restricted to one actual bank0 primary player stage2 packet with a proved no-op model/passive combination.");
        }
    }

    private static bool SameCallback(BattleDiagnosticNoOpPlayerCallback left, BattleDiagnosticNoOpPlayerCallback right)
    {
        ValidateCallback(right);
        return left.Bank == right.Bank && left.Slot == right.Slot && left.Subcommand == right.Subcommand && left.ActorId == right.ActorId && left.ActorIndex == right.ActorIndex && left.ModelId == right.ModelId && left.PassiveFlags == right.PassiveFlags && left.Stage == right.Stage && left.SourceHeaderIndex == right.SourceHeaderIndex && left.SourceHeaderGeneration == right.SourceHeaderGeneration && left.Link == right.Link && left.Packet.UnknownByteMask == right.Packet.UnknownByteMask && left.Packet.Bytes.AsSpan().SequenceEqual(right.Packet.Bytes.AsSpan());
    }

    private static void ValidateRandom(BattleDiagnosticRepairRandomState random)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(random.Battle);
        random.Battle.Validate();
    }

    private static bool SameRandom(BattleDiagnosticRepairRandomState left, BattleDiagnosticRepairRandomState right) => left.Battle.Lane == right.Battle.Lane && left.Battle.Indexes.AsSpan().SequenceEqual(right.Battle.Indexes.AsSpan()) && left.Crt == right.Crt && left.Ui == right.Ui && left.Global == right.Global;

    private static void ValidateOwners(BattleDiagnosticInvocationOwners owners, bool callback, BattleDiagnosticTextureQuiescence? textures)
    {
        ArgumentNullException.ThrowIfNull(owners);
        if (textures is not null)
        {
            DiagnosticTextureQuiescenceValidation.Validate(textures);
        }
        if (owners.ActionHeaders != 0 || owners.ContactRows != 0 || owners.DescriptorPackets != 0 || owners.NonPumpDescriptorTasks != 0 || owners.PendingContacts != 0 || owners.Effects != 0 || owners.EffectHead || owners.AuxiliaryHead != (textures is not null) || owners.Modules != 0 || owners.ModuleHead || owners.Files != 0 || owners.ReturnedTaskPointer != 0 || owners.DetachedReturnedTask || owners.CameraMask != 0 || owners.SoundBusy || owners.ReplacementResource || owners.ActionOrCommand != callback || owners.TimedOrReaction || !owners.ObservedOriginalEffect || owners.SelectedReturnedDescriptor != 0 || owners.HeaderGraphicsBusy || owners.OriginalPools.IsDefault || owners.OriginalPools.Any(pool => pool is null || pool.ActiveRows != 0 || pool.Head || pool.Tail || pool.DescriptorAddress == 0 || pool.BankAddress == 0 || pool.Stride == 0))
        {
            throw new InvalidDataException("Aftercare retains an unproved owner outside its exact callback and conditional terminal texture observations.");
        }
    }
}
