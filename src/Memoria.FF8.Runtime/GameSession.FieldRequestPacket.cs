using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeFieldRequestPacket? FieldRequestPacket => State.FieldRequestPacket;
    public bool CanStoreFieldRequestPacket => true;
    public NativeGameOverReceipt? NativeGameOver => State.NativeGameOver;
    public bool CanStoreNativeGameOver => true;

    public void SetNativeGameOver(NativeGameOverReceipt? receipt)
    {
        receipt?.Validate();
        if (receipt is not null && State.FieldRequestPacket?.Kind != 4)
        {
            throw new InvalidOperationException("Native game-over acceptance requires the final request-kind4 packet.");
        }
        State = State with { NativeGameOver = receipt };
    }

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
        state.NativeGameOver?.Validate();
        if (state.NativeGameOver is { } loss && (state.Scene != SceneKind.Field || loss.FieldId != state.FieldId || state.FieldRequestPacket?.Kind != 4))
        {
            throw new InvalidDataException("Native terminal loss requires its issuing field and final request-kind4 packet.");
        }
        if (vm is not JsonElement snapshot)
        {
            return;
        }
        NativeFieldRequestPacket? saved = snapshot.TryGetProperty(nameof(GameState.FieldRequestPacket), out JsonElement packet) && packet.ValueKind != JsonValueKind.Null ? packet.Deserialize<NativeFieldRequestPacket>(JsonOptions) : null;
        saved?.Validate();
        NativeGameOverReceipt? savedLoss = snapshot.TryGetProperty(nameof(GameState.NativeGameOver), out JsonElement lossReceipt) && lossReceipt.ValueKind != JsonValueKind.Null ? lossReceipt.Deserialize<NativeGameOverReceipt>(JsonOptions) : null;
        if (savedLoss != state.NativeGameOver)
        {
            throw new InvalidDataException("Root and active VM native game-over receipt owners disagree.");
        }
        if (saved != state.FieldRequestPacket)
        {
            throw new InvalidDataException("Root and active VM retained native field request packet owners disagree; migrate the required checkpoint explicitly.");
        }
    }
}
