using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed partial class NativeQuake38Execution
    {
        private const int EntryOffset = 0x28000;
        private const int ZoneOffset = 0x33600;

        private void DrawGround()
        {
            NativeBattleStageState stage = session.NativeStage ?? throw new NotSupportedException("Native Quake requires its actual stage owner.");
            NativeBattleStageGroup ground = SelectGroundPose(stage.Groups[1]);
            session.NativeStage = stage with { Groups = stage.Groups.SetItem(1, ground) };
            NativeBattleTemporaryAllocation context = NativeBattleTemporaryMemory.Allocate5082B0(session.NativeFileStaging!, 44);
            int capacity = checked((session.NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native Quake requires its actual posed CPU workspace.")) * 8);
            uint workspace = checked(0x196B018u - (uint)capacity);
            NativeBattleFileStaging staging = context.Staging.Write(context.Address + 4, BitConverter.GetBytes(workspace)).Write(context.Address + 20, new byte[4]).Write(context.Address + 24, BitConverter.GetBytes((ushort)320)).Write(context.Address + 26, BitConverter.GetBytes((ushort)216)).Write(context.Address + 28, BitConverter.GetBytes(ground.ColorFlags)).Write(context.Address + 32, BitConverter.GetBytes(uint.MaxValue)).Write(context.Address + 36, BitConverter.GetBytes(ground.ColorDepth));
            (byte[] copied, bool[] copiedKnown) = staging.ReadSnapshot(0x1D969A8, 4);
            for (int index = 0; index < 4; index++)
            {
                staging = copiedKnown[index] ? staging.Write(context.Address + 16 + (uint)index, copied.AsSpan(index, 1)) : staging.MarkUnknown(context.Address + 16 + (uint)index, 1);
            }
            NativeBattleTemporaryAllocation vector = NativeBattleTemporaryMemory.Allocate5082B0(staging, 12);
            staging = vector.Staging.Write(vector.Address + 8, BitConverter.GetBytes(uint.MaxValue));
            ImmutableArray<NativeBattleRawMesh> meshes = session.RequireNativeStageGeometry().Where(mesh => mesh.StageGroup == 1).ToImmutableArray();
            short primitive = 0;
            foreach (NativeBattleRawMesh mesh in meshes)
            {
                (byte[] scratch, bool[] known) = staging.ReadSnapshot(workspace, capacity);
                if (state.Word(0x279299E) == 0)
                {
                    ImmutableArray<byte> posed = NativeBattleQuake38GroundVertices.Transform8D8B10(mesh, ground.Matrices, scratch.ToImmutableArray());
                    for (int index = 0; index < mesh.Vertices.Length; index++)
                    {
                        staging = staging.Write(workspace + checked((uint)index * 8), posed.AsSpan().Slice(index * 8, 6));
                    }
                    scratch = posed.ToArray();
                    for (int index = 0; index < mesh.Vertices.Length; index++)
                    {
                        Array.Fill(known, true, index * 8, 6);
                    }
                    if (mesh.Vertices.Length != 0)
                    {
                        NativeBattleVector transformed = new(BinaryPrimitives.ReadInt16LittleEndian(scratch.AsSpan((mesh.Vertices.Length - 1) * 8)), BinaryPrimitives.ReadInt16LittleEndian(scratch.AsSpan((mesh.Vertices.Length - 1) * 8 + 2)), BinaryPrimitives.ReadInt16LittleEndian(scratch.AsSpan((mesh.Vertices.Length - 1) * 8 + 4)));
                        staging = staging.Write(vector.Address, BitConverter.GetBytes(transformed.X)).Write(vector.Address + 2, BitConverter.GetBytes(transformed.Y)).Write(vector.Address + 4, BitConverter.GetBytes(transformed.Z));
                    }
                }
                if (Enumerable.Range(0, mesh.Vertices.Length).Any(index => known.AsSpan(index * 8, 6).Contains(false)))
                {
                    throw new NotSupportedException("Native Quake requires its actual retained posed ground XYZ bytes.");
                }
                ImmutableArray<NativeBattleRawVertexGroup> groups = mesh.PackedVertexGroups ?? throw new NotSupportedException("Native Quake requires its original packed ground bone/vertex groups.");
                short vertexBase = groups.IsEmpty ? (short)0 : groups[^1].Count;
                state = state.SetWord(0x2792D68, vertexBase);
                session.NativeFileStaging = staging;
                foreach (NativeBattleRawPolygon polygon in mesh.Polygons)
                {
                    primitive = unchecked((short)(primitive + 1));
                    int offset = checked(EntryOffset + primitive * 112);
                    if (state.Word(0x279299E) == 0)
                    {
                        Memory = Memory with { Arena = NativeBattleQuake38GroundEntries.Build(Memory.Arena!, offset, primitive, vertexBase, polygon, scratch) };
                    }
                    int[] perimeter = polygon.Quad ? [0, 1, 3, 2] : [0, 1, 2];
                    int centroid = polygon.Quad ? 4 : 3;
                    for (int edge = 0; edge < perimeter.Length; edge++)
                    {
                        int first = perimeter[edge];
                        int second = perimeter[(edge + 1) % perimeter.Length];
                        if (state.Word(0x279299E) == 0)
                        {
                            Memory = Memory with { Arena = NativeBattleQuake38GroundZones.Assign8D9950(Memory.Arena!, offset, ZoneOffset, edge, first, second, centroid, resources, state.Word(0x278C8E8), state.Word(0x278C8E6), state.Word(0x2792D6E)) };
                        }
                        else if (state.Word(0x279299E) == 1)
                        {
                            Memory = Memory with { Arena = NativeBattleQuake38Adjacency.Classify8D95E0(Memory.Arena!, EntryOffset, ZoneOffset, state.Word(0x278C8EC), state.Word(0x278C8E4), primitive, edge, first, second, centroid) };
                        }
                        DrawEdge(offset, edge, first, second, centroid);
                    }
                }
                staging = session.NativeFileStaging!;
            }
            session.NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(staging, 12);
            GroundState(primitive);
            session.NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(session.NativeFileStaging!, 44);
        }

        private static NativeBattleStageGroup SelectGroundPose(NativeBattleStageGroup group)
        {
            NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Native Quake requires its original ground skeleton and animation.");
            byte frames = NativeBattlePoseDecoder.ReadFrameCounts(source.Animations.AsSpan())[0];
            byte flags = (byte)(group.PoseFlags & 0xF3);
            byte limit = (flags & 1) != 0 ? unchecked((byte)(frames * 2 - 1)) : frames;
            byte counter = checked((byte)Math.Min(limit, ((flags >> 1) & 1) + 1));
            NativeBattleDecodedPose pose = source.Sample(0, counter, flags);
            return group with { Animation = 0, PoseFlags = (byte)(flags | 4), Counter = counter, FrameCount = limit, Matrices = NativeBattlePoseMatrices.StageBoneMatrices(source, pose) };
        }

        private void GroundState(short primitives)
        {
            switch (state.Word(0x279299E))
            {
                case 0:
                    state = state.SetWord(0x278C8EC, primitives).SetWord(0x278C8E4, 0).SetWord(0x279299E, 1);
                    break;
                case 1:
                    short wave = unchecked((short)(state.Word(0x278C8E4) + 1));
                    state = state.SetWord(0x278C8E4, wave);
                    if (wave > 16)
                    {
                        state = state.SetWord(0x279299E, 2);
                    }
                    break;
                case 2:
                    if (state.Word(0x2792E20) == 1)
                    {
                        state = state.SetWord(0x279299E, 3);
                    }
                    break;
                case 3:
                    short crack = unchecked((short)(state.Word(0x278C8EA) + 1));
                    state = state.SetWord(0x278C8EA, crack);
                    if (crack > 13)
                    {
                        state = state.SetWord(0x279299E, 4);
                    }
                    break;
                case 4:
                    short ripple = unchecked((short)(state.Word(0x2792D70) + 1));
                    state = state.SetWord(0x2792D70, ripple);
                    if (ripple > 12)
                    {
                        NativeBattleQuake38WaveStep initialized = NativeBattleQuake38Waves.Initialize8DA580(state, RandomState);
                        state = initialized.State.SetWord(0x279299E, 5);
                        RandomState = initialized.Random;
                    }
                    break;
                case 5:
                    GroundVisible(false);
                    NativeBattleQuake38WaveStep step = NativeBattleQuake38Waves.Advance8DA680(state, RandomState);
                    state = step.State;
                    RandomState = step.Random;
                    short fall = unchecked((short)(state.Word(0x2792B40) + 1));
                    state = state.SetWord(0x2792B40, fall);
                    if (fall > 40)
                    {
                        state = state.SetWord(0x279299E, 6);
                    }
                    if (state.Word(0x279299C) < 30)
                    {
                        for (int index = 0; index < 6; index++)
                        {
                            SpawnDebris();
                        }
                    }
                    if (state.Word(0x2792D6C) < 10)
                    {
                        SpawnGroundSprites(false, fall < 34 ? 6 : fall < 37 ? 5 : 4);
                    }
                    if (state.Word(0x2792E18) < 13)
                    {
                        SpawnGroundSprites(true, 6);
                    }
                    break;
                case 6:
                    NativeBattleQuake38SettleStep settled = NativeBattleQuake38Waves.Settle8DA710(state);
                    state = settled.State;
                    if (settled.Settled)
                    {
                        state = state.SetWord(0x279299E, 7);
                        GroundVisible(true);
                    }
                    break;
                case 7:
                    break;
                default:
                    throw new NotSupportedException("Native Quake ground state leaves its original table.");
            }
        }

        private void GroundVisible(bool visible)
        {
            NativeBattleStageState stage = session.NativeStage!;
            NativeBattleStageGroup ground = stage.Groups[1];
            session.NativeStage = stage with { Groups = stage.Groups.SetItem(1, ground with { Flags = visible ? (byte)(ground.Flags | 2) : (byte)(ground.Flags & ~2) }) };
        }
    }
}
