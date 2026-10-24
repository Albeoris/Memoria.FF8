namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect209Lower
{
    public static NativeBattleEffectArena CreateCaptureContext7D30(NativeBattleEffectArena retained44, uint scratchAddress, uint rendererFlags)
    {
        return CreateCaptureContext7D30(retained44, scratchAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(rendererFlags)));
    }

    public static NativeBattleEffectArena CreateCaptureContext7D30(NativeBattleEffectArena retained44, uint scratchAddress, NativeBattleEffectArena rendererFlags4)
    {
        NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(rendererFlags4, 0, retained44, 0x10, 4);
        return Word(Word(Word(Word(Put(Put(context, 4, scratchAddress), 0x20, 0xFFFFFFFF), 0x14, 0), 0x16, 0), 0x18, 320), 0x1A, 216);
    }
}
