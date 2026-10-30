using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect312Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect312Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleEffect312Frame Frame) CaptureNativeEffect312Stage(NativeBattleGraphicsState graphics, NativeBattleEffect312Frame frame, NativeBattleEffect312CapturePreparation preparation, NativeBattleEffect312Resources resources, NativeBattleEffect312Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect312 capture requires its current camera.");
        NativeBattleCameraProjection prior = camera.Projection ?? throw new NotSupportedException("Effect312 capture requires its current projection.");
        short offsetX = prior.OffsetX ?? throw new NotSupportedException("Effect312 capture requires its actual prior screen X offset.");
        short offsetY = prior.OffsetY ?? throw new NotSupportedException("Effect312 capture requires its actual prior screen Y offset.");
        ushort restoredH = unchecked((ushort)(camera.FocalLength ?? throw new NotSupportedException("Effect312 capture requires its actual scalar focal WORD.")));
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect312 capture requires its current primitive process.");
        byte side = process.Buffer80 ?? throw new NotSupportedException("Effect312 capture requires its actual display-side selector.");
        NativeSoftwareDrawingEnvironmentBuffers environments = process.Software?.EnvironmentBuffers ?? throw new NotSupportedException("Effect312 capture requires its actual DRAWENV records.");
        ImmutableArray<byte> environment = side switch { 0 => environments.Buffer1, 1 => environments.Buffer0, _ => throw new InvalidDataException("Effect312 capture requires a native display side.") };
        NativeBattleEffectArena restore = frame.State.ReadView(EnvironmentRestoreAddress, EnvironmentRestoreLength);
        NativeBattleEffectArena helperView = CopyBytes(restore, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 64), 0, EnvironmentRestoreLength);
        (byte[] retained, _) = helperView.ReadSnapshot(0, 64);
        ImmutableArray<byte> restoredEnvironment = NativeBattleDrawEnvironmentPackets.Restore45C0F0(retained.ToImmutableArray(), environment);
        int storedLength = environment[0x18] == 0 ? 24 : 36;
        restore = restore.Write(3, restoredEnvironment.AsSpan().Slice(3, storedLength - 3));
        frame = frame with { State = frame.State.WriteView(EnvironmentRestoreAddress, restore, EnvironmentRestoreLength) };
        Link(EnvironmentRestoreAddress, 0);

        NativeCamera = camera with { Projection = prior with { OffsetX = 128, OffsetY = 128, FocalLength = 32767 } };
        frame = LoadMatrix(frame, preparation.Matrix32);
        (frame, uint contextAddress) = Allocate(frame, 44);
        int scratchLength = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect312 capture requires its actual shared projected capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        NativeBattleEffectArena context = ReadBytes(frame, contextAddress, 44).Write(4, BitConverter.GetBytes(scratchAddress)).Write(0x10, BitConverter.GetBytes(uint.MaxValue)).Write(0x14, new byte[4]).Write(0x18, BitConverter.GetBytes((short)320)).Write(0x1A, BitConverter.GetBytes((short)216)).Write(0x20, BitConverter.GetBytes(uint.MaxValue));
        frame = WriteBytes(frame, contextAddress, context, 44);
        for (int index = 1; index >= 0; index--)
        {
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect312 capture requires its current stage.");
            NativeBattleStageGroup group = stage.Groups[index];
            if ((group.Flags & 3) != 3)
            {
                continue;
            }
            context = ReadBytes(frame, contextAddress, 44).Write(0x1C, BitConverter.GetBytes(group.ColorFlags)).Write(0x24, BitConverter.GetBytes(group.ColorDepth));
            frame = WriteBytes(frame, contextAddress, context, 44);
            (frame, uint localAddress) = Allocate(frame, 88);
            NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Effect312 capture requires its current stage rig.");
            NativeBattleDecodedPose pose = source.Sample(group.Animation, group.Counter, group.PoseFlags);
            group = NativeBattleCinematicStageShatter49.RestoreStageGroup508C90(group);
            NativeBattleEffectArena local = NativeBattleBodyBoneTemporary.Prepare508C90(source, pose, group.Matrices, ReadBytes(frame, localAddress, 88));
            frame = WriteBytes(frame, localAddress, local, 88);
            frame = Release(frame, 88);
            group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(group.Matrices, preparation.Matrix32.ReadMatrix(), 0) };
            NativeStage = stage with { Groups = stage.Groups.SetItem(index, group) };
            NativeFileStaging = frame.Staging;
            uint geometry = RequireNativeCurrentStageGeometryAddress(index);
            NativeBattleModelContext44Step drawn = NativeBattleModelContext44.Draw(frame.Memory, context, group.Matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray(), (offset, count) => ReadBytes(frame, checked(geometry + (uint)offset), count).Read(0, count), geometry, ReadBytes(frame, scratchAddress, scratchLength), scratchLength, NativeCamera.Projection!, inputs.Colors, definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Effect312 capture requires its renderer capability."), 17, 16, frame.State.ReadDword(CursorGlobal), pilotIdentity: pilotIdentity is { } identity ? identity with { Primitive = unchecked(identity.Primitive * 397 + index), ResourceGeneration = geometry } : null, pilotScreenSpace: true, pilotContinuous: false);
            frame = frame with { Memory = drawn.Memory };
            frame = WriteBytes(frame, contextAddress, drawn.Context44, 44);
            frame = WriteBytes(frame, scratchAddress, drawn.ProjectedScratch, scratchLength);
            Deliver(drawn.Batch);
        }
        frame = Release(frame, 44);
        NativeCamera = NativeCamera with { Projection = NativeCamera.Projection! with { OffsetX = offsetX, OffsetY = offsetY, FocalLength = restoredH } };
        frame = frame with { State = frame.State.BindCapturePacket(resources) };
        Link(CapturePacketAddress, 1);
        byte[] rect = preparation.Rectangle8.Read(0, 8);
        short x = BinaryPrimitives.ReadInt16LittleEndian(rect);
        short y = BinaryPrimitives.ReadInt16LittleEndian(rect.AsSpan(2));
        short width = BinaryPrimitives.ReadInt16LittleEndian(rect.AsSpan(4));
        short height = BinaryPrimitives.ReadInt16LittleEndian(rect.AsSpan(6));
        NativeBattleEffectArena area = frame.State.ReadView(EnvironmentAreaAddress, 12);
        ImmutableArray<byte> areaPacket = NativeBattleDrawEnvironmentPackets.Area45C940(area.ReadSnapshot(0, 12).Data.ToImmutableArray(), x, y, width, height);
        frame = frame with { State = frame.State.WriteView(EnvironmentAreaAddress, area.Write(3, areaPacket.AsSpan().Slice(3)), 12) };
        Link(EnvironmentAreaAddress, 0);
        frame = frame with { State = frame.State.Write(CapturedOffsetAddress, rect.AsSpan(0, 4)) };
        NativeBattleEffectArena offset = frame.State.ReadView(EnvironmentOffsetAddress, 8);
        ImmutableArray<byte> offsetPacket = NativeBattleDrawEnvironmentPackets.Offset45C9B0(offset.ReadSnapshot(0, 8).Data.ToImmutableArray(), x, y);
        frame = frame with { State = frame.State.WriteView(EnvironmentOffsetAddress, offset.Write(3, offsetPacket.AsSpan().Slice(3)), 8) };
        Link(EnvironmentOffsetAddress, 0);
        return (graphics, frame);

        void Deliver(NativeBattleGfGeometryBatch batch)
        {
            batch = NativeBattleEffect312Presentation.PreserveNativeDepths(batch);
            RequirePacketCapacity(frame, batch);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect312 capture requires its current ordering table.");
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(CursorGlobal), 0);
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect312 capture requires its current packet arena.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(batch, cursor, NativeBattleEffectArena.Length, address =>
            {
                (byte[] data, bool[] known) = arena.ReadSnapshot(address, 1);
                return (data[0], known[0]);
            });
            NativeBattleEffectPacketWrite write = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, primitive.Pending, resolved);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = write.Ordering } };
            frame = frame with { Memory = write.Memory, State = frame.State.WriteDword(CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)write.Cursor)) };
        }

        void Link(uint address, ushort flags)
        {
            bool executable = address == CapturePacketAddress;
            NativeBattleEffectArena bytes = executable ? frame.State.ReadCapturePacket() : frame.State.ReadView(address, address == EnvironmentRestoreAddress ? EnvironmentRestoreLength : address == EnvironmentAreaAddress ? 12 : 8);
            int length = checked(4 + bytes.Read(3, 1)[0] * 4);
            (byte[] data, bool[] known) = bytes.ReadSnapshot(0, length);
            if (known.Skip(3).Any(value => !value))
            {
                throw new NotSupportedException("Effect312 capture links require their consumed packet fields; only the old low TAG link may be unobserved.");
            }
            ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
            NativeBattlePrimitiveReference reference = new(executable ? "CDF6B0" : "223CCA0", 0, executable ? 0 : checked((int)(address - BaseAddress)));
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect312 capture requires its retained ordering table.");
            using NativeBattleLogicalOrdering.Builder links = new(primitive.Pending);
            links.InvalidatePacketRange(reference, length);
            bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
            links.Prepend(17, reference, words, flags, [0, 0, 0, 0]);
            if (linked)
            {
                bytes = bytes.Write(0, NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink));
                frame = frame with { State = executable ? frame.State.WriteCapturePacket(bytes) : frame.State.WriteView(address, bytes, address == EnvironmentRestoreAddress ? EnvironmentRestoreLength : address == EnvironmentAreaAddress ? 12 : 8) };
            }
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = links.ToImmutable() } };
        }
    }
}
