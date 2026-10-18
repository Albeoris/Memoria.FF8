using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private NativeBattleGilgameshEffectResources? nativeGilgameshEffectResources;

    public NativeBattleGilgameshEffectResources ReadNativeGilgameshEffectResources() => nativeGilgameshEffectResources ??= new(ReadNativeGilgameshResources(), ReadNativeGilgameshModelResources(), ReadNativeGilgameshDirectorResources(), ReadNativeGilgameshFlareResources(), ReadNativeGilgameshOpeningResources(), ReadNativeGilgameshParticleResources(), ReadNativeGilgameshMeshAuxiliaryResources(), ReadNativeGilgameshCommonParticleResources(), ReadNativeGilgameshPointResources(), ReadNativeGilgameshFinalRingResource(), ReadNativeGilgameshOpeningFlareResources(), System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(new NativeExecutableMemory(GameDataResources.ReadNativeExecutable(resources)).Read(0xE84BE0, 4)), ReadNativeGilgameshSwordParticleResources(), ReadNativeGilgameshSwordTrailResources());
}
