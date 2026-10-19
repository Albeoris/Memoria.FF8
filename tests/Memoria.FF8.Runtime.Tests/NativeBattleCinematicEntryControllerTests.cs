using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicEntryControllerTests
{
    [Theory]
    [InlineData(6)]
    [InlineData(201)]
    [InlineData(202)]
    [InlineData(203)]
    [InlineData(204)]
    [InlineData(205)]
    [InlineData(206)]
    public void Entry_selects_actual_publications_source_base_and_physical_pool(int effect)
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect);
        var fixture = NativeBattleIfritEntryTests.Fixture();
        NativeBattleOriginalEffectState selected = fixture.Effect with
        {
            RequestedEffect = checked((ushort)effect),
            SelectedIndex = effect - 1,
            Binding = new(checked((ushort)effect), profile.Entry, profile.Initializer),
            ResourcePointers = [new(profile.File0Publication, fixture.Inputs.Resource0.ArenaOffset), new(profile.File1Publication, fixture.Inputs.Resource1.ArenaOffset)]
        };
        NativeBattleWaterInitializationInputs inputs = fixture.Inputs with { InitializationDirectoryAddress = profile.InitializationAddress };
        NativeBattleCinematicEntryStep result = NativeBattleCinematicEntry.Initialize(profile, fixture.Memory, selected, fixture.Directories, inputs);
        NativeBattleEffectTaskPool pool = Assert.Single(result.Effect.Pools);
        Assert.Equal(profile.QueueAddress, result.Effect.ReturnedDescriptor);
        Assert.Equal(profile.BankAddress, pool.BankAddress);
        Assert.Equal(profile.Callback, Assert.Single(pool.Slots).Callback);
        Assert.Equal(profile.SourceBase, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.ScriptedEffects!.Read(0x2797690 + 0xA0, 2)));
        Assert.Equal(profile.InitializationAddress + 0x20u, BinaryPrimitives.ReadUInt32LittleEndian(result.Memory.ScriptedEffects.Read(0x2797690 + 0x98, 4)));
        Assert.Equal(effect, result.Memory.Cinematic!.Effect);
        Assert.Equal(effect == 201, result.Memory.Ifrit201 is not null);
        string json = JsonSerializer.Serialize(result.Memory);
        Assert.DoesNotContain("\"Ifrit201\"", json);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleEffectMemory>(json)));
        Assert.True(NativeBattleCinematicProfile.IsSupported(effect));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(201)]
    [InlineData(202)]
    [InlineData(203)]
    [InlineData(204)]
    [InlineData(205)]
    [InlineData(206)]
    public void Controller_restores_one_owner_and_releases_without_reentry(int effect)
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect);
        NativeBattleIfrit201Frame fixture = NativeBattleIfritCallbackTests.ControllerFrame();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.QueueAddress, profile.BankAddress, 16, 1);
        (pool, _) = pool.Append(profile.Callback);
        NativeBattleCinematicFrame frame = new(fixture.Memory with { Cinematic = new(effect, 9) }, fixture.Effect with { RequestedEffect = checked((ushort)effect), SelectedIndex = effect - 1, Binding = new(checked((ushort)effect), profile.Entry, profile.Initializer), Pools = [pool], ReturnedDescriptor = profile.QueueAddress }, fixture.Random);
        frame = JsonSerializer.Deserialize<NativeBattleCinematicFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleScriptedCallbackServices services = NativeBattleIfritCallbackTests.Services() with
        {
            DrawSlots = (current, _) =>
            {
                Assert.Equal((byte)9, current.ReadTemporaryByte(0x145));
                return WaterSchedulerFixture.CoreWord(current.WriteTemporaryByte(0x145, 6), 0x2797628 + 0xA, 0);
            }
        };
        NativeBattleEffectMemory latest = frame.Memory with { ScriptedGraphics = frame.Memory.ScriptedGraphics! with { Dqa = -7 } };
        NativeBattleCinematicFrame completed = NativeBattleCinematicController.Advance(profile, frame, NativeBattleIfritCallbackTests.Input(1), services, 0x1D9A000, () => latest);
        Assert.Equal(new NativeBattleCinematicState(effect, 6), completed.Memory.Cinematic);
        Assert.Equal((short)-7, completed.Memory.ScriptedGraphics!.Dqa);
        Assert.Equal(0u, completed.Effect.ReturnedDescriptor);
        NativeBattleCinematicFrame again = NativeBattleCinematicController.Advance(profile, completed, NativeBattleIfritCallbackTests.Input(1), services, 0x1D9A000, () => throw new InvalidOperationException());
        Assert.Equal(JsonSerializer.Serialize(completed), JsonSerializer.Serialize(again));
    }

    [Fact]
    public void Legacy_adapter_has_one_backing_owner_and_missing_state_is_not_hydrated()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Ifrit201 = new(9) };
        Assert.Equal(new NativeBattleCinematicState(201, 9), memory.Cinematic);
        Assert.Null((memory with { Cinematic = new(202, 6) }).Ifrit201);
        JsonObject legacy = JsonSerializer.SerializeToNode(memory)!.AsObject();
        legacy.Remove("Cinematic");
        legacy["Ifrit201"] = new JsonObject { ["Filter145"] = 9 };
        Assert.Null(legacy.Deserialize<NativeBattleEffectMemory>()!.Cinematic);
        Assert.False(NativeBattleCinematicProfile.IsSupported(200));
        Assert.False(NativeBattleCinematicProfile.IsSupported(207));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(201)]
    [InlineData(202)]
    [InlineData(203)]
    [InlineData(204)]
    [InlineData(205)]
    [InlineData(206)]
    public void Drawing_consumes_lighting_only_for_selected_lit_profiles(int effect)
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect);
        NativeBattleWaterExecutionFrame frame = NativeBattleIfritCallbackTests.Create();
        frame = frame with { Core = frame.Core.Write(0x2797554, [0, 0xFF]), Arena = frame.Arena.MarkUnknown(0x20E1, 1) };
        int draws = 0;
        NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame current, byte slot)
        {
            Assert.Equal((byte)0, slot);
            draws++;
            return current;
        }
        if (profile.Lit)
        {
            Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDraw.Run(profile, frame, (current, _) => current, Draw));
            Assert.Equal(0, draws);
            frame = frame with { Arena = frame.Arena.Write(0x20E1, [0x83]) };
        }
        int lights = 0;
        NativeBattleCinematicDraw.Run(profile, frame, (current, light) =>
        {
            Assert.Equal(unchecked((sbyte)0x83), light);
            lights++;
            return current;
        }, Draw);
        Assert.Equal(1, draws);
        Assert.Equal(profile.Lit ? 1 : 0, lights);
    }
}
