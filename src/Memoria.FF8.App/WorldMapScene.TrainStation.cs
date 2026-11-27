using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    internal const int NativeEventChoiceOperation = 0x5438D0;

    private static int TrainStation(NativeTrainRailBank bank)
    {
        //548BF0 tests the physical status, stop counter and signed direction independently.
        if (bank.Status is not (1 or 2))
        {
            return -1;
        }
        if (bank.StopCounter == 1)
        {
            return bank.TrackIndex == 0 ? 0 : 2;
        }
        return bank.TrackIndex == 0 && bank.Direction == -1 && bank.StopCounter == 0 ? 1 : -1;
    }

    private static NativeTrainRailBank PrepareTrainStationPrompt(NativeTrainRailBank bank)
    {
        //5484B0 opens channel0 only during a positive stop pause and then owns WORD28=-1.
        return TrainStation(bank) >= 0 && bank.Pause > 0 ? bank with { Pause = -1 } : bank;
    }

    public DialogueState? CurrentDialogue
    {
        get
        {
            if (State.DrawPoint is { } drawPoint)
            {
                return DrawPointDialogue(drawPoint);
            }
            if ((State.EventUi?.FocusedChoiceMessage ?? State.EventUi?.FocusedPageMessage ?? State.EventUi?.Windows.LastOrDefault()) is { } nativeMessage)
            {
                NativeWorldMessage prepared = nativeMessage.FuelRemaining is byte remaining ? assets.ReadNativeFuelMessage(remaining) : assets.ReadNativeWorldMessage(nativeMessage.WindowId, nativeMessage.MessageId);
                bool emptyFuel = nativeMessage.WindowId == 1 && nativeMessage.MessageId == 43;
                byte[] source = nativeMessage.FuelRemaining is byte fuel ? assets.ReadFuelMessageBytes(fuel) : emptyFuel ? assets.ReadFuelMessageBytes(0) : assets.ReadWorldChannelMessageBytes(nativeMessage.MessageId);
                if (prepared != (nativeMessage with { TextComplete = false, Text = null }) || nativeMessage.Text is { } text && !text.Bytes.AsSpan().SequenceEqual(source))
                {
                    throw new InvalidDataException("The saved world message differs from its installed source.");
                }
                // Confirm supplies a real digital pulse to authored world-event predicates; it does not assert native text completion or close the window itself.
                string messageText = nativeMessage.Text is { } textState ? assets.ReadWorldMessagePage(textState) : nativeMessage.FuelRemaining is byte count ? assets.ReadFuelMessage(count) : emptyFuel ? assets.ReadFuelMessage(0) : assets.ReadWorldMessage(nativeMessage.MessageId);
                if (State.EventUi?.FocusedChoice is { } choice && choice.Channel == nativeMessage.Channel)
                {
                    return new DialogueState(nativeMessage.Channel, nativeMessage.MessageId, messageText, nativeMessage.X, nativeMessage.Y, 1, 2, choice.Selection, 2, Operation: NativeEventChoiceOperation, CanConfirm: State.EventUi.CanAcceptChoice);
                }
                return new DialogueState(nativeMessage.Channel, nativeMessage.MessageId, messageText, nativeMessage.X, nativeMessage.Y, Operation: 0x543790);
            }
            if (State.NativeTrainRuntime is not { Primary.Pause: -1 } train)
            {
                return null;
            }
            int station = TrainStation(train.Primary);
            if (station < 0)
            {
                throw new NotSupportedException("Native paused train has no reached station dialogue producer.");
            }
            int message = station == 0 ? 4 : station == 1 ? 5 : 6;
            return new DialogueState(0, message, assets.ReadWorldMessage(message), 8, 8, 1, 2, 1, 2, Operation: 0x5484B0);
        }
    }
}
