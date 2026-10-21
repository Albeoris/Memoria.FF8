using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect20RenderingTests
{
    [Fact]
    public void Mesh_context_uses_full_windows_signed_bias_and_CPU_composition_without_extra_GTE_product()
    {
        NativeBattleModulePoolTask task = Task(0x8A0990).WriteDword(0x4C, 0x1629E00).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteDword(0x40, 0xABCDEF).WriteWord(0x50, 0xFFFE).WriteWord(0x52, 0xFFF0).WriteWord(0x5E, 777).WriteWord(0x1C, 10).WriteWord(0x1E, 20).WriteWord(0x20, 30);
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 104).ToArray());
        NativeBattleEffect20MeshPreparation prepared = NativeBattleEffect20MeshRendering.Prepare(task, NativeBattleCuragaMatrices.Identity8DD770() with { X = 11, Y = 22, Z = 33 }, retained);
        Assert.Equal((21, 42, 63), (prepared.Matrix.X, prepared.Matrix.Y, prepared.Matrix.Z));
        Assert.Equal(4, prepared.Operations.Length);
        Assert.Equal(new[] { 'Y', 'X', 'Z' }, prepared.Operations.OfType<NativeBattleDrawStock15Rotate>().Select(operation => operation.Axis));
        Assert.IsType<NativeBattleDrawStock15Matrix>(prepared.Operations[3]);
        Assert.DoesNotContain(prepared.Operations, operation => operation is NativeBattleDrawStock15MatrixProduct);
        Assert.Equal(0xF0u, Dword(prepared.Context104, 0x14));
        Assert.Equal(unchecked((uint)-2), Dword(prepared.Context104, 12));
        Assert.Equal(unchecked((uint)-16), Dword(prepared.Context104, 16));
        foreach (int offset in new[] { 0x18, 0x1A, 0x1C, 0x1E, 0x24, 0x26 })
        {
            Assert.Equal(0, Word(prepared.Context104, offset));
        }
        foreach (int offset in new[] { 0x20, 0x22, 0x28, 0x2A })
        {
            Assert.Equal(256, Word(prepared.Context104, offset));
        }
        Assert.Equal(0xA5A5A5A5u, Dword(prepared.Context104, 4));
        Assert.Equal(0xA5A5A5A5u, Dword(prepared.Context104, 0x30));
    }

    [Fact]
    public void Body_pivot_compensates_first_bone_and_publishes_only_native_context_fields_with_cold_next_equivalence()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        NativeBattleModulePoolTask task = Task(0x8A2C70).WriteWord(0x58, 10).WriteWord(0x5A, 20).WriteWord(0x5C, 30);
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 140).ToArray());
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        List<NativeBattleDrawStock15Rotate> rotations = [];
        NativeBattleEffect20BodyInputs inputs = new(identity with { X = 1000, Y = 2000, Z = 3000 }, 0x1234, identity with { X = 100, Y = 200, Z = 300 }, NativeBattlePackedMatrix32.FromMatrix(identity with { X = 11, Y = 22, Z = 33 }, 0x5678), 0x1968000, NativeBattleEffectArena.Clear().Write(0, [1, 2, 3, 4]).MarkUnknown(1, 1), 0x12345678, 17, Observe);
        NativeBattleEffect20BodyPreparation prepared = NativeBattleEffect20BodyRendering.Prepare(memory, task, retained, inputs);
        Assert.Equal((1121, 2242, 3363), (prepared.FirstBone.X, prepared.FirstBone.Y, prepared.FirstBone.Z));
        Assert.Equal(new[] { 'Y', 'X', 'Z' }, rotations.Select(operation => operation.Axis));
        Assert.All(rotations, operation => Assert.Equal((ushort)0xA5A5, operation.Padding18));
        Assert.Equal(new int?[] { 1011, 2022, 3033 }, prepared.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(0x1968000u, Dword(prepared.Temporary8C, 0x64));
        Assert.Equal(0x12345678u, Dword(prepared.Temporary8C, 0x7C));
        Assert.Equal(2u, Dword(prepared.Temporary8C, 0x80));
        Assert.Equal(320, Word(prepared.Temporary8C, 0x78));
        Assert.Equal(216, Word(prepared.Temporary8C, 0x7A));
        Assert.Equal(new byte[] { 17, 17, 17, 0xA5 }, prepared.Temporary8C.Read(0x88, 4));
        Assert.Equal(0xA5A5A5A5u, Dword(prepared.Temporary8C, 0x60));
        Assert.False(prepared.Temporary8C.ReadSnapshot(0x70, 4).Known[1]);
        NativeBattleEffect20BodyPreparation restored = JsonSerializer.Deserialize<NativeBattleEffect20BodyPreparation>(JsonSerializer.Serialize(prepared))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleEffect20BodyRendering.Prepare(prepared.Memory, task, prepared.Temporary8C, inputs)), JsonSerializer.Serialize(NativeBattleEffect20BodyRendering.Prepare(restored.Memory, task, restored.Temporary8C, inputs)));

        NativeBattleEffectMemory Observe(NativeBattleEffectMemory current, NativeBattleDrawStock15Rotate rotation)
        {
            rotations.Add(rotation);
            return current;
        }
    }

    [Fact]
    public void Shared_D8_preparation_accepts_actual_effect_task_without_Raldo_frame_or_common_cursor_alias()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        NativeBattleModulePoolTask task = Task(0x8A01B0).WriteWord(0x1C, 100).WriteWord(0x1E, 200).WriteWord(0x20, 300).WriteDword(0x4C, 0x1629C54).WriteWord(0x50, 2).WriteWord(0x56, 0xFFF0);
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 216);
        NativeBattleProjectedSpriteHead prepared = NativeBattleProjectedSpriteD8.PrepareHead(memory, task, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0), retained);
        Assert.Equal(new int?[] { 100, 200, 300 }, prepared.Memory.GeometryRegisters!.Translation);
        Assert.Equal(0x1629C54u, Dword(prepared.TemporaryD8, 0));
        Assert.Equal(2, Word(prepared.TemporaryD8, 4));
        Assert.Equal(0xFFF0, Word(prepared.TemporaryD8, 0xB4));
        Assert.Null(prepared.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Same(memory.PrimitiveProcess, prepared.Memory.PrimitiveProcess);
    }

    private static NativeBattleModulePoolTask Task(uint callback) => new(0, 0, null, callback, new byte[140].ToImmutableArray());
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
}
