using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeFieldColdRegistrationTests
{
    [Theory]
    [InlineData("MEMORIA_FF8_GAME_DIR")]
    [InlineData("MEMORIA_FF8_REMASTERED_GAME_DIR")]
    public void InstalledColdProcessStartsWithAnExplicitlyFreshPlaneCache(string gameDirectoryVariable)
    {
        string? game = Environment.GetEnvironmentVariable(gameDirectoryVariable);
        Assert.SkipUnless(Directory.Exists(game), $"Select optional installed game resources with {gameDirectoryVariable}.");
        using GameAssets assets = new(game!);
        NativeBattleRetainedMemory cold = assets.ReadColdProcessNativeBattleMemory();
        Assert.True(cold.FieldTexturePlaneCacheFresh);
        Assert.Null(cold.FieldTextureAnimations);
        GameSession session = GameSession.CreateColdProcess(cold);
        session.StartNewGame();
        Assert.True(session.State.NativeBattleMemory!.FieldTexturePlaneCacheFresh);
        Assert.Null(session.State.NativeBattleMemory.FieldTextureAnimations);
    }

    [Fact]
    public void InstalledRemasteredColdProcessRegistersTheFirstFieldWithFreshPlaneState()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game) && File.Exists(Path.Combine(game!, "main.zzz")), "Select optional corresponding Remastered resources with MEMORIA_FF8_REMASTERED_GAME_DIR.");
        using GameAssets assets = new(game!);
        NativeBattleRetainedMemory cold = assets.ReadColdProcessNativeBattleMemory();
        Assert.True(cold.FieldTexturePlaneCacheFresh);
        Assert.Null(cold.FieldTextureAnimations);
        FieldAssets field = assets.LoadField(689);
        Assert.Equal(NativeFieldRuntimeProfile.RemasteredEnImageHash, field.NativeRegistration!.ExecutableHash);
        int count = field.Jsm[0] + field.Jsm[1] + field.Jsm[2] + field.Jsm[3];
        ushort group = Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(field.Jsm.AsSpan(8 + 2 * index))).OrderBy(value => value >> 7).ElementAt(3);
        NativeFieldRetainedActor[] actors = [new(3, 3, group >> 7, (group >> 7) + (group & 127) + 1)];
        NativeFieldRegistrationStep registered = NativeFieldRegistrationMachine.ReloadKind3(field.NativeRegistration, actors, cold);
        registered.State.Validate(registered.Memory);
        Assert.False(registered.Memory.FieldTexturePlaneCacheFresh);
        Assert.NotNull(registered.Memory.FieldTextureAnimations!.Planes);
        Assert.Throws<NotSupportedException>(() => NativeFieldRegistrationMachine.ReloadKind3(field.NativeRegistration, actors, cold with { FieldTexturePlaneCacheFresh = null }));
        Assert.Throws<NotSupportedException>(() => NativeFieldRegistrationMachine.ReloadKind3(field.NativeRegistration, actors, registered.Memory with { FieldTextureAnimations = null }));
    }
}
