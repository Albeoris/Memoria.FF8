using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayWalkRejectionProofTests
{
    [Theory]
    [InlineData(Jsm.Opcode.REQ)]
    [InlineData(Jsm.Opcode.REQEW)]
    [InlineData(Jsm.Opcode.PREQ)]
    [InlineData(Jsm.Opcode.PREQEW)]
    public void LiteralRequestsCertifyOnlyTheirBoundedPresentationAndTemporaryMovementClosure(Jsm.Opcode request)
    {
        byte[] bytes = Program([], [], [(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 3), (request, 0)],
            [(Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.POPM_B, 1026), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.MOVE, 0), (Jsm.Opcode.JOIN, 0), (Jsm.Opcode.AMESW, 0), (Jsm.Opcode.UCON, 0)],
            [(Jsm.Opcode.RND, 0)]);
        byte[] original = bytes.ToArray();

        Assert.True(GameWorld.CanCacheAutoPlayWalkRejectionSource(JsmProgram.Read(bytes), "600:talk:0", 0));
        Assert.Equal(original, bytes);
    }

    [Theory]
    [InlineData(Jsm.Opcode.POPM_B, 256, false)]
    [InlineData(Jsm.Opcode.POPM_B, 720, false)]
    [InlineData(Jsm.Opcode.POPM_B, 1023, false)]
    [InlineData(Jsm.Opcode.POPM_B, 1024, true)]
    [InlineData(Jsm.Opcode.POPM_B, 1279, true)]
    [InlineData(Jsm.Opcode.POPM_B, 1280, false)]
    [InlineData(Jsm.Opcode.POPM_W, 1023, false)]
    [InlineData(Jsm.Opcode.POPM_W, 1024, true)]
    [InlineData(Jsm.Opcode.POPM_W, 1278, true)]
    [InlineData(Jsm.Opcode.POPM_W, 1279, false)]
    [InlineData(Jsm.Opcode.POPM_L, 1276, true)]
    [InlineData(Jsm.Opcode.POPM_L, 1277, false)]
    public void WritesMustFitEntirelyInsideTheTemporaryByteRange(Jsm.Opcode opcode, int address, bool expected)
    {
        JsmProgram program = JsmProgram.Read(Program([], [], [(Jsm.Opcode.PSHN_L, 0), (opcode, address)]));

        Assert.Equal(expected, GameWorld.CanCacheAutoPlayWalkRejectionSource(program, "600:talk:0", 0));
    }

    [Theory]
    [InlineData(Jsm.Opcode.ADDGIL)]
    [InlineData(Jsm.Opcode.ADDITEM)]
    [InlineData(Jsm.Opcode.ADDMAGIC)]
    [InlineData(Jsm.Opcode.SETCARD)]
    [InlineData(Jsm.Opcode.RND)]
    [InlineData(Jsm.Opcode.GETHP)]
    [InlineData(Jsm.Opcode.GETINFO)]
    [InlineData(Jsm.Opcode.PGETINFO)]
    [InlineData(Jsm.Opcode.KEYSCAN2)]
    [InlineData(Jsm.Opcode.MAPJUMP)]
    [InlineData(Jsm.Opcode.MAPJUMP3)]
    [InlineData(Jsm.Opcode.WORLDMAPJUMP)]
    [InlineData(Jsm.Opcode.BATTLE)]
    [InlineData(Jsm.Opcode.BATTLEON)]
    [InlineData(Jsm.Opcode.BATTLEOFF)]
    [InlineData(Jsm.Opcode.CARDGAME)]
    public void ProgressRandomInputAndTransitionEffectsRefuseCertification(Jsm.Opcode opcode)
    {
        Assert.False(GameWorld.CanCacheAutoPlayWalkRejectionSource(JsmProgram.Read(Program([], [], [(opcode, 0)])), "600:talk:0", 0));
    }

    [Theory]
    [InlineData(Jsm.Opcode.ADDGIL)]
    [InlineData(Jsm.Opcode.RND)]
    public void UnsafeRequestedCalleeRefusesTheEntireSource(Jsm.Opcode opcode)
    {
        JsmProgram program = JsmProgram.Read(Program([], [], [(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.REQEW, 0)], [(opcode, 0)]));

        Assert.False(GameWorld.CanCacheAutoPlayWalkRejectionSource(program, "600:talk:0", 0));
    }

    [Fact]
    public void PresentationOperationsDoNotBroadenTheExistingAbsenceProof()
    {
        JsmProgram program = JsmProgram.Read(Program([], [], [(Jsm.Opcode.PSHAC, 0), (Jsm.Opcode.CTURN, 0), (Jsm.Opcode.CTURNL, 0), (Jsm.Opcode.CTURNR, 0), (Jsm.Opcode.PCTURN, 0), (Jsm.Opcode.ANIMEKEEP, 1), (Jsm.Opcode.RANIMEKEEP, 1), (Jsm.Opcode.ANIMESYNC, 0), (Jsm.Opcode.EFFECTPLAY2, 1), (Jsm.Opcode.BGANIMESPEED, 0), (Jsm.Opcode.RBGANIMELOOP, 0), (Jsm.Opcode.BGOFF, 0), (Jsm.Opcode.RUNENABLE, 0), (Jsm.Opcode.AMESW, 0)]));

        Assert.True(GameWorld.CanCacheAutoPlayWalkRejectionSource(program, "600:talk:0", 0));
        Assert.False(GameWorld.CanCacheAutoPlayUnavailableSource(program, "600:talk:0", 0));
    }

    [Fact]
    public void ExistingAbsenceProofKeepsItsOriginalWriteAndMapBehavior()
    {
        JsmProgram program = JsmProgram.Read(Program([], [], [(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.POPM_B, 256), (Jsm.Opcode.MAPJUMP, 0)]));

        Assert.True(GameWorld.CanCacheAutoPlayUnavailableSource(program, "600:talk:0", 0));
        Assert.False(GameWorld.CanCacheAutoPlayWalkRejectionSource(program, "600:talk:0", 0));
        Assert.True(GameWorld.CanCacheAutoPlayUnavailableSource(program, "600:gateway:0", -1));
        Assert.False(GameWorld.CanCacheAutoPlayWalkRejectionSource(program, "600:gateway:0", -1));
    }

    [Theory]
    [InlineData(Jsm.Opcode.REQ)]
    [InlineData(Jsm.Opcode.PREQ)]
    public void DynamicRequestSelectorsRemainUnproved(Jsm.Opcode request)
    {
        JsmProgram program = JsmProgram.Read(Program([], [], [(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHM_B, 1026), (request, 0)], [(Jsm.Opcode.AMESW, 0)]));

        Assert.False(GameWorld.CanCacheAutoPlayWalkRejectionSource(program, "600:talk:0", 0));
    }

    [Fact]
    public void JumpIntoRequestOperandsCannotSupplyAFalseLiteralCertificate()
    {
        JsmProgram program = JsmProgram.Read(Program([], [], [(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.JPF, 3), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.REQ, 0)], [(Jsm.Opcode.AMESW, 0)]));

        Assert.False(GameWorld.CanCacheAutoPlayWalkRejectionSource(program, "600:talk:0", 0));
    }

    [Fact]
    public void ActualPartySlotSelectsItsOwnerWithoutCertifyingAnUnsafeHypotheticalActor()
    {
        JsmProgram program = JsmProgram.Read(TwoActorProgram());

        Assert.False(GameWorld.CanCacheAutoPlayWalkRejectionSource(program, "600:talk:0", 0));
        Assert.True(GameWorld.CanCacheAutoPlayWalkRejectionSource(program, "600:talk:0", 0, new[] { 0, 1, -1 }));
        Assert.False(GameWorld.CanCacheAutoPlayWalkRejectionSource(program, "600:talk:0", 0, new[] { 1, 0, -1 }));
        Assert.False(GameWorld.CanCacheAutoPlayWalkRejectionSource(program, "600:talk:0", 0, new[] { -1, 0, 1 }));
    }

    [Fact]
    public void InvalidOrDuplicatePartyBindingsCannotProvidePointerAuthority()
    {
        JsmProgram program = JsmProgram.Read(TwoActorProgram());

        Assert.False(GameWorld.CanCacheAutoPlayWalkRejectionSource(program, "600:talk:0", 0, new[] { 0, 1 }));
        Assert.False(GameWorld.CanCacheAutoPlayWalkRejectionSource(program, "600:talk:0", 0, new[] { 0, 0, -1 }));
        Assert.False(GameWorld.CanCacheAutoPlayWalkRejectionSource(program, "600:talk:0", 0, new[] { 0, 2, -1 }));
    }

    [Fact]
    public void WriteOnlyTemporaryStateChangesTheRejectionFingerprint()
    {
        byte[] bytes = Program([], [], [(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.POPM_B, 1026)]);
        JsmProgram program = JsmProgram.Read(bytes);
        GameSession session = new(new GameState());
        EventEngine events = new(session);
        events.LoadField(600, bytes, []);
        int[] operations = program.Objects[0].Scripts[2].ReachableOperations.ToArray();
        string? before = GameWorld.ReadAutoPlayWalkRejectionState(events, program, operations, new[] { 0, -1, -1 });
        Assert.NotNull(before);
        session.SetEventMemory(events.EventMemory.Write(1026, 1, NativeScalarValue.Exact(0)));
        Assert.Equal(before, GameWorld.ReadAutoPlayWalkRejectionState(events, program, operations, new[] { 0, -1, -1 }));
        session.SetEventMemory(events.EventMemory.Write(1026, 1, NativeScalarValue.Exact(1)));
        Assert.NotEqual(before, GameWorld.ReadAutoPlayWalkRejectionState(events, program, operations, new[] { 0, -1, -1 }));
    }

    [Fact]
    public void SelfUpdateReadsRemainPartOfTheExactRejectionState()
    {
        byte[] bytes = Program([], [], [(Jsm.Opcode.PSHM_B, 1026), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.CAL, 0), (Jsm.Opcode.POPM_B, 1026)]);
        JsmProgram program = JsmProgram.Read(bytes);
        GameSession session = new(new GameState());
        EventEngine events = new(session);
        events.LoadField(600, bytes, []);
        Assert.Empty(GameWorld.DiscoverAutoPlayConditionReads(program, "600:talk:0", 0));
        int[] operations = program.Objects[0].Scripts[2].ReachableOperations.ToArray();
        string? before = GameWorld.ReadAutoPlayWalkRejectionState(events, program, operations, new[] { 0, -1, -1 });
        Assert.NotNull(before);
        session.SetEventMemory(events.EventMemory.Write(1026, 1, NativeScalarValue.Exact(1)));
        Assert.NotEqual(before, GameWorld.ReadAutoPlayWalkRejectionState(events, program, operations, new[] { 0, -1, -1 }));
    }

    [Theory]
    [InlineData(Jsm.Opcode.PSHM_B, 1)]
    [InlineData(Jsm.Opcode.PSHSM_W, 2)]
    [InlineData(Jsm.Opcode.PSHM_L, 4)]
    public void ExactPhaseCanExcludeAProgressBranchAndIsPartOfTheReceipt(Jsm.Opcode read, int width)
    {
        byte[] bytes = Program([], [], [(read, 1024), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.CAL, 6), (Jsm.Opcode.JPF, 2), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.AMESW, 0)]);
        JsmProgram program = JsmProgram.Read(bytes);
        GameSession session = new(new GameState());
        EventEngine events = new(session);
        events.LoadField(600, bytes, []);
        int[] bindings = [0, -1, -1];
        string? Proof(int value)
        {
            session.SetEventMemory(events.EventMemory.Write(1024, width, NativeScalarValue.Exact(value)));
            return GameWorld.ReadAutoPlayPhaseWalkRejectionState(events, program, "600:talk:0", 0, bindings);
        }

        Assert.False(GameWorld.CanCacheAutoPlayWalkRejectionSource(program, "600:talk:0", 0, bindings));
        Assert.Null(Proof(3));
        string? phaseFour = Proof(4);
        Assert.NotNull(phaseFour);
        Assert.NotEqual(phaseFour, Proof(5));
        Assert.Equal(phaseFour, Proof(4));
    }

    [Fact]
    public void ReachableCalleeWritingAnOverlappingByteInvalidatesTheSpecializedPhase()
    {
        byte[] bytes = Program([], [],
            [(Jsm.Opcode.PSHM_B, 1025), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.CAL, 6), (Jsm.Opcode.JPF, 2), (Jsm.Opcode.ADDGIL, 0),
             (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.REQEW, 0)],
            [(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.POPM_W, 1024)]);
        JsmProgram program = JsmProgram.Read(bytes);
        GameSession session = new(new GameState());
        EventEngine events = new(session);
        events.LoadField(600, bytes, []);
        session.SetEventMemory(events.EventMemory.Write(1025, 1, NativeScalarValue.Exact(4)));

        Assert.Null(GameWorld.ReadAutoPlayPhaseWalkRejectionState(events, program, "600:talk:0", 0, new[] { 0, -1, -1 }));
    }

    [Fact]
    public void SignedLongPhaseReadsUseNativeSaturationBeforeEquality()
    {
        byte[] bytes = Program([], [], [(Jsm.Opcode.PSHSM_L, 1024), (Jsm.Opcode.PSHN_L, 0x7FFFFF), (Jsm.Opcode.CAL, 6), (Jsm.Opcode.JPF, 2), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.AMESW, 0)]);
        JsmProgram program = JsmProgram.Read(bytes);
        GameSession session = new(new GameState());
        EventEngine events = new(session);
        events.LoadField(600, bytes, []);
        session.SetEventMemory(events.EventMemory.Write(1024, 4, NativeScalarValue.Exact(int.MaxValue)));

        Assert.Null(GameWorld.ReadAutoPlayPhaseWalkRejectionState(events, program, "600:talk:0", 0, new[] { 0, -1, -1 }));
    }

    private static byte[] TwoActorProgram()
    {
        byte[] single = Program([], [], [(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PREQEW, 0)], [(Jsm.Opcode.AMESW, 0)], [], [], [], [(Jsm.Opcode.ADDGIL, 0)]);
        byte[] bytes = new byte[single.Length + 2];
        single.AsSpan(0, 8).CopyTo(bytes);
        bytes[3] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 12);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), checked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(single.AsSpan(6)) + 2)));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 3);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), (4 << 7) | 3);
        single.AsSpan(10).CopyTo(bytes.AsSpan(12));
        return bytes;
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
            int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHI_L or Jsm.Opcode.REQ or Jsm.Opcode.REQEW or Jsm.Opcode.PREQ or Jsm.Opcode.PREQEW or Jsm.Opcode.LBL or Jsm.Opcode.CAL ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + index * 4), encoded);
        }
        return bytes;
    }
}
