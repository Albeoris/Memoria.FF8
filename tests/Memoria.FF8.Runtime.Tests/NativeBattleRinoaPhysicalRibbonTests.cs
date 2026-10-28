using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoaPhysicalRibbonTests
{
    [Fact]
    public void InitialPointProjectsWithoutPacketsAndStillAdvancesCommonThirtySixBytes()
    {
        Fixture fixture = new();
        NativeBattleRinoaPhysicalRibbonDraw result = fixture.Draw(0);
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(36, result.Batch.AllocationBytes);
        Assert.True(result.Batch.NativeUnlinkedAllocation);
        Assert.Equal(new NativeBattleVector(0, 0, 0), Point(result.Staging, 96));
        Assert.Equal(0xBEEF, Word(result.Staging, 102));
        Assert.Equal(0xA5A5, Word(result.Staging, 110));
        Assert.Equal(0xA5A5, Word(result.Staging, 118));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 24), result.Staging.Read(Fixture.Temporary + 72, 24));
        Assert.Equal(0x3A309090u - 0x71414u, Dword(result.Staging, 40));
        Assert.Equal(0x3A286060u - 0x61010u, Dword(result.Staging, 44));
        Assert.Equal(Dword(result.Staging, 24), result.Memory.GeometryRegisters!.Otz);
        Assert.Equal(0xA5A5, BinaryPrimitives.ReadUInt16LittleEndian(result.Memory.GeometryRegisters.Rotation20.Skip(18).Select(value => value!.Value).ToArray()));
    }

    [Fact]
    public void ThreeSegmentsEmitOrderedGouraudPairsWithNativeColorDecrementsAndCursorGap()
    {
        NativeBattleRinoaPhysicalRibbonDraw result = new Fixture().Draw(3);
        Assert.Equal(6, result.Batch.Attempts.Length);
        Assert.Equal(252, result.Batch.AllocationBytes);
        Assert.True(result.Batch.NativeUnlinkedAllocation);
        for (int pair = 0; pair < 3; pair++)
        {
            NativeBattleGfGeometryAttempt p = result.Batch.Attempts[pair * 2];
            NativeBattleGfGeometryAttempt q = result.Batch.Attempts[pair * 2 + 1];
            uint currentA = unchecked(0x3A309090u - (uint)(pair + 2) * 0x71414u);
            uint currentB = unchecked(0x3A286060u - (uint)(pair + 2) * 0x61010u);
            uint previousA = unchecked(currentA + 0x71414u);
            uint previousB = unchecked(currentB + 0x61010u);
            Assert.Equal(pair * 72, p.ByteOffsetRelative);
            Assert.Equal(pair * 72 + 36, q.ByteOffsetRelative);
            Assert.Equal(36, p.PacketLength);
            Assert.Equal(36, q.PacketLength);
            Assert.Equal(new uint[] { 0x08000000, currentB, currentA, previousB, previousA }, ColorWords(p));
            Assert.Equal(new uint[] { 0x08000000, currentA, currentB, previousA, previousB }, ColorWords(q));
            Assert.Equal(p.Primitive!.Bucket, q.Primitive!.Bucket);
            Assert.Equal(p.Primitive.VertexPayload4.ToArray(), q.Primitive.VertexPayload4.ToArray());
            Assert.All(p.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
            Assert.All(q.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
            Assert.Equal(p.Primitive.Words[2], q.Primitive.Words[4]);
            Assert.Equal(p.Primitive.Words[6], q.Primitive.Words[8]);
        }
    }

    [Fact]
    public void RepeatedRenderingAtUnchangedProgressStillDrawsAndPreservesCurveStorage()
    {
        Fixture fixture = new();
        byte[] before = fixture.Modules.ReadPhysicalModuleBytes(0x209D7C0, 512);
        NativeBattleRinoaPhysicalRibbonDraw first = fixture.Draw(3);
        NativeBattleRinoaPhysicalRibbonDraw repeated = fixture.Draw(3, first.Memory, first.Staging);
        Assert.Equal(6, repeated.Batch.Attempts.Length);
        Assert.Equal(first.Batch.AllocationBytes, repeated.Batch.AllocationBytes);
        Assert.Equal(first.Batch.Attempts.SelectMany(attempt => attempt.Data), repeated.Batch.Attempts.SelectMany(attempt => attempt.Data));
        Assert.Equal(before, fixture.Modules.ReadPhysicalModuleBytes(0x209D7C0, 512));
    }

    [Fact]
    public void LastSampleTangentReadsPhysicalSampleSixtyFourAndFeedsFollowingPair()
    {
        Fixture first = new(new NativeBattleVector(1200, 0, 3000));
        Fixture second = new(new NativeBattleVector(-1200, 0, 3000));
        Assert.Equal(66, (int)((0x209D9C0u - NativeBattleModuleState.BankAddress) / NativeBattleModuleState.TaskStride));
        NativeBattleRinoaPhysicalRibbonDraw a = first.Draw(63);
        NativeBattleRinoaPhysicalRibbonDraw b = second.Draw(63);
        Assert.Equal(10, a.Batch.Attempts.Length);
        Assert.Equal(396, a.Batch.AllocationBytes);
        Assert.NotEqual(a.Batch.Attempts[0].Primitive!.Words[8], b.Batch.Attempts[0].Primitive!.Words[8]);
        Assert.All(a.Batch.Attempts, attempt =>
        {
            Assert.NotNull(attempt.Primitive);
            Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        });
    }

    private static uint[] ColorWords(NativeBattleGfGeometryAttempt packet) => new[] { 0, 1, 3, 5, 7 }.Select(index => packet.Primitive!.Words[index]).ToArray();
    private static ushort Word(NativeBattleFileStaging staging, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(staging.Read(Fixture.Temporary + (uint)offset, 2));
    private static uint Dword(NativeBattleFileStaging staging, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(Fixture.Temporary + (uint)offset, 4));
    private static NativeBattleVector Point(NativeBattleFileStaging staging, int offset)
    {
        byte[] bytes = staging.Read(Fixture.Temporary + (uint)offset, 6);
        return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
    }

    private sealed class Fixture
    {
        public const uint Temporary = NativeBattleTemporaryMemory.Address;
        public NativeBattleModuleState Modules { get; }
        private readonly ImmutableArray<NativeBattleVector> curve = Enumerable.Range(0, 64).Select(index => new NativeBattleVector((short)(index * 10), (short)(index * 2), (short)(index * 30))).ToImmutableArray();
        private readonly NativeBattleActionMatrix camera = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 4096 };
        private readonly NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with
        {
            GeometryRegisters = NativeBattleGeometryRegisters.Unknown(),
            ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(),
            ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0()
        };
        private readonly NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(Temporary, Enumerable.Repeat((byte)0xA5, 248).ToArray());

        public Fixture(NativeBattleVector? sample64 = null)
        {
            NativeBattleModuleState modules = NativeBattleModuleState.Clear();
            for (int index = 0; index <= 64; index++)
            {
                NativeBattleVector point = index == 64 ? sample64 ?? new(640, 0, 1920) : curve[index];
                byte[] bytes = new byte[8];
                BinaryPrimitives.WriteInt16LittleEndian(bytes, point.X);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), point.Y);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), point.Z);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 0xBEEF);
                modules = modules.WritePhysicalModuleBytes(0x209D7C0u + (uint)index * 8, bytes);
            }
            Modules = modules;
        }

        public NativeBattleRinoaPhysicalRibbonDraw Draw(int first, NativeBattleEffectMemory? currentMemory = null, NativeBattleFileStaging? currentStaging = null) => NativeBattleRinoaPhysicalRibbon.Draw(currentMemory ?? memory, currentStaging ?? staging, Temporary, curve, camera, first, first + 3, new(camera, 256, 160, 108), Modules);
    }
}
