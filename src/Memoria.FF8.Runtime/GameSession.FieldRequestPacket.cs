using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeFieldRequestPacket? FieldRequestPacket => State.FieldRequestPacket;
    public bool CanStoreFieldRequestPacket => true;

    public void SetFieldRequestPacket(NativeFieldRequestPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        packet.Validate();
        State = State with { FieldRequestPacket = packet };
    }

    public void SetNativeBattleEntryOwners(NativeExamTimerState? timer, NativeFieldRequestPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        packet.Validate();
        timer?.Validate();
        GameState candidate = State with { ExamTimer = timer ?? State.ExamTimer, FieldRequestPacket = packet };
        candidate = AdvanceNativePlaytime(candidate);
        ValidateLiveEventMemoryOwners(candidate, State.EventMemory);
        State = candidate;
    }

    public void RequestTimerExpiredField(NativeFieldRequestPacket expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (!ReferenceEquals(State.FieldRequestPacket, expected) || expected.Kind != 0 || State.ExamTimer is not { TimerMemoryActivated: true, Event183Mask1: false } timer || timer.Alternatives.Any(alternative => alternative.Remaining != 0) || State.WorldControls is not { } controls || (controls.EventFlags104 & 0x40u) == 0)
        {
            throw new InvalidOperationException("The native expired timer request no longer owns its exact packet, timer and display flags.");
        }
        NativeFieldRequestPacket packet = expected.WithTimerExpiry();
        FieldTransition transition = packet.ToFieldTransition();
        GameState candidate = PrepareFieldRequest(transition.FieldId, 0) with { X = transition.X, Y = transition.Y, FieldArrival = transition, FieldRequestPacket = packet };
        Dialog = null;
        Change(candidate);
    }

    private static void ValidateFieldRequestPacketCheckpoint(GameState state, JsonElement? vm)
    {
        state.FieldRequestPacket?.Validate();
        if (vm is not JsonElement snapshot)
        {
            return;
        }
        NativeFieldRequestPacket? saved = snapshot.TryGetProperty(nameof(GameState.FieldRequestPacket), out JsonElement packet) && packet.ValueKind != JsonValueKind.Null ? packet.Deserialize<NativeFieldRequestPacket>(JsonOptions) : null;
        saved?.Validate();
        if (saved != state.FieldRequestPacket)
        {
            throw new InvalidDataException("Root and active VM retained native field request packet owners disagree; migrate the required checkpoint explicitly.");
        }
    }
}
