using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayPhysicalRequestClosureTests
{
    [Theory]
    [InlineData(Jsm.Opcode.PREQ)]
    [InlineData(Jsm.Opcode.PREQSW)]
    [InlineData(Jsm.Opcode.PREQEW)]
    public void BorrowedPhysicalCodeContributesItsStoryDestinationAndPredicateDependencies(Jsm.Opcode request)
    {
        byte[] source = Program(request, 1, [(Jsm.Opcode.PSHM_B, 720), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.CAL, 6), (Jsm.Opcode.JPF, 3), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 256), (Jsm.Opcode.PSHN_L, 251), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MAPJUMP3, 0)]);
        byte[] before = source.ToArray();
        JsmProgram program = JsmProgram.Read(source);
        EventScript root = program.Objects[1].Scripts[2];
        Assert.Single(program.Objects[0].Scripts);
        Assert.Equal(1, program.ResolveNativeRequestPointer(0, 1, [0, 1]).Script.Id);

        Assert.True(GameWorld.AutoPlayScriptChangesStory(program, root));
        Assert.Equal(251, GameWorld.AutoPlayScriptDestination(program, root));
        Assert.Contains(720, GameWorld.DiscoverAutoPlayConditionReads(program, "600:talk:1", 1).Select(index => program.Operations[index].Parameter));
        Assert.True(GameWorld.CanCacheAutoPlayUnavailableSource(program, "600:talk:1", 1));
        Assert.Equal(before, source);
    }

    [Fact]
    public void AnUnrepresentedInputInBorrowedCodeRefusesTheSourceCertificate()
    {
        JsmProgram program = JsmProgram.Read(Program(Jsm.Opcode.PREQ, 1, [(Jsm.Opcode.GETHP, 0)]));

        Assert.False(GameWorld.CanCacheAutoPlayUnavailableSource(program, "600:talk:1", 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnsupportedOrDynamicPointersKeepMetadataBroadAndCannotCertifyAbsence(bool dynamicSelector)
    {
        JsmProgram program = JsmProgram.Read(Program(Jsm.Opcode.PREQ, 500, [(Jsm.Opcode.PSHM_B, 720), (Jsm.Opcode.POPM_B, 720), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 256)], dynamicSelector));
        EventScript root = program.Objects[1].Scripts[2];

        Assert.True(GameWorld.AutoPlayScriptChangesStory(program, root));
        Assert.Contains(720, GameWorld.DiscoverAutoPlayConditionReads(program, "600:talk:1", 1).Select(index => program.Operations[index].Parameter));
        Assert.False(GameWorld.CanCacheAutoPlayUnavailableSource(program, "600:talk:1", 1));
    }

    private static byte[] Program(Jsm.Opcode request, int selector, (Jsm.Opcode Opcode, int Parameter)[] borrowed, bool dynamicSelector = false)
    {
        (Jsm.Opcode Opcode, int Parameter)[][] scripts = [[], borrowed, [], [(Jsm.Opcode.PSHN_L, 1), (dynamicSelector ? Jsm.Opcode.PSHM_W : Jsm.Opcode.PSHN_L, selector), (request, 0)]];
        List<(Jsm.Opcode Opcode, int Parameter)> operations = [];
        List<ushort> positions = [];
        for (int index = 0; index < scripts.Length; index++)
        {
            positions.Add(checked((ushort)operations.Count));
            operations.Add((Jsm.Opcode.LBL, index));
            operations.AddRange(scripts[index]);
            operations.Add((Jsm.Opcode.RET, 8));
        }
        positions.Add(checked((ushort)operations.Count));
        int offset = 12 + positions.Count * 2;
        byte[] bytes = new byte[offset + operations.Count * 4];
        bytes[3] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 12);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), checked((ushort)offset));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 1 << 7 | 2);
        for (int index = 0; index < positions.Count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12 + index * 2), positions[index]);
        }
        for (int index = 0; index < operations.Count; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHM_W or Jsm.Opcode.PREQ or Jsm.Opcode.PREQSW or Jsm.Opcode.PREQEW or Jsm.Opcode.LBL or Jsm.Opcode.MAPJUMP3 ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + index * 4), encoded);
        }
        return bytes;
    }
}
