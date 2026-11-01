using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleModelGeometryExtentTests
{
    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    [InlineData((byte)6)]
    [InlineData((byte)7)]
    [InlineData((byte)8)]
    [InlineData((byte)9)]
    [InlineData((byte)10)]
    public void Installed_body_and_weapon_geometry_fit_their_current_extents_after_row_reuse_and_restore(byte model)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        NativeBattleModelCpuResourceRow stale = NativeBattleModelCpuResourceRow.Unknown() with { Lengths = ImmutableDictionary<int, int>.Empty.Add(16, 1) };
        ImmutableArray<NativeBattleModelCpuResourceRow> rows = Enumerable.Repeat(stale, 11).ToImmutableArray();
        NativeBattleModelDatSource body = assets.ReadNativeModelDatSource(model, false, 0);
        NativeBattleModelResourceCompletion loaded = Complete(body, NativeBattleModelCpuBanks.Unknown(), rows, null);
        NativeBattleModelDatSource? weapon = model == 7 ? null : assets.ReadNativeModelDatSource(model, true, 0);
        if (weapon is not null)
        {
            loaded = Complete(weapon, loaded.Cpu, loaded.Rows, 1);
        }
        loaded = JsonSerializer.Deserialize<NativeBattleModelResourceCompletion>(JsonSerializer.Serialize(loaded))!;
        ReadGeometry(loaded.Rows[0]);
        if (loaded.ActiveLinkedRow is int linked)
        {
            ReadGeometry(loaded.Rows[linked]);
        }

        NativeBattleModelResourceCompletion Complete(NativeBattleModelDatSource source, NativeBattleModelCpuBanks cpu, ImmutableArray<NativeBattleModelCpuResourceRow> current, int? linked)
        {
            NativeBattleModelLoadTask task = NativeBattleModelLoader.Queue507080((ushort)(model | (source.Linked ? 0x1000 : 0)), 0, 0, 0, 0).Task!;
            return NativeBattleModelResourceCopies.Complete(new(task, source, new([])), cpu, current, 0, linked);
        }

        void ReadGeometry(NativeBattleModelCpuResourceRow row)
        {
            NativeBattleModelCpuReference geometry = row.Require(16);
            NativeBattleModelCpuReference skeleton = row.Require(12);
            int bones = loaded.Cpu.Banks[skeleton.Bank].Read(skeleton.Offset, 1)[0];
            int extent = row.Lengths[16];
            ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) =>
            {
                Assert.InRange(offset, 0, extent - length);
                return loaded.Cpu.Banks[geometry.Bank].Read(geometry.Offset + offset, length);
            }, bones);
            Assert.NotEmpty(meshes);
            Assert.All(meshes, mesh => mesh.Validate());
        }
    }
}
