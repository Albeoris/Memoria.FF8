using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect290SourceAdmissionTests
{
    [Fact]
    public void Fresh_source_current_pose_has_a_native_first_selected_fragment_with_nonzero_divisor()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT290_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT290_ADMISSION_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a fresh standalone and a new input-admission evidence directory.");
        Assert.False(Directory.Exists(output));
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        string unchanged = JsonSerializer.Serialize(source);
        BattleSession session = BattleSession.Restore(source);
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
        Assert.True(session.NativeEntryReady);
        Assert.Equal(0, source.Frame);
        Assert.Null(source.NativeGraphics!.OriginalEffect);
        NativeBattleEffectMemory memory = source.NativeEffectMemory!;
        Assert.Equal(0, memory.Active);
        NativeBattleGraphicsModel model = source.NativeGraphics.Models[3];
        BattleCombatantDefinition caster = session.GetCombatantDefinition(source.NativeGraphics.ActorIds[3]!);
        Assert.Equal(109, caster.ModelId);
        Assert.Equal((byte)125, model.ModelId4);
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(caster).PoseSource ?? throw new NotSupportedException("The source requires its current skeleton.");
        Assert.Equal(28, pose.BoneCount);
        NativeBattleEffect290Resources resources = game.ReadNativeEffect290Resources();
        NativeBattleFileStaging staging = session.NativeFileStaging!;
        NativeBattleEffect290State state = memory.Effect290 ?? throw new NotSupportedException("The source requires its retained script owners.");
        (state, staging) = state.BindSources(staging, resources);
        uint script = NativeBattleEffect290Resources.ScriptRanges[0].Address;
        uint cursor = script + 2;
        while (true)
        {
            short opcode = resources.ReadScriptWord(staging, cursor);
            cursor += 2;
            if (opcode == 0x1090)
            {
                break;
            }
            if (opcode == 0x10F0)
            {
                cursor += 4;
            }
            else if (opcode != 0x10FF)
            {
                throw new NotSupportedException("The first selected fragment recipe requires its actual bounded script grammar.");
            }
        }
        List<int> selected = [];
        short operand;
        while ((operand = resources.ReadScriptWord(staging, cursor)) != -1)
        {
            Assert.InRange(operand, (short)0, checked((short)(pose.BoneCount - 1)));
            selected.Add(operand);
            cursor += 2;
        }
        cursor += 2;
        int limit = resources.ReadScriptWord(staging, cursor);
        int speed = resources.ReadScriptWord(staging, cursor + 2);
        int scale = resources.ReadScriptWord(staging, cursor + 4);
        int threshold = resources.ReadScriptWord(staging, cursor + 6);
        Assert.True(limit > 0);
        NativeBattleEffectArena offsets = NativeBattleEffectArena.Clear();
        foreach (int bone in selected)
        {
            offsets = offsets.Write(bone * 16 + 6, BitConverter.GetBytes((short)2));
        }
        NativeBattleEffect298PosePreparation prepared = NativeBattleEffect298Pose.Prepare662C00(model, pose, offsets, NativeBattleEffectArena.Clear().MarkUnknown(0, 96));
        NativeBattleGraphicsModel composedModel = NativeBattleBodyBoneMatrices.Compose5095B0(prepared.Model, model.GeometryWorldMatrix ?? throw new NotSupportedException("The source requires its current world matrix."));
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(composedModel, pose);
        NativeBattleModelCpuResources cpu = session.NativeModelCpuResources!;
        Assert.Null(model.ActiveStagedResourceRow100);
        NativeBattleModelCpuResourceRow row = cpu.Rows[model.ActiveResourceRow100 ?? throw new NotSupportedException("The source requires its current resource row.")];
        NativeBattleModelCpuReference geometry = row.Require(16);
        NativeBattleModelCpuReference skeleton = row.Require(12);
        for (int bone = 0; bone < bones.Length; bone++)
        {
            int matrix = checked(0x20 + bone * 48);
            NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(bones[bone], null);
            Write(skeleton, matrix, packed.Bytes.AsSpan().Slice(0, 18));
            Write(skeleton, matrix + 20, packed.Bytes.AsSpan().Slice(20, 12));
            Write(skeleton, matrix + 18, BitConverter.GetBytes((short)0));
        }
        foreach (int bone in selected)
        {
            Write(skeleton, checked(0x32 + bone * 48), BitConverter.GetBytes((short)1));
        }
        ushort maximum = cpu.ScratchMaximum ?? throw new NotSupportedException("The source requires its retained scratch maximum.");
        uint scratch = checked(0x196B018u - (uint)maximum * 8);
        uint mask = model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("The source requires its current object mask.");
        NativeBattleGeometryRegisterStep step = new(memory.GeometryRegisters!, memory.ScriptedColorRegisters!);
        int objects = I32(0);
        int draws = 0;
        object? chosen = null;
        for (int obj = 0; obj < objects && chosen is null; obj++)
        {
            if ((mask & (1u << (obj & 31))) == 0)
            {
                continue;
            }
            int position = I32(4 + obj * 4);
            int groups = I16(position);
            position += 2;
            int total = 0;
            for (int group = 0; group < groups; group++)
            {
                int bone = I16(position);
                int count = I16(position + 2);
                position += 4;
                Assert.InRange(bone, 0, bones.Length - 1);
                Assert.InRange(count, 0, maximum - total);
                byte[] current = Read(skeleton, 0x20 + bone * 48, 32);
                short padding = BinaryPrimitives.ReadInt16LittleEndian(current.AsSpan(18));
                Assert.Equal(selected.Contains(bone) ? 1 : 0, padding);
                step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, bones[bone], true, true, padding) };
                for (int vertex = 0; vertex < count; vertex++)
                {
                    uint destination = checked(scratch + (uint)(total * 8));
                    if (padding != 0)
                    {
                        step = NativeBattleGeometry.LoadVector(step, 0, new(I16(position), I16(position + 2), I16(position + 4)), 0);
                        step = NativeBattleGeometry.Transform(step);
                        byte[] bytes = new byte[8];
                        for (int axis = 0; axis < 3; axis++)
                        {
                            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(axis * 2), checked((short)step.Colors.Ir[axis]!.Value));
                        }
                        staging = staging.Write(destination, bytes);
                    }
                    else
                    {
                        staging = staging.Write(destination + 6, BitConverter.GetBytes((short)1));
                    }
                    position += 6;
                    total++;
                }
            }
            position = (position + 3) & ~3;
            int triangles = U16(position);
            int quads = U16(position + 2);
            Assert.Equal(0, U16(position + 4));
            Assert.Equal(0, U16(position + 6));
            position += 12;
            for (int face = 0; face < triangles + quads && chosen is null; face++)
            {
                bool quad = face >= triangles;
                int[] indices = Enumerable.Range(0, quad ? 4 : 3).Select(corner => U16(position + corner * 2) & 0xFFF).ToArray();
                Assert.All(indices, index => Assert.InRange(index, 0, total - 1));
                bool eligible = indices.All(index => staging.Read(checked(scratch + (uint)(index * 8 + 6)), 1)[0] == 0);
                if (eligible)
                {
                    memory = memory.DrawSharedEffectRandom7059E0(out ushort sample);
                    draws++;
                    if (sample <= threshold)
                    {
                        int[][] points = indices.Take(3).Select(index => Point(staging.Read(checked(scratch + (uint)(index * 8)), 6))).ToArray();
                        int[] a = Enumerable.Range(0, 3).Select(axis => points[0][axis] - points[1][axis]).ToArray();
                        int[] b = Enumerable.Range(0, 3).Select(axis => points[2][axis] - points[0][axis]).ToArray();
                        byte?[] rotation = step.Geometry.Rotation20.ToArray();
                        for (int axis = 0; axis < 3; axis++)
                        {
                            byte[] bytes = BitConverter.GetBytes(a[axis]);
                            for (int lane = 0; lane < 4; lane++)
                            {
                                rotation[axis * 8 + lane] = bytes[lane];
                            }
                        }
                        step = step with { Geometry = step.Geometry with { Rotation20 = rotation.ToImmutableArray() }, Colors = step.Colors with { Ir = b.Select(value => (int?)value).ToImmutableArray() } };
                        step = NativeBattleGeometry.OuterProduct45E670(step);
                        int[] cross = step.Colors.Mac.Select(value => value!.Value).ToArray();
                        int largest = cross.Select(value => value < 0 ? unchecked(-value) : value).Max();
                        step = step with { Geometry = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(step.Geometry, unchecked((uint)largest)) };
                        int leading = checked((int)step.Geometry.Lzcr!.Value);
                        int[] reduced = cross.Select(value => leading < 18 ? value >> (18 - leading) : value).ToArray();
                        step = step with { Colors = step.Colors with { Ir = reduced.Select(value => (int?)value).ToImmutableArray() } };
                        step = NativeBattleReflectBeam135Geometry.Square45F930(step);
                        int sum = unchecked(step.Colors.Mac[0]!.Value + step.Colors.Mac[1]!.Value + step.Colors.Mac[2]!.Value);
                        step = step with { Geometry = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(step.Geometry, unchecked((uint)sum)) };
                        int even = checked((int)step.Geometry.Lzcr!.Value) & ~1;
                        int tableIndex = even <= 24 ? sum >> (24 - even) : unchecked(sum << (even - 24));
                        Assert.InRange(tableIndex, 64, 255);
                        ushort table = BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(NativeBattleEffect290Profile.NormalizationAddress + (uint)(2 * (tableIndex - 64)), 2));
                        int length = unchecked(table << ((31 - even) >> 1)) >> 12;
                        Assert.True(length > 0, "The native first selected face reaches a zero normal divisor in this current pose.");
                        int[] velocity = reduced.Select(value => unchecked(speed * value) / length).ToArray();
                        chosen = new { Object = obj, Face = face, Quad = quad, Indices = indices, Points = points, Cross = cross, Reduced = reduced, TableIndex = tableIndex, Table = table, Length = length, Velocity = velocity, GateSample = sample };
                    }
                }
                position += quad ? 20 : 16;
            }
        }
        Assert.NotNull(chosen);
        Assert.Equal(unchanged, JsonSerializer.Serialize(source));
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
        Directory.CreateDirectory(output!);
        File.WriteAllText(Path.Combine(output!, "current-input-admission.json"), JsonSerializer.Serialize(new { Status = "passed", Scope = "Current snapshot pose and first selected fragment recipe only; no effect execution, future pose, pool or complete action claim", source.Frame, source.NativeGraphics.Callback, Caster = 3, Bones = bones.Length, Geometry = geometry.PhysicalAddress, Skeleton = skeleton.PhysicalAddress, Scratch = scratch, ScratchMaximum = maximum, Script = script, SelectedBones = selected, Limit = limit, Speed = speed, Scale = scale, Threshold = threshold, ProcessGateDraws = draws, FirstSelectedFace = chosen }));

        byte[] Read(NativeBattleModelCpuReference reference, int offset, int length) => NativeBattleModelEnemyHeap.Read(cpu, staging, reference with { Offset = checked(reference.Offset + offset) }, length);
        short I16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(Read(geometry, offset, 2));
        ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Read(geometry, offset, 2));
        int I32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(geometry, offset, 4));
        static int[] Point(byte[] bytes) => [BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4))];

        void Write(NativeBattleModelCpuReference reference, int offset, ReadOnlySpan<byte> bytes)
        {
            if (reference.Bank < 3)
            {
                NativeBattleModelCpuBank bank = cpu.Cpu.Banks[reference.Bank].Write(checked(reference.Offset + offset), bytes);
                cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(reference.Bank, bank) } };
            }
            else
            {
                staging = staging.Write(checked(reference.SharedAddress + (uint)offset), bytes);
            }
        }
    }
}
