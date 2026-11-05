using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelResourceViewTests
{
    [Fact]
    public void Section_bounds_prevent_reads_into_an_observed_neighbor_and_slices_keep_the_same_publication()
    {
        NativeBattleModelCpuResources cpu = Cpu([1, 2, 3, 4, 5, 6]);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleResourcePointer pointer = new(new(0, 1), null, 3);
        NativeBattleModelResourceView view = NativeBattleModelResourceView.Open(cpu, staging, pointer, 3);
        Assert.Equal(3, view.Length);
        Assert.Equal(new byte[] { 2, 3, 4 }, view.Read(0, 3));
        Assert.Equal(new byte[] { 3 }, view.Slice(1, 2).Slice(0, 1).Read(0, 1));
        Assert.Empty(view.Slice(3, 0).Read(0, 0));
        Assert.Throws<InvalidDataException>(() => view.Read(2, 2));
        Assert.Throws<InvalidDataException>(() => view.Read(-1, 1));
        Assert.Throws<InvalidDataException>(() => view.Slice(1, int.MaxValue));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.Open(cpu, staging, pointer, 4));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.Open(cpu, staging, new NativeBattleModelCpuReference(0, 65535), 2));
        byte[] detached = view.Read(0, 3);
        detached[0] = 99;
        Assert.Equal((byte)2, view.Read(0, 1)[0]);
    }

    [Fact]
    public void Missing_bytes_and_nonsection_pointer_owners_remain_explicit()
    {
        NativeBattleModelCpuResources cpu = Cpu([1]);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(NativeBattleFileStaging.Address, [7]);
        Assert.Throws<NotSupportedException>(() => NativeBattleModelResourceView.Open(cpu, staging, new NativeBattleModelCpuReference(0, 0), 2).Read(0, 2));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelResourceView.Open(cpu, staging, new NativeBattleResourcePointer(null, NativeBattleFileStaging.Address, 2), 2).Read(0, 2));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelResourceView.Open(cpu, staging, new NativeBattleResourcePointer(null, null) { CpuRow = 0 }, 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.Open(cpu, staging, new NativeBattleResourcePointer(new(0, 0), NativeBattleFileStaging.Address), 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.Open(cpu, staging, new NativeBattleResourcePointer(null, uint.MaxValue, 2), 2));
    }

    [Fact]
    public void Cpu_views_retain_their_publication_and_a_new_view_reads_the_replacement()
    {
        NativeBattleModelCpuResources original = Cpu([1, 2, 3]);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleModelCpuReference reference = new(0, 0);
        NativeBattleModelResourceView prior = NativeBattleModelResourceView.Open(original, staging, reference, 3);
        NativeBattleModelCpuResources replaced = original with { Cpu = original.Cpu with { Banks = original.Cpu.Banks.SetItem(0, original.Cpu.Banks[0].Write(0, [7, 8, 9])) } };
        Assert.Equal(new byte[] { 1, 2, 3 }, prior.Read(0, 3));
        Assert.Equal(new byte[] { 7, 8, 9 }, NativeBattleModelResourceView.Open(replaced, staging, reference, 3).Read(0, 3));
    }

    [Fact]
    public void Shared_views_observe_alias_writes_and_transaction_rollback_without_a_second_copy()
    {
        uint address = NativeBattleModelCpuResources.EnemyAddress;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, [1, 2, 3]);
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        NativeBattleModelResourceView shared = NativeBattleModelResourceView.Open(cpu, staging, new NativeBattleModelCpuReference(3, 0), 3);
        NativeBattleModelResourceView numeric = NativeBattleModelResourceView.Open(cpu, staging, new NativeBattleResourcePointer(null, address, 3), 3);
        staging.Write(address + 1, [8]);
        Assert.Equal(new byte[] { 1, 8, 3 }, shared.Read(0, 3));
        Assert.Equal(shared.Read(0, 3), numeric.Read(0, 3));
        NativeBattleModelResourceView temporary;
        using (NativeBattleFileStagingTransaction transaction = new(staging))
        {
            temporary = NativeBattleModelResourceView.Open(cpu, transaction.View, new NativeBattleModelCpuReference(3, 0), 3);
            transaction.View.Write(address + 1, [9]);
            Assert.Equal(new byte[] { 1, 9, 3 }, temporary.Read(0, 3));
            Assert.Equal(new byte[] { 1, 8, 3 }, shared.Read(0, 3));
        }
        Assert.Equal(new byte[] { 1, 8, 3 }, shared.Read(0, 3));
        Assert.Throws<ObjectDisposedException>(() => temporary.Read(0, 1));
    }

    [Fact]
    public void Legacy_pointer_json_and_retained_alias_metadata_open_without_new_saved_fields()
    {
        const string json = "{\"Cpu\":{\"Bank\":0,\"Offset\":1},\"StagingAddress\":null,\"Length\":2}";
        NativeBattleResourcePointer pointer = JsonSerializer.Deserialize<NativeBattleResourcePointer>(json)!;
        NativeBattleModelCpuResources cpu = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuResources>(NativeBattleTestSnapshots.Json(Cpu([1, 2, 3])));
        uint row = NativeBattleFileStaging.Address;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().WriteResourcePointer(row, pointer);
        string before = NativeBattleTestSnapshots.Json(staging);
        staging = NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(before);
        NativeBattleResourcePointer restored = staging.ReadResourcePointer(row)!;
        Assert.Equal(pointer, restored);
        Assert.Equal(new byte[] { 2, 3 }, NativeBattleModelResourceView.Open(cpu, staging, restored, 2).Read(0, 2));
        Assert.Throws<NotSupportedException>(() => staging.Read(row, 4));
        Assert.Equal(before, NativeBattleTestSnapshots.Json(staging));
        NativeBattleResourcePointer numeric = JsonSerializer.Deserialize<NativeBattleResourcePointer>("{\"Cpu\":null,\"StagingAddress\":26849280}")!;
        staging.Write(numeric.StagingAddress!.Value, [4, 5]);
        Assert.Equal(new byte[] { 4, 5 }, NativeBattleModelResourceView.Open(cpu, staging, numeric, 2).Read(0, 2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Party_body_and_linked_loading_rebinding_restore_and_replacement_use_current_section_owners(bool remastered)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        (cpu, staging) = Load(cpu, staging, Source(false, 2, 11), 0, null);
        (cpu, staging) = Load(cpu, staging, Source(true, 3, 22), 0, 1);
        NativeBattleModelResourceView priorBody = NativeBattleModelResourceView.Open(cpu, staging, cpu.Rows[0].Require(12), 64);
        NativeBattleModelResourceView priorLinked = NativeBattleModelResourceView.Open(cpu, staging, cpu.Rows[1].Require(12), 64);
        Assert.Equal((byte)11, priorBody.Read(2, 1)[0]);
        Assert.Equal((byte)22, priorLinked.Read(2, 1)[0]);
        Assert.Equal(cpu.Rows[0].Require(8).Offset, cpu.Rows[1].Require(12).Offset);
        Assert.Equal(layout, cpu.Rows[1].Require(12).EffectiveLayout);
        uint directory = NativeBattleFileStaging.Address + 0x1000;
        uint row = directory + 0x100;
        staging.Write(directory, Dat([Animation(4), new byte[4], Animation(5)]));
        NativeBattleGraphicsModel model = new(1, 2) { ModelId4 = 0, Flags2 = 4, ActiveResourceRow100 = 0, OriginalResourceRow104 = 0, LinkedTextureResourceIndex = 1, LinkedTransform = new(new(0, 0, 0), new(0, 0, 0)), SequenceState = new() { Default2 = 6 } };
        NativeBattleVictoryResourceBinding bound = NativeBattleVictoryResources.Bind5022C0(staging, cpu, model, row, directory);
        NativeBattleGraphicsResource original = new([0, 0]) { AnimationFrames = [], Geometry = [new([], [])], LinkedGeometry = [new([], [])] };
        Pilot restored = NativeBattleTestSnapshots.RestoreJson<Pilot>(NativeBattleTestSnapshots.Json(new Pilot(cpu, bound.Staging, bound.Model)));
        NativeBattleGraphicsResource resolved = NativeBattleVictoryResources.Resolve(restored.Staging, restored.Cpu, restored.Model, original);
        Assert.Equal((byte)11, resolved.PoseSource!.Skeleton[2]);
        Assert.Equal((byte)22, resolved.LinkedPoseSource!.Skeleton[2]);
        Assert.Equal(new byte[] { 4 }, resolved.AnimationFrames.ToArray());
        Assert.Equal(new byte[] { 5 }, resolved.LinkedAnimationFrames.ToArray());
        Assert.True(original.Geometry!.Value.Equals(resolved.Geometry!.Value));
        Assert.True(original.LinkedGeometry!.Value.Equals(resolved.LinkedGeometry!.Value));
        Assert.Equal(0, restored.Model.OriginalResourceRow104);
        Assert.Equal((short)6, restored.Model.SequenceState!.Locals[7]);
        (cpu, staging) = Load(restored.Cpu, restored.Staging, Source(false, 7, 33), 0, null);
        (cpu, staging) = Load(cpu, staging, Source(true, 8, 44), 0, 1);
        NativeBattleGraphicsResource changed = NativeBattleVictoryResources.Resolve(staging, cpu, restored.Model, original);
        Assert.Equal((byte)33, changed.PoseSource!.Skeleton[2]);
        Assert.Equal((byte)44, changed.LinkedPoseSource!.Skeleton[2]);
        Assert.Equal((byte)11, priorBody.Read(2, 1)[0]);
        Assert.Equal((byte)22, priorLinked.Read(2, 1)[0]);
        Assert.Equal(new byte[] { 4 }, changed.AnimationFrames.ToArray());
        Assert.Equal(new byte[] { 5 }, changed.LinkedAnimationFrames.ToArray());
    }

    private static NativeBattleModelCpuResources Cpu(byte[] bytes)
    {
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        return cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, cpu.Cpu.Banks[0].Write(0, bytes)) } };
    }

    private static (NativeBattleModelCpuResources Cpu, NativeBattleFileStaging Staging) Load(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, NativeBattleModelDatSource source, int bodyRow, int? linkedRow)
    {
        NativeBattleModelLoader loader = NativeBattleModelLoader.Queue507080(source.Linked ? (ushort)0x1000 : (ushort)0, 1, 0, source.Selector, source.Selector);
        NativeBattleModelLoadStep request = loader.Poll507070(-1, [source], staging);
        NativeBattleModelLoadStep prepared = request.State.Poll507070(source.Bytes.Length, [source], request.Staging);
        NativeBattleModelLoadStep loaded = prepared.State.Poll507070(source.Bytes.Length, [source], prepared.Staging);
        Assert.Null(loaded.State.Task);
        Assert.Equal(0, loaded.Remaining);
        NativeBattleModelResourceCompletion completed = NativeBattleModelResourceCopies.Complete(loaded.Bind!, cpu.Cpu, cpu.Rows, bodyRow, linkedRow);
        Assert.Contains(completed.Cpu.Banks[1].ResourceSegments, segment => segment.Source.Equals(loaded.Bind!.PreparedDat.Bytes));
        NativeBattleModelCpuBank animations = completed.AnimationCopy!.BindTo(cpu.AnimationFrames);
        Assert.Contains(animations.ResourceSegments, segment => segment.Source.Equals(loaded.Bind!.PreparedDat.Bytes));
        return (cpu with { Cpu = completed.Cpu, Rows = completed.Rows, AnimationFrames = animations }, loaded.Staging);
    }

    private static NativeBattleModelDatSource Source(bool linked, byte selector, byte scale)
    {
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        skeleton[2] = scale;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        byte[] geometry = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(geometry.AsSpan(4), 32);
        byte[] textures = [0, 0, 0, 0, 8, 0, 0, 0];
        byte[][] sections = linked ? [skeleton, geometry, Animation(1), new byte[4], new byte[4], [], textures, Animation(1)] : [skeleton, geometry, Animation(1), [], [], textures, Animation(1), new byte[4]];
        return new(0, linked, selector, 310, linked ? "linked.dat" : "body.dat", Dat(sections).ToImmutableArray());
    }

    private static byte[] Animation(byte frames) => [1, 0, 0, 0, 8, 0, 0, 0, frames, 0, 0, 0];

    private static byte[] Dat(byte[][] sections)
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
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + sections.Length * 4), cursor);
        return bytes;
    }

    private sealed record Pilot(NativeBattleModelCpuResources Cpu, NativeBattleFileStaging Staging, NativeBattleGraphicsModel Model);
}
