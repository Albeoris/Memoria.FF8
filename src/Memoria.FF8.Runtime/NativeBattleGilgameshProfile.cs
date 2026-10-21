namespace Memoria.FF8.Runtime;

public sealed class NativeBattleGilgameshProfile
{
    private NativeBattleGilgameshProfile(ushort effect)
    {
        Effect = effect;
    }

    public ushort Effect { get; }
    public int Variant => Effect == 329 ? 2 : 3;
    public uint Entry => Effect == 329 ? 0x58DB10u : 0x58DCF0u;
    public uint Initializer => Effect == 329 ? 0x58D720u : 0x58D740u;
    public uint TextureGlobal => Effect == 329 ? 0x2200F9Cu : 0x2200EC0u;
    public uint CameraAddress => Effect == 329 ? 0xCD0788u : 0xCD0874u;
    public uint VibrationAddress => Effect == 329 ? 0xCD00E4u : 0xCD0230u;
    public int VibrationLength => Effect == 329 ? 0x14E : 0x14A;
    public short VibrationChannelCount => Effect == 329 ? (short)68 : (short)64;
    public uint InitialStreamAddress => Effect == 329 ? 0xE88EF4u : 0xE84BE0u;
    public uint AttackCallback => Effect == 329 ? 0x591250u : 0x5920E0u;
    public ushort AttackFile => Effect == 329 ? (ushort)756 : (ushort)757;
    public uint ClosingSoundAddress => Effect == 329 ? 0xCD038Cu : 0xCD0390u;
    public uint ClosingSound => Effect == 329 ? 0x3AAA2u : 0x3AAA5u;

    public static NativeBattleGilgameshProfile ForEffect(ushort effect)
    {
        return effect is 329 or 330 ? new(effect) : throw new NotSupportedException("The Gilgamesh family requires its implemented original sword attack.");
    }
}
