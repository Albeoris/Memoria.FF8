using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelTextureOwnerTests
{
    private static readonly NativeBattlePrimitiveReference Reference = new("off_B6D070", 0, 0);
    private static readonly NativeBattleModelTextureOwner FirstOwner = new("party-0");
    private static readonly NativeBattleModelTextureOwner ReplacementOwner = new("party-7");
    private static readonly ImmutableArray<uint> Words = [0x07000000, 0x24808080, 0, 0, 80, 0x017A0000, 80u << 16, 0];
    private const int Bucket = 17;

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("party-0", true)]
    public void Owner_requires_a_nonblank_actor_or_explicit_invalidation(string? actorId, bool invalidated)
    {
        Assert.Throws<InvalidDataException>(() => new NativeBattleModelTextureOwner(actorId, invalidated));
    }

    [Fact]
    public void Native_json_omits_model_ownership_and_keeps_existing_dispatch()
    {
        NativeBattleLogicalOrdering ordering = CreateOrdering(null);
        NativeBattleLogicalCommand command = Assert.Single(ordering.Traverse(Tables().WordLengths));
        Assert.DoesNotContain("ModelTextureOwner", NativeBattleTestSnapshots.Json(ordering));
        Assert.DoesNotContain("ModelTextureOwner", NativeBattleTestSnapshots.Json(command));
        Assert.DoesNotContain("ModelTextureOwner", NativeBattleTestSnapshots.Json(new NativeBattleGeometryPrimitive(Bucket, Words, [])));
        Assert.Single(NativeBattleLogicalPasses.Execute(ordering, Tables(), new(0), drawFlags: 3).Commands);
        Probe probe = new();
        Assert.Single(NativeBattleLogicalPasses.Execute(ordering, Tables(), new(0), probe).Commands);
        Assert.Equal(1, probe.SoftwareCalls);
        Assert.Equal(1, probe.NormalCalls);
    }

    [Fact]
    public void Captured_packet_and_extracted_command_keep_their_own_material_identity()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear().Prepend(Bucket, Reference, Words, 0, [], textureSourceOwner: "replacement", presentationActorId: "visible", modelTextureOwner: FirstOwner);
        NativeBattleLogicalOrdering detached = NativeBattleStorageSnapshots.Capture(ordering);
        NativeBattleLogicalOrdering restored = RoundTrip(detached);
        NativeBattleLogicalPacket packet = Assert.Single(restored.Packets);
        Assert.Equal(FirstOwner, packet.ModelTextureOwner);
        NativeBattleLogicalCommand command = RoundTrip(Assert.Single(restored.Traverse(Tables().WordLengths)));
        Assert.Equal("party-0", command.ModelTextureOwner!.RequireActorId());
        Assert.Equal("replacement", command.TextureSourceOwner);
        Assert.Equal("visible", command.PresentationActorId);
        Assert.Equal(Words.Skip(1), command.Words);
    }

    [Fact]
    public void Proxy_aliases_use_the_newest_physical_packet_owner()
    {
        NativeBattleLogicalOrdering ordering = CreateOrdering(FirstOwner).Prepend(Bucket - 1, Reference, Words, 0, [], modelTextureOwner: ReplacementOwner);
        Assert.Equal(2, ordering.Proxies.Length);
        Assert.All(ordering.Proxies, proxy => Assert.Equal(Reference, proxy.Packet));
        Assert.Equal(ReplacementOwner, Assert.Single(ordering.Packets).ModelTextureOwner);
        NativeBattleLogicalCommand command = Assert.Single(ordering.Traverse(Tables().WordLengths));
        Assert.Equal(Bucket, command.OrderingBucket);
        Assert.Equal(ReplacementOwner, command.ModelTextureOwner);
    }

    [Fact]
    public void Raw_overlap_invalidates_the_owner_until_a_complete_packet_replaces_it()
    {
        NativeBattleLogicalOrdering source = CreateOrdering(FirstOwner);
        NativeBattleLogicalOrdering before = NativeBattleStorageSnapshots.Capture(source);
        const uint nextAddress = 0x1D8E058 + 4 * (Bucket - 1);
        Func<NativeBattlePrimitiveReference, int, byte[]> reader = (_, count) =>
        {
            byte[] bytes = PacketBytes();
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, Words[0] | (nextAddress & 0xFFFFFF));
            return bytes[..count];
        };

        NativeBattleLogicalOrdering overlapped;
        using (NativeBattleLogicalOrdering.Builder builder = new(source))
        {
            builder.InvalidatePacketRange(Reference with { ByteOffset = 4 }, 4);
            overlapped = builder.ToImmutable();
        }
        NativeBattleLogicalOrdering invalidated = RoundTrip(overlapped);
        NativeBattleLogicalPacket packet = Assert.Single(invalidated.Packets);
        Assert.True(packet.RequiresPhysicalRead);
        Assert.Equal(NativeBattleModelTextureOwner.InvalidatedOwner, packet.ModelTextureOwner);
        Assert.Equal(FirstOwner, Assert.Single(before.Packets).ModelTextureOwner);
        NativeBattleLogicalCommand command = Assert.Single(invalidated.Traverse(Tables().WordLengths, reader));
        Assert.Equal(NativeBattleModelTextureOwner.InvalidatedOwner, command.ModelTextureOwner);
        MaterialProbe probe = new();
        Assert.Throws<InvalidDataException>(() => NativeBattleLogicalPasses.Execute(invalidated, Tables(), new(0), probe, reader));
        Assert.Equal(0, probe.ValidationCalls);
        Assert.Equal(0, probe.SoftwareCalls);
        Assert.Equal(0, probe.NormalCalls);

        NativeBattleLogicalOrdering known = RoundTrip(invalidated).Prepend(Bucket - 1, Reference, Words, 0, [], modelTextureOwner: ReplacementOwner);
        Assert.Equal(ReplacementOwner, Assert.Single(known.Traverse(Tables().WordLengths)).ModelTextureOwner);
        NativeBattleLogicalOrdering global = RoundTrip(invalidated).Prepend(Bucket - 1, Reference, Words, 0, []);
        Assert.Null(Assert.Single(global.Traverse(Tables().WordLengths)).ModelTextureOwner);
    }

    [Fact]
    public void Model_owned_dispatch_requires_explicit_context_support()
    {
        NativeBattleLogicalOrdering ordering = CreateOrdering(FirstOwner);
        Assert.Throws<NotSupportedException>(() => NativeBattleLogicalPasses.Execute(ordering, Tables(), new(0), drawFlags: 3));
        Probe legacyProbe = new();
        Assert.Throws<NotSupportedException>(() => NativeBattleLogicalPasses.Execute(ordering, Tables(), new(0), legacyProbe));
        Assert.Equal(0, legacyProbe.SoftwareCalls);
        Assert.Equal(0, legacyProbe.NormalCalls);
        MaterialProbe probe = new();
        var execution = NativeBattleLogicalPasses.Execute(ordering, Tables(), new(0), probe);
        Assert.Equal(1, probe.ValidationCalls);
        Assert.Equal(1, probe.SoftwareCalls);
        Assert.Equal(1, probe.NormalCalls);
        Assert.Equal(FirstOwner, Assert.Single(execution.Commands).Command.ModelTextureOwner);
    }

    [Fact]
    public void Common_regional_and_effect_packet_receipts_retain_model_ownership()
    {
        NativeBattleGeometryPrimitive primitive = new(Bucket, Words, []) { ModelTextureOwner = FirstOwner };
        NativeBattleGfGeometryBatch attempts = new([new(0, PacketBytes().ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 32).ToImmutableArray(), primitive)], 32);
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        NativeBattleCommonPacketWrite common = NativeBattleCommonPacketArena.Append(process, NativeBattleLogicalOrdering.Clear(), attempts);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0);
        NativeBattleEffectPacketWrite captured = NativeBattleEffectPacketArena.Append(memory, 256, NativeBattleLogicalOrdering.Clear(), attempts);
        NativeBattleRegionalPacketWrite regional = NativeBattleRegionalPacketArena.Append(memory.Arena!, "regional", 256, NativeBattleEffectArena.Length, NativeBattleLogicalOrdering.Clear(), attempts);
        NativeBattleModulePacketBatch batch = new([new(primitive, 0, 0)], 32);
        NativeBattleEffectPacketWrite module = NativeBattleEffectPacketArena.Append(memory, 256, NativeBattleLogicalOrdering.Clear(), batch, NativeBattleEffectGouraudWrites.FullDword);

        foreach (NativeBattleLogicalOrdering ordering in new[] { common.Ordering, captured.Ordering, regional.Ordering, module.Ordering })
        {
            Assert.Equal(FirstOwner, Assert.Single(ordering.Traverse(Tables().WordLengths)).ModelTextureOwner);
        }

        const uint root = NativeBattleEffectNodeMemory.ArenaAddress;
        memory = memory with { Arena = memory.Arena!.Write(4 * Bucket, BitConverter.GetBytes(uint.MaxValue)) };
        NativeBattleEffectPacketWrite addressed = NativeBattleEffectPacketArena.AppendPrivate(memory, 256, NativeBattleLogicalOrdering.Clear(), attempts, root);
        NativeBattleEffectArena arena = addressed.Memory.Arena!;
        NativeBattleLogicalCommand command = Assert.Single(addressed.Ordering.Traverse(Tables().WordLengths, (reference, count) => arena.Read(reference.ByteOffset, count), root + 4 * Bucket, address => new(BinaryPrimitives.ReadUInt32LittleEndian(arena.Read((int)(address - root), 4)), Bucket)));
        Assert.Equal(FirstOwner, command.ModelTextureOwner);
    }

    private static NativeBattleLogicalOrdering CreateOrdering(NativeBattleModelTextureOwner? owner) =>
        NativeBattleLogicalOrdering.Clear().Prepend(Bucket, Reference, Words, 0, [], modelTextureOwner: owner);

    private static T RoundTrip<T>(T value) => NativeBattleTestSnapshots.RestoreJson<T>(NativeBattleTestSnapshots.Json(value))!;

    private static byte[] PacketBytes()
    {
        byte[] bytes = new byte[Words.Length * 4];
        for (int index = 0; index < Words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 * index), Words[index]);
        }
        return bytes;
    }

    private static NativeBattlePrimitiveTables Tables()
    {
        byte[] lengths = new byte[256];
        lengths[0x24] = 7;
        uint[] preprocessing = new uint[256];
        preprocessing[0x24] = 1;
        ImmutableArray<uint> empty = new uint[256].ToImmutableArray();
        return new(lengths.ToImmutableArray(), new byte[256].ToImmutableArray(), preprocessing.ToImmutableArray(), empty, empty);
    }

    private class Probe : INativeBattleLogicalExecutionContext
    {
        public uint LiveMode => 3;
        public uint LiveDebug => 0;
        public bool HardwareEnabled => true;
        public NativeSoftwarePrimitiveTable SoftwareTable { get; } = new(Tables().PreprocessingHandlers);
        public NativeBattlePcTextureWindowCache? Cache { get; set; }
        public int NormalCalls { get; private set; }
        public int SoftwareCalls { get; private set; }

        public void ExecuteSoftware(NativeBattleLogicalCommand command)
        {
            SoftwareCalls++;
        }

        public void ExecuteNormal(NativeBattleLogicalCommand command, NativeBattlePrimitiveDrawMode mode, byte pass)
        {
            NormalCalls++;
        }

        public NativeBattlePrimitiveDrawMode SynchronizeDrawMode(NativeBattlePrimitiveDrawMode mode) => mode;

        public void EndBatch()
        {
        }
    }

    private sealed class MaterialProbe : Probe, INativeBattleLogicalExecutionContext
    {
        public int ValidationCalls { get; private set; }

        public void ValidateModelTextureOwner(NativeBattleLogicalCommand command)
        {
            Assert.Equal(FirstOwner, command.ModelTextureOwner);
            ValidationCalls++;
        }
    }
}
