using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record BattleComponentHeldScreenBlendTask(int Slot, ushort Flags, ushort State, int? Next, NativeBattleAuxiliaryKind Kind, uint UnknownByteMask, bool HasProgram, ImmutableArray<byte> Payload)
{
    [JsonIgnore]
    public short Elapsed => Payload.IsDefault || Payload.Length < 2 ? (short)-1 : BinaryPrimitives.ReadInt16LittleEndian(Payload.AsSpan());
    [JsonIgnore]
    public short Duration => Payload.IsDefault || Payload.Length < 4 ? (short)0 : BinaryPrimitives.ReadInt16LittleEndian(Payload.AsSpan().Slice(2));
    [JsonIgnore]
    public bool IsHeld => Slot is >= 0 and < 16 && (Flags & 1) != 0 && State == 0 && Next is null && Kind == NativeBattleAuxiliaryKind.ScreenBlend501E80 && (UnknownByteMask & 0x3F) == 0 && !HasProgram && !Payload.IsDefault && Payload.Length == 32 && Duration > 0 && Elapsed == Duration && Payload[4] == 0 && Payload[5] == 255;
    [JsonIgnore]
    public string Fingerprint => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this)));
}

public sealed record BattleComponentHeldScreenBlendObservation(ushort Effect, long GraphicsCallback, int NativeActive, uint CanonicalReturnedTask, BattleDiagnosticInvocationOwners Owners, bool PhysicalCommandBanksEmpty, int PhysicalResidentCount, int? Head, int? Tail, BattleComponentHeldScreenBlendTask ScreenBlend, string ResidentFingerprint, ImmutableArray<string> RawBlockers)
{
    [JsonIgnore]
    public bool IsEligible => ScreenBlend is not null && Owners is not null && NativeActive == 0 && CanonicalReturnedTask == 0 && PhysicalCommandBanksEmpty && PhysicalResidentCount == 1 && Head == ScreenBlend.Slot && Tail == ScreenBlend.Slot && ScreenBlend.IsHeld && ResidentFingerprint == ScreenBlend.Fingerprint && !RawBlockers.IsDefault && RawBlockers.Length == 1 && RawBlockers[0] == "effect-or-auxiliary-task" && Owners.AuxiliaryHead && BattleComponentOwnerClosure.AreNonAuxiliaryOwnersReleased(Owners);
}

public sealed record BattleComponentHeldScreenBlendCertificate(string CompletionKind, bool Completed, bool Drained, bool ResidentTasksRemain, bool HeldRenderWorkRemains, ushort Effect, string SourceSha256, BattleDiagnosticEffectDispatch? NativeDispatch, BattleComponentAdmissionIdentity? NativeAdmission, BattleComponentHeldScreenBlendObservation First, BattleComponentHeldScreenBlendObservation Last, long FirstPresentedFrame, long LastPresentedFrame, long TailPresentedFrames, long NativeCallbacksAdvanced);

public sealed partial class BattleSession
{
    /// <summary>Observe the original resident constant screen blend; neither release it nor stop its native rectangle publication.</summary>
    public BattleComponentHeldScreenBlendObservation? ObserveComponentHeldScreenBlend(ushort expectedEffect)
    {
        if (NativeGraphics is not { OriginalEffect: { ReturnedDescriptor: 0 } selected } graphics || selected.RequestedEffect != expectedEffect || selected.Binding.Effect != expectedEffect || NativeEffectMemory is not { Active: 0 } memory)
        {
            return null;
        }
        nativeActionPackets.Validate();
        NativeBattleAuxiliaryState auxiliary = graphics.Auxiliary;
        auxiliary.Validate();
        int[] active = Enumerable.Range(0, auxiliary.Slots.Length).Where(slot => (auxiliary.Slots[slot].Flags & 1) != 0).ToArray();
        if (active.Length != 1 || auxiliary.Head != active[0] || auxiliary.Tail != active[0])
        {
            return null;
        }
        int index = active[0];
        NativeBattleAuxiliaryTask task = auxiliary.Slots[index];
        BattleComponentHeldScreenBlendTask held = new(index, task.Flags, task.State, task.Next, task.Kind, task.UnknownByteMask, task.Program is not null, task.Payload);
        if (!held.IsHeld)
        {
            return null;
        }
        BattleComponentHeldScreenBlendObservation observation = new(expectedEffect, graphics.Callback, 0, checked((uint)memory.ReadReturnedTask5044B0()), CaptureDiagnosticInvocationOwnerState(graphics), DiagnosticPhysicalCommandBanksMatch(null), active.Length, auxiliary.Head, auxiliary.Tail, held, held.Fingerprint, ObserveDiagnosticDrainBlockers().ToImmutableArray());
        return observation.IsEligible ? observation : null;
    }
}
