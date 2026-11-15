namespace Memoria.FF7.Runtime;

internal static class Ff7ChocoboAutoPlay
{
    internal static ushort ChooseInput(Ff7ChocoboRuntime race)
    {
        Ff7ChocoboRacerState player = race.Player;
        ushort held = (ushort)(0xA0 | (race.RecoverySide ? 8 : 2));
        if (player.Automatic != 0)
        {
            held |= 0x100;
        }
        if (race.SecondFinished)
        {
            held |= 0x800;
        }
        return held;
    }
}
