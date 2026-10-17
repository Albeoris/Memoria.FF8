using Memoria.FF8.Runtime;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeActorInfoArenaTests
{
    [Fact]
    public void InstalledDefinitionUsesEnabledResourceLengthsAndCachesAcrossAssetOwners()
    {
        string directory = TestResources.GameDirectory;
        using ClassicGameDataSource source = new(directory);
        ResourceService resources = new(source);
        using GameAssets first = new(directory, resources: resources);
        NativeActorInfoArenaDefinition definition = first.ReadNativeActorInfoArena();
        definition.Validate();
        Assert.InRange(definition.MaximumDeclaredRecords, 1, byte.MaxValue);
        Assert.Equal(resources.Read(new ResourceKey("main/kernel.bin")).Length, definition.KernelBytes);
        Assert.Equal(resources.Read(new ResourceKey("main/wm2field.tbl")).Length, definition.WorldTableBytes);
        using GameAssets restored = new(directory, resources: resources);
        Assert.Same(definition, restored.ReadNativeActorInfoArena());
        Assert.Equal(0, resources.ResidentTextureCount);
    }

    [Fact]
    public void HeaderCertificateRequiresTheAuthenticatedRuntimeAndCountsEveryRecordKind()
    {
        byte[] header = [1, 2, 3, 4, 28, 0, 32, 0];
        Assert.Equal(10, NativeActorInfoArenaReader.ReadDeclaredRecords(NativeFieldRuntimeProfile.FF8En, header, 64));
        Assert.Throws<NotSupportedException>(() => NativeActorInfoArenaReader.ReadDeclaredRecords(null, header, 64));
        Assert.Throws<NotSupportedException>(() => NativeActorInfoArenaReader.ReadDeclaredRecords(NativeFieldRuntimeProfile.FF8En with { Version = 2 }, header, 64));
        Assert.Throws<NotSupportedException>(() => NativeActorInfoArenaReader.ReadDeclaredRecords(NativeFieldRuntimeProfile.FF8En with { ExecutableHash = new string('0', 64) }, header, 64));
    }

    [Fact]
    public void MalformedOrOversizedDeclaredTablesCannotCertifyAnArena()
    {
        Assert.Throws<InvalidDataException>(() => NativeActorInfoArenaReader.ReadDeclaredRecords(NativeFieldRuntimeProfile.FF8En, new byte[7], 64));
        Assert.Throws<InvalidDataException>(() => NativeActorInfoArenaReader.ReadDeclaredRecords(NativeFieldRuntimeProfile.FF8En, [64, 64, 64, 64, 8, 2, 16, 2], 1024));
        Assert.Throws<InvalidDataException>(() => NativeActorInfoArenaReader.ReadDeclaredRecords(NativeFieldRuntimeProfile.FF8En, [1, 2, 3, 4, 8, 0, 32, 0], 64));
        Assert.Throws<InvalidDataException>(() => NativeActorInfoArenaReader.ReadDeclaredRecords(NativeFieldRuntimeProfile.FF8En, [1, 2, 3, 4, 28, 0, 24, 0], 64));
        Assert.Throws<InvalidDataException>(() => NativeActorInfoArenaReader.ReadDeclaredRecords(NativeFieldRuntimeProfile.FF8En, [1, 2, 3, 4, 28, 0, 68, 0], 64));
    }
}
