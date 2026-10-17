using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleQuezacotlParticleFlight
{
    public static NativeBattleQuezacotlParticleSpawn Spawn6C4043(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
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
    public static short SelectVertex12AFE40(NativeBattleQuezacotlResources resources, ref ushort random)
    {
        var vertices = resources.AnchorVertexIndices11 ?? throw new NotSupportedException("Native6C41D2 requires authenticated11WORD12AFE40 vertex selectors; explicitly migrate the needed standalone metadata.");
        if (vertices.IsDefault || vertices.Length != 11)
        {
            throw new InvalidDataException("Native12AFE40 requires its exact11 signed WORD selectors.");
        }
        return vertices[NativeBattleQuezacotlPrivateRandom.Draw6C7CF0(ref random) * 11 >> 15];
    }

    public static NativeBattleQuezacotlParticleSpawn Spawn6C41BF(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        NativeBattleQuezacotlResources resources = effect.Binding.NativeQuezacotlResources ?? throw new NotSupportedException("Native6C41D2 requires its actual Quezacotl resource binding.");
        ushort random = effect.Quezacotl!.Random17A0;
        short vertex = SelectVertex12AFE40(resources, ref random);
        effect = effect with { Quezacotl = effect.Quezacotl! with { Random17A0 = random } };
        NativeBattleQuezacotlAnchor anchor = NativeBattleQuezacotlRig86.ReadMeshAnchor6C6440(memory, effect, 0, vertex);
        return SpawnFromAnchor6C41E2(anchor.Memory, anchor.Effect, anchor.Position);
    }

    public static NativeBattleQuezacotlParticleSpawn SpawnFromAnchor6C41E2(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleVector start)
    {
        ushort random = effect.Quezacotl!.Random17A0;
        NativeBattleVector target = new(unchecked((short)(start.X + (Next() & 2047) - 1024)), unchecked((short)(start.Y - (Next() & 1023) - 1024)), unchecked((short)(start.Z + (Next() & 2047) - 1024)));
        NativeBattleVector drift = new(unchecked((short)((Next() & 127) - 64)), unchecked((short)(-64 - (Next() & 63))), unchecked((short)((Next() & 127) - 64)));
        effect = effect with { Quezacotl = effect.Quezacotl! with { Random17A0 = random } };
        return NativeBattleQuezacotlParticleProducer.Spawn6C7D10(memory, effect, new(drift, start, target, null, 0, 0, 256, 250, 0, 3));

        ushort Next() => NativeBattleQuezacotlPrivateRandom.Draw6C7CF0(ref random);
    }
}
