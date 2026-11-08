using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record BattleComponentAdmissionIdentity(ushort Effect, byte Command, ushort Action, byte Actor, int HeaderIndex, long Callback);

public sealed record BattleComponentTextureQuiescenceObservation(ushort Effect, long GraphicsCallback, int NativeActive, uint CanonicalReturnedTask, BattleDiagnosticInvocationOwners Owners, bool PhysicalCommandBanksEmpty, BattleDiagnosticTextureQuiescence Textures, string ResidentFingerprint, ImmutableArray<string> RawBlockers)
{
    [JsonIgnore]
    public bool IsEligible => Owners is not null && Textures is not null && !RawBlockers.IsDefault && NativeActive == 0 && CanonicalReturnedTask == 0 && PhysicalCommandBanksEmpty && RawBlockers.Length == 1 && RawBlockers[0] == "effect-or-auxiliary-task" && Textures.IsTerminalQuiescent && Owners.AuxiliaryHead && BattleComponentOwnerClosure.AreNonAuxiliaryOwnersReleased(Owners);
}

public static class BattleComponentOwnerClosure
{
    public static bool AreNonAuxiliaryOwnersReleased(BattleDiagnosticInvocationOwners owners)
    {
        return owners is not null && owners.DescriptorPackets == 0 && owners.NonPumpDescriptorTasks == 0 && owners.PendingContacts == 0 && owners.Effects == 0 && !owners.EffectHead && owners.Modules == 0 && !owners.ModuleHead && owners.Files == 0 && owners.ReturnedTaskPointer == 0 && !owners.DetachedReturnedTask && owners.CameraMask == 0 && !owners.SoundBusy && !owners.ReplacementResource && !owners.ActionOrCommand && !owners.TimedOrReaction && owners.ActionHeaders <= NativeBattleActionPacketsState.HeaderCapacity && owners.ContactRows <= NativeBattleActionPacketsState.ContactCapacity && owners.RetainedActiveHeader is not (< 0 or >= NativeBattleActionPacketsState.HeaderCapacity) && owners.RetainedActiveContact is not (< 0 or > NativeBattleActionPacketsState.ContactCapacity) && owners.ObservedOriginalEffect && owners.SelectedReturnedDescriptor == 0 && !owners.HeaderGraphicsBusy && !owners.OriginalPools.IsDefault && owners.OriginalPools.All(pool => pool is not null && pool.ActiveRows == 0 && !pool.Head && !pool.Tail);
    }
}

public sealed record BattleComponentTerminalTextureCertificate(string CompletionKind, bool Completed, bool Drained, bool ResidentTasksRemain, ushort Effect, string SourceSha256, BattleDiagnosticEffectDispatch? NativeDispatch, BattleComponentAdmissionIdentity? NativeAdmission, BattleComponentTextureQuiescenceObservation First, BattleComponentTextureQuiescenceObservation Last, long FirstPresentedFrame, long LastPresentedFrame, long TailPresentedFrames, long NativeCallbacksAdvanced);

public sealed partial class BattleSession
{
    /// <summary>Observe a conditional resident-texture fixed point without changing any native owner or the raw drain contract.</summary>
    public BattleComponentTextureQuiescenceObservation? ObserveComponentTerminalTextures(ushort expectedEffect)
    {
        if (NativeGraphics is not { OriginalEffect: { ReturnedDescriptor: 0 } selected } graphics || selected.RequestedEffect != expectedEffect || selected.Binding.Effect != expectedEffect || NativeEffectMemory is not { Active: 0 } memory)
        {
            return null;
        }
        BattleDiagnosticTextureQuiescence textures = CaptureDiagnosticTextureQuiescence(graphics);
        if (!textures.IsTerminalQuiescent)
        {
            return null;
        }
        nativeActionPackets.Validate();
        BattleDiagnosticInvocationOwners owners = CaptureDiagnosticInvocationOwnerState(graphics);
        uint returned = checked((uint)memory.ReadReturnedTask5044B0());
        ImmutableArray<string> blockers = ObserveDiagnosticDrainBlockers().ToImmutableArray();
        BattleComponentTextureQuiescenceObservation observation = new(expectedEffect, graphics.Callback, 0, returned, owners, DiagnosticPhysicalCommandBanksMatch(null), textures, "", blockers);
        if (!observation.IsEligible)
        {
            return null;
        }
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        HashValue(hash, owners.ActionHeaders);
        HashValue(hash, owners.ContactRows);
        HashValue(hash, owners.RetainedActiveHeader ?? -1);
        HashValue(hash, owners.RetainedActiveContact ?? -1);
        HashValue(hash, graphics.Auxiliary.Head!.Value);
        HashValue(hash, graphics.Auxiliary.Tail!.Value);
        foreach (BattleDiagnosticTextureTask row in textures.Tasks)
        {
            NativeBattleAuxiliaryTask task = graphics.Auxiliary.Slots[row.Slot];
            NativeBattleGraphicsModel model = graphics.Models[row.ModelIndex!.Value];
            HashValue(hash, row.Slot);
            HashValue(hash, task.Flags);
            HashValue(hash, task.State);
            HashValue(hash, task.Next ?? -1);
            HashValue(hash, (int)task.Kind);
            HashValue(hash, task.UnknownByteMask);
            hash.AppendData(task.Payload.AsSpan());
            HashValue(hash, task.Program!.Offset);
            HashValue(hash, task.Program.Source.Length);
            hash.AppendData(task.Program.Source.AsSpan());
            HashValue(hash, model.Index);
            HashValue(hash, model.Flags0 & 2);
            HashValue(hash, model.ResourceFlags44!.Value & 0x40);
            HashValue(hash, model.Sequence!.Value);
            HashValue(hash, model.ActiveResourceRow100 ?? -1);
            HashValue(hash, model.ActiveStagedResourceRow100 is uint staged ? staged : -1L);
            HashValue(hash, model.ResourceMagicId is ushort magic ? magic : -1L);
            if (model.BodyTextureSlots is { } binding)
            {
                foreach (byte? slot in binding.PaletteRows)
                {
                    HashValue(hash, slot is byte value ? value : -1);
                }
            }
        }
        return observation with { ResidentFingerprint = Convert.ToHexStringLower(hash.GetHashAndReset()) };
    }

    private static void HashValue(IncrementalHash hash, long value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }
}
