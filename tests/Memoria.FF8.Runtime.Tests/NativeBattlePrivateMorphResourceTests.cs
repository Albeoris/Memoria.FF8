using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePrivateMorphResourceTests
{
    [Theory]
    [InlineData(314, 1, false, false)]
    [InlineData(314, 2, false, false)]
    [InlineData(314, 3, true, false)]
    [InlineData(314, 4, true, false)]
    [InlineData(316, 1, false, false)]
    [InlineData(316, 2, false, false)]
    [InlineData(316, 3, true, false)]
    [InlineData(316, 4, true, false)]
    [InlineData(316, 1, false, true)]
    [InlineData(316, 2, false, true)]
    [InlineData(316, 3, true, true)]
    [InlineData(316, 4, true, true)]
    public void Private_body_walk_keeps_packets_and_context_receipts_for_pinned_or_oversized_sources(int family, int domain, bool quad, bool expanded)
    {
        byte[] bytes = NativeBattleEffect301BodyTests.Geometry(quad);
        NativeBattleOffsetBodyResourceFixture source = NativeBattleOffsetBodyResourceFixture.Create(bytes, domain);
        BodyFixture numeric = new(family, bytes);
        BodyFixture typed = new(family, bytes);
        numeric.Draw(null, expanded);
        typed.Draw(source.Source, expanded);
        Assert.NotEmpty(typed.Batches);
        Assert.Equal(NativeBattleTestSnapshots.Json(numeric.Batches), NativeBattleTestSnapshots.Json(typed.Batches));
        Assert.Equal(numeric.Staging.Read(BodyFixture.Scratch, expanded ? 64 : 32), typed.Staging.Read(BodyFixture.Scratch, expanded ? 64 : 32));
        source.AssertCursor(typed.Staging, NativeBattleTemporaryMemory.Address + 32, quad ? 52 : 44, bytes);
        int cursor = expanded ? 0x74 : 0x54;
        source.AssertCursor(typed.Staging, NativeBattleTemporaryMemory.Address + 32 + (uint)cursor, quad ? 52 : 44, bytes);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void Morph_body_uses_the_same_bounded_geometry_and_retains_its_published_face_cursor(int domain, bool quad)
    {
        byte[] bytes = NativeBattleEffect301BodyTests.Geometry(quad);
        NativeBattleOffsetBodyResourceFixture fixture = NativeBattleOffsetBodyResourceFixture.Create(bytes, domain);
        NativeBattleEffect314MorphBodyStep Draw(NativeBattleModelGeometrySource source)
        {
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
            NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().Write(4, BitConverter.GetBytes(0x1000u)).Write(0x1C, BitConverter.GetBytes(1u)).Write(0x10, BitConverter.GetBytes((short)320)).Write(0x12, BitConverter.GetBytes((short)216)).Write(0x14, BitConverter.GetBytes(0x123456u));
            return NativeBattleEffect314Morph.DrawBodyWithPresentation(memory, temporary, NativeBattleEffectArena.Clear(), source, [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null)], NativeBattleEffectArena.Clear(), new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), 0x1000, 32);
        }
        NativeBattleEffect314MorphBodyStep expected = Draw(NativeBattleOffsetBodyResourceFixture.Create(bytes, 0).Source);
        NativeBattleEffect314MorphBodyStep actual = Draw(fixture.Source);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Batch), NativeBattleTestSnapshots.Json(actual.Batch));
        Assert.Equal(expected.VertexScratch.Read(0, 32), actual.VertexScratch.Read(0, 32));
        NativeBattleFileStaging staging = NativeBattleOffsetBodyContextMemory.Publish(NativeBattleFileStaging.Unknown(), NativeBattleTemporaryMemory.Address, actual.Temporary156, 156);
        fixture.AssertCursor(staging, NativeBattleTemporaryMemory.Address, quad ? 52 : 44, bytes);
    }

    [Theory]
    [InlineData(314, false, false)]
    [InlineData(314, false, true)]
    [InlineData(314, true, false)]
    [InlineData(314, true, true)]
    [InlineData(316, false, false)]
    [InlineData(316, false, true)]
    [InlineData(316, true, false)]
    [InlineData(316, true, true)]
    public void Production_pose_reader_and_marker_writer_keep_instance_authority_source_knownness_and_selected_COW_domain(int family, bool remastered, bool logical)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        byte[] geometry = NativeBattleEffect301BodyTests.Geometry(false);
        geometry[59] |= 0x80;
        int origin = logical ? layout.BankLength + 4 : 4;
        int geometryOrigin = origin + 128;
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[0].BindPartyResource(origin, skeleton.ToImmutableArray(), 0, 50).BindPartyResource(origin + 52, skeleton.ToImmutableArray(), 52, 12).BindPartyResource(geometryOrigin, geometry.ToImmutableArray(), 0, geometry.Length);
        NativeBattleModelCpuReference rig = new(0, origin) { Layout = layout, ResourceOnly = logical };
        NativeBattleModelCpuReference mesh = new(0, geometryOrigin) { Layout = layout, ResourceOnly = logical };
        NativeBattleModelCpuResourceRow row = cpu.Rows[0].Set(12, rig).Set(16, mesh) with { Lengths = ImmutableDictionary<int, int>.Empty.Add(12, 64).Add(16, geometry.Length) };
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) }, Rows = cpu.Rows.SetItem(0, row) };
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1]);
        NativeBattleActionMatrix matrix = new([1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000], 123456, -234567, 345678, 4096);
        BattleSession session = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        BattleDefinition definition = (BattleDefinition)typeof(BattleSession).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
        typeof(BattleDefinition).GetProperty(nameof(BattleDefinition.NativeGraphics))!.SetValue(definition, new NativeBattleGraphicsDefinition(null!, [new("party", 3, new([0, 0]) { PoseSource = source })], []));
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeModelCpuResources))!.SetValue(session, cpu);
        NativeBattleGraphicsModel model = new(0, 3) { ModelId4 = 3, ActiveResourceRow100 = 0, CurrentBodyBoneMatrices = new(NativeBattleBodyBoneMatrices.Identity(source), [matrix]) };
        NativeBattleGraphicsState graphics = new(NativeBattleGraphicsModels.ClearBank().ToImmutableArray().SetItem(0, model), ["party", null, null, "enemy", null, null, null], 0, 0);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x100, [17]) };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        object frame = family == 314 ? (object)new NativeBattleEffect314Frame(memory, new(314, 313, new(314, NativeBattleEffect314Profile.Entry, NativeBattleEffect314Profile.Initializer), null, 0), new(42), NativeBattleEffect314State.FromColdProcess()) { Staging = staging } : new NativeBattleEffect316Frame(memory, new(316, 315, new(316, NativeBattleEffect316Profile.Entry, NativeBattleEffect316Profile.Initializer), null, 0), new(42), NativeBattleEffect316State.FromColdProcess()) { Staging = staging };
        object Invoke(string suffix, params object?[] arguments) => typeof(BattleSession).GetMethod($"{suffix}", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, arguments)!;
        NativeBattlePackedMatrix32 packed = (NativeBattlePackedMatrix32)Invoke($"ReadNativeEffect{family}BoneMatrix", graphics, frame, 0x1D972C0u, 0);
        Assert.Equal(matrix.Rotation, packed.ReadMatrix().Rotation);
        Assert.Equal(matrix.X, packed.ReadMatrix().X);
        Assert.False(packed.Known[18]);
        Assert.False(packed.Known[19]);
        NativeBattleEffectArena unrelated = (NativeBattleEffectArena)Invoke($"ReadNativeEffect{family}Arena", graphics, frame, 0x20DFAB8u + 0x100, 1);
        Assert.Equal((byte)17, unrelated.Read(0, 1)[0]);
        NativeBattleModelGeometrySource selected = (NativeBattleModelGeometrySource)Invoke($"ReadNativeEffect{family}Geometry", graphics, frame, 0x1D972C0u, true);
        NativeBattleEffect64GeometryCleanup.Clear(selected, (offset, value) => frame = Invoke($"WriteNativeEffect{family}Geometry", graphics, frame, 0x1D972C0u, offset, BitConverter.GetBytes(value)), NativeBattleEffectArena.Clear());
        Assert.NotSame(bank, session.NativeModelCpuResources!.Cpu.Banks[0]);
        Assert.Equal((byte)0, (byte)(selected.Read(59, 1)[0] & 0x80));
        Assert.Equal((byte)0x80, (byte)(NativeBattleModelResourceView.Open(cpu, staging, new NativeBattleResourcePointer(mesh, null, geometry.Length), geometry.Length).Read(59, 1)[0] & 0x80));
        Assert.Throws<TargetInvocationException>(() => Invoke($"WriteNativeEffect{family}Geometry", graphics, frame, 0x1D972C0u, geometry.Length, new byte[1]));
        if (family == 314)
        {
            string before = NativeBattleTestSnapshots.Json(session.NativeModelCpuResources!);
            NativeBattleActionMatrix replacement = matrix with { X = 999999 };
            object?[] arguments = [graphics, frame, 0x1D972C0u, 0, NativeBattlePackedMatrix32.FromMatrix(replacement, 77)];
            typeof(BattleSession).GetMethod("WriteNativeEffect314BoneMatrix", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, arguments);
            graphics = (NativeBattleGraphicsState)arguments[0]!;
            Assert.Equal(999999, graphics.Models[0].CurrentBodyBoneMatrices!.Matrices[0].X);
            Assert.Equal(before, NativeBattleTestSnapshots.Json(session.NativeModelCpuResources!));
        }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void Shards_keep_material_rng_and_live_marker_COW_when_two_objects_alias_the_same_faces(int domain, bool quad)
    {
        byte[] original = NativeBattleEffect301BodyTests.Geometry(quad);
        byte[] bytes = new byte[original.Length + 4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 12);
        original.AsSpan(8).CopyTo(bytes.AsSpan(12));
        int face = quad ? 56 : 48;
        NativeBattlePartyCpuLayout layout = domain >= 3 ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        bool logical = domain is 2 or 4;
        int origin = logical ? layout.BankLength + 4 : 4;
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[0].BindPartyResource(origin, bytes.ToImmutableArray(), 0, bytes.Length);
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) } };
        NativeBattleResourcePointer pointer = new(new(0, origin) { Layout = layout, ResourceOnly = logical }, null, bytes.Length);
        NativeBattleModelGeometrySource selected = NativeBattleModelGeometrySource.OpenEditor(cpu, NativeBattleFileStaging.Unknown(), pointer, (offset, length) => NativeBattleModelResourceView.Open(cpu, NativeBattleFileStaging.Unknown(), pointer, bytes.Length).Read(offset, length));
        (NativeBattleEffect314Frame Frame, int BoneWrites) Draw(bool typed)
        {
            byte[] numeric = bytes.ToArray();
            NativeBattleEffect314Frame frame = NativeBattleEffect314ControllerTests.Frame();
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() }, Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[532]).Write(BodyFixture.Scratch, new byte[32]) };
            uint main = NativeBattleEffect314Controller.CommonAddress + NativeBattleEffect314Profile.MainOffset;
            uint offsets = main + 0x20;
            frame = NativeBattleEffect314Controller.WriteBytes(frame, main + 0x4724, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(main + NativeBattleEffect314Profile.FragmentOffset)), 4);
            frame = NativeBattleEffect314Controller.WriteBytes(frame, main + 0xF20, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(main + NativeBattleEffect314Profile.SparkOffset)), 4);
            frame = NativeBattleEffect314Controller.WriteBytes(frame, offsets, NativeBattleEffectArena.Clear().Write(6, BitConverter.GetBytes((short)1)), 16);
            byte[] skeleton = new byte[64];
            skeleton[0] = 1;
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
            NativeBattlePoseSource pose = new(skeleton.ToImmutableArray(), [1]);
            NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
            NativeBattleEffect298BodySource body = typed ? new(pose, new(new(0, 0, 0), [], false), [matrix], selected) : new(pose, new(new(0, 0, 0), [], false), [matrix], 0x1000, (address, length) => NativeBattleEffectArena.Clear().Write(0, numeric.AsSpan((int)(address - 0x1000), length)));
            body = body with { VertexScratchAddress = BodyFixture.Scratch, VertexScratchLength = 32 };
            int writes = 0;
            NativeBattleEffect314Inputs inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, (_, _) => new(0, 3) { GeometryWorldMatrix = matrix.ReadMatrix(), Color40 = 0x123456, SequenceState = new() { MeshMask124 = 3 } }, _ => matrix, null!, _ => 0, NativeBattleEffect314Controller.ReadBytes, null!, (_, _, _) => 0x2000, (current, length) =>
            {
                NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging!, length);
                return new(current with { Staging = allocation.Staging }, allocation.Address);
            }, (current, length) => current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging!, length) }, (current, operation) =>
            {
                if (operation is NativeBattleEffect314WriteArena write)
                {
                    if (write.Address == 0x2000)
                    {
                        writes++;
                        return current;
                    }
                    if (write.Address >= 0x1000 && write.Address < 0x1000 + numeric.Length)
                    {
                        write.Data.Read(0, write.Length).CopyTo(numeric, (int)(write.Address - 0x1000));
                        return current;
                    }
                    return NativeBattleEffect314Controller.WriteBytes(current, write.Address, write.Data, write.Length);
                }
                return current;
            }) { ReadBody = (_, _) => body, WriteBoneMatrix = typed ? (current, _, _, _) =>
            {
                writes++;
                return current;
            } : null, WriteGeometry = typed ? (current, _, offset, data) =>
            {
                cpu = NativeBattleModelResourceView.WritePartySection(cpu, current.Staging!, pointer, offset, data);
                return current;
            } : null };
            frame = NativeBattleEffect314Controller.EmitShards664420(frame, null!, inputs, 0x1D972C0, offsets, main + 0x4728, 30000, 7, 8, 9);
            if (!typed)
            {
                Assert.Equal((byte)0x80, (byte)(numeric[face + 15] & 0x80));
            }
            return (frame, writes);
        }
        var expected = Draw(false);
        var actual = Draw(true);
        Assert.Equal(1, actual.BoneWrites);
        Assert.Equal(expected.BoneWrites, actual.BoneWrites);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Frame.Effect), NativeBattleTestSnapshots.Json(actual.Frame.Effect));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Frame.State), NativeBattleTestSnapshots.Json(actual.Frame.State));
        Assert.Equal((byte)0x80, (byte)(selected.Read(face + 15, 1)[0] & 0x80));
        Assert.Equal((byte)0, (byte)(NativeBattleModelResourceView.Open(cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) } }, NativeBattleFileStaging.Unknown(), pointer, bytes.Length).Read(face + 15, 1)[0] & 0x80));
        if (logical)
        {
            new NativeBattleOffsetBodyResourceFixture(selected, cpu, pointer).AssertCursor(actual.Frame.Staging!, NativeBattleTemporaryMemory.Address, face, selected.Read(0, bytes.Length));
        }
        else
        {
            NativeBattleFileStaging restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(NativeBattleTestSnapshots.Json(actual.Frame.Staging!));
            Assert.Equal(pointer.Cpu!.PhysicalAddress + (uint)face, restored.ReadUInt32(NativeBattleTemporaryMemory.Address));
        }
        uint main = NativeBattleEffect314Controller.CommonAddress + NativeBattleEffect314Profile.MainOffset;
        Assert.Equal(main + NativeBattleEffect314Profile.FragmentOffset + NativeBattleEffect314Profile.FragmentStride, NativeBattleEffect314Controller.Read(actual.Frame, main).ReadDword(0x4724));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void Morph_wrapper_keeps_logical_source_and_publishes_pose_through_the_typed_writer(int domain)
    {
        byte[] bytes = NativeBattleEffect301BodyTests.Geometry(false);
        NativeBattleOffsetBodyResourceFixture source = NativeBattleOffsetBodyResourceFixture.Create(bytes, domain);
        BodyFixture fixture = new(314, bytes);
        fixture.DrawMorph(source.Source);
        Assert.NotEmpty(fixture.Batches);
        source.AssertCursor(fixture.Staging, NativeBattleTemporaryMemory.Address, 44, bytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Particle_translation_consumes_current_pose_matrix_without_a_numeric_bone_record(bool typed)
    {
        NativeBattleEffect314Frame frame = NativeBattleEffect314ControllerTests.Frame();
        uint program = NativeBattleEffect314Profile.ProgramAddresses[0];
        NativeBattleEffect314Resources resources = new(null!, new("camera", []), new("vibration", []), [], ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(program, [0, 0, 4, 0x10, 0xFF, 0x10]));
        uint offsets = NativeBattleEffect314Controller.CommonAddress + NativeBattleEffect314Profile.MainOffset + 0x20;
        NativeBattleEffect314AddressStep creation = NativeBattleEffect314Controller.SpawnParticle664CD0(frame, resources, 0x1D972C0, offsets, program);
        NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Y = -1000 }, null);
        int reads = 0;
        NativeBattleEffect314Inputs inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, (_, _) => new(0, 3) { GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770() }, null!, null!, null!, (_, address, length) =>
        {
            Assert.False(typed);
            Assert.Equal(0x2014u, address);
            Assert.Equal(12, length);
            reads++;
            return NativeBattleEffectNodeContextMemory.Copy(NativeBattleEffect314Controller.PackedArena(matrix), 20, NativeBattleEffectArena.Clear(), 0, 12);
        }, null!, (_, _, bone) =>
        {
            Assert.False(typed);
            Assert.Equal(0, bone);
            return 0x2000;
        }, null!, null!, null!) { ReadBoneMatrix = typed ? (_, _, bone) =>
        {
            Assert.Equal(0, bone);
            reads++;
            return matrix;
        } : null };
        NativeBattleEffect314Step result = NativeBattleEffect314Controller.AdvanceParticle664D20(creation.Frame, creation.Address, resources, inputs);
        Assert.Equal(2, result.Result);
        Assert.Equal(1, reads);
        Assert.Equal(frame.State.ReadDword(NativeBattleEffect314Profile.RandomGlobal), result.Frame.State.ReadDword(NativeBattleEffect314Profile.RandomGlobal));
        Assert.Equal((byte)0, NativeBattleEffect314Controller.Read(result.Frame, creation.Address).ReadByte(0x2E));
    }

    private sealed class BodyFixture
    {
        internal const uint Scratch = 0x1900000;
        private readonly int family;
        private readonly byte[] bytes;
        private NativeBattleEffect314Frame frame314;
        private NativeBattleEffect316Frame frame316;
        internal List<NativeBattleGfGeometryBatch> Batches = [];
        internal NativeBattleFileStaging Staging => family == 314 ? frame314.Staging! : frame316.Staging!;

        internal BodyFixture(int family, byte[] bytes)
        {
            this.family = family;
            this.bytes = bytes;
            NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
            NativeBattleFileStaging Stage() => NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[532]).Write(Scratch, new byte[64]);
            frame314 = new(Memory(), new(314, 313, new(314, NativeBattleEffect314Profile.Entry, NativeBattleEffect314Profile.Initializer), null, 0), new(42), NativeBattleEffect314State.FromColdProcess().WriteDword(NativeBattleEffect314Profile.CursorGlobal, 0x20F0000)) { Staging = Stage() };
            frame316 = new(Memory(), new(316, 315, new(316, NativeBattleEffect316Profile.Entry, NativeBattleEffect316Profile.Initializer), null, 0), new(42), NativeBattleEffect316State.FromColdProcess().WriteDword(NativeBattleEffect316Profile.CursorGlobal, 0x20F0000)) { Staging = Stage() };
        }

        private NativeBattleEffectArena Special(uint address, int length, Func<NativeBattleEffectArena> fallback)
        {
            if (address >= 0x1000 && address + length <= 0x1000 + bytes.Length)
            {
                return NativeBattleEffectArena.Clear().Write(0, bytes.AsSpan((int)(address - 0x1000), length));
            }
            uint? value = address switch { 0x1D8E054 => 0x1901018, 0x1D98B3C => Scratch, 0x1D972E8 => 0x123456, 0x1D9733C => 1, _ => null };
            return value is uint word ? NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(word)) : fallback();
        }

        internal void Draw(NativeBattleModelGeometrySource? source, bool expanded)
        {
            NativeBattlePackedMatrix32 bone = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
            NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
            if (family == 314)
            {
                NativeBattleEffect314Inputs inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, null!, _ => bone, _ => projection, _ => 0, (frame, at, count) => Special(at, count, () => NativeBattleEffect314Controller.ReadBytes(frame, at, count)), (_, _) => 0x1000, null!, (frame, count) =>
                {
                    NativeBattleTemporaryAllocation allocated = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging!, count);
                    return new(frame with { Staging = allocated.Staging }, allocated.Address);
                }, (frame, count) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging!, count) }, (frame, operation) => operation switch
                {
                    NativeBattleEffect314WriteArena write => NativeBattleEffect314Controller.WriteBytes(frame, write.Address, write.Data, write.Length),
                    NativeBattleEffect314DrawBatch draw => Capture(frame, draw.Batch),
                    _ => frame
                }) { ReadGeometrySource = source is { } selected ? (_, _) => selected : null, ReadBoneMatrix = (_, _, _) => bone };
                frame314 = NativeBattleEffect314Body.Draw663590(frame314, inputs, 0x1D972C0, -1024, 0, 0);
            }
            else
            {
                NativeBattleEffect316Inputs inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, null!, _ => bone, _ => projection, _ => 0, (frame, at, count) => Special(at, count, () => NativeBattleEffect316Controller.ReadBytes(frame, at, count)), (_, _) => 0x1000, null!, (frame, count) =>
                {
                    NativeBattleTemporaryAllocation allocated = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging!, count);
                    return new(frame with { Staging = allocated.Staging }, allocated.Address);
                }, (frame, count) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging!, count) }, (frame, operation) => operation switch
                {
                    NativeBattleEffect316WriteArena write => NativeBattleEffect316Controller.WriteBytes(frame, write.Address, write.Data, write.Length),
                    NativeBattleEffect316DrawBatch draw => Capture(frame, draw.Batch),
                    _ => frame
                }) { ReadGeometrySource = source is { } selected ? (_, _) => selected : null, ReadBoneMatrix = (_, _, _) => bone };
                frame316 = expanded ? NativeBattleEffect316Body.Draw661CC0(frame316, null!, inputs, 0x20DFAB8, 0x1D972C0, Scratch, -1024, 0, 0) : NativeBattleEffect316Body.Draw661550(frame316, null!, inputs, 0x1D972C0, -1024, 0, 0);
            }
        }

        internal void DrawMorph(NativeBattleModelGeometrySource source)
        {
            NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
            byte[] skeleton = new byte[64];
            skeleton[0] = 1;
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
            NativeBattlePoseSource pose = new(skeleton.ToImmutableArray(), [1]);
            NativeBattleEffect298BodySource body = new(pose, new(new(0, 0, 0), [], false), [matrix], source) { VertexScratchAddress = Scratch, VertexScratchLength = 32 };
            uint offsets = NativeBattleTemporaryMemory.Address + 0x1000;
            frame314 = frame314 with { Staging = frame314.Staging!.Write(offsets, new byte[16]) };
            int writes = 0;
            int prepares = 0;
            NativeBattleEffect314Inputs inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, (_, _) => new(0, 3) { GeometryWorldMatrix = matrix.ReadMatrix(), Color40 = 0x123456, SequenceState = new() { MeshMask124 = 1 } }, _ => matrix, _ => new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), _ => 0, NativeBattleEffect314Controller.ReadBytes, (_, _) => throw new InvalidOperationException("Logical morph must not request a numeric geometry address."), (_, _, _) => throw new InvalidOperationException("Logical morph must not request a numeric matrix address."), (frame, length) =>
            {
                NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging!, length);
                return new(frame with { Staging = allocation.Staging }, allocation.Address);
            }, (frame, length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging!, length) }, (frame, operation) =>
            {
                switch (operation)
                {
                    case NativeBattleEffect314WriteArena write:
                        return NativeBattleEffect314Controller.WriteBytes(frame, write.Address, write.Data, write.Length);
                    case NativeBattleEffect314DrawBatch draw:
                        return Capture(frame, draw.Batch);
                    case NativeBattleEffect314Prepare662C00:
                        prepares++;
                        return frame;
                    default:
                        return frame;
                }
            }) { ReadBody = (_, _) => body, WriteBoneMatrix = (frame, model, bone, packed) =>
            {
                Assert.Equal(0x1D972C0u, model);
                Assert.Equal(0, bone);
                Assert.Equal(matrix.ReadMatrix().Rotation, packed.ReadMatrix().Rotation);
                writes++;
                return frame;
            } };
            frame314 = NativeBattleEffect314Morph.Draw663850(frame314, null!, inputs, 0x1D972C0, offsets, 8, 9);
            Assert.Equal(1, writes);
            Assert.Equal(2, prepares);
        }

        private NativeBattleEffect314Frame Capture(NativeBattleEffect314Frame frame, NativeBattleGfGeometryBatch batch)
        {
            Batches.Add(batch);
            return frame;
        }
        private NativeBattleEffect316Frame Capture(NativeBattleEffect316Frame frame, NativeBattleGfGeometryBatch batch)
        {
            Batches.Add(batch);
            return frame;
        }
    }
}
