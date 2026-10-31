using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect289Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect289Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect289ControllerTests
{
    [Fact]
    public void Entry_preserves_warm_COMMON_and_defers_current_contact()
    {
        Harness harness = new();
        NativeBattleEffect289Frame frame = Initialize(Inactive(), Resources(), harness.Inputs());
        Assert.Single(frame.Effect.Pools);
        Assert.Null(frame.State.RootBankStorage);
        Assert.Equal(ModelAddress(4), frame.State.ReadDword(CasterGlobal));
        Assert.Equal(new byte[] { 0xA5 }, frame.Memory.Arena!.Read(0, 1));
        Assert.Equal(new byte[] { 0x5A }, frame.Memory.Arena.Read(ScratchOffset, 1));
        Assert.Equal(0, harness.ContactReads);
        Assert.Empty(harness.Operations);
        Assert.Equal(new byte[8], TaskView(Read(frame, RootBank), 12, 8).Read(0, 8));
    }

    [Theory]
    [InlineData(0u, SecondPacketOffset, 1u)]
    [InlineData(1u, FirstPacketOffset, 0u)]
    [InlineData(0x80000000u, FirstPacketOffset, 0u)]
    public void Root_packet_parity_uses_entire_old_DWORD(uint parity, int offset, uint next)
    {
        Harness harness = new();
        NativeBattleEffect289Frame frame = Initialize(Inactive(), Resources(), harness.Inputs());
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteDword(16, parity));
        frame = AdvanceTask(frame, RootBank, Resources(), harness.Inputs()).Frame;
        Assert.Equal(ArenaAddress + (uint)offset, frame.State.ReadDword(CursorGlobal));
        Assert.Equal(next, Read(frame, RootBank).ReadDword(16));
        Assert.Equal((ushort)1, Read(frame, RootBank).ReadWord(12));
        Assert.Equal(0, harness.ContactReads);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Main_birth_captures_actual_first_contact_and_transfers_exclusive_warm_banks(int target)
    {
        Harness harness = new() { Target = target };
        NativeBattleEffect289Resources resources = Resources();
        NativeBattleEffect289Frame frame = Initialize(Inactive(), resources, harness.Inputs());
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        frame = AdvanceTask(frame, RootBank, resources, harness.Inputs()).Frame;
        NativeBattleModulePoolTask main = Read(frame, ArenaAddress + MainOffset);
        Assert.Equal(ModelAddress(target), main.ReadDword(0x10));
        Assert.Equal((ushort)500, main.ReadWord(0x14));
        Assert.Equal((ushort)100, main.ReadWord(0x16));
        Assert.Equal((ushort)1000, main.ReadWord(0x18));
        Assert.Equal((ushort)777, main.ReadWord(0x1A));
        Assert.Equal((ushort)1, main.ReadWord(12));
        Assert.Equal(1, harness.ContactReads);
        Assert.Equal((byte)0x6B, frame.Effect.Pools[2].Slots[63].ReadByte(0xC));
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena!.Read(0, 1));
        Assert.Equal((ushort)200, BinaryPrimitives.ReadUInt16LittleEndian(frame.State.ReadStatic(resources, MeshAddresses[0] + 14 + 10 * 8, 2).Read(0, 2)));
        Assert.Equal((ushort)119, BinaryPrimitives.ReadUInt16LittleEndian(resources.Meshes[0].Bytes.AsSpan().Slice(14 + 10 * 8, 2)));
        Assert.Equal(3, harness.Operations.OfType<NativeBattleEffect289DrawCustom108>().Count());
    }

    [Fact]
    public void Target_particle_uses_current_angle_and_signed_model_minimum_while_programme_uses_captured_anchor()
    {
        Harness harness = new();
        NativeBattleEffect289Resources resources = Resources();
        NativeBattleEffect289Frame frame = Initialize(Inactive(), resources, harness.Inputs());
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        frame = AdvanceTask(frame, RootBank, resources, harness.Inputs()).Frame;
        uint mainAddress = ArenaAddress + MainOffset;
        frame = Write(frame, mainAddress, Read(frame, mainAddress).WriteWord(12, 14));
        _ = frame.Random.Draw(out ushort x).Draw(out ushort z).Draw(out ushort y);
        frame = AdvanceMain(frame, mainAddress, resources, harness.Inputs()).Frame;
        NativeBattleModulePoolTask particle = Read(frame, ArenaAddress);
        Assert.Equal(0x106EC38u, particle.ReadDword(0xC));
        Assert.Equal(unchecked((ushort)(500 + (x & 511) - 256)), particle.ReadWord(0x14));
        Assert.Equal(unchecked((ushort)(1000 + (z & 511) - 256)), particle.ReadWord(0x18));
        Assert.Equal(unchecked((ushort)(777 + ((y * -3000) >> 15))), particle.ReadWord(0x16));
        Assert.Equal((ushort)16, particle.ReadWord(0x22));
        NativeBattleModulePoolTask child = Read(frame, ArenaAddress + ProgrammeOffset);
        Assert.Equal((ushort)777, child.ReadWord(0xE));
        Assert.Throws<NotSupportedException>(() => child.ReadWord(0x12));
        NativeBattleEffectArena caller = harness.Programmes[0].Caller;
        Assert.Equal(100, BinaryPrimitives.ReadInt32LittleEndian(caller.Read(24, 4)));
        Assert.Equal(-128, BinaryPrimitives.ReadInt32LittleEndian(caller.Read(32, 4)));
        Assert.Equal(ArenaAddress + ScratchOffset, BinaryPrimitives.ReadUInt32LittleEndian(caller.Read(36, 4)));
    }

    [Fact]
    public void Native_timeline_retires_root_with_last_particle_bytes_and_exact_shared_CRT_stream()
    {
        Harness harness = new();
        NativeBattleEffect289Resources resources = Resources();
        NativeBattleEffect289Frame frame = Initialize(Inactive(), resources, harness.Inputs());
        int frames = 0;
        while (frame.Effect.ReturnedDescriptor != 0 && frames < 50)
        {
            frame = Advance(frame, resources, harness.Inputs());
            frames++;
        }
        Assert.Equal(37, frames);
        Assert.Empty(frame.Effect.Pools);
        Assert.Equal((ushort)36, BinaryPrimitives.ReadUInt16LittleEndian(frame.State.RootBankStorage!.Read(12, 2)));
        Assert.Equal(66, harness.Operations.OfType<NativeBattleEffect289DrawCustom108>().Count());
        Assert.Single(harness.Operations.OfType<NativeBattleEffect289Contact506690>());
        Assert.Equal(new NativeBattleEffect289Sound501330(0x61B85, 0, 128), Assert.Single(harness.Operations.OfType<NativeBattleEffect289Sound501330>()));
        Assert.Equal(20 * 4, harness.Programmes.Count(item => item.Source == MainProgrammeAddress));
        Assert.Equal(14 * 15, harness.Programmes.Count(item => item.Source == ChildProgrammeAddress));
        NativeCrtRandomState expected = NativeCrtRandomState.FromColdProcess();
        for (int index = 0; index < 282; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        NativeBattleEffectArena particleBank = frame.Memory.Arena!;
        int survivors = 0;
        for (int index = 0; index < ParticleCount; index++)
        {
            int offset = ParticleOffset + index * ParticleStride;
            if (particleBank.Read(offset, 1)[0] == 0)
            {
                continue;
            }
            survivors++;
            Assert.Equal(0x106EC38u, BinaryPrimitives.ReadUInt32LittleEndian(particleBank.Read(offset + 0xC, 4)));
            Assert.Equal((ushort)15, BinaryPrimitives.ReadUInt16LittleEndian(particleBank.Read(offset + 0x1A, 2)));
            Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(particleBank.Read(offset + 0x22, 2)));
        }
        Assert.Equal(1, survivors);
        NativeBattleEffect289Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect289Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Advance(restored, resources, harness.Inputs()))));
    }

    [Fact]
    public void Capture_callbacks_preserve_complete_native_timeline_CRT_and_scope_actual_task_and_node_births()
    {
        List<string> native = [];
        Harness plainHarness = new();
        NativeBattleEffect289Resources plainResources = Resources();
        NativeBattleEffect289Frame plain = Initialize(Inactive(), plainResources, plainHarness.Inputs());
        while (plain.Effect.ReturnedDescriptor != 0 && native.Count < 50)
        {
            plain = Advance(plain, plainResources, plainHarness.Inputs());
            native.Add(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(plain)));
        }
        Assert.Equal(37, native.Count);
        Harness capturedHarness = new();
        NativeBattleEffect289Resources capturedResources = Resources();
        List<NativeBattlePilotVisualIdentity> draws = [];
        List<NativeBattlePilotVisualIdentity> nodes = [];
        NativeBattleEffect289Inputs originalInputs = capturedHarness.Inputs();
        NativeBattleEffect289Inputs inputs = originalInputs with
        {
            Execute = (current, operation) =>
            {
                if (operation is NativeBattleEffect289DrawCustom108 or NativeBattleEffect289DrawSprite180)
                {
                    Assert.NotNull(current.PilotIdentity);
                    draws.Add(current.PilotIdentity!.Value);
                }
                return originalInputs.Execute(current, operation);
            },
            DrawProgrammeNode = (current, source, node, caller) =>
            {
                Assert.NotNull(current.PilotIdentity);
                Assert.Equal(node.ResourceOffset, current.PilotIdentity!.Value.Primitive);
                Assert.NotEqual(0, current.PilotIdentity.Value.ResourceGeneration);
                nodes.Add(current.PilotIdentity.Value);
                return originalInputs.DrawProgrammeNode(current, source, node, caller);
            }
        };
        var captured = Initialize(Inactive() with { CapturePresentation = true }, capturedResources, inputs);
        for (int index = 0; index < native.Count; index++)
        {
            captured = Advance(captured, capturedResources, inputs);
            Assert.Equal(native[index], JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(captured)));
            Assert.Null(captured.PilotIdentity);
        }
        Assert.True(draws.Count > 66);
        Assert.NotEmpty(nodes);
        Assert.Equal(3, draws.Take(3).Select(identity => identity.Primitive).Distinct().Count());
        Assert.All(draws.Concat(nodes), identity => Assert.True(identity.Generation > 0));
        Assert.Equal(plainHarness.Operations, capturedHarness.Operations);
    }

    private static NativeBattleEffect289Frame Inactive()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with
        {
            Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim).Write(0, [0xA5]).Write(63 * ParticleStride + 0xC, [0x6B]).Write(ScratchOffset, [0x5A]),
            GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, null),
            ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(),
            ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null),
            ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0()
        };
        NativeBattleOriginalEffectState effect = new(289, 288, new(289, Entry, Initializer), new(4, 0, 0x7F, 0, 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, NativeCrtRandomState.FromColdProcess(), NativeBattleEffect289State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEffect289Resources Resources()
    {
        ImmutableArray<NativeBattleStaticMeshResource> meshes = MeshAddresses.Select((address, index) =>
        {
            byte[] bytes = new byte[MeshLengths[index]];
            Put(bytes, 0, 8 + MeshVertexCount * 8);
            Put(bytes, 4, MeshVertexCount);
            for (int vertex = 0; vertex < MeshVertexCount; vertex++)
            {
                Word(bytes, 12 + vertex * 8, -vertex * 20);
                Word(bytes, 14 + vertex * 8, 119);
            }
            int cursor = 8 + MeshVertexCount * 8;
            int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
            for (int group = 0; group < 8; group++)
            {
                int count = group == 3 ? 74 : group == 7 ? 6 : 0;
                Put(bytes, cursor, count);
                cursor += 4 + count * strides[group];
            }
            return new NativeBattleStaticMeshResource(0, address, bytes.ToImmutableArray());
        }).ToImmutableArray();
        ImmutableArray<NativeBattleModuleResource> sprites = SpriteAddresses.Select((address, index) =>
        {
            byte[] bytes = new byte[SpriteLengths[index]];
            Put(bytes, 0, index == 0 ? 0x09010524 : 0x07010524);
            int frames = index == 2 ? 16 : 12;
            Word(bytes, 8, frames);
            for (int phase = 0; phase <= frames; phase++)
            {
                Word(bytes, 10 + phase * 2, 64);
            }
            Put(bytes, 64, 1);
            return new NativeBattleModuleResource(0, address, 0, 0, bytes.ToImmutableArray());
        }).ToImmutableArray();
        byte[] camera = new byte[CameraLength];
        Word(camera, 0, 2);
        Word(camera, 2, 8);
        Word(camera, 4, 52);
        Word(camera, 6, CameraLength);
        byte[] point = new byte[8];
        Word(point, 6, 9);
        return new(Programme(MainProgrammeAddress, MainProgrammeLength, 2, 4, 20), Programme(ChildProgrammeAddress, ChildProgrammeLength, 1, 1, 15), meshes, sprites, new("FF8_EN:10724E0", camera.ToImmutableArray()), 0x61B85, point.ToImmutableArray(), new byte[LookupLength].ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Programme(uint address, int length, int geometries, int nodes, int frames)
    {
        byte[] bytes = new byte[length];
        Put(bytes, 0, geometries);
        Put(bytes, 4, 256);
        for (int geometry = 0; geometry < geometries; geometry++)
        {
            Put(bytes, 8 + geometry * 4, 64);
        }
        Put(bytes, 64, 20);
        Put(bytes, 68, 1);
        Put(bytes, 72, 1);
        Put(bytes, 256, nodes);
        Put(bytes, 260, frames);
        for (int node = 0; node < nodes; node++)
        {
            Put(bytes, 264 + node * 4, 8 + nodes * 4 + node * 8);
        }
        return new(address, bytes.ToImmutableArray());
    }

    private static void Put(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
    private static void Word(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), unchecked((ushort)value));

    private sealed class Harness
    {
        public int Target { get; init; } = 6;
        public int ContactReads { get; private set; }
        public List<NativeBattleEffect289Operation> Operations { get; } = [];
        public List<(uint Source, NativeBattleEffectArena Caller)> Programmes { get; } = [];

        public NativeBattleEffect289Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, frame => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), frame => new(null, 100, 160, 108), (frame, address) => new((int)((address - ModelAddress(0)) / 156), 2) { Transform = new(new(0, 0, 0), new(100, 200, 300), 999, 777, -123), MinimumY146 = -3000 }, frame =>
        {
            ContactReads++;
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(4, 8, 13, -1, 153, 289, null));
            (packets, _) = packets.AppendContact(new(new((byte)Target, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            return packets;
        }, (frame, address) => new(frame, PointView(new(500, 100, 1000), 777)), (frame, source, retained) =>
        {
            Assert.Equal((ushort)9, BinaryPrimitives.ReadUInt16LittleEndian(source.Read(6, 2)));
            return new(frame, retained.Write(0, PointView(new(100, 200, 300)).Read(0, 6)));
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        }, (frame, source, node, caller) =>
        {
            Programmes.Add((source.Address, caller));
            return frame;
        });
    }
}
