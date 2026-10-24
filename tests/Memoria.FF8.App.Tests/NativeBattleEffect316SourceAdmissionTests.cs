using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect316SourceAdmissionTests
{
    [Fact]
    public void Fresh_original130_has_current_geometry_pose_and_live_texture_copy_owners_without_source_mutation()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT316_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT316_ADMISSION_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a fresh standalone and a new source-admission evidence directory.");
        Assert.False(Directory.Exists(output));
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        string unchanged = JsonSerializer.Serialize(source);
        BattleSession session = BattleSession.Restore(source);
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
        Assert.True(session.NativeEntryReady);
        Assert.Equal(0, source.Frame);
        Assert.Equal(441, source.Definition.EncounterId);
        Assert.Equal(124, source.Definition.StageId);
        NativeBattleGraphicsState graphics = source.NativeGraphics!;
        Assert.Null(graphics.OriginalEffect);
        Assert.Equal(0, source.NativeEffectMemory!.Active);
        string casterId = graphics.ActorIds[3]!;
        BattleCombatantDefinition caster = session.GetCombatantDefinition(casterId);
        Assert.Equal(130, caster.ModelId);
        Assert.Equal(0, caster.SceneSlot);
        Assert.True(caster.InitialActive);
        Assert.False(caster.InitialHidden);
        Assert.False(caster.InitialSuspended);
        Assert.False(caster.InitialUntargetable);
        BattleEnemyPlacement placement = Assert.Single(assets.LoadEncounter(441).Enemies);
        Assert.Equal(130, placement.ModelId);
        Assert.Equal(0, placement.Slot);
        Assert.True(placement.Enabled);
        NativeBattleGraphicsModel model = graphics.Models[3];
        Assert.Equal((byte)146, model.ModelId4);
        Assert.NotEqual(0, model.Flags0 & 2);
        Assert.NotNull(model.Transform);
        Assert.NotNull(model.GeometryWorldMatrix);
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(caster).PoseSource!;
        Assert.Equal(29, pose.BoneCount);
        Assert.Equal(29, NativeBattleBodyBoneMatrices.Require(model, pose).Length);
        NativeBattleModelCpuResources cpu = source.NativeModelCpuResources!;
        NativeBattleFileStaging staging = source.NativeFileStaging!;
        int rowIndex = model.ActiveResourceRow100!.Value;
        NativeBattleModelCpuResourceRow row = cpu.Rows[rowIndex];
        NativeBattleModelCpuReference geometryReference = row.Require(16);
        byte[] geometry = NativeBattleModelEnemyHeap.Read(cpu, staging, geometryReference, row.Lengths[16]);
        int objectCount = I32(geometry, 0);
        Assert.Equal(2, objectCount);
        int markers = 0;
        int maximumVertices = 0;
        List<object> objects = [];
        for (int item = 0; item < objectCount; item++)
        {
            int offset = I32(geometry, 4 + 4 * item);
            int cursor = offset;
            int groups = I16(geometry, cursor);
            cursor += 2;
            int vertices = 0;
            for (int group = 0; group < groups; group++)
            {
                int bone = I16(geometry, cursor);
                int count = I16(geometry, cursor + 2);
                Assert.InRange(bone, 0, 28);
                Assert.True(count >= 0);
                vertices += count;
                cursor += 4 + count * 6;
            }
            cursor = (cursor + 3) & ~3;
            int triangles = U16(geometry, cursor);
            int quads = U16(geometry, cursor + 2);
            Assert.Equal(0, U16(geometry, cursor + 4));
            Assert.Equal(0, U16(geometry, cursor + 6));
            cursor += 12;
            for (int kind = 0; kind < 2; kind++)
            {
                int count = kind == 0 ? triangles : quads;
                int stride = kind == 0 ? 16 : 20;
                for (int face = 0; face < count; face++)
                {
                    for (int corner = 0; corner < (kind == 0 ? 3 : 4); corner++)
                    {
                        Assert.InRange(U16(geometry, cursor + face * stride + 2 * corner) & 0xFFF, 0, vertices - 1);
                    }
                    int markerOffset = cursor + face * stride + 14;
                    ushort marker = U16(geometry, markerOffset);
                    uint address = checked(geometryReference.SharedAddress + (uint)markerOffset);
                    NativeBattleFileStaging isolated = staging.Write(address, BitConverter.GetBytes((ushort)(marker & 0x7FFF)));
                    Assert.Equal((ushort)(marker & 0x7FFF), BinaryPrimitives.ReadUInt16LittleEndian(isolated.Read(address, 2)));
                    Assert.Equal(marker, BinaryPrimitives.ReadUInt16LittleEndian(staging.Read(address, 2)));
                    markers++;
                }
                cursor += count * stride;
            }
            Assert.InRange(cursor, 0, geometry.Length);
            maximumVertices = Math.Max(maximumVertices, vertices);
            objects.Add(new { Object = item, Vertices = vertices, Triangles = triangles, Quads = quads });
        }
        Assert.Equal(1661, markers);
        Assert.Equal(907, maximumVertices);
        Assert.True(cpu.ScratchMaximum >= maximumVertices);
        Assert.True(maximumVertices * 16 <= 0x8000);
        NativeBattleModelCpuReference textureReference = row.Require(48);
        byte[] motion = NativeBattleModelEnemyHeap.Read(cpu, staging, textureReference, row.Lengths[48]);
        Assert.Equal(60, motion.Length);
        Assert.Equal((ushort)6, U16(motion, 2));
        Assert.Equal(new byte[] { 2, 2, 93, 4, 8, 10, 0, 0 }, motion.AsSpan(6, 8).ToArray());
        ushort allocation = model.TextureAllocationMask!.Value;
        Assert.True(System.Numerics.BitOperations.PopCount((uint)allocation) >= 3);
        Assert.NotNull(source.NativeModelTextureCache);
        Assert.NotNull(source.NativeEffectMemory.Textures);
        List<NativeBattleTextureCopy> copies = [];
        for (int variant = 0; variant <= 10; variant++)
        {
            NativeBattleTextureCopy copy = NativeBattleTextureMotion.Plan50C780(System.Collections.Immutable.ImmutableArray.CreateRange(motion), allocation, 1, variant)!;
            Assert.Equal((short)4, copy.Width);
            Assert.Equal((short)8, copy.Height);
            copies.Add(copy);
        }
        BattleNativeAction action = Assert.Single(assets.NativeActions, value => value.Command == 8 && value.ActionId == 203 && value.MagicId == 316 && value.KernelSection == 3 && value.KernelIndex == 203);
        Assert.Equal((byte)15, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        Assert.Equal(unchanged, JsonSerializer.Serialize(source));
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
        Directory.CreateDirectory(output!);
        File.WriteAllText(Path.Combine(output!, "current-input-admission.json"), JsonSerializer.Serialize(new { Status = "passed", Scope = "Read-only current source; no action execution or later-pose claim", source.Frame, graphics.Callback, source.Definition.EncounterId, source.Definition.StageId, PhysicalCaster = 3, caster.Level, Bones = 29, Objects = objects, MarkerWords = markers, MaximumVertices = maximumVertices, cpu.ScratchMaximum, TextureReference = textureReference, AllocationMask = allocation, TextureCopies = copies, SnapshotUnchanged = true }));
    }

    private static short I16(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset, 2));
    private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    private static int I32(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
}
