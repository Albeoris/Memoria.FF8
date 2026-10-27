using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicSessionPublicationTests
{
    [Theory]
    [InlineData(false, 24)]
    [InlineData(true, 104)]
    public void Actual_session_wrappers_publish_owned_packet_prefix_before_memory_setter(bool grid, int expectedBytes)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x1000);
        uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset;
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A6C, Address(0x1000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x90, Address(0x2000));
        frame = WaterSchedulerFixture.CoreWord(frame, NativeBattleScriptedEffectMemory.Address + 0x38, 2);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797624, frame.TemporaryFrame.Address);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x74, Address(0x4000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x9C, ushort.MaxValue);
        byte[] matrix = new byte[32];
        for (int lane = 0; lane < 9; lane++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(matrix.AsSpan(lane * 2), lane % 4 == 0 ? (short)4096 : (short)0);
        }
        BinaryPrimitives.WriteInt32LittleEndian(matrix.AsSpan(28), 4096);
        frame = frame with { Core = frame.Core.Write(0x27979E8, matrix).Write(0x27971E4, new byte[6]) };
        NativeBattleCinematicResources resources = NativeBattleCinematicTextureMetadataTests.Resources();
        if (grid)
        {
            byte[] payload = new byte[12];
            short[] values = [-1, 22, 2, 1, -100, 1000];
            for (int index = 0; index < values.Length; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(index * 2), values[index]);
            }
            frame = frame with { Arena = frame.Arena.Write(0x3000, payload) };
            frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Address(0x3000));
            frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0x02030201);
            frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, 256);
            frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8E, 0x1000);
            frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x90, 256);
            frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x98, resources.TextureProfile.PixelRectangleAddress);
            frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x9C, resources.TextureProfile.ClutRectangleAddress);
            frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0xA4, resources.TextureProfile.PixelSourceMapAddress);
        }
        else
        {
            byte[] points = new byte[36];
            BinaryPrimitives.WriteInt16LittleEndian(points.AsSpan(2), 2);
            for (int index = 0; index < 2; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(points.AsSpan(8 + index * 16), 1000);
                BinaryPrimitives.WriteUInt32LittleEndian(points.AsSpan(12 + index * 16), 0x68030201);
            }
            frame = frame with { Arena = frame.Arena.Write(0x5000, points) };
            frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, Address(0x5000));
        }
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400().ApplyCommonWrite(0, Enumerable.Repeat((byte)0xA5, 128).ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 128).ToImmutableArray());
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = frame.Arena, ScriptedEffects = frame.Core, PrimitiveProcess = process, ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        BattleSession session = (BattleSession)RuntimeHelpers.GetUninitializedObject(typeof(BattleSession));
        typeof(BattleSession).GetField("nativeEffectMemory", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(session, memory);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeCamera))!.SetValue(session, NativeBattleCameraState.Initialize() with { Projection = new(null, 216, 160, 108) });
        NativeBattleGraphicsState graphics = new([], [], 0, 0) { PrimitiveFrame = NativeBattlePrimitiveFrame.Clear() };
        NativeBattleOriginalEffectState effect = new(202, 201, new(202, 1, 0), null, null);
        using (NativeBattleTaskOperation operation = new(memory, effect))
        {
            string method = grid ? "DrawNativeCinematicGrid28" : "DrawNativeCinematicPointCloud27";
            object?[] args = grid ? [frame, 0x2000, resources, graphics, null] : [frame, 0x2000, graphics];
            var result = ((NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics))typeof(BattleSession).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(session, args)!;
            Assert.Equal(expectedBytes, session.NativeEffectMemory!.PrimitiveProcess!.ByteCursor);
            Assert.Equal(2, result.Graphics.PrimitiveFrame!.Pending.Packets.Length);
            Assert.Null(session.NativeEffectMemory.PrimitiveProcess.CommonTransaction);
            NativeBattleEffectArenaTransaction.RequireCommittedForCapture(frame.Arena.Working);
            byte code = session.NativeEffectMemory.PrimitiveProcess.ReadCommonPacket(3, 1)[0];
            session.NativeEffectMemory.PrimitiveProcess.ApplyCommonWrite(3, [9], [NativeBattleGfPacketByteState.Known]);
            Assert.NotEqual((byte)9, code);
        }
        Assert.Equal(expectedBytes, session.NativeEffectMemory!.PrimitiveProcess!.ByteCursor);
        Assert.NotEqual((byte)9, session.NativeEffectMemory.PrimitiveProcess.ReadCommonPacket(3, 1)[0]);
    }
}
