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
    public BattleComponentHeldScreenBlendObservation? ObserveComponentHeldScreenBlend(ushort expectedEffect)
    {
        if (Presentation is not { } process || ObserveDiagnosticDrainBlockers() is not ["effect-or-auxiliary-task"])
        {
            return null;
        }
        return process.ObserveComponentHeldScreenBlend(expectedEffect, CaptureDiagnosticInvocationOwnerState(process.Graphics),
            ActiveAction is null && actions.Count == 0 && expandedHostedEvent is null, ["effect-or-auxiliary-task"]);
    }
}
