using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed partial class NativeTornado146Runner
    {
        private void InitializeCraterGrid()
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(owner.NativeFileStaging ?? throw new NotSupportedException("Native Tornado crater initialization requires its retained temporary memory."), 200);
            NativeBattleFileStaging staging = allocation.Staging.Write(allocation.Address + 0x30, BitConverter.GetBytes(resources.Crater.Address));
            state = NativeBattleTornado146Ground.Initialize615160(state, resources.Crater, (offset, value) => staging = staging.Write(allocation.Address + (uint)offset, BitConverter.GetBytes(value)));
            owner.NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(staging, 200);
        }

        private void CaptureGround(NativeBattleVector center)
        {
            NativeBattlePrimitiveProcess process = Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Tornado capture requires its current primitive owner.");
            byte buffer = process.Buffer80 ?? throw new NotSupportedException("Native Tornado capture requires its actual environment selector.");
            NativeSoftwareDrawingEnvironmentBuffers environments = process.Software?.EnvironmentBuffers ?? throw new NotSupportedException("Native Tornado capture requires its retained drawing environments.");
            using (NativeBattleTornado146Work environmentWork = new(state))
            {
                ImmutableArray<byte> environment = NativeBattleDrawEnvironmentPackets.Restore45C0F0(environmentWork.Read(0x2493710, 64).ToImmutableArray(), buffer == 0 ? environments.Buffer1 : environments.Buffer0);
                environmentWork.Write(0x2493710, LinkGroundPacket(environment, NativeBattleTornado146State.PacketBank, checked((int)(0x2493710 - NativeBattleTornado146State.WorkAddress)), 0).AsSpan());
                state = environmentWork.Freeze();
            }
            NativeBattleCameraProjection projection = NativeBattleTornado146Ground.CaptureProjection(center, Projection.FocalLength ?? throw new NotSupportedException("Native Tornado capture requires its actual focal WORD."));
            NativeBattleStageState stage = owner.NativeStage ?? throw new NotSupportedException("Native Tornado capture requires its actual stage rows.");
            NativeBattleStageGroup group = stage.Groups[1];
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(owner.NativeFileStaging ?? throw new NotSupportedException("Native Tornado capture requires its retained temporary memory."), 44);
            uint context = allocation.Address;
            int capacity = checked((owner.NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native Tornado capture requires its actual projected workspace.")) * 8);
            uint workspace = checked(0x196B018u - (uint)capacity);
            NativeBattleFileStaging staging = allocation.Staging.Write(context + 4, BitConverter.GetBytes(workspace)).Write(context + 16, BitConverter.GetBytes(uint.MaxValue)).Write(context + 32, BitConverter.GetBytes(uint.MaxValue));
            staging = staging.Write(context + 20, BitConverter.GetBytes((short)0)).Write(context + 22, BitConverter.GetBytes((short)0)).Write(context + 24, BitConverter.GetBytes((short)320)).Write(context + 26, BitConverter.GetBytes((short)216));
            staging = staging.Write(context + 28, BitConverter.GetBytes(group.ColorFlags)).Write(context + 36, BitConverter.GetBytes(group.ColorDepth));
            if ((group.Flags & 1) != 0)
            {
                NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Native Tornado capture requires its current ground skeleton.");
                NativeBattleDecodedPose pose = source.Sample(group.Animation, group.Counter, group.PoseFlags);
                group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(NativeBattlePoseMatrices.StageBoneMatrices(source, pose), projection.View!, 0) };
                owner.NativeStage = stage with { Groups = stage.Groups.SetItem(1, group) };
                var gate = process.Enter5099D0(owner.definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native Tornado capture requires its authenticated renderer capability."));
                process = gate.Process;
                if (gate.Draw)
                {
                    ImmutableArray<NativeBattleRawMesh> meshes = GroundMeshes();
                    (byte[] retained, _) = staging.ReadSnapshot(workspace, capacity);
                    NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(meshes, group.Matrices, projection, group.ColorFlags, uint.MaxValue, retained.ToImmutableArray(), Colors, 16, new(0, 0, 320, 216), 17, group.ColorDepth, NativeBattlePilotVisualLifetimes.Identity(146, pools[0].Slots[0], -1), pilotScreenSpace: true, pilotContinuous: false);
                    Append(drawn.Packets);
                    staging = staging.Write(workspace, drawn.ProjectedScratch.AsSpan().Slice(0, drawn.ProjectedBytesWritten));
                    process = process.PublishModelColor(group.ColorFlags).Publish50FDF0(state.Storage.ReadUInt32(checked((int)(0x24AA628 - NativeBattleTornado146State.WorkAddress))), null);
                }
            }
            owner.NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(staging, 44);
            Memory = Memory with { PrimitiveProcess = process };
            state = state with { CapturePacket = LinkGroundPacket(state.CapturePacket ?? resources.CapturePacket, "DD6780", 0, 1) };
            using NativeBattleTornado146Work work = new(state);
            byte[] mask = work.Read(0x249A5F8, 8);
            BinaryPrimitives.WriteUInt32LittleEndian(mask, (BinaryPrimitives.ReadUInt32LittleEndian(mask) & 0xFFFFFF) | 0x1000000);
            BinaryPrimitives.WriteUInt32LittleEndian(mask.AsSpan(4), 0xE6000000);
            work.Write(0x249A5F8, LinkGroundPacket(mask.ToImmutableArray(), NativeBattleTornado146State.PacketBank, checked((int)(0x249A5F8 - NativeBattleTornado146State.WorkAddress)), 0).AsSpan());
            ImmutableArray<byte> area = NativeBattleDrawEnvironmentPackets.Area45C940(work.Read(0x2491E40, 12).ToImmutableArray(), 640, 256, 256, 256);
            work.Write(0x2491E40, LinkGroundPacket(area, NativeBattleTornado146State.PacketBank, checked((int)(0x2491E40 - NativeBattleTornado146State.WorkAddress)), 0).AsSpan());
            work.WriteWord(0x249A5F0, 640);
            work.WriteWord(0x249A5F2, 256);
            ImmutableArray<byte> offset = NativeBattleDrawEnvironmentPackets.Offset45C9B0(work.Read(0x24936F8, 8).ToImmutableArray(), 640, 256);
            work.Write(0x24936F8, LinkGroundPacket(offset, NativeBattleTornado146State.PacketBank, checked((int)(0x24936F8 - NativeBattleTornado146State.WorkAddress)), 0).AsSpan());
            state = work.Freeze();
        }

        private ImmutableArray<byte> LinkGroundPacket(ImmutableArray<byte> bytes, string bank, int offset, ushort flags)
        {
            using NativeBattleLogicalOrdering.Builder links = new(graphics.PrimitiveFrame!.Pending);
            NativeBattlePrimitiveReference reference = new(bank, 0, offset);
            links.InvalidatePacketRange(reference, bytes.Length);
            ImmutableArray<uint> words = Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(index * 4))).ToImmutableArray();
            bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
            links.Prepend(17, reference, words, flags, [0, 0, 0, 0]);
            byte[] retained = bytes.ToArray();
            if (linked)
            {
                NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink).CopyTo(retained, 0);
            }
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = links.ToImmutable() } };
            return retained.ToImmutableArray();
        }

        private int Ground(ref NativeBattleModulePoolTask task)
        {
            NativeBattleStageState stage = owner.NativeStage ?? throw new NotSupportedException("Native Tornado ground requires its current stage rows.");
            NativeBattleStageGroup group = stage.Groups[1] with { Flags = unchecked((byte)(stage.Groups[1].Flags & 0xFD)) };
            owner.NativeStage = stage with { Groups = stage.Groups.SetItem(1, group) };
            short age = Word(task, 12);
            if (age < 68)
            {
                DrawGround(task, group, age >= 64 ? (68 - age) << 10 : 4096);
            }
            if (DrawOnly)
            {
                return 0;
            }
            age = unchecked((short)(age + 1));
            task = task.WriteWord(12, unchecked((ushort)age));
            if (age <= 68)
            {
                return 0;
            }
            stage = owner.NativeStage!;
            owner.NativeStage = stage with { Groups = stage.Groups.SetItem(1, stage.Groups[1] with { Flags = unchecked((byte)(stage.Groups[1].Flags | 2)) }) };
            return 2;
        }

        private void DrawGround(NativeBattleModulePoolTask task, NativeBattleStageGroup group, int fade)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(owner.NativeFileStaging ?? throw new NotSupportedException("Native Tornado ground requires its retained temporary memory."), 200);
            uint context = allocation.Address;
            NativeBattleFileStaging staging = allocation.Staging;
            void Store(int offset, uint value) => staging = staging.Write(context + (uint)offset, BitConverter.GetBytes(value));
            void WordStore(int offset, ushort value) => staging = staging.Write(context + (uint)offset, BitConverter.GetBytes(value));
            NativeBattleVector center = Position(task);
            Store(0xB8, task.ReadDword(16));
            Store(0xBC, task.ReadDword(20));
            Store(0x30, resources.Crater.Address);
            Store(0x3C, unchecked((uint)fade));
            int capacity = checked((owner.NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native Tornado ground requires its actual transformed workspace.")) * 8);
            uint workspace = checked(0x196B018u - (uint)capacity);
            Store(4, workspace);
            (byte[] rendererFlags, bool[] known) = staging.ReadSnapshot(0x1D969A8, 4);
            for (int index = 0; index < 4; index++)
            {
                staging = known[index] ? staging.Write(context + 16u + (uint)index, rendererFlags.AsSpan(index, 1)) : staging.MarkUnknown(context + 16u + (uint)index, 1);
            }
            WordStore(20, 0);
            WordStore(22, 0);
            WordStore(24, 320);
            WordStore(26, 216);
            Store(28, group.ColorFlags);
            Store(32, uint.MaxValue);
            WordStore(36, unchecked((ushort)group.ColorDepth));
            staging = staging.MarkUnknown(context + 44, 4);
            Store(0x50, unchecked((uint)(center.X - 0x8D8)));
            Store(0x54, unchecked((uint)(center.X + 0x7E8)));
            Store(0x58, unchecked((uint)(center.Z - 0x80E)));
            Store(0x5C, unchecked((uint)(center.Z + 0x7DF)));
            Store(0x60, 0);
            Store(0x64, 0);
            NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Native Tornado ground requires its actual current ground skeleton.");
            NativeBattleDecodedPose pose = source.Sample(group.Animation, group.Counter, group.PoseFlags);
            group = group with { Matrices = NativeBattlePoseMatrices.StageBoneMatrices(source, pose) };
            NativeBattleStageState stage = owner.NativeStage!;
            owner.NativeStage = stage with { Groups = stage.Groups.SetItem(1, group) };
            Memory = Memory with { PrimitiveProcess = (Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Tornado ground requires its current far-color owner.")).PublishFarColor45DDA0(0, 0, 0) };
            NativeBattleScriptedGraphicsMemory controls = Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Tornado ground requires its retained GTE controls.");
            using NativeBattleTornado146Work work = new(state);
            ImmutableArray<NativeBattleRawMesh> groundMeshes = GroundMeshes();
            for (int meshIndex = 0; meshIndex < groundMeshes.Length; meshIndex++)
            {
                NativeBattleRawMesh mesh = groundMeshes[meshIndex];
                if (mesh.Vertices.Length * 8 > capacity)
                {
                    throw new InvalidDataException("Native Tornado ground leaves its installed CPU vertex capacity.");
                }
                ImmutableArray<NativeBattleTornado146GroundVertex>.Builder transformed = ImmutableArray.CreateBuilder<NativeBattleTornado146GroundVertex>(mesh.Vertices.Length);
                foreach (NativeBattleRawVertex vertex in mesh.Vertices)
                {
                    NativeBattleVector world = NativeBattleGfMatrices.Transform460840(group.Matrices[vertex.Bone], vertex.Position);
                    NativeBattleTornado146GroundVertex changed = world.X >= center.X - 0x8D8 && world.X <= center.X + 0x7E8 && world.Z >= center.Z - 0x80E && world.Z <= center.Z + 0x7DF ? NativeBattleTornado146Ground.Deform6167E0(work, resources.Crater, world, center, fade, Store) : new(world, 0);
                    uint destination = checked(workspace + (uint)transformed.Count * 8);
                    staging = staging.Write(destination, BitConverter.GetBytes(changed.Position.X)).Write(destination + 2, BitConverter.GetBytes(changed.Position.Y)).Write(destination + 4, BitConverter.GetBytes(changed.Position.Z)).Write(destination + 6, BitConverter.GetBytes(changed.Weight));
                    transformed.Add(changed);
                }
                staging = staging.MarkUnknown(context, 4);
                WordStore(8, 0);
                WordStore(10, 0);
                WordStore(12, checked((ushort)mesh.Polygons.Count(polygon => !polygon.Quad)));
                WordStore(14, checked((ushort)mesh.Polygons.Count(polygon => polygon.Quad)));
                NativeBattleGfGeometryBatch batch = NativeBattleTornado146GroundPackets.Emit616AC0(mesh.Polygons, transformed.ToImmutable(), View, Projection, controls.Zsf3 ?? throw new NotSupportedException("Native Tornado ground requires its current ZSF3."), controls.Zsf4 ?? throw new NotSupportedException("Native Tornado ground requires its current ZSF4."), controls.Dqa, controls.Dqb, Colors, Store, NativeBattlePilotVisualLifetimes.Identity(146, task, meshIndex));
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Tornado ground requires its actual COMMON packet cursor."), graphics.PrimitiveFrame!.Pending, batch);
                Memory = Memory with { PrimitiveProcess = written.Process };
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            }
            owner.NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(staging, 200);
        }

        private ImmutableArray<NativeBattleRawMesh> GroundMeshes() => owner.RequireNativeStageGeometry().Where(mesh => mesh.StageGroup == 1).ToImmutableArray();
    }
}
