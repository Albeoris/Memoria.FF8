using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed record NativeUltraWavesCaptureResult(NativeBattleGraphicsState Graphics, NativeBattleEffectArena Packets, NativeBattleEffectArena CapturePackets, int Cursor);

    private NativeUltraWavesCaptureResult CaptureNativeUltraWaves(NativeBattleGraphicsState graphics, NativeBattleEffectArena packets, NativeBattleEffectArena capturePackets, int cursor, string packetBank, string captureBank, ImmutableArray<int> directory)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Ultra Waves capture requires its process memory.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Ultra Waves capture requires its primitive process.");
        byte buffer = process.Buffer80 ?? throw new NotSupportedException("Ultra Waves capture requires its display buffer.");
        NativeSoftwareDrawingEnvironmentBuffers environments = process.Software?.EnvironmentBuffers ?? throw new NotSupportedException("Ultra Waves capture requires its retained drawing environments.");
        ImmutableArray<byte> alternate = buffer == 0 ? environments.Buffer1 : environments.Buffer0;
        ImmutableArray<byte> restore = NativeBattleDrawEnvironmentPackets.Restore45C0F0(packets.Read(cursor, 64).ToImmutableArray(), alternate);
        Append(NativeBattleUltraWavesEnvironmentPackets.Environment(restore, 64, alternate[0x18] != 0 ? 36 : 24, 0x101B));
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Ultra Waves capture requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Ultra Waves capture requires its current view.");
        NativeBattleCameraProjection captureProjection = projection with { OffsetX = 160, OffsetY = 108 };
        NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Ultra Waves capture requires its shared resource memory.");
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Ultra Waves capture requires its shared model heap.");
        int scratchLength = checked((cpu.ScratchMaximum ?? throw new NotSupportedException("Ultra Waves capture requires its loaded vertex capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Ultra Waves capture requires its retained stage rows.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 84);
        staging = allocation.Staging;
        NativeBattleEffectArena temporary = NativeBattleUltraWavesStageCapture84.Prepare(View(allocation.Address, 84), scratchAddress, View(0x1D969A8, 4));
        staging = NativeBattleTemporaryMemory.PublishView(staging, allocation.Address, temporary, 84);
        ImmutableArray<NativeBattleStageGroup>.Builder groups = stage.Groups.ToBuilder();
        for (int index = 0; index < 4; index++)
        {
            NativeBattleStageGroup group = groups[index];
            NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Ultra Waves capture requires its current stage skeleton.");
            NativeBattleDecodedPose decoded = source.Sample(group.Animation, group.Counter, group.PoseFlags);
            ImmutableArray<NativeBattleActionMatrix> local = NativeBattlePoseMatrices.StageBoneMatrices(source, decoded);
            NativeBattleTemporaryAllocation boneAllocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 88);
            staging = boneAllocation.Staging;
            NativeBattleEffectArena boneTemporary = NativeBattleBodyBoneTemporary.Prepare508C90(source, decoded, local, View(boneAllocation.Address, 88));
            staging = NativeBattleTemporaryMemory.PublishView(staging, boneAllocation.Address, boneTemporary, 88);
            staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88);
            group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(local, view, 0) };
            groups[index] = group;
            if ((group.Flags & 1) == 0)
            {
                continue;
            }
            (uint geometryAddress, int geometryLength) = stage.CurrentGroupGeometryRange(index, (address, length) => staging.Read(address, length)) ?? throw new NotSupportedException("Ultra Waves capture requires its selected current stage geometry.");
            ImmutableArray<NativeBattlePackedMatrix32> bones = group.Matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
            NativeBattleUltraWavesStageCapture84Step drawn;
            try
            {
                drawn = NativeBattleUltraWavesStageCapture84.DrawGroup(memory, temporary, group.ColorFlags, group.ColorDepth, bones, Read, geometryAddress, View(scratchAddress, scratchLength), scratchLength, captureProjection, definition.NativeGraphics!.Rules.SummonColors!, definition.NativeGraphics.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Ultra Waves capture requires its renderer capability."), directory[index * 2], checked((byte)directory[index * 2 + 1]));
            }
            catch (InvalidDataException exception)
            {
                int objectCount = BinaryPrimitives.ReadInt32LittleEndian(staging.Read(geometryAddress, 4));
                throw new InvalidDataException($"Ultra Waves stage group {index} geometry 0x{geometryAddress:X8}, length {geometryLength}, object count {objectCount}, enemy heap cursor {cpu.EnemyCursor}: {exception.Message}", exception);
            }
            memory = drawn.Memory;
            temporary = drawn.Temporary84;
            staging = NativeBattleTemporaryMemory.PublishView(staging, allocation.Address, temporary, 84);
            staging = NativeBattleTemporaryMemory.PublishView(staging, scratchAddress, drawn.ProjectedScratch, scratchLength);
            NativeBattleCommonPacketWrite common = NativeBattleCommonPacketArena.Append(memory.PrimitiveProcess!, graphics.PrimitiveFrame!.Pending, drawn.Batch);
            memory = memory with { PrimitiveProcess = common.Process };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = common.Ordering } };

            byte[] Read(int offset, int length)
            {
                if (offset < 0 || length < 0 || offset > geometryLength - length)
                {
                    throw new InvalidDataException("Ultra Waves stage geometry read leaves its actual loaded extent.");
                }
                return staging.Read(checked(geometryAddress + (uint)offset), length);
            }
        }
        NativeStage = stage with { Groups = groups.ToImmutable() };
        NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(staging, 84);
        NativeEffectMemory = memory;
        int staticOffset = buffer * 24;
        byte[] packet = capturePackets.Read(staticOffset, 24);
        ImmutableArray<uint> words = Enumerable.Range(0, 6).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(index * 4))).ToImmutableArray();
        NativeBattleLogicalOrdering.Builder links = new(graphics.PrimitiveFrame!.Pending);
        NativeBattlePrimitiveReference reference = new(captureBank, 0, staticOffset);
        links.InvalidatePacketRange(reference, 24);
        bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
        links.Prepend(0x1121, reference, words, 1, [0, 0, 0, 0]);
        if (linked)
        {
            NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink).CopyTo(packet, 0);
        }
        capturePackets = capturePackets.Write(staticOffset, packet);
        graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = links.ToImmutable() } };
        Append(NativeBattleUltraWavesEnvironmentPackets.Environment(NativeBattleDrawEnvironmentPackets.Area45C940(packets.Read(cursor, 12).ToImmutableArray(), 384, 256, 320, 216), 12, 12, 0x1121));
        Append(NativeBattleUltraWavesEnvironmentPackets.Environment(NativeBattleDrawEnvironmentPackets.Offset45C9B0(packets.Read(cursor, 8).ToImmutableArray(), 384, 256), 12, 8, 0x1121));
        return new(graphics, packets, capturePackets, cursor);

        NativeBattleEffectArena View(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);

        void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(packets, packetBank, cursor, 0x18000, graphics.PrimitiveFrame!.Pending, batch);
            packets = written.Arena;
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            cursor = written.Cursor;
        }
    }
}
