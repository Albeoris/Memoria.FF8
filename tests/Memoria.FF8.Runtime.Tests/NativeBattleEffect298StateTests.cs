using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect298Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect298StateTests
{
    [Fact]
    public void Cold_process_owns_sparse_globals_root_and_pristine_PE_overlay()
    {
        var state = NativeBattleEffect298State.FromColdProcess();
        state.Validate();
        Assert.Equal(new byte[12], state.Read(BaseAddress, 12));
        Assert.Equal(new byte[8], state.Read(CursorGlobal, 8));
        Assert.Equal(new byte[4], state.Read(ArenaGlobal, 4));
        Assert.Equal(new byte[20], state.RootBankStorage!.Read(0, 20));
        Assert.Empty(state.ScriptCounterWrites.Regions);
        Assert.Empty(state.ScriptCounterWrites.UnknownRegions);
        Assert.Equal(new[] { 20, 36, 28, 328, 35316 }, Pools.Select(pool => (int)pool.Stride));
        Assert.Equal(new[] { 1, 45, 64, 8, 1 }, Pools.Select(pool => (int)pool.Capacity));
        Assert.Equal(IoOffset, MainOffset + MainLength);
        Assert.Equal(FirstPacketOffset, IoOffset + IoLength);
        Assert.Equal(SecondPacketOffset, FirstPacketOffset + PacketBankBytes);
        Assert.Equal(ScratchOffset, SecondPacketOffset + PacketBankBytes);
    }

    [Fact]
    public void Written_zero_counter_survives_JSON_and_never_reloads_pristine_resource_value()
    {
        var resources = Resources();
        uint address = ScriptCounterAddresses[0];
        var state = NativeBattleEffect298State.FromColdProcess();
        Assert.Equal((ushort)17, BinaryPrimitives.ReadUInt16LittleEndian(state.ReadStatic(resources, address, 2)));
        state = state.WriteWord(address, 0) with { RootBankStorage = null };
        var restored = JsonSerializer.Deserialize<NativeBattleEffect298State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Null(restored.RootBankStorage);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(restored.ReadStatic(resources, address, 2)));
        Assert.Equal((ushort)17, BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(address, 2).AsSpan()));
        Assert.Equal(2, restored.ScriptCounterWrites.Regions.Single().Data.Length);
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
        restored = restored.WriteWord(address, 9);
        Assert.Equal((ushort)9, BinaryPrimitives.ReadUInt16LittleEndian(restored.ReadStatic(resources, address, 2)));
        Assert.Equal((ushort)17, BinaryPrimitives.ReadUInt16LittleEndian(restored.ReadStatic(resources, ScriptCounterAddresses[1], 2)));
    }

    [Fact]
    public void Imported_or_explicitly_unknown_counter_bytes_cannot_fall_back_to_PE_seed()
    {
        var resources = Resources();
        uint address = ScriptCounterAddresses[0];
        var state = NativeBattleEffect298State.FromUnknown();
        state.Validate();
        Assert.Throws<NotSupportedException>(() => state.ReadStatic(resources, address, 2));
        Assert.Equal(resources.ReadStatic(address - 2, 2).ToArray(), state.ReadStatic(resources, address - 2, 2));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(CasterGlobal));
        state = state.WriteWord(address, 0);
        Assert.Equal(new byte[2], state.ReadStatic(resources, address, 2));
        Assert.Throws<NotSupportedException>(() => state.ReadStatic(resources, ScriptCounterAddresses[1], 2));
        var partial = NativeBattleEffectArena.Clear().MarkUnknown(0, 2).Write(0, new byte[] { 7 });
        var cold = NativeBattleEffect298State.FromColdProcess().WriteView(address, partial, 2);
        Assert.Equal(new byte[] { 7 }, cold.ReadStatic(resources, address, 1));
        Assert.Throws<NotSupportedException>(() => cold.ReadStatic(resources, address + 1, 1));
        var restored = JsonSerializer.Deserialize<NativeBattleEffect298State>(JsonSerializer.Serialize(cold))!;
        restored.Validate();
        Assert.Throws<NotSupportedException>(() => restored.ReadStatic(resources, address, 2));
    }

    [Fact]
    public void Descriptor_gaps_COMMON_and_non_counter_PE_writes_are_rejected()
    {
        var state = NativeBattleEffect298State.FromColdProcess();
        foreach (uint address in new[] { BaseAddress + 12, RootBank, RootDescriptor, CursorGlobal + 8, ArenaGlobal - 1, ArenaGlobal + 4, ArenaAddress, ArenaAddress + ScratchOffset })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Throws<InvalidDataException>(() => state.WriteWord(ScriptCounterAddresses[0] + 2, 1));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(ScriptCounterAddresses[0], 1));
        Assert.Throws<InvalidDataException>(() => (state with { ScriptCounterWrites = state.ScriptCounterWrites.Write(0, new byte[] { 1 }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = NativeBattleEffectArena.Clear().MarkUnknown(20, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.MarkUnknown(12, 1) }).Validate());
    }

    internal static NativeBattleEffect298Resources Resources()
    {
        byte[] camera = new byte[CameraLength];
        W(camera, 0, 2);
        W(camera, 2, 8);
        W(camera, 4, 24);
        W(camera, 6, CameraLength);
        uint[] addresses = [ProgrammeAAddress, ProgrammeBAddress, ProgrammeCAddress, ProgrammeDAddress];
        int[] clears = [ProgrammeAWorkspaceLength, ProgrammeBWorkspaceLength, ProgrammeCWorkspaceLength, ProgrammeDWorkspaceLength];
        byte[] table = new byte[32];
        byte[] spriteTable = new byte[16];
        for (int index = 0; index < 4; index++)
        {
            D(table, index * 8, addresses[index]);
            D(table, index * 8 + 4, (uint)clears[index]);
            D(spriteTable, index * 4, SpriteAddresses[index]);
        }
        byte[] scripts = new byte[ScriptLength];
        foreach (uint address in ScriptCounterAddresses)
        {
            int offset = checked((int)(address - ScriptAddress));
            W(scripts, offset - 4, 0x10FF);
            W(scripts, offset - 2, 0x10F0);
            W(scripts, offset, 17);
            W(scripts, offset + 2, 17);
            W(scripts, offset + 4, ushort.MaxValue);
        }
        byte[] vibration = new byte[VibrationLength];
        D(vibration, 4, 256);
        W(vibration, 256, 44);
        byte[] completion = new byte[33824];
        D(completion, 0, 16);
        D(completion, 4, 8);
        D(completion, 8, 1036);
        W(completion, 12, 624);
        W(completion, 14, 224);
        W(completion, 16, 16);
        W(completion, 18, 32);
        D(completion, 1044, 32780);
        W(completion, 1048, 704);
        W(completion, 1050, 256);
        W(completion, 1052, 64);
        W(completion, 1054, 256);
        byte[] normalization = new byte[NormalizationLength];
        for (int index = 0; index < normalization.Length / 2; index++)
        {
            W(normalization, index * 2, 4096);
        }
        var resources = new NativeBattleEffect298Resources(Source(ProgrammeAAddress, ProgrammeALength, 1, 1, 22), Source(ProgrammeBAddress, ProgrammeBLength, 2, 3, 22), Source(ProgrammeCAddress, ProgrammeCLength, 2, 4, 18), Source(ProgrammeDAddress, ProgrammeDLength, 2, 4, 18), MeshAddresses.Select(Mesh).ToImmutableArray(), SpriteAddresses.Select((address, index) => Sprite(address, SpriteLengths[index], SpriteFrames[index], index == 1 ? (byte)8 : (byte)6)).ToImmutableArray(), new("FF8_EN:1052298", camera.ToImmutableArray()), 0x64230, new(VibrationAddress, new(new string('0', 64), [0, 256], vibration.ToImmutableArray())), new(CompletionFileId, "MA8DEF_0.TIM", completion.ToImmutableArray()), scripts.ToImmutableArray(), table.ToImmutableArray(), spriteTable.ToImmutableArray(), new byte[TrigLength].ToImmutableArray(), normalization.ToImmutableArray());
        resources.Validate();
        return resources;

        static NativeBattleGfAnimationSource Source(uint address, int length, int geometries, int nodes, int frames)
        {
            byte[] bytes = new byte[length];
            D(bytes, 0, (uint)geometries);
            D(bytes, 4, 0x100);
            for (int geometry = 0; geometry < geometries; geometry++)
            {
                D(bytes, 8 + geometry * 4, 64);
            }
            D(bytes, 64, 20);
            D(bytes, 68, 1);
            D(bytes, 72, 1);
            D(bytes, 0x100, (uint)nodes);
            D(bytes, 0x104, (uint)frames);
            for (int node = 0; node < nodes; node++)
            {
                D(bytes, 0x108 + node * 4, (uint)(8 + nodes * 4 + node * 8));
            }
            return new(address, bytes.ToImmutableArray());
        }

        static NativeBattleStaticMeshResource Mesh(uint address)
        {
            byte[] bytes = new byte[248];
            D(bytes, 0, 72);
            D(bytes, 4, 8);
            D(bytes, 72, 12);
            return new(0, address, bytes.ToImmutableArray());
        }

        static NativeBattleModuleResource Sprite(uint address, int length, int frames, byte kind)
        {
            byte[] bytes = new byte[length];
            D(bytes, 0, (uint)kind << 24 | 0x010524);
            W(bytes, 8, frames);
            for (int phase = 0; phase <= frames; phase++)
            {
                W(bytes, 10 + phase * 2, 64);
            }
            return new(0, address, 0, 0, bytes.ToImmutableArray());
        }
    }

    private static void W(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), checked((ushort)value));
    private static void D(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
}
