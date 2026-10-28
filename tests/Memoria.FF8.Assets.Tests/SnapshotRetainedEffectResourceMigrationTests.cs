using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class SnapshotRetainedEffectResourceMigrationTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter(), new ImmutableBytesJsonConverter() } };

    [Theory]
    [InlineData("OilBlast137")]
    [InlineData("BadBreath131")]
    [InlineData("Saliva138")]
    [InlineData("MagmaBreath48")]
    [InlineData("BladeShot12")]
    [InlineData("OilShot136")]
    [InlineData("ElectricDischarge126")]
    [InlineData("Effect301")]
    [InlineData("Effect308")]
    [InlineData("Effect251")]
    [InlineData("Effect83")]
    [InlineData("Effect345")]
    [InlineData("Effect213")]
    [InlineData("Effect290")]
    [InlineData("Effect54")]
    [InlineData("Effect79")]
    [InlineData("Effect80")]
    [InlineData("Effect68")]
    public void Historical_unknown_owner_migrates_before_binding_installed_sources_and_retains_bound_JSON(string owner)
    {
        using GameAssets game = Open();
        switch (owner)
        {
            case "OilBlast137":
                Verify("OilBlast137", NativeBattleOilBlast137State.FromUnknown, NativeBattleOilBlast137State.FromColdProcess, memory => memory.OilBlast137!, (memory, state) => memory with { OilBlast137 = state }, (state, retained) => (state.SeedColdSource(game.ReadNativeOilBlast137Resources()), retained));
                break;
            case "BadBreath131":
                Verify("BadBreath131", NativeBattleBadBreath131State.FromUnknown, NativeBattleBadBreath131State.FromColdProcess, memory => memory.BadBreath131!, (memory, state) => memory with { BadBreath131 = state }, (state, retained) => (state.SeedColdSources(game.ReadNativeBadBreath131Resources()), retained));
                break;
            case "Saliva138":
                Verify("Saliva138", NativeBattleSaliva138State.FromUnknown, NativeBattleSaliva138State.FromColdProcess, memory => memory.Saliva138!, (memory, state) => memory with { Saliva138 = state }, (state, retained) => (state.SeedColdSources(game.ReadNativeSaliva138Resources()), retained));
                break;
            case "MagmaBreath48":
                Verify("MagmaBreath48", NativeBattleMagmaBreath48State.FromUnknown, NativeBattleMagmaBreath48State.FromColdProcess, memory => memory.MagmaBreath48!, (memory, state) => memory with { MagmaBreath48 = state }, (state, retained) => (state.EnsureSources(game.ReadNativeMagmaBreath48Resources()), retained));
                break;
            case "BladeShot12":
                Verify("BladeShot12", NativeBattleBladeShot12State.FromUnknown, NativeBattleBladeShot12State.FromColdProcess, memory => memory.BladeShot12!, (memory, state) => memory with { BladeShot12 = state }, (state, retained) => (state.EnsureVertexSources(game.ReadNativeBladeShot12Resources()), retained));
                break;
            case "OilShot136":
                Verify("OilShot136", NativeBattleOilShot136State.FromUnknown, NativeBattleOilShot136State.FromColdProcess, memory => memory.OilShot136!, (memory, state) => memory with { OilShot136 = state }, (state, retained) => (state.SeedColdSources(game.ReadNativeOilShot136Resources()), retained));
                break;
            case "ElectricDischarge126":
                Verify("ElectricDischarge126", NativeBattleElectricDischarge126State.FromUnknown, NativeBattleElectricDischarge126State.FromColdProcess, memory => memory.ElectricDischarge126!, (memory, state) => memory with { ElectricDischarge126 = state }, (state, retained) => (state.SeedColdSources(game.ReadNativeElectricDischarge126Resources()), retained));
                break;
            case "Effect301":
                Verify("Effect301", NativeBattleEffect301State.FromUnknown, NativeBattleEffect301State.FromColdProcess, memory => memory.Effect301!, (memory, state) => memory with { Effect301 = state }, (state, retained) => (state.BindSources(game.ReadNativeEffect301Resources()), retained));
                break;
            case "Effect308":
                Verify("Effect308", NativeBattleEffect308State.FromUnknown, NativeBattleEffect308State.FromColdProcess, memory => memory.Effect308!, (memory, state) => memory with { Effect308 = state }, (state, retained) => (state.BindOriginalScriptSources(game.ReadNativeEffect308Resources()), retained));
                break;
            case "Effect251":
                Verify("Effect251", NativeBattleEffect251State.FromUnknown, NativeBattleEffect251State.FromColdProcess, memory => memory.Effect251!, (memory, state) => memory with { Effect251 = state }, (state, retained) => (state.BindOriginalScriptSources(game.ReadNativeEffect251Resources()), retained));
                break;
            case "Effect83":
                Verify("Effect83", NativeBattleEffect83State.FromUnknown, NativeBattleEffect83State.FromColdProcess, memory => memory.Effect83!, (memory, state) => memory with { Effect83 = state }, (state, retained) => (state.BindNodeSource(game.ReadNativeEffect83Resources()), retained));
                break;
            case "Effect345":
                Verify("Effect345", NativeBattleEffect345State.FromUnknown, NativeBattleEffect345State.FromColdProcess, memory => memory.Effect345!, (memory, state) => memory with { Effect345 = state }, (state, retained) => (state.BindNodeSources(game.ReadNativeEffect345Resources()), retained));
                break;
            case "Effect213":
                Verify("Effect213", NativeBattleEffect213State.FromUnknown, NativeBattleEffect213State.FromColdProcess, memory => memory.Effect213!, (memory, state) => memory with { Effect213 = state }, (state, retained) =>
                {
                    var bound = state.BindSources(retained.FileStaging!, game.ReadNativeEffect213Resources());
                    return (bound.State, retained with { FileStaging = bound.Staging });
                });
                break;
            case "Effect290":
                Verify("Effect290", NativeBattleEffect290State.FromUnknown, NativeBattleEffect290State.FromColdProcess, memory => memory.Effect290!, (memory, state) => memory with { Effect290 = state }, (state, retained) =>
                {
                    var bound = state.BindSources(retained.FileStaging!, game.ReadNativeEffect290Resources());
                    return (bound.State, retained with { FileStaging = bound.Staging });
                });
                break;
            case "Effect54":
                Verify("Effect54", NativeBattleEffect54State.FromUnknown, NativeBattleEffect54State.FromColdProcess, memory => memory.Effect54!, (memory, state) => memory with { Effect54 = state }, (state, retained) =>
                {
                    var bound = state.BindSources(retained.FileStaging!, game.ReadNativeEffect54Resources());
                    return (bound.State, retained with { FileStaging = bound.Staging });
                });
                break;
            case "Effect79":
                Verify("Effect79", NativeBattleEffect79State.FromUnknown, NativeBattleEffect79State.FromColdProcess, memory => memory.Effect79!, (memory, state) => memory with { Effect79 = state }, (state, retained) => (state.BindProgram(game.ReadNativeEffect79Resources()).BindNodeSource(0x176A958, game.ReadNativeEffect79Resources()), retained));
                break;
            case "Effect80":
                Verify("Effect80", NativeBattleEffect80State.FromUnknown, NativeBattleEffect80State.FromColdProcess, memory => memory.Effect80!, (memory, state) => memory with { Effect80 = state }, (state, retained) =>
                {
                    NativeBattleEffect80Resources resources = game.ReadNativeEffect80Resources();
                    foreach (uint address in NativeBattleEffect80Profile.NodeSourceLengths.Keys)
                    {
                        state = state.BindNodeSource(resources, address);
                    }
                    foreach (uint address in NativeBattleEffect80Profile.InitializedSourceLengths.Keys)
                    {
                        state = state.BindInitializedSource(resources, address);
                    }
                    return (state, retained);
                });
                break;
            case "Effect68":
                Verify("Effect68", NativeBattleEffect68State.FromUnknown, NativeBattleEffect68State.FromColdProcess, memory => memory.Effect68!, (memory, state) => memory with { Effect68 = state }, (state, retained) =>
                {
                    var bound = state.BindMeshSources(retained.EffectMemory!, game.ReadNativeEffect68Resources());
                    return (bound.State, retained with { EffectMemory = bound.Memory });
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(owner));
        }
    }

    [Fact]
    public void Effect320_migration_establishes_cold_origin_for_its_controller_only_source_gate()
    {
        using GameAssets game = Open();
        NativeBattleEffect320Resources resources = game.ReadNativeEffect320Resources();
        resources.Validate();
        NativeBattleEffect320State unknown = NativeBattleEffect320State.FromUnknown();
        Assert.Null(unknown.MutableAnchorSourceBound);
        NativeBattleRetainedMemory retained = Migrate("Effect320", unknown);
        NativeBattleEffect320State migrated = retained.EffectMemory!.Effect320!;
        Assert.Equal(StateJson(NativeBattleEffect320State.FromColdProcess()), StateJson(migrated));
        Assert.False(migrated.MutableAnchorSourceBound);
        Assert.Equal(2, resources.ReadStatic(NativeBattleEffect320Profile.MutableAnchorAddress, 2).Length);
        Assert.Equal(StateJson(migrated), StateJson(RoundTrip(retained).EffectMemory!.Effect320!));
        // Initialize owns this binding and also requires physical models, parameters and COMMON resources.
        // NativeBattleEffect320ControllerTests and the installed presentation cover that execution boundary.
    }

    private static void Verify<T>(string owner, Func<T> unknownFactory, Func<T> coldFactory, Func<NativeBattleEffectMemory, T> read, Func<NativeBattleEffectMemory, T, NativeBattleEffectMemory> write, Func<T, NativeBattleRetainedMemory, (T State, NativeBattleRetainedMemory Retained)> bind) where T : class
    {
        T unknown = unknownFactory();
        NativeBattleRetainedMemory migrated = Migrate(owner, unknown);
        T cold = read(migrated.EffectMemory!);
        Assert.Equal(StateJson(coldFactory()), StateJson(cold));

        Exception? refusal = Record.Exception(() => bind(unknown, migrated));
        Assert.True(refusal is NotSupportedException or InvalidDataException, owner + " must reject unknown source provenance before migration: " + refusal);

        var bound = bind(cold, migrated);
        NativeBattleRetainedMemory warm = bound.Retained with { EffectMemory = write(bound.Retained.EffectMemory!, bound.State) };
        Assert.NotEqual(StateJson(coldFactory()), StateJson(read(warm.EffectMemory!)));
        string expected = RetainedJson(warm);
        NativeBattleRetainedMemory restored = RoundTrip(warm);
        Assert.Equal(expected, RetainedJson(restored));
        var rebound = bind(read(restored.EffectMemory!), restored);
        NativeBattleRetainedMemory result = rebound.Retained with { EffectMemory = write(rebound.Retained.EffectMemory!, rebound.State) };
        Assert.Equal(expected, RetainedJson(result));
    }

    private static NativeBattleRetainedMemory Migrate<T>(string owner, T unknown)
    {
        GameState source = GameSession.CreateColdProcess().State;
        JsonObject saved = JsonSerializer.SerializeToNode(source, Options)!.AsObject();
        saved["SnapshotSchemaVersion"] = 3;
        saved["NativeBattleMemory"]!["EffectMemory"]![owner] = JsonSerializer.SerializeToNode(NativeBattleStorageSnapshots.Capture(unknown), Options);
        using MemoryStream input = new(Encoding.UTF8.GetBytes(saved.ToJsonString(Options)));
        GameState migrated = SnapshotDefaults.Deserialize(input, Options);
        Assert.Equal(GameState.CurrentSnapshotSchemaVersion, migrated.SnapshotSchemaVersion);
        return NativeBattleStorageSnapshots.Restore(migrated.NativeBattleMemory!);
    }

    private static NativeBattleRetainedMemory RoundTrip(NativeBattleRetainedMemory retained)
    {
        NativeBattleRetainedMemory snapshot = NativeBattleStorageSnapshots.Capture(retained);
        NativeBattleRetainedMemory restored = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(snapshot, Options), Options)!;
        return NativeBattleStorageSnapshots.Restore(restored);
    }

    private static string StateJson<T>(T state) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state), Options);

    private static string RetainedJson(NativeBattleRetainedMemory retained) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(retained), Options);

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for retained-owner migration acceptance.");
        return new GameAssets(directory!);
    }
}

