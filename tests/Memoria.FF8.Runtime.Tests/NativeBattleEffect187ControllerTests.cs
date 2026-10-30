using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect187Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect187Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect187ControllerTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(10, false)]
    [InlineData(11, false)]
    [InlineData(1, true)]
    public void Authored_fullscreen_flash_capture_keeps_actual_tile_native_state_pause_and_expiration(int age, bool paused)
    {
        List<NativeBattleEffect187DeliverBatch> plainPackets = [];
        List<NativeBattleEffect187DeliverBatch> capturedPackets = [];
        NativeBattleEffect187Step Draw(bool capture, List<NativeBattleEffect187DeliverBatch> packets)
        {
            NativeBattleEffect187Frame frame = Frame();
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, true, true, null) with { Otz = 40 }, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() } };
            (frame, uint address) = Spawn647900(frame, 0x64B130);
            frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)age)).WriteWord(14, 0xA10).WriteWord(0x1C, 255));
            NativeBattleEffect187Inputs inputs = new(Colors: null!, ReadPackets: null!, ReadFlags: _ => paused ? 0x201u : 0u, ReadStep508500: null!, ReadView: null!, ReadProjection: _ => new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), ReadCameraRoll: null!, ReadCameraPose: _ => new(new(0, 0, -1000), new(0, 0, 0), 100, 0), ReadEyePadding: null!, ReadTargetPadding: _ => 0, ReadModel: null!, ReadWorld: null!, ReadTargetCenter5020A0: null!, ReadBone502170: null!, ReadVibrationPlaying4A2900: null!, Execute: (current, operation) =>
            {
                packets.Add(Assert.IsType<NativeBattleEffect187DeliverBatch>(operation));
                return current;
            }) { CapturePresentation = capture };
            return AdvanceFlash64B130(frame, address, inputs);
        }
        NativeBattleEffect187Step plain = Draw(false, plainPackets);
        string frozen = NativeBattleTestSnapshots.Json(plain);
        NativeBattleEffect187Step captured = Draw(true, capturedPackets);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(plain.Frame.Random, captured.Frame.Random);
        Assert.Equal(plainPackets.Count, capturedPackets.Count);
        if (age == 11)
        {
            Assert.Empty(capturedPackets);
            Assert.Equal(2, captured.Result);
            return;
        }
        NativeBattleEffect187DeliverBatch expected = Assert.Single(plainPackets);
        NativeBattleEffect187DeliverBatch actual = Assert.Single(capturedPackets);
        Assert.Equal((ushort)1, actual.Flags);
        NativeBattleGfGeometryAttempt a = Assert.Single(expected.Batch.Attempts);
        NativeBattleGfGeometryAttempt b = Assert.Single(actual.Batch.Attempts);
        Assert.Equal(a.Data, b.Data);
        Assert.Equal(a.WriteMask, b.WriteMask);
        Assert.Equal(a.Primitive!.Words, b.Primitive!.Words);
        Assert.Equal(a.Primitive.VertexPayload4, b.Primitive.VertexPayload4);
        Assert.Null(a.Primitive.PilotGeometry);
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(b.Primitive.PilotGeometry);
        Assert.Equal((ushort)187, geometry.Identity.Effect);
        Assert.True(geometry.Identity.Generation > 0);
        Assert.Equal(0x64B130, geometry.Identity.ResourceGeneration);
        Assert.True(geometry.ScreenSpace);
        Assert.False(geometry.Continuous);
        Assert.Equal(8, geometry.ScreenScale);
        Assert.Equal(new System.Numerics.Vector3(320, 216, 0), geometry.D);
        Assert.Equal(System.Numerics.Vector4.Zero, geometry.SourceDepths);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(57, b.Primitive.Bucket);
        Assert.Equal(new NativeBattlePrimitiveReference("off_B6D070", 0, 12528), Assert.Single(NativeBattleEffectPresentationPackets.Record([], "off_B6D070", 0, 12528, actual.Batch)));
    }

    [Fact]
    public void ScrollBirthPreservesUnwrittenPayloadAndConsumesExactlyOneCrtDraw()
    {
        NativeBattleEffect187Frame frame = Frame();
        NativeBattleEffectTaskPool child = frame.Effect.Pools[1];
        foreach (NativeBattleModulePoolTask task in child.Slots)
        {
            task.WriteWord(0x14, 0xABCD);
        }
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, child) } };
        NativeCrtRandomState expected = frame.Random.Draw(out ushort phase);
        frame = InitializeScrollBurst6483B0(frame);
        Assert.Equal(expected, frame.Random);
        NativeBattleModulePoolTask warp = Assert.Single(frame.Effect.Pools[1].Slots, task => task.Callback == 0x648490);
        Assert.Equal((ushort)(phase & 0xFFF), warp.ReadWord(0x18));
        NativeBattleModulePoolTask scroll = Assert.Single(frame.Effect.Pools[1].Slots, task => task.Callback == 0x6485C0);
        Assert.Equal((ushort)0, scroll.ReadWord(0x14));
        NativeBattleModulePoolTask[] delayed = frame.Effect.Pools[1].Slots.Where(task => task.Callback == 0x6486F0).ToArray();
        Assert.Equal(2, delayed.Length);
        Assert.All(delayed, task => Assert.Equal((ushort)0xABCD, task.ReadWord(0x14)));
    }

    [Fact]
    public void PayloadWritesKeepTheNextLinkCreatedByAChildAppend()
    {
        NativeBattleEffect187Frame frame = Frame();
        (frame, uint first) = Spawn647900(frame, 0x647F80);
        NativeBattleModulePoolTask prior = Read(frame, first);
        (frame, _) = Spawn647900(frame, 0x649D00);
        int? next = Read(frame, first).Next;
        Assert.NotNull(next);
        frame = Write(frame, first, prior.WriteWord(12, 7));
        Assert.Equal(next, Read(frame, first).Next);
        Assert.Equal((ushort)7, Read(frame, first).ReadWord(12));
    }

    [Fact]
    public void PausedTintStillPublishesCurrentColorWithoutAdvancingItsClock()
    {
        NativeBattleEffect187Frame frame = InitializeBodyBurst64B040(Frame());
        int slot = Enumerable.Range(0, frame.Effect.Pools[1].Slots.Length).Single(index => frame.Effect.Pools[1].Slots[index].Callback == 0x64B0A0);
        uint address = checked(ChildBank + (uint)(slot * 36));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 4));
        NativeBattleEffect187Inputs inputs = new(Colors: null!, ReadPackets: null!, ReadFlags: _ => 0x201, ReadStep508500: null!, ReadView: null!, ReadProjection: null!, ReadCameraRoll: null!, ReadCameraPose: null!, ReadEyePadding: null!, ReadTargetPadding: null!, ReadModel: null!, ReadWorld: null!, ReadTargetCenter5020A0: null!, ReadBone502170: null!, ReadVibrationPlaying4A2900: null!, Execute: null!);
        NativeBattleEffect187Step step = AdvanceTint64B0A0(frame, address, inputs);
        Assert.Equal(0x242424u, step.Frame.State.ReadDword(0x24FD900));
        Assert.Equal((ushort)4, Read(step.Frame, address).ReadWord(12));
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void BoneParticleBirthClearsOnlyRecordHeadsAndKeepsRetainedBodies()
    {
        NativeBattleEffect187Frame frame = Frame();
        byte[] retained = Enumerable.Repeat((byte)0xA7, 60).ToArray();
        frame = WriteBytes(frame, 0x24FD510, NativeBattleEffectArena.Clear().Write(0, retained), retained.Length);
        frame = InitializeBoneParticles64B610(frame);
        byte[] current = ReadBytes(frame, 0x24FD510, 60).Read(0, 60);
        for (int offset = 0; offset < current.Length; offset++)
        {
            Assert.Equal(offset % 20 < 4 ? (byte)0 : (byte)0xA7, current[offset]);
        }
        NativeBattleModulePoolTask task = Assert.Single(frame.Effect.Pools[1].Slots, task => task.Callback == 0x64B640);
        Assert.Equal((ushort)0, task.ReadWord(12));
    }

    [Fact]
    public void TextureReleaseConsumesTheImmediateCompletionByteBeforeReturning()
    {
        NativeBattleEffect187Frame frame = Frame();
        frame = frame with { State = frame.State.WriteDword(ArenaGlobal, CommonAddress) };
        (frame, uint address) = Spawn647900(frame, 0x649CC0);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 1));
        NativeBattleEffect187Inputs inputs = new(Colors: null!, ReadPackets: null!, ReadFlags: null!, ReadStep508500: null!, ReadView: null!, ReadProjection: null!, ReadCameraRoll: null!, ReadCameraPose: null!, ReadEyePadding: null!, ReadTargetPadding: null!, ReadModel: null!, ReadWorld: null!, ReadTargetCenter5020A0: null!, ReadBone502170: null!, ReadVibrationPlaying4A2900: null!, Execute: (current, operation) =>
        {
            NativeBattleEffect187QueueTextureRelease508630 queued = Assert.IsType<NativeBattleEffect187QueueTextureRelease508630>(operation);
            Assert.Equal(CommonAddress + 0x14000, queued.SourceAddress);
            Assert.Equal(address + 14, queued.CompletionAddress);
            return WriteCompletionByte(current, queued.CompletionAddress, 1);
        });
        NativeBattleEffect187Step step = AdvanceTextureRelease649CC0(frame, address, inputs);
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)2, Read(step.Frame, address).ReadWord(12));
        Assert.Equal((ushort)1, Read(step.Frame, address).ReadWord(14));
    }

    private static NativeBattleEffect187Frame Frame()
    {
        NativeBattleOriginalEffectState effect = new(Effect, 186, new(Effect, Entry, Initializer), new(0, 0, 8, 0, 0, 0), null);
        ImmutableArray<NativeBattleEffectTaskPool> pools = Pools.Select(shape => NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity)).ToImmutableArray();
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect with { Pools = pools }, NativeCrtRandomState.FromColdProcess(), NativeBattleEffect187State.FromColdProcess().DetachBank(0).DetachBank(1));
    }
}
