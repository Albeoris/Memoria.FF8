using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedSessionModelLoaderTests
{
    private static readonly BattleEnemyModelOwner FirstEnemy = new BattleEnemyActorModel(new("first"));
    private static readonly BattleEnemyModelOwner SecondEnemy = new BattleEnemyActorModel(new("second"));
    private static readonly BattlePartyModelOwner FirstParty = new BattlePartyActorModel(new("first"));
    private static readonly BattlePartyModelOwner SecondParty = new BattlePartyActorModel(new("second"));
    private static readonly BattleModelOwner FirstOriginal = new BattleEnemyActorModel(new("first"));
    private static readonly BattleModelOwner SecondOriginal = new BattleEnemyActorModel(new("second"));

    [Fact]
    public void HostedEnemyLoadsOfTheSameModelPublishIndependentTextureAndCpuOwners()
    {
        NativeBattleModelDatSource source = EnemySource(77);
        BattleSession session = Session([source], Roster(false, false));
        Load(session, 77, FirstEnemy);
        NativeBattleEffectMemory first = Drain(session);
        Set(session, nameof(BattleSession.NativeEffectMemory), first);
        Load(session, 77, SecondEnemy);
        NativeBattleEffectTextureState queued = session.NativeEffectMemory!.Textures!;
        Assert.Equal(0, queued.Count);
        Assert.Equal(0, queued.RequirePresentationTexture("first").Textures.Count);
        Assert.Equal(1, queued.RequirePresentationTexture("second").Textures.Count);
        NativeBattleEffectTextureState drained = Drain(session).Textures!;
        Assert.Equal((ushort)0, drained.ReadVramWord(1, 230));
        Assert.Equal((ushort)0x1F, drained.RequirePresentationTexture("first").Textures.ReadVramWord(1, 230));
        Assert.Equal((ushort)0x1F, drained.RequirePresentationTexture("second").Textures.ReadVramWord(1, 230));

        NativeBattleModelCpuResources cpu = session.NativeModelCpuResources!;
        NativeBattleGraphicsState graphics = session.NativeGraphics!;
        int firstAlias = graphics.ActorModels.Get(FirstEnemy).ActiveResourceRow100!.Value;
        int secondAlias = graphics.ActorModels.Get(SecondEnemy).ActiveResourceRow100!.Value;
        Assert.NotEqual(firstAlias, secondAlias);
        ImmutableDictionary<int, BattleModelOwner> owners = session.NativeModelTextureCache!.PresentationActors!;
        Assert.Equal(FirstEnemy, owners[firstAlias]);
        Assert.Equal(SecondEnemy, owners[secondAlias]);
        Assert.NotEqual(cpu.Rows[firstAlias].Require(16).EnemyResourceId, cpu.Rows[secondAlias].Require(16).EnemyResourceId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HostedPartyBodyAndLinkedCompleteTheirOwnPlanAndSavedPalette(bool first)
    {
        BattlePartyModelOwner actor = first ? FirstParty : SecondParty;
        NativeBattleModelDatSource body = WithTim(NativeBattleModelResourceCapacityTests.Source(false, 8), 5);
        NativeBattleModelDatSource linked = WithTim(NativeBattleModelResourceCapacityTests.Source(true, 8), 6);
        BattleSession session = Session([body, linked], Roster(true, true));
        Load(session, 0, actor, restorePending: true);
        Load(session, 0x1000, actor);
        NativeBattleGraphicsModel model = session.NativeGraphics!.ActorModels.Get(actor);
        NativeBattleModelCpuResources cpu = session.NativeModelCpuResources!;
        int bodyAlias = model.ActiveResourceRow100!.Value;
        int linkedAlias = model.LinkedTextureResourceIndex!.Value;
        Assert.NotEqual(bodyAlias, linkedAlias);
        ImmutableDictionary<int, BattleModelOwner> owners = session.NativeModelTextureCache!.PresentationActors!;
        Assert.Equal(actor, owners[bodyAlias]);
        Assert.Equal(actor, owners[linkedAlias]);
        Assert.Equal(cpu.Cpu.PartyBankIndex(actor), (int)cpu.Rows[bodyAlias].Require(8).Bank);
        Assert.Equal(cpu.Cpu.PartyBankIndex(actor), (int)cpu.Rows[linkedAlias].Require(12).Bank);
        Assert.Equal((byte)0x1F, cpu.ReadPartyStatusPalette(actor)[2]);

        string id = first ? "first" : "second";
        NativeBattleEffectTextureState drained = Drain(session).Textures!;
        Assert.Equal((ushort)0, drained.ReadVramWord(1, 224));
        Assert.Equal((ushort)0x1F, drained.RequirePresentationTexture(id).Textures.ReadVramWord(1, 224));
        Assert.Equal((ushort)0x1F, drained.RequirePresentationTexture(id).Textures.ReadVramWord(1, 236));
    }

    [Fact]
    public void NativeEnemyCloneSkipsFileIoAndKeepsTheExistingTextureBinding()
    {
        BattleSession session = Session([EnemySource(77)], null);
        Load(session, 77, FirstOriginal);
        NativeBattleEffectMemory drained = Drain(session);
        Set(session, nameof(BattleSession.NativeEffectMemory), drained);
        string fileState = NativeBattleTestSnapshots.Json(session.NativeFileIo!);
        string staging = NativeBattleTestSnapshots.Json(session.NativeFileStaging!);
        const int ioResult = 0x1234;
        Set(session, nameof(BattleSession.NativeEffectIoResult), ioResult);

        Queue(session, 77, SecondOriginal);
        Assert.Equal((byte)0, session.NativeModelLoader!.Task!.Phase);
        Invoke(session, "PumpNativeFileIo482590");
        Assert.Equal(fileState, NativeBattleTestSnapshots.Json(session.NativeFileIo!));
        Poll(session);

        Assert.Null(session.NativeModelLoader!.Task);
        Assert.Equal(fileState, NativeBattleTestSnapshots.Json(session.NativeFileIo!));
        Assert.Equal(ioResult, session.NativeEffectIoResult);
        Assert.Equal(staging, NativeBattleTestSnapshots.Json(session.NativeFileStaging!));
        Assert.Same(drained.Textures, session.NativeEffectMemory!.Textures);
        NativeBattleGraphicsState graphics = session.NativeGraphics!;
        NativeBattleGraphicsModel first = graphics.ActorModels.Get(FirstOriginal);
        NativeBattleGraphicsModel second = graphics.ActorModels.Get(SecondOriginal);
        int firstAlias = Assert.IsType<int>(first.TextureResourceIndex);
        int secondAlias = Assert.IsType<int>(second.TextureResourceIndex);
        Assert.NotEqual(firstAlias, secondAlias);
        NativeBattleModelTextureCache cache = session.NativeModelTextureCache!;
        Assert.Equal(first.BodyTextureSlots!.PaletteRows.ToArray(), second.BodyTextureSlots!.PaletteRows.ToArray());
        Assert.Equal(first.TextureAllocationMask, second.TextureAllocationMask);
        foreach ((NativeBattleGraphicsModel model, int alias) in new[] { (first, firstAlias), (second, secondAlias) })
        {
            NativeBattleModelTextureCacheRow row = cache.Rows[alias];
            Assert.Equal((byte)3, row.Kind);
            Assert.Equal((byte?)77, row.ModelId);
            Assert.Equal(Assert.IsType<ushort>(row.TextureMask), Assert.IsType<ushort>(model.TextureAllocationMask));
            Assert.Equal(row.Binding!.PaletteRows.ToArray(), model.BodyTextureSlots!.PaletteRows.ToArray());
        }
    }

    [Fact]
    public void Model143UsesTheSelectedDonorCpuPointersWithoutUploadingItsMaterial()
    {
        NativeBattleModelDatSource companion = new(143, false, 0, 293, "companion.dat", Dat([new byte[4], new byte[4]]));
        BattleSession session = Session([EnemySource(142), companion], Roster(false, false));
        Load(session, 142, FirstEnemy);
        Load(session, 143, SecondEnemy);
        NativeBattleModelCpuResources cpu = session.NativeModelCpuResources!;
        NativeBattleGraphicsState graphics = session.NativeGraphics!;
        int donor = graphics.ActorModels.Get(FirstEnemy).TextureResourceIndex!.Value;
        int destination = graphics.ActorModels.Get(SecondEnemy).TextureResourceIndex!.Value;
        Assert.Equal(donor, cpu.LastModel142Row);
        Assert.Equal(cpu.Rows[donor].Require(12), cpu.Rows[destination].Require(12));
        Assert.Equal(new NativeBattleModelCpuReference(4, 0), cpu.Rows[destination].Require(16));
        NativeBattleModelTextureCache cache = session.NativeModelTextureCache!;
        Assert.Equal(SecondEnemy, cache.PresentationActors![destination]);
        Assert.Equal((ushort?)0, cache.Rows[destination].TextureMask);
        NativeBattleEffectTextureState textures = session.NativeEffectMemory!.Textures!;
        Assert.Equal(1, textures.RequirePresentationTexture("first").Textures.Count);
        Assert.Equal(0, textures.RequirePresentationTexture("second").Textures.Count);
    }

    [Fact]
    public void HostedOwnerMismatchRejectsQueueAndPollBeforeAnyIoOrStagingPublication()
    {
        BattleModelRoster roster = Roster(false, false);
        BattleSession session = Session([EnemySource(77)], roster);
        NativeBattleModelTextureCache cache = session.NativeModelTextureCache!;
        BattleModelRoster mismatch = new([FirstEnemy, new BattleEnemyActorModel(new("different"))]);
        Set(session, nameof(BattleSession.NativeModelTextureCache), NativeBattleModelTextureCache.FromColdProcess().WithPresentationRoster(mismatch));
        NativeBattleFileIoState file = session.NativeFileIo!;
        NativeBattleFileStaging staging = session.NativeFileStaging!;
        TargetInvocationException queueFailure = Assert.Throws<TargetInvocationException>(() => Queue(session, 77, FirstEnemy));
        Assert.IsType<InvalidDataException>(queueFailure.InnerException);
        Assert.Null(session.NativeModelLoader);

        Set(session, nameof(BattleSession.NativeModelTextureCache), cache);
        Queue(session, 77, FirstEnemy);
        NativeBattleEffectMemory memory = session.NativeEffectMemory!;
        Set(session, nameof(BattleSession.NativeEffectMemory), memory with { Textures = memory.Textures! with { PresentationTextures = memory.Textures!.PresentationTextures!.Remove("first") } });
        TargetInvocationException pollFailure = Assert.Throws<TargetInvocationException>(() => Poll(session));
        Assert.IsType<InvalidDataException>(pollFailure.InnerException);
        Assert.Same(file, session.NativeFileIo);
        Assert.Same(staging, session.NativeFileStaging);
        Assert.Equal((byte)0, session.NativeModelLoader!.Task!.Phase);
    }

    [Fact]
    public void FullHostedCacheRejectsNewAliasBeforeQueueButAllowsSameActorReload()
    {
        BattleSession session = Session([EnemySource(77)], Roster(false, false));
        Load(session, 77, FirstEnemy);
        NativeBattleModelTextureCache cache = session.NativeModelTextureCache!;
        int alias = session.NativeGraphics!.ActorModels.Get(FirstEnemy).TextureResourceIndex!.Value;
        NativeBattleModelTextureCacheRow template = cache.Rows[alias];
        for (int row = 0; row < cache.Rows.Length; row++)
        {
            if (row != alias)
            {
                cache = cache with
                {
                    Rows = cache.Rows.SetItem(row, template with { ModelId = checked((byte)(90 + row)) }),
                    PresentationActors = cache.PresentationActors!.Add(row, SecondEnemy)
                };
            }
        }
        cache.Validate();
        Set(session, nameof(BattleSession.NativeModelTextureCache), cache);
        NativeBattleModelLoader loader = session.NativeModelLoader!;
        NativeBattleFileIoState io = session.NativeFileIo!;
        NativeBattleFileStaging staging = session.NativeFileStaging!;
        NativeBattleEffectTextureState textures = session.NativeEffectMemory!.Textures!;
        NativeBattleModelCpuResources cpu = session.NativeModelCpuResources!;

        TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() => Queue(session, 78, FirstEnemy));
        Assert.IsType<NotSupportedException>(failure.InnerException);
        Assert.Same(loader, session.NativeModelLoader);
        Assert.Same(io, session.NativeFileIo);
        Assert.Same(staging, session.NativeFileStaging);
        Assert.Same(textures, session.NativeEffectMemory!.Textures);
        Assert.Same(cpu, session.NativeModelCpuResources);
        Assert.Same(cache, session.NativeModelTextureCache);

        Load(session, 77, FirstEnemy);
        Assert.Equal(alias, session.NativeGraphics!.ActorModels.Get(FirstEnemy).TextureResourceIndex);
        Assert.Equal(cache.Rows.Length, session.NativeModelTextureCache!.PresentationActors!.Count);
    }

    private static BattleSession Session(ImmutableArray<NativeBattleModelDatSource> sources, BattleModelRoster? roster)
    {
        NativeBattleGraphicsResource resource = new([0, 0]) { ModelLoadSources = sources };
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [], []);
        BattleDefinition definition = new()
        {
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }],
            NativeGraphics = new(rules, [new("party", 0, resource)], [])
        };
        BattleSession session = new(definition with { NativeGraphics = null }, BattleAtbMode.Normal);
        typeof(BattleSession).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, definition);
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.ClearUnloadedModel(null, null, null, null, null) with { FileSelector128 = 0, FileSelector129 = 0 };
        NativeBattleGraphicsState graphics = roster is null
            ? OriginalGraphics([(3, FirstOriginal, model), (4, SecondOriginal, model)])
            : NativeBattleHostedTestFixture.Graphics(roster, new(roster, []), _ => model);
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.FromColdProcess() with { EnemyCursor = 0, ScratchMaximum = 0, EnemyLinkedSlots = 0 };
        NativeBattleModelTextureCache cache = NativeBattleModelTextureCache.Initialize508260(null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0);
        if (roster is not null)
        {
            cpu = cpu.WithPresentationRoster(roster, new(roster, []));
            cache = cache.WithPresentationRoster(roster);
            NativeBattlePcTextureWindowCache imageCache = NativeBattlePcTextureWindowCache.Reset4647A0(null);
            NativeBattleEffectTextureState textures = memory.Textures!
                .WithPresentationTexture("first", new(NativeBattleEffectTextureState.FromColdProcess(), imageCache))
                .WithPresentationTexture("second", new(NativeBattleEffectTextureState.FromColdProcess(), imageCache));
            memory = memory with { Textures = textures };
        }
        Set(session, nameof(BattleSession.NativeGraphics), graphics);
        Set(session, nameof(BattleSession.NativeModelCpuResources), cpu);
        Set(session, nameof(BattleSession.NativeModelTextureCache), cache);
        Set(session, nameof(BattleSession.NativeEffectMemory), memory);
        Set(session, nameof(BattleSession.NativeFileIo), NativeBattleFileIoState.FromColdProcess());
        Set(session, nameof(BattleSession.NativeFileStaging), NativeBattleFileStaging.Unknown());
        Set(session, nameof(BattleSession.NativeEffectIoResult), 0);
        return session;
    }

    private static BattleModelRoster Roster(bool firstParty, bool secondParty) => new([
        firstParty ? (BattleModelOwner)FirstParty : FirstEnemy,
        secondParty ? (BattleModelOwner)SecondParty : SecondEnemy]);

    private static void Load(BattleSession session, ushort request, BattleModelOwner owner, bool restorePending = false)
    {
        Queue(session, request, owner);
        for (int callback = 0; session.NativeModelLoader!.Task is not null && callback < 24; callback++)
        {
            Invoke(session, "PumpNativeFileIo482590");
            Poll(session);
            if (restorePending && callback == 0)
            {
                NativeBattleModelLoader pending = session.NativeModelLoader!;
                Assert.NotNull(pending.Task);
                NativeBattleModelLoader restored = NativeBattleTestSnapshots.Independent(pending);
                Assert.Equal(owner, restored.Task!.Owner);
                Set(session, nameof(BattleSession.NativeModelLoader), restored);
            }
        }
        Assert.Null(session.NativeModelLoader!.Task);
    }

    private static void Queue(BattleSession session, ushort request, BattleModelOwner owner) => Invoke(session, "QueueNativeModelResource507080", session.NativeGraphics!, request, owner, 0u);

    private static void Poll(BattleSession session)
    {
        object[] args = [session.NativeGraphics!];
        Invoke(session, "PollNativeModelResource507070", args);
        Set(session, nameof(BattleSession.NativeGraphics), args[0]);
    }

    private static NativeBattleEffectMemory Drain(BattleSession session)
    {
        NativeBattleEffectMemory memory = session.NativeEffectMemory!;
        return BattleSession.DrainNativeTextureUploads(memory, memory.Textures!, session.NativeFileStaging);
    }

    private static NativeBattleModelDatSource EnemySource(byte model)
    {
        NativeBattleModelDatSource source = NativeBattleEnemyOwnedHeapTests.Source(72) with { ModelId = model };
        return WithTim(source, 10);
    }

    private static NativeBattleModelDatSource WithTim(NativeBattleModelDatSource source, int textureSection)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan());
        byte[][] sections = Enumerable.Range(0, count).Select(index => source.Bytes.AsSpan(source.Section(index), source.Section(index + 1) - source.Section(index)).ToArray()).ToArray();
        byte[] tim = new byte[8 + 8 + 524 + 14];
        BinaryPrimitives.WriteInt32LittleEndian(tim, 1);
        BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(12), 9);
        BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(16), 524);
        BinaryPrimitives.WriteInt16LittleEndian(tim.AsSpan(22), 224);
        BinaryPrimitives.WriteInt16LittleEndian(tim.AsSpan(24), 256);
        BinaryPrimitives.WriteInt16LittleEndian(tim.AsSpan(26), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(30), 0x1F);
        BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(540), 14);
        BinaryPrimitives.WriteInt16LittleEndian(tim.AsSpan(548), 1);
        BinaryPrimitives.WriteInt16LittleEndian(tim.AsSpan(550), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(552), 0x0101);
        sections[textureSection] = tim;
        return source with { Bytes = Dat(sections) };
    }

    private static ImmutableArray<byte> Dat(byte[][] sections)
    {
        int cursor = 4 + 4 * (sections.Length + 1);
        byte[] bytes = new byte[cursor + sections.Sum(section => section.Length)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, sections.Length);
        for (int index = 0; index < sections.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + index * 4), cursor);
            sections[index].CopyTo(bytes, cursor);
            cursor += sections[index].Length;
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + 4 * sections.Length), cursor);
        return bytes.ToImmutableArray();
    }

    private static void Invoke(BattleSession session, string name, params object[] args) => typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, args);

    private static void Set(BattleSession session, string property, object value) => typeof(BattleSession).GetProperty(property)!.SetValue(session, value);
}
