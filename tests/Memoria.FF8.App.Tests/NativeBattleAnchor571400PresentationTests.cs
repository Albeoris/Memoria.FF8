using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleAnchor571400PresentationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_Morph172_warm_native_entry_publishes_ordered_anchor_or_preserves_disabled_registers(bool disabled)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? snapshotPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_ANCHOR_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_ANCHOR_EVIDENCE");
        Assert.SkipUnless(directory is not null && snapshotPath is not null && output is not null, "Select installed original resources, a drained original Morph172 snapshot and an evidence directory.");
        Assert.True(Directory.Exists(directory));
        Assert.True(File.Exists(snapshotPath));
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(snapshotPath!);
        NativeBattleGraphicsState sourceGraphics = source.NativeGraphics ?? throw new InvalidDataException("The selected original snapshot requires its actual graphics owner.");
        NativeBattleOriginalEffectState sourceEffect = sourceGraphics.OriginalEffect ?? throw new InvalidDataException("The selected original snapshot requires its retained Morph172 binding.");
        Assert.Equal((ushort)172, sourceEffect.RequestedEffect);
        Assert.Equal(NativeBattleMorph172Profile.Entry, sourceEffect.Binding.Entry);
        Assert.Equal(0u, sourceEffect.ReturnedDescriptor);
        Assert.Empty(sourceEffect.Pools);
        NativeBattleMorph172State retainedState = source.NativeEffectMemory?.Morph172 ?? throw new InvalidDataException("The original snapshot requires its retained Morph172 storage.");
        Assert.NotNull(retainedState.RootBankStorage);
        Assert.NotNull(retainedState.ChildBankStorage);
        NativeBattleOriginalEffectState materialized = NativeBattleEffectResourceResolver.MaterializeSelected(sourceEffect, effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        source = source with { NativeGraphics = sourceGraphics with { OriginalEffect = materialized } };

        BattleSession initialized = BattleSession.Restore(source);
        NativeBattleGraphicsState warmGraphics = Run(initialized, source.NativeGraphics!, true);
        BattleSnapshot warm = initialized.Capture();
        NativeBattleOriginalEffectState warmEffect = warmGraphics.OriginalEffect ?? throw new InvalidDataException("The actual warm native entry lost its effect.");
        Assert.Equal(NativeBattleMorph172Profile.RootDescriptor, warmEffect.ReturnedDescriptor);
        NativeBattleEffectTaskPool children = warmEffect.Pools[1];
        NativeBattleModulePoolTask main = Assert.Single(children.Slots.Where(task => (task.Flags & 1) != 0));
        Assert.Equal(NativeBattleMorph172Profile.MainCallback, main.Callback);
        Assert.Equal((ushort)0, main.ReadWord(12));
        int caster = checked((int)warm.NativeEffectMemory!.Morph172!.ReadDword(NativeBattleMorph172Profile.CasterGlobal));
        Assert.Equal(3, caster);
        NativeBattleGraphicsModel originalModel = warmGraphics.Models[caster];
        Assert.Equal(2, originalModel.Flags0 & 2);
        BattleCombatantDefinition casterDefinition = warm.Definition.Combatants.Single(actor => actor.Id == warmGraphics.ActorIds[caster]);
        Assert.Equal(29, casterDefinition.ModelId);
        NativeBattleGraphicsResource originalResource = assets.ReadNativeGraphicsResource(casterDefinition);
        NativeBattlePoseSource originalPose = originalResource.PoseSource ?? throw new InvalidDataException("The original Morph172 caster requires its skeleton.");
        Assert.Equal(41, originalPose.BoneCount);
        Assert.Equal(41, NativeBattleBodyBoneMatrices.Require(originalModel, originalPose).Length);
        Assert.NotNull(originalModel.GeometryWorldMatrix);
        if (disabled)
        {
            warmGraphics = warmGraphics with { Models = warmGraphics.Models.SetItem(caster, originalModel with { Flags0 = (ushort)(originalModel.Flags0 & ~2) }) };
            warm = warm with { NativeGraphics = warmGraphics };
        }

        string name = disabled ? "disabled" : "enabled";
        Directory.CreateDirectory(output!);
        File.WriteAllText(Path.Combine(output!, "anchor-" + name + "-warm-entry.battle.json"), JsonSerializer.Serialize(warm));
        NativeBattleEffectMemory memoryBefore = warm.NativeEffectMemory!;
        NativeBattleFileStaging stagingBefore = warm.NativeFileStaging ?? throw new InvalidDataException("The original warm entry requires its physical temporary memory.");
        uint temporaryAddress = BinaryPrimitives.ReadUInt32LittleEndian(stagingBefore.Read(NativeBattleTemporaryMemory.Cursor, 4));
        NativeBattleEffectMemory expectedMemory = memoryBefore;
        NativeBattleFileStaging expectedStaging = stagingBefore;
        NativeBattleVector expectedPoint;
        List<object> queries = [];
        if (disabled)
        {
            expectedPoint = warmGraphics.Models[caster].Transform!.Position28;
        }
        else
        {
            BattleSession oracle = BattleSession.Restore(warm);
            NativeBattleGraphicsResource resource = Invoke<NativeBattleGraphicsResource>(oracle, "NativeGraphicsPoseResource", warmGraphics, caster);
            NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("The current original caster requires its actual pose source.");
            Assert.Equal(originalPose.Skeleton.ToArray(), pose.Skeleton.ToArray());
            NativeBattleVector first = Observe(0xF0);
            NativeBattleVector second = Observe(0xF1);
            expectedPoint = new(Average(first.X, second.X), Average(first.Y, second.Y), Average(first.Z, second.Z));
            expectedStaging = oracle.NativeFileStaging!;
            Assert.True(JsonSerializer.Serialize(memoryBefore.GeometryRegisters) != JsonSerializer.Serialize(expectedMemory.GeometryRegisters) || JsonSerializer.Serialize(memoryBefore.ScriptedColorRegisters) != JsonSerializer.Serialize(expectedMemory.ScriptedColorRegisters) || JsonSerializer.Serialize(stagingBefore) != JsonSerializer.Serialize(expectedStaging), "The selected original anchor must distinguish publication from an unchanged frame.");

            NativeBattleVector Observe(byte selector)
            {
                NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(warmGraphics.Models[caster], pose, selector, 0);
                Assert.True(producer.Enabled);
                expectedMemory = Invoke<NativeBattleEffectMemory>(oracle, "ObserveNativeBoneAnchor", expectedMemory, producer);
                NativeBattleScriptedColorRegisters registers = expectedMemory.ScriptedColorRegisters ?? throw new InvalidDataException("The bone observer lost its canonical full MAC registers.");
                NativeBattleVector point = new(unchecked((short)registers.Mac[0]!.Value), unchecked((short)registers.Mac[1]!.Value), unchecked((short)registers.Mac[2]!.Value));
                queries.Add(new { Selector = selector, Distance = producer.Distance, Point = point, Geometry = expectedMemory.GeometryRegisters, Colors = registers });
                return point;
            }
        }
        short w = warmGraphics.Models[caster].Transform!.Angle36;
        NativeBattleEffectArena expectedPoint8 = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(expectedPoint.X)).Write(2, BitConverter.GetBytes(expectedPoint.Y)).Write(4, BitConverter.GetBytes(expectedPoint.Z)).Write(6, BitConverter.GetBytes(w));
        BattleSession actual = BattleSession.Restore(warm);
        NativeBattleGraphicsState result = Run(actual, warmGraphics, false);
        BattleSnapshot after = actual.Capture();
        NativeBattleEffectMemory actualMemory = after.NativeEffectMemory ?? throw new InvalidDataException("The actual anchor lost its process memory.");
        NativeBattleFileStaging actualStaging = after.NativeFileStaging ?? throw new InvalidDataException("The actual anchor lost its temporary memory.");
        NativeBattleModulePoolTask advanced = Assert.Single(result.OriginalEffect!.Pools[1].Slots.Where(task => (task.Flags & 1) != 0));
        Assert.Equal((ushort)1, advanced.ReadWord(12));
        NativeBattleEffectArena actualPoint8 = NativeBattleMorph172Controller.TaskView(advanced, 16, 8);
        Assert.Equal(expectedPoint8.Read(0, 8), actualPoint8.Read(0, 8));
        Assert.Equal(expectedPoint8.ReadSnapshot(0, 8).Known, actualPoint8.ReadSnapshot(0, 8).Known);
        Assert.Equal(JsonSerializer.Serialize(expectedMemory.GeometryRegisters), JsonSerializer.Serialize(actualMemory.GeometryRegisters));
        Assert.Equal(JsonSerializer.Serialize(expectedMemory.ScriptedColorRegisters), JsonSerializer.Serialize(actualMemory.ScriptedColorRegisters));
        Assert.True(JsonSerializer.Serialize(expectedStaging) == JsonSerializer.Serialize(actualStaging), "The Session anchor must preserve the exact ordered bone temporary bytes and knownness.");
        Assert.Equal(stagingBefore.Read(NativeBattleTemporaryMemory.Cursor, 4), actualStaging.Read(NativeBattleTemporaryMemory.Cursor, 4));
        Assert.Equal(expectedStaging.ReadSnapshot(temporaryAddress, 52).Data, actualStaging.ReadSnapshot(temporaryAddress, 52).Data);
        Assert.Equal(expectedStaging.ReadSnapshot(temporaryAddress, 52).Known, actualStaging.ReadSnapshot(temporaryAddress, 52).Known);
        Assert.Equal(warm.NativeCrtRandom, after.NativeCrtRandom);
        Assert.Equal(warm.NativeGlobalRandom, after.NativeGlobalRandom);
        Assert.Equal(warm.Frame, after.Frame);
        Assert.Equal(warm.NativeGraphics!.Models, result.Models);
        Assert.Equal(warm.Events.Length, after.Events.Length);
        if (disabled)
        {
            Assert.Empty(queries);
            Assert.Equal(JsonSerializer.Serialize(memoryBefore.GeometryRegisters), JsonSerializer.Serialize(actualMemory.GeometryRegisters));
            Assert.Equal(JsonSerializer.Serialize(memoryBefore.ScriptedColorRegisters), JsonSerializer.Serialize(actualMemory.ScriptedColorRegisters));
        }
        File.WriteAllText(Path.Combine(output!, "anchor-" + name + "-after.battle.json"), JsonSerializer.Serialize(after));
        File.WriteAllText(Path.Combine(output!, "anchor-" + name + ".json"), JsonSerializer.Serialize(new { Status = "passed", Effect = 172, Actor = caster, SourceSnapshot = Path.GetFileName(snapshotPath), SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(snapshotPath!))), ControlledBranch = disabled ? "Only actual caster Flags0 bit02 cleared after real warm native entry" : "Actual enabled original caster retained", WarmNativeEntry = true, NativeAdvance = 1, ExpectedPoint = expectedPoint8, ActualPoint = actualPoint8, Queries = queries, TemporaryAddress = temporaryAddress, TemporaryBytes = new { actualStaging.ReadSnapshot(temporaryAddress, 52).Data, actualStaging.ReadSnapshot(temporaryAddress, 52).Known }, Geometry = actualMemory.GeometryRegisters, Colors = actualMemory.ScriptedColorRegisters, after.NativeCrtRandom }));
    }

    private static short Average(short first, short second) => unchecked((short)((first + second) / 2));

    private static NativeBattleGraphicsState Run(BattleSession session, NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleGraphicsState result = Invoke<NativeBattleGraphicsState>(session, "RunNativeMorph172", graphics, initialize);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(session, result);
        return result;
    }

    private static T Invoke<T>(BattleSession session, string method, params object[] arguments) => (T)(typeof(BattleSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingMethodException(typeof(BattleSession).FullName, method)).Invoke(session, arguments)!;
}
