using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayWorldUnavailableSourceProofTests
{
    [Fact]
    public void LiteralPacketAndUnaryNegativeAreCertifiedWithoutChangingProgram()
    {
        byte[] bytes = Program([], [], [(Jsm.Opcode.PSHN_L, 65536), (Jsm.Opcode.PSHN_L, 65537), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.CAL, 5), (Jsm.Opcode.WORLDMAPJUMP, 0)]);
        byte[] before = bytes.ToArray();
        JsmProgram program = JsmProgram.Read(bytes);
        NativeWorldTransitionRequest request = Request(program, new WorldTransition(0, 1, -1));

        Assert.True(GameWorld.CanCacheAutoPlayWorldUnavailableSource(program, "600:talk:0", 0, request));
        Assert.False(GameWorld.CanCacheAutoPlayUnavailableSource(program, "600:talk:0", 0));
        Assert.Equal(before, bytes);
    }

    [Fact]
    public void BothLiteralWorldBranchesAreCheckedWithoutRequiringTheSamePacket()
    {
        JsmProgram program = JsmProgram.Read(Program([], [], [(Jsm.Opcode.PSHM_W, 256), (Jsm.Opcode.JPF, 6), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.WORLDMAPJUMP, 0), (Jsm.Opcode.JMP, 5), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.WORLDMAPJUMP, 0)]));

        Assert.True(GameWorld.CanCacheAutoPlayWorldUnavailableSource(program, "600:talk:0", 0, Request(program, new WorldTransition(0, 0, 0))));
        Assert.True(GameWorld.CanCacheAutoPlayWorldUnavailableSource(program, "600:talk:0", 0, Request(program, new WorldTransition(0, 1, -1), site: 1)));
    }

    [Theory]
    [InlineData(Jsm.Opcode.PSHM_W, 256)]
    [InlineData(Jsm.Opcode.PSHI_L, 0)]
    [InlineData(Jsm.Opcode.CAL, 15)]
    public void NonliteralPacketOperandRefusesCertification(Jsm.Opcode opcode, int parameter)
    {
        JsmProgram program = JsmProgram.Read(Program([], [], [(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (opcode, parameter), (Jsm.Opcode.WORLDMAPJUMP, 0)]));

        Assert.False(GameWorld.CanCacheAutoPlayWorldUnavailableSource(program, "600:talk:0", 0, Request(program, new WorldTransition(0, 0, 0))));
    }

    [Fact]
    public void AnUnselectedDynamicWorldBranchAlsoRefusesCertification()
    {
        JsmProgram program = JsmProgram.Read(Program([], [], [(Jsm.Opcode.PSHM_W, 256), (Jsm.Opcode.JPF, 6), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.WORLDMAPJUMP, 0), (Jsm.Opcode.JMP, 5), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHM_W, 720), (Jsm.Opcode.WORLDMAPJUMP, 0)]));

        Assert.False(GameWorld.CanCacheAutoPlayWorldUnavailableSource(program, "600:talk:0", 0, Request(program, new WorldTransition(0, 0, 0))));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void JumpIntoLiteralOrNegationSpanRefusesCertification(int jump)
    {
        JsmProgram program = JsmProgram.Read(Program([], [], [(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.JPF, jump), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.CAL, 5), (Jsm.Opcode.WORLDMAPJUMP, 0)]));

        Assert.False(GameWorld.CanCacheAutoPlayWorldUnavailableSource(program, "600:talk:0", 0, Request(program, new WorldTransition(0, 0, -1))));
    }

    [Fact]
    public void RequestMustIdentifyTheActualReachableSiteAndPacket()
    {
        JsmProgram program = JsmProgram.Read(Program([], [], [(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.WORLDMAPJUMP, 0)]));
        NativeWorldTransitionRequest request = Request(program, new WorldTransition(0, 0, -1));

        Assert.False(GameWorld.CanCacheAutoPlayWorldUnavailableSource(program, "601:talk:0", 0, request));
        Assert.False(GameWorld.CanCacheAutoPlayWorldUnavailableSource(program, "600:talk:0", 0, request with { SourceObjectId = null }));
        Assert.False(GameWorld.CanCacheAutoPlayWorldUnavailableSource(program, "600:talk:0", 0, request with { SourceScriptId = 1 }));
        Assert.False(GameWorld.CanCacheAutoPlayWorldUnavailableSource(program, "600:talk:0", 0, request with { SourceOperation = request.SourceOperation - 1 }));
        Assert.False(GameWorld.CanCacheAutoPlayWorldUnavailableSource(program, "600:talk:0", 0, request with { Transition = new WorldTransition(0, 1, -1) }));
        Assert.False(GameWorld.CanCacheAutoPlayWorldUnavailableSource(program, "600:talk:0", 0, request with { Origin = NativeWorldTransitionOrigin.Gateway }));
    }

    [Fact]
    public void AnUnrequestedWorldScriptDoesNotCertifyTheSourceInteraction()
    {
        JsmProgram program = JsmProgram.Read(Program([], [], [], [(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.WORLDMAPJUMP, 0)]));

        Assert.False(GameWorld.CanCacheAutoPlayWorldUnavailableSource(program, "600:talk:0", 0, Request(program, new WorldTransition(0, 0, 0), scriptId: 3)));
    }

    [Fact]
    public void ARequestedWorldScriptCanCertifyTheSourceInteraction()
    {
        JsmProgram program = JsmProgram.Read(Program([], [], [(Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.REQ, 0)], [(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.WORLDMAPJUMP, 0)]));

        Assert.True(GameWorld.CanCacheAutoPlayWorldUnavailableSource(program, "600:talk:0", 0, Request(program, new WorldTransition(0, 0, 0), scriptId: 3)));
    }

    [Fact]
    public void DynamicEligibilityOutsideThePacketStillRefusesCertification()
    {
        JsmProgram program = JsmProgram.Read(Program([], [], [(Jsm.Opcode.RND, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.WORLDMAPJUMP, 0)]));

        Assert.False(GameWorld.CanCacheAutoPlayWorldUnavailableSource(program, "600:talk:0", 0, Request(program, new WorldTransition(0, 0, 0))));
    }

    [Theory]
    [InlineData(Jsm.Opcode.BATTLEON)]
    [InlineData(Jsm.Opcode.BATTLEOFF)]
    public void RandomEncounterEnableSettersDoNotIntroduceAnUnrepresentedInput(Jsm.Opcode opcode)
    {
        byte[] bytes = Program([], [], [(Jsm.Opcode.UCOFF, 0), (opcode, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.POPM_B, 479), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.CAL, 5), (Jsm.Opcode.WORLDMAPJUMP, 0)]);
        byte[] before = bytes.ToArray();
        JsmProgram program = JsmProgram.Read(bytes);

        Assert.True(GameWorld.CanCacheAutoPlayWorldUnavailableSource(program, "600:talk:0", 0, Request(program, new WorldTransition(0, 0, -1))));
        Assert.Equal(before, bytes);
    }

    [Fact]
    public void ExecutingABattleRemainsOutsideTheSourceCertificate()
    {
        JsmProgram program = JsmProgram.Read(Program([], [], [(Jsm.Opcode.BATTLE, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.WORLDMAPJUMP, 0)]));

        Assert.False(GameWorld.CanCacheAutoPlayWorldUnavailableSource(program, "600:talk:0", 0, Request(program, new WorldTransition(0, 0, 0))));
    }

    private static NativeWorldTransitionRequest Request(JsmProgram program, WorldTransition packet, int site = 0, int scriptId = 2)
    {
        EventScript script = program.Objects[0].Scripts.Single(candidate => candidate.Id == scriptId);
        int operation = script.ReachableOperations.Where(index => program.Operations[index].Opcode == Jsm.Opcode.WORLDMAPJUMP).Order().ElementAt(site);
        NativeWorldControlState controls = NativeWorldControlState.CreateNewGame();
        return new NativeWorldTransitionRequest(1, 600, 1, NativeWorldTransitionOrigin.Script, packet, controls, controls, controls, SourceObjectId: 0, SourceScriptId: scriptId, SourceOperation: operation);
    }

    private static byte[] Program(params (Jsm.Opcode Opcode, int Parameter)[][] scripts)
    {
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
        int offset = 10 + positions.Count * 2;
        byte[] bytes = new byte[offset + operations.Count * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), checked((ushort)offset));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), checked((ushort)(scripts.Length - 1)));
        for (int index = 0; index < positions.Count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + index * 2), positions[index]);
        }
        for (int index = 0; index < operations.Count; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHI_L or Jsm.Opcode.REQ or Jsm.Opcode.PREQ or Jsm.Opcode.LBL or Jsm.Opcode.CAL ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + index * 4), encoded);
        }
        return bytes;
    }
}
