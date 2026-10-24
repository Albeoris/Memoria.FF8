using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect345SourceAdmissionTests
{
    [Fact]
    public void Fresh_original114_source_preserves_current_actor_temporary_and_cold_effect_owners()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT345_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT345_ADMISSION_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a fresh standalone and a new input-admission evidence directory.");
        Assert.False(Directory.Exists(output));
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        string unchanged = JsonSerializer.Serialize(source);
        BattleSession session = BattleSession.Restore(source);
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
        Assert.True(session.NativeEntryReady);
        Assert.Equal(462, source.Definition.EncounterId);
        Assert.Equal(121, source.Definition.StageId);
        Assert.Null(source.NativeGraphics!.OriginalEffect);
        NativeBattleEffectMemory memory = source.NativeEffectMemory!;
        Assert.Equal(0, memory.Active);
        BattleCombatantSnapshot enemy = Assert.Single(source.Combatants, combatant => !session.GetCombatantDefinition(combatant.Id).IsPlayer && combatant.NativeParticipant is { Active: true });
        Assert.Equal((byte)3, enemy.NativeParticipant!.ActorIndex);
        Assert.False(enemy.NativeParticipant.Hidden);
        Assert.False(enemy.NativeParticipant.Suspended);
        Assert.False(enemy.NativeParticipant.Untargetable);
        BattleCombatantDefinition caster = session.GetCombatantDefinition(enemy.Id);
        Assert.Equal(114, caster.ModelId);
        Assert.Equal(0, caster.SceneSlot);
        NativeBattleGraphicsModel model = source.NativeGraphics.Models[3];
        Assert.Equal((byte)130, model.ModelId4);
        Assert.NotNull(model.GeometryWorldMatrix);
        Assert.NotNull(model.Transform);
        Assert.NotNull(model.SequenceState);
        NativeBattleGraphicsResource actorResource = assets.ReadNativeGraphicsResource(caster);
        NativeBattlePoseSource pose = actorResource.PoseSource ?? throw new NotSupportedException("The selected caster requires its actual current skeleton.");
        Assert.Equal(64, pose.BoneCount);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, pose);
        Assert.Equal(64, bones.Length);
        int start = actorResource.SequenceStart(11);
        int end = actorResource.SequenceEnd(11);
        ImmutableArray<byte> sequence = actorResource.SequenceSection.Slice(start, end - start);
        Assert.Equal(new byte[] { 0xBB, 0xA5, 4, 3, 4, 5, 0xA0, 5 }, sequence.AsSpan().Slice(0, 8).ToArray());
        Assert.Equal((byte)0xA2, sequence[^1]);
        Assert.Contains(assets.NativeActions, action => action.Command == 8 && action.ActionId == 124 && action.MagicId == 345);
        NativeBattleEffect345Resources resources = game.ReadNativeEffect345Resources();
        resources.Validate();
        Assert.Equal(28, resources.Model.PoseSource.BoneCount);
        Assert.Equal(28, resources.Model.PoseSource.Sample(5, 24, 0).Bones.Length);
        Assert.Equal(28, resources.Model.PoseSource.Sample(6, 18, 0).Bones.Length);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([345])[344];
        Assert.Same(resources, binding.Effect345Resources);
        Assert.Equal(NativeBattleEffect345Profile.Entry, binding.Entry);
        NativeBattleEffect345State state = memory.Effect345 ?? throw new NotSupportedException("The source requires its explicitly retained cold effect345 owner.");
        state.Validate();
        Assert.True(state.ColdSourceOrigin);
        Assert.False(state.OwnedModelSourceBound);
        Assert.Null(state.NodeData);
        Assert.Empty(state.OwnedModels);
        Assert.Equal(8, state.BankStorage.Length);
        List<object> banks = [];
        for (int index = 0; index < NativeBattleEffect345Profile.Pools.Length; index++)
        {
            NativeBattleCureFamilyPool pool = NativeBattleEffect345Profile.Pools[index];
            NativeBattleEffectArena bank = state.ReadBank(index);
            int length = checked(pool.Stride * pool.Capacity);
            Assert.Equal(new byte[length], bank.Read(0, length));
            banks.Add(new { Index = index, pool.Bank, pool.Stride, pool.Capacity, Bytes = length });
        }
        NativeBattleFileStaging staging = session.NativeFileStaging ?? throw new NotSupportedException("The source requires its current physical temporary owner.");
        Assert.NotEmpty(staging.LoaderZeroRanges);
        Assert.Equal(new byte[2], staging.Read(0x255FEC2, 2));
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(staging, NativeBattleTemporaryMemory.Address, NativeBattleTemporaryMemory.ReachedLength);
        (byte[] temporaryBytes, bool[] temporaryKnown) = temporary.ReadSnapshot(0, NativeBattleTemporaryMemory.ReachedLength);
        uint currentCursor = BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(NativeBattleTemporaryMemory.Cursor, 4));
        ushort highWord46 = BinaryPrimitives.ReadUInt16LittleEndian(staging.Read(NativeBattleTemporaryMemory.Address + 0x46, 2));
        Assert.True(currentCursor >= NativeBattleTemporaryMemory.Address);
        Assert.True(temporaryKnown[0x46] && temporaryKnown[0x47]);
        Assert.Equal(unchanged, JsonSerializer.Serialize(source));
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
        Directory.CreateDirectory(output!);
        File.WriteAllText(Path.Combine(output!, "current-input-admission.json"), JsonSerializer.Serialize(new { Status = "passed", Scope = "Fresh original-resource input and physical owner admission only; no effect callback, ordinary AI admission, target eligibility, first appearance shadow index or later runtime draw claim", source.Frame, source.NativeGraphics.Callback, source.Definition.EncounterId, source.Definition.StageId, PhysicalCaster = 3, caster.ModelId, caster.SceneSlot, ActorBones = bones.Length, CurrentWorld = model.GeometryWorldMatrix, model.SequenceState, OriginalKernelAction = 124, OriginalKernelEffect = 345, Sequence11 = sequence, EffectModelBones = 28, SelectedEffectModelFrames = new[] { 24, 18 }, ColdBanks = banks, CurrentTemporaryAddress = NativeBattleTemporaryMemory.Address, CurrentCursor = currentCursor, CurrentHighWord46 = highWord46, TemporaryBytes = temporaryBytes, TemporaryKnown = temporaryKnown, LoaderZeroRanges = staging.LoaderZeroRanges, ReachedPhysicalWord = 0x255FEC2, SnapshotUnchanged = true, FirstAppearanceShadowIndex = "runtime first-draw admission pending" }));
    }
}
