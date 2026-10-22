namespace Memoria.FF8.Runtime;

public sealed class NativeBattleGilgameshProfile
{
    private NativeBattleGilgameshProfile(ushort effect)
    {
        Effect = effect;
    }

    public ushort Effect { get; }
    public int Variant => Effect == 328 ? 1 : Effect == 329 ? 2 : 3;
    public uint Entry => Effect == 328 ? 0x58D930u : Effect == 329 ? 0x58DB10u : 0x58DCF0u;
    public uint Initializer => Effect == 328 ? 0x58D700u : Effect == 329 ? 0x58D720u : 0x58D740u;
    public uint TextureGlobal => Effect == 328 ? 0x2200FB0u : Effect == 329 ? 0x2200F9Cu : 0x2200EC0u;
    public uint CameraAddress => Effect == 328 ? 0xCD069Cu : Effect == 329 ? 0xCD0788u : 0xCD0874u;
    public uint VibrationAddress => Effect == 328 ? 0xCCFF98u : Effect == 329 ? 0xCD00E4u : 0xCD0230u;
    public int VibrationLength => Effect == 328 ? 0x14C : Effect == 329 ? 0x14E : 0x14A;
    public short VibrationChannelCount => Effect == 328 ? (short)66 : Effect == 329 ? (short)68 : (short)64;
    public uint InitialStreamAddress => Effect == 328 ? 0xE8D208u : Effect == 329 ? 0xE88EF4u : 0xE84BE0u;
    public uint AttackCallback => Effect == 328 ? 0x590510u : Effect == 329 ? 0x591250u : 0x5920E0u;
    public ushort AttackFile => Effect == 328 ? (ushort)754 : Effect == 329 ? (ushort)756 : (ushort)757;
    public int AttackFileLength => Effect == 328 ? 33312 : 99360;
    public string AttackFileName => Effect == 328 ? "mag327_j.dat" : Effect == 329 ? "mag328_j.dat" : "mag329_j.dat";
    public uint ClosingSoundAddress => Effect == 328 ? 0xCD0388u : Effect == 329 ? 0xCD038Cu : 0xCD0390u;
    public uint ClosingSound => Effect == 328 ? 0x3AA9Fu : Effect == 329 ? 0x3AAA2u : 0x3AAA5u;

    public static NativeBattleGilgameshProfile ForEffect(ushort effect)
    {
        return effect is 328 or 329 or 330 ? new(effect) : throw new NotSupportedException("The Gilgamesh family requires its implemented original sword attack.");
    }
}
