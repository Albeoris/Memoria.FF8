using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
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
