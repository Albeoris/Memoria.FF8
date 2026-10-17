using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMusicEntryResourcesTests
{
    [Fact]
    public void Original_request_and_encounter_table_select_the_actual_AKAO_byte_and_three_special_SEs()
    {
        NativeMusicCatalog catalog = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleMusicEntryResources resources = catalog.ReadBattleEntryResources();
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, resources.ExecutableSha256);
        Assert.Equal(36, resources.Template36.Length);
        Assert.Equal(24, resources.MusicByteOffset);
        Assert.Equal(43, resources.SpecialEncounters.Length);
        Assert.Contains((ushort)94, resources.SpecialEncounters);
        Assert.DoesNotContain((ushort)512, resources.SpecialEncounters);
        Assert.DoesNotContain(ushort.MaxValue, resources.SpecialEncounters);
        Assert.Equal(Convert.FromHexString("414B414F0600900B"), resources.SelectPayload(5));
        Assert.Equal(Convert.FromHexString("414B414F0000900B"), resources.SelectPayload(255));
        NativeMusicProcessState buffers = new(1, null, null, null, 0, null);
        foreach (ushort encounter in new ushort[] { 94, 512 })
        {
            NativeBattleMusicEntryPreparation entry = NativeBattleMusicEntry.Prepare52CE30(encounter, 0, buffers, 5, 0, NativeGlobalSoundProcess.FromBufferlessColdProcess(), resources);
            Assert.Equal(encounter == 94 ? new uint[] { 132, 133, 134 } : new uint[] { 10, 11, 12 }, entry.Receipt.Sounds.Select(sound => sound.Sound));
            Assert.Equal(5, entry.Receipt.Playback!.MusicId);
            Assert.Equal(new NativeMusicBufferIdentity(6, NativeMusicBufferProducer.Battle52CE30), entry.MusicProcess.Buffers.ReadBuffer(1));
        }
    }
}
