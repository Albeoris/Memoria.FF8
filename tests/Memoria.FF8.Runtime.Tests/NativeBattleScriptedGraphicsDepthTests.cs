using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedGraphicsDepthTests
{
    [Fact]
    public void Larger_native_mesh_depths_preserve_existing_prefix_unknown_bytes_and_snapshot_state()
    {
        NativeBattleScriptedGraphicsMemory state = NativeBattleScriptedGraphicsMemory.FromColdProcess();
        state = state with { Depths = state.Depths.Write(0, BitConverter.GetBytes(1234u)).MarkUnknown(4, 4).Write(1472 - 4, BitConverter.GetBytes(5678u)) };
        state.Validate();
        NativeBattleScriptedGraphicsMemory restored = JsonSerializer.Deserialize<NativeBattleScriptedGraphicsMemory>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(1234u, BitConverter.ToUInt32(restored.Depths.Read(0, 4)));
        Assert.Throws<NotSupportedException>(() => restored.Depths.Read(4, 4));
        Assert.Equal(5678u, BitConverter.ToUInt32(restored.Depths.Read(1468, 4)));
        Assert.Throws<InvalidDataException>(() => (state with { Depths = state.Depths.Write(NativeBattleScriptedGraphicsMemory.DepthLength, [1]) }).Validate());
    }
}
