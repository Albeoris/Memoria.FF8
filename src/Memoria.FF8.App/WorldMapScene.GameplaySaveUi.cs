using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    internal void RestoreGameplayUi(GameplaySaveWorldUi saved)
    {
        saved.Validate();
        if (State.Frame != 0 || State.NativeScriptedRuntime is not null || State.NativeTrainRuntime is not null || State.EventUi is not { ScriptMode: 0, Windows.Length: 0, ClosingChannels.Length: 0 })
        {
            throw new InvalidOperationException("Gameplay world UI restoration requires a fresh ordinary world allocation.");
        }
        NativeWorldMessage RestoreMessage(GameplaySaveWorldMessage message)
        {
            NativeWorldMessage binding = message.FuelRemaining is byte fuel ? assets.ReadNativeFuelMessage(fuel) : assets.ReadNativeWorldMessage(message.WindowId, message.MessageId);
            if (binding.WindowId != message.WindowId || binding.MessageId != message.MessageId || !binding.SourceSha256.Equals(message.SourceSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Gameplay world message differs from the selected game's source.");
            }
            NativeWorldTextState? text = null;
            if (message.Text is { } progress)
            {
                byte[] bytes = message.FuelRemaining is byte remaining ? assets.ReadFuelMessageBytes(remaining) : message.WindowId == 1 && message.MessageId == 43 ? assets.ReadFuelMessageBytes(0) : assets.ReadWorldMessageBytes(message.MessageId);
                text = new(ImmutableArray.CreateRange(bytes), progress.Position, progress.Speed, progress.Accumulator, progress.Openness, progress.Phase, progress.PauseTicks, progress.PageCounter, progress.Complete);
                text.Validate();
            }
            return binding with { Text = text, TextComplete = message.TextComplete };
        }
        NativeWorldEventUi ui = new(0, saved.CloseSuppressed, saved.Windows.Select(RestoreMessage).ToImmutableArray())
        {
            AliasedWindows = saved.AliasedWindows.Select(RestoreMessage).ToImmutableArray(),
            ClosingChannels = saved.ClosingChannels.ToImmutableArray(),
            RetainedChannelReadiness = saved.RetainedChannelReadiness.ToImmutableArray(),
            PageInputChannel = saved.PageInputChannel
        };
        ui.Validate();
        NativeWorldProcess process = session.State.WorldProcess ?? throw new NotSupportedException("Gameplay world UI requires its initialized process.");
        session.Restore(session.State with { World = State with { EventUi = ui }, WorldProcess = process with { FuelMessageMovementCallbacks2045C78 = saved.FuelMovementCallbacks } });
        _ = CurrentDialogue;
    }
}
