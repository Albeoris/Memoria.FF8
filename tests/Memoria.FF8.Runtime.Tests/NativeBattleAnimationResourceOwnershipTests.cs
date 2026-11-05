using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAnimationResourceOwnershipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Complete_body_and_linked_auxiliary_sources_exceed_the_old_bank_without_overlap_or_duplicate_authority(bool remastered)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        NativeBattleModelDatSource body = Source(false, 0x21);
        cpu = Complete(cpu, body);
        NativeBattleModelCpuResources bodyPublication = cpu;
        NativeBattleModelDatSource linked = Source(true, 0x73);
        cpu = Complete(cpu, linked);
        cpu.Validate();
        Assert.Equal(NativeBattleModelCpuBank.Length, cpu.AnimationFrames.Capacity);
        Assert.Empty(cpu.AnimationFrames.KnownRanges);
        Assert.Empty(cpu.AnimationPointers);
        Assert.Equal(2, cpu.AnimationSources.Count);
        Assert.Equal(body.Bytes.AsMemory().Slice(body.Section(6), AuxiliaryLength), cpu.AnimationSources[40].Data);
        Assert.Equal(linked.Bytes.AsMemory().Slice(linked.Section(7), AuxiliaryLength), cpu.AnimationSources[56].Data);
        Assert.Equal(new byte[] { 0x21, 0x21 }, cpu.ReadAnimationHeader(40, AuxiliaryLength - 2, 2));
        Assert.Equal(new byte[] { 0x73, 0x73 }, cpu.ReadAnimationHeader(56, AuxiliaryLength - 2, 2));
        Assert.Throws<InvalidDataException>(() => cpu.ReadAnimationHeader(40, AuxiliaryLength - 1, 2));
        NativeBattleModelCpuResources restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuResources>(NativeBattleTestSnapshots.Json(cpu));
        restored.Validate();
        Assert.Equal(cpu.ReadAnimationHeader(40, 0, AuxiliaryLength), restored.ReadAnimationHeader(40, 0, AuxiliaryLength));
        Assert.Equal(cpu.ReadAnimationHeader(56, 0, AuxiliaryLength), restored.ReadAnimationHeader(56, 0, AuxiliaryLength));
        NativeBattleModelCpuResources replaced = Complete(cpu, Source(false, 0x44));
        Assert.Equal(2, replaced.AnimationSources.Count);
        Assert.Same(cpu.AnimationSources[56], replaced.AnimationSources[56]);
        Assert.Equal(new byte[] { 0x44 }, replaced.ReadAnimationHeader(40, 0, 1));
        Assert.Equal(new byte[] { 0x21 }, bodyPublication.ReadAnimationHeader(40, 0, 1));
        Assert.Equal(new byte[] { 0x21 }, cpu.ReadAnimationHeader(40, 0, 1));
    }

    [Fact]
    public void Old_snapshot_headers_keep_guest_offsets_unknown_tails_and_historical_bytes_after_new_binding()
    {
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with
        {
            AnimationFrames = NativeBattleModelCpuBank.Unknown().Write(0x20, [1, 2, 3]),
            AnimationPointers = ImmutableDictionary<int, int?>.Empty.Add(8, 0x20).Add(24, null)
        };
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(cpu))!.AsObject();
        json.Remove(nameof(NativeBattleModelCpuResources.AnimationSources));
        NativeBattleModelCpuResources old = JsonSerializer.Deserialize<NativeBattleModelCpuResources>(json.ToJsonString())!;
        old.Validate();
        Assert.Empty(old.AnimationSources);
        Assert.Equal(new byte[] { 2, 3 }, old.ReadAnimationHeader(8, 1, 2));
        Assert.Throws<NotSupportedException>(() => old.ReadAnimationHeader(8, 2, 2));
        Assert.Throws<NotSupportedException>(() => old.ReadAnimationHeader(24, 0, 1));
        NativeBattleModelCpuResources current = old.BindAnimation(0, new(0x20, ImmutableArray.Create((byte)9), 8));
        Assert.Equal(new byte[] { 9 }, current.ReadAnimationHeader(8, 0, 1));
        Assert.Equal(new byte[] { 1, 2, 3 }, current.AnimationFrames.ReadGuest(0x20, 3));
        Assert.Equal(old.AnimationPointers, current.AnimationPointers);
        Assert.Equal(new byte[] { 1 }, old.ReadAnimationHeader(8, 0, 1));
        NativeBattleModelCpuResources absent = old.BindAnimation(0, new(0x20, [], 8));
        Assert.Throws<NotSupportedException>(() => absent.ReadAnimationHeader(8, 0, 0));
        Assert.Throws<InvalidDataException>(() => old.BindAnimation(3, new(0, [], 8)));
        Assert.Throws<InvalidDataException>(() => old.BindAnimation(0, new(0, [], 12)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Malformed_animation_source_map_or_owner_fails_validation_and_reads_explicitly(bool nullEntry)
    {
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(NativeBattleModelCpuResources.Unknown()))!.AsObject();
        json[nameof(NativeBattleModelCpuResources.AnimationSources)] = nullEntry ? new JsonObject { ["8"] = null } : null;
        NativeBattleModelCpuResources malformed = JsonSerializer.Deserialize<NativeBattleModelCpuResources>(json.ToJsonString())!;
        Assert.Throws<InvalidDataException>(() => malformed.Validate());
        Assert.Throws<InvalidDataException>(() => malformed.ReadAnimationHeader(8, 0, 1));
    }

    private const int AuxiliaryLength = NativeBattleModelCpuBank.Length + 37;

    private static NativeBattleModelCpuResources Complete(NativeBattleModelCpuResources cpu, NativeBattleModelDatSource source)
    {
        uint callback = source.Linked ? 0x507BF0u : 0x5077B0u;
        NativeBattleModelResourceBind bind = new(new(0, 1, 0, 0, callback, 2), source, new([]));
        NativeBattleModelResourceCompletion completed = NativeBattleModelResourceCopies.Complete(bind, cpu.Cpu, cpu.Rows, 0, source.Linked ? 1 : null);
        return (cpu with { Cpu = completed.Cpu, Rows = completed.Rows }).BindAnimation(1, completed.AnimationCopy!);
    }

    private static NativeBattleModelDatSource Source(bool linked, byte value)
    {
        int auxiliary = linked ? 7 : 6;
        int[] sections = Enumerable.Range(0, auxiliary + 2).Select(index => 64 + index * 16).ToArray();
        sections[^1] = checked(sections[auxiliary] + AuxiliaryLength);
        byte[] bytes = new byte[sections[^1] + 16];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, sections.Length);
        for (int index = 0; index < sections.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + index * 4), sections[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + sections.Length * 4), bytes.Length);
        bytes.AsSpan(sections[auxiliary], AuxiliaryLength).Fill(value);
        return new(0, linked, 0, 0, "model.dat", bytes.ToImmutableArray());
    }
}
