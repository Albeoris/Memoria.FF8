using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private readonly Dictionary<ushort, NativeBattleGilgameshEffectResources> nativeGilgameshEffectResourcesByEffect = [];

    public NativeBattleGilgameshEffectResources ReadNativeGilgameshEffectResources(ushort effect = 330)
    {
        NativeBattleGilgameshProfile profile = NativeBattleGilgameshProfile.ForEffect(effect);
        if (!nativeGilgameshEffectResourcesByEffect.TryGetValue(effect, out NativeBattleGilgameshEffectResources? selected))
        {
            selected = new(ReadNativeGilgameshResources(effect), ReadNativeGilgameshModelResources(), ReadNativeGilgameshDirectorResources(effect), ReadNativeGilgameshFlareResources(), ReadNativeGilgameshOpeningResources(), ReadNativeGilgameshParticleResources(), ReadNativeGilgameshMeshAuxiliaryResources(), ReadNativeGilgameshCommonParticleResources(), ReadNativeGilgameshPointResources(), ReadNativeGilgameshFinalRingResource(), ReadNativeGilgameshOpeningFlareResources(), System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(new NativeExecutableMemory(GameDataResources.ReadNativeExecutable(resources)).Read(profile.InitialStreamAddress, 4)), ReadNativeGilgameshSwordParticleResources(), ReadNativeGilgameshSwordTrailResources());
            nativeGilgameshEffectResourcesByEffect.Add(effect, selected);
        }
        return selected;
    }
}
