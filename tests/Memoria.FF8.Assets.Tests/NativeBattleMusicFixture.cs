using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

internal static class NativeBattleMusicFixture
{
    internal static NativeBattleMusicEntryPreparation Prepare(GameAssets game, int encounter)
    {
        //This fresh component declares source BYTE201=0 and transfers selected bank1 through52CE30.
        //Unselected buffer identities and unrelated pending fields remain unknown.
        NativeMusicProcessState sourceBuffers = new(1, null, null, null, 0, null);
        NativeBattleMusicEntryResources resources = new NativeMusicCatalog(game.GameDirectory).ReadBattleEntryResources();
        return NativeBattleMusicEntry.Prepare52CE30(checked((ushort)encounter), 0x80, sourceBuffers, 5, -1, NativeGlobalSoundProcess.FromBufferlessColdProcess(), resources);
    }
}
