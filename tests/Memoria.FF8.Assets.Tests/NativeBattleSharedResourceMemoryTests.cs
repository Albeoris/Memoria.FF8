using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleSharedResourceMemoryTests
{
    private sealed class CloneCounter : INativeBattleTelemetrySink
    {
        public long Bytes { get; private set; }

        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            if (siteId == 1043 && kind == NativeBattleTrafficKind.CpuCopyPayload)
            {
                Bytes = checked(Bytes + payloadBytes);
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(102)]
    [InlineData(103)]
    [InlineData(104)]
    [InlineData(105)]
    [InlineData(142)]
    [InlineData(143)]
    [InlineData(144)]
    public void Shared_authenticated_source_preserves_complete_resources_without_recloning_the_executable(int effect)
    {
        byte[] executable = File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe"));
        NativeExecutableMemory memory = new(executable);
        CloneCounter sharedCopies = new();
        object shared;
        using (NativeBattleTelemetry.BeginScope(sharedCopies))
        {
            shared = ReadSource(memory);
        }
        CloneCounter privateCopies = new();
        object isolated;
        using (NativeBattleTelemetry.BeginScope(privateCopies))
        {
            isolated = ReadPrivate(executable);
        }
        Assert.Equal(JsonSerializer.Serialize(isolated, isolated.GetType()), JsonSerializer.Serialize(shared, shared.GetType()));
        Assert.Equal(0, sharedCopies.Bytes);
        Assert.Equal(executable.LongLength, privateCopies.Bytes);

        object ReadSource(IGameNativeMemorySource source) => effect switch
        {
            1 => NativeBattleCureResourcesReader.Read(source),
            3 => NativeBattleEffect3ResourcesReader.Read(source),
            102 => NativeBattleThundaraResourcesReader.Read(source),
            103 => NativeBattleBlizzaraResourcesReader.Read(source),
            104 => NativeBattleBlizzagaResourcesReader.Read(source),
            105 => NativeBattleThundaga105ResourcesReader.Read(source),
            142 => NativeBattleFiraResourcesReader.Read(source),
            143 => NativeBattleFiragaResourcesReader.Read(source),
            144 => NativeBattleBlizzardResourcesReader.Read(source),
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };

        object ReadPrivate(byte[] source) => effect switch
        {
            1 => NativeBattleCureResourcesReader.Read(source),
            3 => NativeBattleEffect3ResourcesReader.Read(source),
            102 => NativeBattleThundaraResourcesReader.Read(source),
            103 => NativeBattleBlizzaraResourcesReader.Read(source),
            104 => NativeBattleBlizzagaResourcesReader.Read(source),
            105 => NativeBattleThundaga105ResourcesReader.Read(source),
            142 => NativeBattleFiraResourcesReader.Read(source),
            143 => NativeBattleFiragaResourcesReader.Read(source),
            144 => NativeBattleBlizzardResourcesReader.Read(source),
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
    }

    [Fact]
    public void Public_executable_snapshot_remains_isolated_from_later_caller_and_read_buffer_mutation()
    {
        byte[] executable = File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe"));
        NativeExecutableMemory memory = new(executable);
        byte[] expected = memory.Read(0x1641F78, 12);
        executable[0] ^= 1;
        byte[] read = memory.Read(0x1641F78, 12);
        read[0] ^= 1;
        Assert.Equal(expected, memory.Read(0x1641F78, 12));
        Assert.Throws<NotSupportedException>(() => new NativeExecutableMemory(executable));
    }
}
