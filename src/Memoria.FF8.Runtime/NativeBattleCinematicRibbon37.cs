using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicRibbonParent(NativeBattleWaterExecutionFrame Frame, NativeBattleActionMatrix Matrix);
public sealed record NativeBattleCinematicRibbonProjection(NativeBattleWaterExecutionFrame Frame, uint Screen, uint Depth, uint Flags)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public System.Numerics.Vector3? PilotWorld { get; init; }
}
public sealed record NativeBattleCinematicRibbon37Services(Func<NativeBattleWaterExecutionFrame, ushort, uint> ResolveNode, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> PublishInput, Func<NativeBattleWaterExecutionFrame, uint, uint, uint, NativeBattleWaterExecutionFrame> BuildColorRamp, Func<NativeBattleWaterExecutionFrame, NativeBattleCinematicRibbonParent> PrepareParent, Func<NativeBattleWaterExecutionFrame, NativeBattleActionMatrix, uint, NativeBattleCinematicRibbonProjection> Project, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

public static class NativeBattleCinematicRibbon37
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicRibbon37Services services, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        uint state = SlotDword(0xBC);
        if (state == 0)
        {
            uint payload = SlotDword(0xB8);
            ushort capacity = Word(payload + 8);
            (frame, state) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, unchecked((uint)(capacity * 72 + 32)));
            SlotStoreDword(0xBC, state);
            StoreWord(state + 2, capacity);
            StoreWord(state, 1);
            StoreWord(state + 4, 1);
            StoreWord(state + 8, Word(payload + 14));
            StoreWord(state + 10, Word(payload + 10));
            StoreWord(state + 12, Word(payload + 12));
            StoreWord(state + 14, 0);
            StoreWord(state + 16, Word(payload + 16));
            StoreWord(state + 6, Word(payload));
            frame = frame.WriteTemporaryDword(0x98, services.ResolveNode(frame, Word(state + 6)));
            uint first = state + 32;
            NativeBattleVector position = SlotVector(0x94);
            StoreVector(first, position);
            bool follow = (Read(state + 16, 1)[0] & 1) != 0;
            int dx = follow ? unchecked((int)SlotDword(0x74)) >> 16 : SignedWord(payload + 2);
            int dy = follow ? unchecked((int)SlotDword(0x78)) >> 16 : SignedWord(payload + 4);
            int dz = follow ? unchecked((int)SlotDword(0x7C)) >> 16 : SignedWord(payload + 6);
            (frame, int yaw) = NativeBattleCinematicRibbonBasis.Angle(frame, position.X, position.Z, unchecked(position.X + dx), unchecked(position.Z + dz));
            StoreWord(first + 6, (ushort)yaw);
            StoreWord(first + 78, (ushort)yaw);
            (frame, int pitch) = NativeBattleCinematicRibbonBasis.Angle(frame, position.Y, position.Z, unchecked(position.Y + dy), unchecked(position.Z + dz));
            StoreWord(first + 14, (ushort)pitch);
            StoreWord(first + 86, (ushort)pitch);
            frame = frame.WriteTemporaryWord(0xD2, unchecked((ushort)NativeBattleCameraMath.Sine(pitch))).WriteTemporaryWord(0xD4, unchecked((ushort)NativeBattleCameraMath.Cosine(yaw))).WriteTemporaryWord(0xD0, unchecked((ushort)NativeBattleCameraMath.Sine(yaw)));
            NativeBattleCinematicRibbonBasisResult basis = NativeBattleCinematicRibbonBasis.Prepare(frame, GlobalVector(), TemporaryVector(0xD0));
            frame = basis.Frame;
            NativeBattleActionMatrix matrix = basis.Matrix with { X = position.X, Y = position.Y, Z = position.Z };
            CrossSection(matrix, SignedSlot(0x90), 0, first, basis.Flip);
            CrossSection(matrix, SignedSlot(0x90), follow ? 0 : SignedSlot(0x8C), first + 72, basis.Flip);
            frame = frame.WriteTemporaryDword(0x68, first).WriteTemporaryDword(0x6C, first + 72).WriteTemporaryDword(0x80, Word(state)).WriteTemporaryDword(0x78, Word(state + 4));
            frame = frame.WriteTemporaryDword(0x9C, services.ResolveNode(frame, Word(state + 8)));
        }
        else
        {
            frame = frame.WriteTemporaryDword(0x98, services.ResolveNode(frame, Word(state + 6)));
            frame = frame.WriteTemporaryDword(0x9C, services.ResolveNode(frame, Word(state + 8)));
            uint control = CoreDword(frame, 0x27973B8);
            byte pause = control >= frame.TemporaryFrame.Address && (long)control - frame.TemporaryFrame.Address <= 0x180 - 0x46 ? frame.ReadTemporary(checked((int)(control - frame.TemporaryFrame.Address + 0x45)), 1)[0] : frame.Core.Read(control + 0x45, 1)[0];
            if (pause == 0)
            {
                short speed = SignedSlot(0x8C);
                if (speed < 0)
                {
                    if (speed != -2)
                    {
                        StoreWord(state + 14, unchecked((ushort)(Word(state + 14) + 1)));
                        if (Word(state + 14) >= Word(state + 2))
                        {
                            return frame;
                        }
                    }
                    frame = frame.WriteTemporaryDword(0x6C, Segment(Word(state)));
                }
                else
                {
                    StoreWord(state, unchecked((ushort)(Word(state) + 1)));
                    int visible = Word(state + 4) + 1;
                    if (visible < Word(state + 2))
                    {
                        StoreWord(state + 4, (ushort)visible);
                    }
                    int counter = Word(state);
                    uint current = Segment(counter);
                    uint previous = Segment(counter - 1);
                    frame = frame.WriteTemporaryDword(0x80, (uint)counter).WriteTemporaryDword(0x6C, current).WriteTemporaryDword(0x68, previous).WriteTemporaryDword(0x78, Word(state + 4));
                    uint target = frame.ReadTemporaryDword(0x98);
                    bool follow = (Read(state + 16, 1)[0] & 1) != 0;
                    if (follow)
                    {
                        NativeBattleVector position = SlotVector(0x94);
                        NativeBattleVector old = Vector(previous);
                        StoreTemporaryVector(0xF0, position);
                        StoreTemporaryVector(0xE0, new(unchecked((short)(position.X - old.X)), unchecked((short)(position.Y - old.Y)), unchecked((short)(position.Z - old.Z))));
                        frame = frame.WriteTemporaryDword(0x94, unchecked((uint)NativeBattleCinematicRibbonBasis.Distance(position.X - old.X, position.Y - old.Y, position.Z - old.Z)));
                    }
                    else
                    {
                        Steer(target + 0x94, previous, current);
                    }
                    NativeBattleCinematicRibbonBasisResult basis = NativeBattleCinematicRibbonBasis.Prepare(frame, GlobalVector(), TemporaryVector(0xE0));
                    frame = basis.Frame;
                    NativeBattleVector oldPosition = Vector(previous);
                    NativeBattleActionMatrix matrix = basis.Matrix with { X = oldPosition.X, Y = oldPosition.Y, Z = oldPosition.Z };
                    CrossSection(matrix, SignedSlot(0x90), follow ? unchecked((int)frame.ReadTemporaryDword(0x94)) : SignedSlot(0x8C), current, basis.Flip);
                    if (follow)
                    {
                        NativeBattleVector position = SlotVector(0x94);
                        NativeBattleVector center = Vector(current);
                        for (int index = 0; index < 9; index++)
                        {
                            uint address = unchecked(current + (uint)(index * 8));
                            StoreWord(address, unchecked((ushort)(Word(address) + position.X - center.X)));
                            StoreWord(address + 2, unchecked((ushort)(Word(address + 2) + position.Y - center.Y)));
                            StoreWord(address + 4, unchecked((ushort)(Word(address + 4) + position.Z - center.Z)));
                        }
                    }
                    else
                    {
                        NativeBattleVector center = Vector(current);
                        for (int lane = 0; lane < 3; lane++)
                        {
                            short coordinate = lane == 0 ? center.X : lane == 1 ? center.Y : center.Z;
                            SlotStoreWord(0x94 + lane * 2, unchecked((ushort)coordinate));
                            SlotStoreDword(0x5C + lane * 4, unchecked((uint)(coordinate << 16)));
                        }
                        NativeBattleVector targetPosition = Vector(target + 0x94);
                        int distance = NativeBattleCinematicRibbonBasis.Distance(targetPosition.X - center.X, targetPosition.Y - center.Y, targetPosition.Z - center.Z);
                        if (distance <= SignedWord(target + 0x8E))
                        {
                            SlotStoreByte(0x4A, (byte)(ReadSlot(frame, 0x4A, 1)[0] | 1));
                        }
                        else
                        {
                            SlotStoreWord(0x4A, unchecked((ushort)(SlotWord(frame, 0x4A) & ~1)));
                        }
                    }
                }
            }
        }
        Render();
        return frame;

        void Render()
        {
            state = SlotDword(0xBC);
            int span = (Read(state + 16, 1)[0] & 1) != 0 ? SignedSlot(0x8E) << 4 : SignedWord(frame.ReadTemporaryDword(0x98) + 0x8C) << 4;
            frame = frame.WriteTemporaryDword(0x90, unchecked((uint)span));
            uint colorNode = frame.ReadTemporaryDword(0x9C);
            uint firstColor = PackedColor(colorNode + 0x94);
            uint secondColor = PackedColor(colorNode + 0x8C);
            uint colors = unchecked(ManagerDword(0x74) + 256);
            frame = frame.WriteTemporaryDword(0x84, colors).WriteTemporaryDword(0xF0, Word(state + 10)).WriteTemporaryDword(0xF4, Word(state + 12)).WriteTemporaryDword(0xF8, Word(state + 2));
            frame = services.BuildColorRamp(frame, firstColor, secondColor, colors);
            NativeBattleCinematicRibbonParent parent = services.PrepareParent(frame);
            frame = parent.Frame;
            Dictionary<uint, System.Numerics.Vector3>? pilotVertices = pilotIdentity is null ? null : [];
            uint output = ManagerDword(0x7C);
            uint current = Segment(Word(state));
            frame = frame.WriteTemporaryDword(0x6C, current);
            uint work = ManagerDword(0x74);
            for (int index = 0; index < 9; index++)
            {
                NativeBattleCinematicRibbonProjection projected = services.Project(frame, parent.Matrix, unchecked(current + (uint)(index * 8)));
                frame = projected.Frame.WriteTemporaryDword(0xFC, projected.Flags);
                if (pilotVertices is not null && projected.PilotWorld is { } world)
                {
                    pilotVertices[unchecked(work + (uint)(index * 8))] = world;
                }
                StoreDword(unchecked(work + (uint)(index * 8)), projected.Screen);
                StoreDword(unchecked(work + (uint)(index * 8 + 4)), projected.Depth >> 2);
            }
            frame = frame.WriteTemporaryDword(0x7C, 0).WriteTemporaryDword(0x60, (SlotDword(0xCC) & 0x02000000) | 0x38000000).WriteTemporaryDword(0x64, unchecked((uint)(int)SignedSlot(0x92)) | 0xE1000000).WriteTemporaryDword(0x88, Word(state + 4));
            int timer = Word(state + 14);
            if (timer >= 1)
            {
                frame = frame.WriteTemporaryDword(0x88, unchecked(frame.ReadTemporaryDword(0x88) - (uint)timer));
                colors = unchecked(colors + (uint)(timer * 8));
            }
            frame = frame.WriteTemporaryDword(0x7C, 1);
            while (unchecked((int)frame.ReadTemporaryDword(0x7C)) <= unchecked((int)frame.ReadTemporaryDword(0x88)))
            {
                int strip = unchecked((int)frame.ReadTemporaryDword(0x7C));
                uint segment = Segment(Word(state) - strip);
                frame = frame.WriteTemporaryDword(0x68, segment);
                uint destination = unchecked(work + (uint)((strip & 1) * 128));
                uint oldBank = (strip & 1) == 0 ? work : work + 128;
                uint newBank = (strip & 1) == 0 ? work + 128 : work;
                frame = frame.WriteTemporaryDword(0x70, oldBank).WriteTemporaryDword(0x74, newBank);
                for (int index = 0; index < 9; index++)
                {
                    NativeBattleCinematicRibbonProjection projected = services.Project(frame, parent.Matrix, unchecked(segment + (uint)(index * 8)));
                    frame = projected.Frame;
                    if (pilotVertices is not null && projected.PilotWorld is { } world)
                    {
                        pilotVertices[unchecked(destination + (uint)(index * 8))] = world;
                    }
                    StoreDword(unchecked(destination + (uint)(index * 8)), projected.Screen);
                    StoreWord(unchecked(destination + (uint)(index * 8 + 4)), unchecked((ushort)projected.Depth));
                }
                int[] first = [1, 2, 3, 4, 5, 6, 7, 8];
                int[] second = [3, 4, 0, 0, 7, 8, 0, 0];
                for (int face = 0; face < 8; face++)
                {
                    bool inner = (face & 2) != 0;
                    uint[] addresses = [unchecked(newBank + (uint)(first[face] * 8)), unchecked(newBank + (uint)(second[face] * 8)), unchecked(oldBank + (uint)(first[face] * 8)), unchecked(oldBank + (uint)(second[face] * 8))];
                    uint[] faceColors = inner ? [Dword(colors + 4), Dword(colors), Dword(colors + 12), Dword(colors + 8)] : [frame.ReadTemporaryDword(0x60), Dword(colors + 4), 0, Dword(colors + 12)];
                    byte[] data = new byte[44];
                    data[3] = 10;
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), frame.ReadTemporaryDword(0x64));
                    int depthSum = 0;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(12 + corner * 8), faceColors[corner]);
                        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(16 + corner * 8), Dword(addresses[corner]));
                        depthSum = unchecked(depthSum + Word(addresses[corner] + 4));
                    }
                    uint ordering = unchecked(frame.ReadTemporaryDword(0x14C) + (uint)(((depthSum / 4) >> 2) & ~3));
                    ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, 44).Select(index => index >= 3 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
                    ImmutableArray<uint> words = Enumerable.Range(0, 11).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
                    NativeBattleGeometryPrimitive primitive = new(0, words, [0, 0, 0, 0]) { PhysicalWriteMask = mask };
                    if (pilotIdentity is { } identity && pilotVertices is not null && addresses.All(pilotVertices.ContainsKey))
                    {
                        int newerSegment = checked((int)((Segment(Word(state) - strip + 1) - state - 32) / 72));
                        NativeBattlePilotVisualIdentity segmentIdentity = NativeBattlePilotVisualLifetimes.Identity(identity.Effect, frame.Arena, state + 32, newerSegment, face);
                        NativeBattlePilotVisualGeometry geometry = new(segmentIdentity, pilotVertices[addresses[0]], pilotVertices[addresses[1]], pilotVertices[addresses[2]], pilotVertices[addresses[3]], 4);
                        if (identity.Effect is 219 or 229)
                        {
                            int olderSegment = checked((int)((segment - state - 32) / 72));
                            NativeBattlePilotVisualIdentity olderIdentity = NativeBattlePilotVisualLifetimes.Identity(identity.Effect, frame.Arena, state + 32, olderSegment);
                            NativeBattlePilotVisualIdentity ownerIdentity = identity with { Primitive = unchecked(identity.Primitive * 397 + newerSegment * 8 + face), ResourceGeneration = unchecked((identity.ResourceGeneration * 397 ^ segmentIdentity.Generation) * 397 ^ olderIdentity.Generation) };
                            geometry = geometry with { Identity = ownerIdentity, UseSourceDepths = true, SourceDepths = System.Numerics.Vector4.Zero };
                        }
                        primitive = primitive with { PilotGeometry = geometry };
                    }
                    frame = services.Append(frame, output, ordering, new([new(0, data.ToImmutableArray(), mask, primitive)], 44));
                    output = unchecked(output + 44);
                }
                colors = unchecked(colors + 8);
                frame = frame.WriteTemporaryDword(0x7C, unchecked(frame.ReadTemporaryDword(0x7C) + 1));
            }
            frame = WriteCoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C), output);
        }

        void Steer(uint target, uint previous, uint current)
        {
            NativeBattleVector old = Vector(previous);
            NativeBattleVector aim = Vector(target);
            (frame, int yaw) = NativeBattleCinematicRibbonBasis.Angle(frame, old.X, old.Z, aim.X, aim.Z);
            yaw = NativeBattleCinematicRibbonBasis.Turn(SignedWord(previous + 6), yaw, SignedSlot(0x8E));
            StoreWord(current + 6, (ushort)yaw);
            (frame, int pitch) = NativeBattleCinematicRibbonBasis.Angle(frame, old.Y, old.Z, aim.Y, aim.Z);
            pitch = NativeBattleCinematicRibbonBasis.Turn(SignedWord(previous + 14), pitch, SignedSlot(0x8E));
            StoreWord(current + 14, (ushort)pitch);
            int y = NativeBattleCameraMath.Sine(pitch);
            int z = NativeBattleCameraMath.Cosine(yaw);
            int x = NativeBattleCameraMath.Sine(yaw);
            StoreTemporaryVector(0xE0, new(unchecked((short)x), unchecked((short)y), unchecked((short)z)));
            StoreTemporaryVector(0xF0, new(unchecked((short)(old.X + x)), unchecked((short)(old.Y + y)), unchecked((short)(old.Z + z))));
        }

        void CrossSection(NativeBattleActionMatrix matrix, int width, int distance, uint output, bool flip)
        {
            frame = NativeBattleCinematicRibbonWork.BuildCrossSection(frame, matrix, width, distance, output, flip);
            if (pilotIdentity is not null)
            {
                NativeBattlePilotVisualLifetimes.Allocated(frame.Arena, state + 32, checked((int)((output - state - 32) / 72)));
            }
            frame = services.PublishInput(frame, unchecked(frame.TemporaryFrame.Address + 0xF0));
        }

        uint Segment(int counter) => unchecked(state + 32 + (uint)((counter % Word(state + 2)) * 72));
        uint ManagerDword(uint offset) => CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + offset));
        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        short SignedSlot(int offset) => unchecked((short)SlotWord(frame, offset));
        byte[] Read(uint address, int length) => frame.Arena.Read(ArenaOffset(address, length), length);
        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
        short SignedWord(uint address) => unchecked((short)Word(address));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
        NativeBattleVector Vector(uint address) => new(SignedWord(address), SignedWord(address + 2), SignedWord(address + 4));
        NativeBattleVector SlotVector(int offset) => new(SignedSlot(offset), SignedSlot(offset + 2), SignedSlot(offset + 4));
        NativeBattleVector TemporaryVector(int offset) => new(unchecked((short)frame.ReadTemporaryWord(offset)), unchecked((short)frame.ReadTemporaryWord(offset + 2)), unchecked((short)frame.ReadTemporaryWord(offset + 4)));
        NativeBattleVector GlobalVector() => new(unchecked((short)CoreWord(frame, 0x27971E4)), unchecked((short)CoreWord(frame, 0x27971E6)), unchecked((short)CoreWord(frame, 0x27971E8)));
        uint PackedColor(uint address)
        {
            int value = SignedWord(address + 4);
            value |= 0x200;
            int middle = SignedWord(address + 2);
            value = unchecked(value << 8) | middle;
            int low = SignedWord(address);
            value = unchecked(value << 8) | low;
            return unchecked((uint)value);
        }
        void StoreWord(uint address, ushort value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), BitConverter.GetBytes(value)) };
        void StoreDword(uint address, uint value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), BitConverter.GetBytes(value)) };
        void StoreVector(uint address, NativeBattleVector value)
        {
            StoreWord(address, unchecked((ushort)value.X));
            StoreWord(address + 2, unchecked((ushort)value.Y));
            StoreWord(address + 4, unchecked((ushort)value.Z));
        }
        void StoreTemporaryVector(int offset, NativeBattleVector value) => frame = frame.WriteTemporaryWord(offset, unchecked((ushort)value.X)).WriteTemporaryWord(offset + 2, unchecked((ushort)value.Y)).WriteTemporaryWord(offset + 4, unchecked((ushort)value.Z));
        void SlotStoreByte(int offset, byte value) => frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, offset, 1), [value]) };
        void SlotStoreWord(int offset, ushort value) => frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, offset, 2), BitConverter.GetBytes(value)) };
        void SlotStoreDword(int offset, uint value) => frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, offset, 4), BitConverter.GetBytes(value)) };
    }
}
