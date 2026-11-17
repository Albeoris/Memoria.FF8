using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedRosterResourceInitializationTests
{
    private static readonly BattlePartyModelOwner Source = new BattlePartyActorModel(new("actor-7"));
    private static readonly BattleEnemyModelOwner Target = new BattleEnemyActorModel(new("actor-24"));

    [Theory]
    [InlineData(2, 2)]
    [InlineData(11, 14)]
    public void Constructor_loads_every_bound_model_into_the_matching_isolated_owners(int partyCount, int enemyCount)
    {
        NativeBattleEffectTextureState global = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(640, 0, 1, 1, [0x61, 0]);
        NativeBattleEffectPresentationInputs inputs = Inputs(partyCount, enemyCount, global);
        BattlePresentationProcess session = BattlePresentationProcess.CreateEffectPresentation(inputs);
        NativeBattleGraphicsState graphics = session.Graphics;
        NativeBattleModelCpuResources cpu = session.ModelCpuResources;
        NativeBattleModelTextureCache cache = session.ModelTextureCache;
        NativeBattleEffectTextureState textures = session.Memory.Textures!;
        BattleModelRoster roster = graphics.ActorModels.Roster;
        Assert.Equal(roster.Owners, cpu.Cpu.PresentationRoster!.Owners);
        Assert.Equal(roster.Owners, cache.PresentationRoster!.Owners);
        Assert.Equal(partyCount * 2 + enemyCount, cache.PresentationActors!.Count);
        Assert.Equal(partyCount + enemyCount, textures.PresentationTextures!.Count);
        Assert.Equal((ushort)0x61, textures.ReadVramWord(640, 0));
        Assert.Equal(inputs.Random, session.ReadEffectPresentationRandom());
        Assert.Null(session.NativeModelLoader!.Task);

        foreach (BattleModelOwner owner in roster.Owners)
        {
            bool party = owner is BattlePartyModelOwner;
            int originalSlot = graphics.NativeBindings.TryEncodeExactActor(owner, graphics.ActorModels, out byte operand) ? operand : 0;
            NativeBattleGraphicsModel other = owner == Source ? inputs.Source : inputs.NativeTargets.Single(target => target.Owner == owner).Model;
            NativeBattleGraphicsModel model = graphics.ActorModels.Get(owner);
            Assert.Equal(other.Flags0, model.Flags0);
            Assert.Equal(other.SequenceState, model.SequenceState);
            Assert.Equal(other.Transform, model.Transform);
            int body = Assert.IsType<int>(model.ActiveResourceRow100);
            Assert.Equal<int?>(body, model.OriginalResourceRow104);
            Assert.Equal(owner, cache.PresentationActors[body]);
            Assert.NotNull(cpu.Rows[body].Require(16));
            NativeBattleEffectTextureState actor = textures.RequirePresentationTexture(ActorId(owner)).Textures;
            Assert.Equal(0, actor.Count);
            int bodySlot = party ? 2 * originalSlot : 6;
            Assert.Equal(party ? (ushort)0x13 : (ushort)0x39, actor.ReadVramWord((bodySlot / 2 + 10) * 64, 0));
            if (owner is BattlePartyModelOwner partyOwner)
            {
                int linked = Assert.IsType<int>(model.LinkedTextureResourceIndex);
                Assert.Equal(owner, cache.PresentationActors[linked]);
                Assert.Equal(cpu.Cpu.PartyBankIndex(partyOwner), cpu.Rows[body].Require(16).Bank);
                Assert.Equal(cpu.Cpu.PartyBankIndex(partyOwner), cpu.Rows[linked].Require(16).Bank);
                Assert.Equal((ushort)0x27, actor.ReadVramWord((10 + originalSlot) * 64, 128));
                Assert.Equal(new byte[] { 1, 0, 0, 0 }, cpu.ReadAnimationSection(partyOwner, 8, 0, 4));
                Assert.Equal(new byte[] { 1, 0, 0, 0 }, cpu.ReadAnimationSection(partyOwner, 24, 0, 4));
            }
            else
            {
                NativeBattleGraphicsResource selected = Pose(session, graphics, owner);
                Assert.True(selected.GeometryRelocated507550);
                Assert.Single(selected.Geometry!.Value);
                Assert.Equal((ushort)0x39, actor.ReadVramWord(832, 0));
            }
        }
    }

    [Fact]
    public void Hosted_cpu_and_staging_use_the_selected_remastered_executable_profile()
    {
        string hash = NativeFieldRuntimeProfile.RemasteredEnImageHash;
        NativeBattleEffectPresentationInputs input = Inputs(2, 2, NativeBattleEffectTextureState.FromColdProcess(), loadSources: false, imageHash: hash);

        BattlePresentationProcess session = BattlePresentationProcess.CreateEffectPresentation(input);

        NativeBattleModelCpuBanks banks = session.ModelCpuResources.Cpu;
        Assert.Equal(NativeBattlePartyCpuLayout.Remastered, banks.EffectiveLayout);
        Assert.Equal(0x20000, banks.GetBank(0).Capacity);
        Assert.Equal(0x20000, banks.GetBank(banks.PartyBankIndex(Source)).Capacity);
        Assert.Equal(NativeBattleFileStaging.FromColdProcess(hash).LoaderZeroRanges.ToArray(), session.NativeFileStaging!.LoaderZeroRanges.ToArray());
        Assert.Equal(NativeBattleTemporaryMemory.Address, session.NativeFileStaging.ReadUInt32(NativeBattleTemporaryMemory.Cursor));
    }

    [Fact]
    public void Missing_old_map_forks_published_image_without_draining_its_queue_when_no_models_load()
    {
        NativeBattleEffectTextureState global = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [7, 0]).EnqueueCopy505EB0(0, 0, 1, 1, 1, 0);
        NativeBattleEffectPresentationInputs inputs = Inputs(2, 2, global, loadSources: false);
        BattlePresentationProcess session = BattlePresentationProcess.CreateEffectPresentation(inputs);
        NativeBattleEffectTextureState current = session.Memory.Textures!;
        Assert.Same(global.VramPublicationIdentity, current.VramPublicationIdentity);
        Assert.Equal(global.Slots, current.Slots);
        Assert.Equal(global.Count, current.Count);
        Assert.Equal(global.Animation, current.Animation);
        NativeBattlePresentationTextureBank child = current.RequirePresentationTexture("actor-7");
        Assert.Equal(0, child.Textures.Count);
        Assert.All(child.Textures.Slots, slot => Assert.Null(slot));
        Assert.Equal((ushort)7, child.Textures.ReadVramWord(0, 0));
        Assert.Equal((ushort)0, child.Textures.ReadVramWord(1, 0));
        Assert.NotSame(global.VramCaptureIdentity, child.Textures.VramCaptureIdentity);
        Assert.Null(session.NativeModelLoader);
    }

    [Fact]
    public void Declared_map_requires_every_bound_actor_and_loaded_conflicts_remain_visible()
    {
        NativeBattleEffectTextureState global = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattleEffectTextureState incomplete = global.WithPresentationTexture("actor-7", new(global, NativeBattlePcTextureWindowCache.Reset4647A0(null)));
        Assert.Throws<InvalidDataException>(() => BattlePresentationProcess.CreateEffectPresentation(Inputs(2, 2, incomplete)));

        NativeBattleEffectPresentationInputs input = Inputs(2, 2, global);
        NativeBattleModelDatSource changed = input.SourceResource.ModelLoadSources!.Value[0] with { FileId = 123 };
        NativeBattleGraphicsResource conflict = input.SourceResource with { ModelLoadSources = [changed] };
        input = Inputs(2, 2, global, bindings: [new("actor-7", 0, conflict)]);
        Assert.Contains("conflicting immutable sources", Assert.Throws<InvalidDataException>(() => BattlePresentationProcess.CreateEffectPresentation(input)).Message);
    }

    [Fact]
    public void Legacy_incoming_texture_publications_keep_current_actor_banks_and_declared_banks_are_validated()
    {
        BattlePresentationProcess session = BattlePresentationProcess.CreateEffectPresentation(Inputs(2, 2, NativeBattleEffectTextureState.FromColdProcess(), loadSources: false));
        NativeBattleEffectTextureState current = session.Memory.Textures!;
        NativeBattleEffectTextureState legacy = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [9, 0]);
        NativeBattleEffectTextureState accepted = Accept(session, legacy);
        Assert.Same(current.PresentationTextures, accepted.PresentationTextures);
        Assert.Equal((ushort)9, accepted.ReadVramWord(0, 0));
        Assert.Same(accepted, Accept(session, accepted));
        NativeBattleEffectTextureState missing = accepted with { PresentationTextures = accepted.PresentationTextures!.Remove("actor-8") };
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Accept(session, missing));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    [Fact]
    public void Hosted_pose_keeps_the_supplied_enemy_resource_when_its_external_alias_was_not_loaded()
    {
        NativeBattleEffectPresentationInputs input = Inputs(2, 2, NativeBattleEffectTextureState.FromColdProcess(), loadSources: false);
        NativeBattleEffectModelBinding enemy = input.NativeTargets[^1];
        input = input with { NativeTargets = input.NativeTargets.SetItem(input.NativeTargets.Length - 1, enemy with { Model = enemy.Model with { ActiveResourceRow100 = 3 } }) };
        BattlePresentationProcess session = BattlePresentationProcess.CreateEffectPresentation(input);

        Assert.Same(enemy.Resource, Pose(session, session.Graphics, enemy.Owner));
    }

    private static NativeBattleEffectPresentationInputs Inputs(int partyCount, int enemyCount, NativeBattleEffectTextureState textures, bool loadSources = true, string imageHash = NativeFieldRuntimeProfile.FF8EnExecutableHash, ImmutableArray<NativeBattleGraphicsBinding> bindings = default)
    {
        int[] parties = partyCount == 11 ? [0, 1, 2, 7, 8, 9, 10, 11, 12, 13, 14] : [7, 8];
        int[] enemies = enemyCount == 14 ? [3, 4, 5, 6, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24] : [15, 24];
        Dictionary<int, BattleModelOwner> owners = parties.ToDictionary(row => row, row => row == 7 ? Source : new BattlePartyActorModel(new($"actor-{row}")))
            .Select(pair => (pair.Key, Owner: (BattleModelOwner)pair.Value))
            .Concat(enemies.Select(row => (Key: row, Owner: row == 24 ? (BattleModelOwner)Target : new BattleEnemyActorModel(new($"actor-{row}")))))
            .ToDictionary(pair => pair.Key, pair => pair.Owner);
        BattleModelRoster roster = new(owners.OrderBy(pair => pair.Key).Select(pair => pair.Value));
        NativeBattleModelAliases aliases = new(roster, owners.Where(pair => pair.Key < NativeBattleModelBoundary.CellCount).Select(pair => (NativeBattleModelSlot.Decode((byte)pair.Key), pair.Value)));
        NativeBattleModelDatSource body = NativeBattleModelResourceCapacityTests.Source(false, 8);
        NativeBattleModelDatSource linked = NativeBattleModelResourceCapacityTests.Source(true, 8);
        NativeBattleModelDatSource enemyBody = NativeBattleEnemyOwnedHeapTests.Source(72);
        NativeBattlePoseSource bodyPose = PoseSource(body);
        NativeBattlePoseSource linkedPose = PoseSource(linked);
        NativeBattlePoseSource enemyPose = PoseSource(enemyBody);
        if (loadSources)
        {
            int geometryOffset = enemyBody.Section(1);
            Assert.Single(NativeBattleRawGeometryReader.Read(enemyBody.Bytes.AsSpan(geometryOffset, enemyBody.Section(2) - geometryOffset).ToArray(), enemyPose.BoneCount));
        }
        NativeBattleGraphicsResource party = new([0, 0]) { BodyFileSelector129 = 0, LinkedFileSelector128 = 0, PoseSource = bodyPose, LinkedPoseSource = linkedPose };
        NativeBattleGraphicsResource enemy = new([0, 0]) { PoseSource = enemyPose };
        if (loadSources)
        {
            party = party with { ModelLoadSources = [WithTexture(body, 5, 0x13), WithTexture(linked, 6, 0x27)] };
            enemy = enemy with { ModelLoadSources = [WithTexture(enemyBody, 10, 0x39)] };
        }
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases);
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(Source, 8, 0, -1, 1, 1, null));
        (packets, _) = packets.AppendContact(new(new(Target, 0, 0, 0, 0, 0, 0), null));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(header, 1);
        NativeBattleEffectParameters parameters = new(Source, 0, BattleModelTargets.From([Target]), header, 0, 0);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        BattleDefinition definition = new()
        {
            NativeEffectCatalog = [.. Enumerable.Range(1, 400).Select(effect => new NativeBattleEffectBinding((ushort)effect, effect == 1 ? 1u : 0u, 0))],
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(imageHash) with { EffectMemory = memory with { PrimitiveProcess = memory.PrimitiveProcess!.Initialize500400() } }
        };
        NativeBattleGraphicsDefinition graphics = new(new(imageHash, [], []), bindings.IsDefault ? [] : bindings, []);
        return new(new(definition, graphics), Model(7, true, bodyPose, linkedPose), party, new NativeBattleEffectInvocation.Combat(packets, parameters), 1, textures, new(21), new(NativeBattleCuragaMatrices.Identity8DD770(), 512, 160, 108), [])
        {
            NativeTargets = [.. owners.Where(pair => pair.Value != Source).Select(pair => pair.Value is BattlePartyModelOwner
                ? new NativeBattleEffectModelBinding(pair.Value, Model(pair.Key, true, bodyPose, linkedPose), party)
                : new NativeBattleEffectModelBinding(pair.Value, Model(pair.Key, false, enemyPose), enemy))],
            DeferInvocation = true
        };
    }

    private static string ActorId(BattleModelOwner owner) => owner switch
    {
        BattlePartyActorModel party => party.Actor.ActorId,
        BattleEnemyActorModel enemy => enemy.Actor.ActorId,
        _ => throw new InvalidDataException("The fixture requires a canonical model owner.")
    };

    private static NativeBattleGraphicsModel Model(int row, bool party, NativeBattlePoseSource body, NativeBattlePoseSource? linked = null)
    {
        NativeBattleVector position = new((short)(100 * row), 0, (short)(100 + row));
        NativeBattleGraphicsModel model = new(row % 2 == 0 ? (ushort)0 : (ushort)2)
        {
            ModelId4 = party ? (byte)0 : (byte)16, Flags2 = 0, Rotation12 = 0, Rotation14 = 0, Rotation16 = 0,
            Flags8 = 0, ResourceFlags44 = 0, Color40 = 0x808080, Color44 = 0x32808080, Alpha7 = 0,
            SequenceState = new() { Pose = 0, PoseFrame114 = 1, PoseLimit115 = 1, LinkedPose = linked is null ? null : new(0, 1, 1, 0) },
            Transform = new(position, position, 0, 0, 4096),
            GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770() with { X = position.X, Y = position.Y, Z = position.Z }
        };
        return NativeBattleBodyBoneMatrices.Publish508F60(model, body, linked);
    }

    private static NativeBattlePoseSource PoseSource(NativeBattleModelDatSource source)
    {
        return new(source.Bytes.Slice(source.Section(0), source.Section(1) - source.Section(0)), source.Bytes.Slice(source.Section(2), source.Section(3) - source.Section(2)));
    }

    private static NativeBattleModelDatSource WithTexture(NativeBattleModelDatSource source, int index, ushort pixel)
    {
        const int paletteBlock = 16;
        const int paletteBlockLength = 12 + 256 * sizeof(ushort);
        const int imageBlock = paletteBlock + paletteBlockLength;
        byte[] pack = new byte[imageBlock + 20];
        BinaryPrimitives.WriteInt32LittleEndian(pack, 1);
        BinaryPrimitives.WriteInt32LittleEndian(pack.AsSpan(4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(pack.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(pack.AsSpan(12), 9);
        BinaryPrimitives.WriteInt32LittleEndian(pack.AsSpan(paletteBlock), paletteBlockLength);
        BinaryPrimitives.WriteInt16LittleEndian(pack.AsSpan(22), 224);
        BinaryPrimitives.WriteInt16LittleEndian(pack.AsSpan(24), 256);
        BinaryPrimitives.WriteInt16LittleEndian(pack.AsSpan(26), 1);
        BinaryPrimitives.WriteInt32LittleEndian(pack.AsSpan(imageBlock), 20);
        BinaryPrimitives.WriteInt16LittleEndian(pack.AsSpan(imageBlock + 8), 2);
        BinaryPrimitives.WriteInt16LittleEndian(pack.AsSpan(imageBlock + 10), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(pack.AsSpan(imageBlock + 12), pixel);
        int start = source.Section(index);
        int end = source.Section(index + 1);
        byte[] bytes = [.. source.Bytes.Take(start), .. pack, .. source.Bytes.Skip(end)];
        int sectionCount = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        for (int field = index + 1; field <= sectionCount; field++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + field * 4), source.Section(field) + pack.Length - (end - start));
        }
        return source with { Bytes = bytes.ToImmutableArray() };
    }

    private static NativeBattleEffectTextureState Accept(BattlePresentationProcess process, NativeBattleEffectTextureState incoming) => (NativeBattleEffectTextureState)typeof(BattlePresentationProcess).GetMethod("AcceptHostedPresentationTextures", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(process, [incoming])!;
    private static NativeBattleGraphicsResource Pose(BattlePresentationProcess process, NativeBattleGraphicsState graphics, BattleModelOwner owner) => (NativeBattleGraphicsResource)typeof(BattlePresentationProcess).GetMethod("NativeGraphicsPoseResource", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(process, [graphics, owner])!;
}
