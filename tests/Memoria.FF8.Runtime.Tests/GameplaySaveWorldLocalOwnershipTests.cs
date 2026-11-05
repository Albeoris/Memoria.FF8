namespace Memoria.FF8.Runtime.Tests;

public sealed class GameplaySaveWorldLocalOwnershipTests
{
    [Theory]
    [InlineData(1023, 2)]
    [InlineData(2303, 2)]
    [InlineData(1021, 4)]
    [InlineData(2301, 4)]
    public void World_projection_rejects_cross_owner_cells_without_discarding_global_bytes(int address, int width)
    {
        GameState source = GameSession.CreateColdProcess().State with
        {
            Scene = SceneKind.WorldMap,
            FieldId = 106,
            EventMemory = new(NativeEventMemory.CurrentVersion, new Dictionary<int, NativeMemoryValue> { [address] = new(width, NativeScalarValue.Exact(width == 2 ? 0x5678 : 0x12345678)) })
        };
        int original = source.EventMemory.Read(address, width).RequireExact();

        Assert.Throws<NotSupportedException>(() => GameplaySaveProgression.Capture(source));

        Assert.Equal(width, source.EventMemory.Cells[address].Width);
        Assert.Equal(original, source.EventMemory.Read(address, width).RequireExact());
    }
}
