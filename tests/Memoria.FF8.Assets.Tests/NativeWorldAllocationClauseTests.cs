using System.Buffers.Binary;
using System.Reflection;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeWorldAllocationClauseTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Theory]
    [InlineData(0, 0, false, false)]
    [InlineData(17, 0, false, false)]
    [InlineData(739, 7, false, false)]
    [InlineData(740, 8, true, false)]
    [InlineData(2139, 11, true, false)]
    [InlineData(2140, 12, false, false)]
    [InlineData(3099, 13, false, false)]
    [InlineData(3100, 14, true, true)]
    [InlineData(3899, 14, true, true)]
    [InlineData(3900, 15, false, true)]
    public void VehiclePresenceComesFromTheSelectedOriginalInstructions(ushort story, int index, bool garden, bool ragnarok)
    {
        using WorldAssets assets = new(GameDirectory);
        NativeWorldAllocationClause clause = assets.ReadWorldAllocationClause(story);
        Assert.Equal(index, clause.Index);
        Assert.Equal(garden, clause.ContainsShape(64));
        Assert.Equal(garden, clause.ContainsShape(65));
        Assert.Equal(ragnarok, clause.ContainsShape(1));
        Assert.Equal(clause.Length / 2, clause.Words.Length);
        Assert.True(story >= clause.MinimumStory && story < clause.MaximumStory);
    }

    [Fact]
    public void InitializedVehiclePosesAndEventFlagsDoNotCreateVehiclesInStory17()
    {
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame();
        Assert.NotNull(saved.GardenPose3);
        Assert.NotNull(saved.RagnarokPose2);
        using WorldAssets assets = new(GameDirectory);
        NativeWorldAllocationClause clause = assets.ReadWorldAllocationClause(17);
        Assert.Equal(84, clause.Offset);
        Assert.Equal(24, clause.Length);
        Assert.Equal(new ushort[] { 0xFF01, 0, 0xFF02, 0, 0xFF03, 34, 0xFF04, 0, 0xFF13, 0, 0xFF05, 0 }, clause.Words);
        Assert.True(clause.ContainsShape(0));
        Assert.False(clause.ContainsShape(64));
        Assert.False(clause.ContainsShape(1));
        NativeTrainWorldAllocation unflagged = assets.ReadTrainWorldAllocation(17, 0);
        NativeTrainWorldAllocation flagged = assets.ReadTrainWorldAllocation(17, 255);
        Assert.True(unflagged.ClauseWords.SequenceEqual(flagged.ClauseWords));
        Assert.Equal(unflagged.Objects[0].RawOperand, flagged.Objects[0].RawOperand);
        Assert.True(unflagged.Objects[0].CoordinateBytes.SequenceEqual(flagged.Objects[0].CoordinateBytes));
        Assert.Equal(new byte[] { 0 }, flagged.Objects.Select(item => item.ShapeId));
    }

    [Fact]
    public void EveryOriginalClauseCanBeSelectedWithoutMaterializingItsVehicleOrConditionalObjects()
    {
        using WorldAssets assets = new(GameDirectory);
        byte[] program = OriginalProgram(assets);
        int count = BinaryPrimitives.ReadInt32LittleEndian(program) / 4 - 1;
        Assert.Equal(20, count);
        for (int index = 0; index < count; index++)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(program.AsSpan(4 * index));
            ushort minimum = BinaryPrimitives.ReadUInt16LittleEndian(program.AsSpan(offset + 6));
            NativeWorldAllocationClause clause = assets.ReadWorldAllocationClause(minimum);
            Assert.Equal(index, clause.Index);
            Assert.Equal(offset, clause.Offset);
            Assert.Equal((ushort)0xFF05, clause.Words[^2]);
            Assert.Equal((ushort)0, clause.Words[^1]);
        }
        NativeWorldAllocationClause last = assets.ReadWorldAllocationClause(20000);
        Assert.Equal(988, last.Offset);
        Assert.Equal(48, last.Length);
        Assert.Equal(program.Length - 4, last.Offset + last.Length);
        Assert.Throws<NotSupportedException>(() => assets.ReadWorldAllocationClause(5000));
    }

    [Fact]
    public void MalformedPointersCommandsTerminalOperandsAndPaddingFailExplicitly()
    {
        using WorldAssets assets = new(GameDirectory);
        byte[] original = OriginalProgram(assets);
        byte[] pointer = (byte[])original.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(pointer.AsSpan(4), 84);
        Assert.Throws<InvalidDataException>(() => NativeWorldAllocationClauseReader.Read(pointer, 17));
        byte[] opcode = (byte[])original.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(opcode.AsSpan(84 + 16), 0xFF14);
        Assert.Throws<NotSupportedException>(() => NativeWorldAllocationClauseReader.Read(opcode, 17));
        byte[] operand = (byte[])original.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(operand.AsSpan(84 + 22), 1);
        Assert.Throws<NotSupportedException>(() => NativeWorldAllocationClauseReader.Read(operand, 17));
        byte[] padding = (byte[])original.Clone();
        padding[^1] = 1;
        Assert.Throws<NotSupportedException>(() => NativeWorldAllocationClauseReader.Read(padding, 20000));
        byte[] premature = (byte[])original.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(premature.AsSpan(84 + 16), 0xFF05);
        Assert.Throws<NotSupportedException>(() => NativeWorldAllocationClauseReader.Read(premature, 17));
    }

    private static byte[] OriginalProgram(WorldAssets assets)
    {
        byte[] wmset = (byte[])typeof(WorldAssets).GetField("wmset", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(assets)!;
        int[] sections = (int[])typeof(WorldAssets).GetField("sections", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(assets)!;
        return wmset[sections[9]..sections[10]];
    }
}
