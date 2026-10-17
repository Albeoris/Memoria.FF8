using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleQuezacotlParticleFlight
{
    public static NativeBattleQuezacotlParticleSpawn SpawnFirst88(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native6C3E69 requires its original controller coordinates.");
        short x = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(0x44, 2));
        short z = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(0x48, 2));
        ushort random = effect.Quezacotl!.Random17A0;
        NativeBattleVector start = new(unchecked((short)(x + (Next() & 1023) - 512)), 0, unchecked((short)(z + (Next() & 1023) - 512)));
        NativeBattleVector target = new(unchecked((short)((Next() & 2047) - 1024)), unchecked((short)(-2048 - (Next() & 1023))), unchecked((short)(start.Z + (Next() & 2047) - 1024)));
        NativeBattleVector drift = new(unchecked((short)((Next() & 127) - 64)), unchecked((short)(-64 - (Next() & 63))), unchecked((short)((Next() & 127) - 64)));
        effect = effect with { Quezacotl = effect.Quezacotl! with { Random17A0 = random } };
        return NativeBattleQuezacotlParticleProducer.Spawn6C7D10(memory, effect, new(drift, start, target, null, 0, 0, 256, 250, 0, 3));

        ushort Next() => NativeBattleQuezacotlPrivateRandom.Draw6C7CF0(ref random);
    }
}
