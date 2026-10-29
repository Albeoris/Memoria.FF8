using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanOpeningInputs(NativeBattleActionPacketsState Packets, Func<uint, ImmutableArray<byte>> ReadLineRecords, Func<uint, NativeBattleModuleResource> ReadSprite, NativeBattleCameraProjection Projection, short CameraRoll, Func<NativeBattleScanReticleFrame, NativeBattleModulePacketBatch, NativeBattleScanReticleFrame> Submit);

public static class NativeBattleScanOpeningChildren
{
    public static NativeBattleScanReticleStep Advance84DDD0(NativeBattleScanReticleFrame frame, uint address, NativeBattleScanOpeningInputs inputs) => Advance(frame, address, inputs, 0x84DDD0);
    public static NativeBattleScanReticleStep Advance84E010(NativeBattleScanReticleFrame frame, uint address, NativeBattleScanOpeningInputs inputs) => Advance(frame, address, inputs, 0x84E010);
    public static NativeBattleScanReticleStep Advance84E3D0(NativeBattleScanReticleFrame frame, uint address, NativeBattleScanOpeningInputs inputs) => Advance(frame, address, inputs, 0x84E3D0);
    public static NativeBattleScanReticleStep Advance84E510(NativeBattleScanReticleFrame frame, uint address, NativeBattleScanOpeningInputs inputs) => Advance(frame, address, inputs, 0x84E510);

    public static NativeBattleModulePacketBatch Lines84DF00(NativeBattleModulePoolTask task, ImmutableArray<byte> records)
    {
        if (records.IsDefault)
        {
            throw new NotSupportedException("Native Scan corner drawing requires its actual line records.");
        }
        ImmutableArray<NativeBattleAllocatedPrimitive>.Builder packets = ImmutableArray.CreateBuilder<NativeBattleAllocatedPrimitive>();
        for (int offset = 0; offset <= records.Length - 16; offset += 16)
        {
            ReadOnlySpan<byte> record = records.AsSpan().Slice(offset, 16);
            if (BinaryPrimitives.ReadUInt16LittleEndian(record) == 0xFF)
            {
                return new(packets.ToImmutable(), packets.Count * 16);
            }
            short scale = unchecked((short)task.ReadWord(0x64));
            int x = unchecked((short)task.ReadWord(0x1C));
            int y = unchecked((short)task.ReadWord(0x1E));
            uint first = Pack(x + unchecked(BinaryPrimitives.ReadInt16LittleEndian(record[4..]) * scale) / 4096 + 160, y + unchecked(BinaryPrimitives.ReadInt16LittleEndian(record[6..]) * scale) / 4096 + 108);
            uint second = Pack(x + unchecked(BinaryPrimitives.ReadInt16LittleEndian(record[8..]) * scale) / 4096 + 160, y + unchecked(BinaryPrimitives.ReadInt16LittleEndian(record[10..]) * scale) / 4096 + 108);
            uint color = 0x40000000u | record[12] | (uint)record[13] << 8 | (uint)record[14] << 16;
            NativeBattleGeometryPrimitive primitive = new(5, [0x03000000, color, first, second], [0, 0, 0, 0]);
            primitive = primitive with { PilotGeometry = new(NativeBattlePilotVisualLifetimes.Identity(40, task, offset / 16), new(unchecked((short)first), unchecked((short)(first >> 16)), 0), new(unchecked((short)second), unchecked((short)(second >> 16)), 0), default, default, 2) { ScreenSpace = true } };
            packets.Add(new(primitive, packets.Count * 16, 0));
        }
        throw new NotSupportedException("Native Scan line records have no reached original sentinel.");
    }

    public static NativeBattleModulePacketBatch Sprite84E580(NativeBattleModulePoolTask task, NativeBattleModuleResource resource, NativeBattleCameraProjection projection, short cameraRoll, short scale)
    {
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return new([], 0);
        }
        NativeBattleActionMatrix direct = new([scale, 0, 0, 0, scale, 0, 0, 0, scale], unchecked((short)task.ReadWord(0x1C)), unchecked((short)task.ReadWord(0x1E)), unchecked((short)task.ReadWord(0x20)), 4096);
        NativeBattleActionMatrix origin = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], direct.X, direct.Y, direct.Z, 4096);
        int frame = unchecked((short)task.ReadWord(0x50));
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(frame), Phase = unchecked((ushort)frame), Anchor = new(0, 0, 0, 0), SpriteFlags = 0, SpriteWorldRotation = direct.Rotation, PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(40, task, unchecked((int)task.ReadDword(0x4C))) };
        NativeBattleActionMatrix camera = NativeBattlePoseMatrices.ModuleSpriteCamera(draw, origin, cameraRoll);
        return NativeBattleModulePackets.EmitSprite571DC0(draw, projection, cameraRoll, 17, 2, currentMatrix: camera);
    }

    private static NativeBattleScanReticleStep Advance(NativeBattleScanReticleFrame frame, uint address, NativeBattleScanOpeningInputs inputs, uint callback)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(inputs.ReadLineRecords);
        ArgumentNullException.ThrowIfNull(inputs.ReadSprite);
        ArgumentNullException.ThrowIfNull(inputs.Submit);
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != callback)
        {
            throw new InvalidDataException("Native Scan opening child requires its actual original callback.");
        }
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        if (callback == 0x84DDD0)
        {
            switch (phase)
            {
                case 0:
                    int corner = unchecked((short)task.ReadWord(0x74));
                    if ((uint)corner <= 3)
                    {
                        task = task.WriteDword(0x50, 0x15D1718u + (uint)(0x30 * corner)).WriteWord(0x1C, unchecked((ushort)((corner & 1) == 0 ? -144 : 144))).WriteWord(0x1E, unchecked((ushort)(corner < 2 ? -96 : 96)));
                    }
                    task = task.WriteWord(0x64, 0x1000);
                    Next();
                    break;
                case 1:
                    DrawLines();
                    CompleteWhenInactive();
                    break;
                case 2:
                    break;
                default:
                    throw new InvalidDataException("Native Scan corner phase exceeds its original callbacks.");
            }
        }
        else if (callback == 0x84E010)
        {
            switch (phase)
            {
                case 0:
                    CopyParentPosition();
                    int corner = unchecked((short)task.ReadWord(0x74));
                    if ((uint)corner <= 3)
                    {
                        task = task.WriteDword(0x50, 0x15D17D8u + (uint)(0x30 * corner)).WriteWord(0x1C, 0).WriteWord(0x1E, 0).WriteWord(0x48, unchecked((ushort)((corner & 1) == 0 ? -1 : 1))).WriteWord(0x4A, unchecked((ushort)(corner < 2 ? -1 : 1)));
                    }
                    task = task.WriteWord(0x64, 0x1000).WriteWord(0x66, 30);
                    Next();
                    break;
                case 1:
                    CopyParentPosition();
                    DrawLines();
                    task = task.WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 1)));
                    if (unchecked((short)task.ReadWord(0x66)) < 0)
                    {
                        task = task.WriteWord(0x66, 4);
                        Next();
                    }
                    break;
                case 2:
                    task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + task.ReadWord(0x48)))).WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) + task.ReadWord(0x4A)))).WriteWord(0x64, unchecked((ushort)(task.ReadWord(0x64) + 0x400))).WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 1)));
                    if (unchecked((short)task.ReadWord(0x66)) < 0)
                    {
                        Next();
                    }
                    DrawLines();
                    break;
                case 3:
                    DrawLines();
                    CompleteWhenInactive();
                    break;
                case 4:
                    break;
                default:
                    throw new InvalidDataException("Native Scan bracket phase exceeds its original callbacks.");
            }
        }
        else if (callback == 0x84E3D0)
        {
            switch (phase)
            {
                case 0:
                    task = task.WriteWord(0x66, 0);
                    Next();
                    break;
                case 1:
                    task = task.WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) + 4)));
                    if (unchecked((short)task.ReadWord(0x66)) >= 24)
                    {
                        task = task.WriteWord(0x66, 24);
                        Next();
                    }
                    DrawRing(false, false);
                    break;
                case 2:
                    DrawRing(true, false);
                    NativeBattleEffectParameters parameters = frame.Scan.Effect.Parameters ?? throw new InvalidDataException("Native Scan ring requires its actual action parameters.");
                    hierarchy = hierarchy.Write(address, task);
                    hierarchy = hierarchy.Append8DC540(3, 0x84E510, 0x58, address, (pointer, group, actor) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, actor)).Hierarchy;
                    task = hierarchy.Read(address);
                    Next();
                    break;
                case 3:
                    DrawRing(true, true);
                    CompleteWhenInactive();
                    break;
                case 4:
                    break;
                default:
                    throw new InvalidDataException("Native Scan ring phase exceeds its original callbacks.");
            }
        }
        else
        {
            switch (phase)
            {
                case 0:
                    task = task.WriteWord(0x1C, 0x190).WriteWord(0x1E, 0xC0).WriteWord(0x20, 0x900).WriteDword(0x4C, 0x15D07D4).WriteWord(0x52, 0x1F);
                    Next();
                    break;
                case 1:
                    task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
                    if (unchecked((short)task.ReadWord(0x50)) > unchecked((short)task.ReadWord(0x52)))
                    {
                        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4)).WriteWord(0x50, task.ReadWord(0x52));
                        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & ~4)).WriteDword(0x4C, 0x15D0AF8).WriteWord(0x50, 0).WriteWord(0x52, 3);
                        Next();
                    }
                    break;
                case 2:
                    task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
                    if (unchecked((short)task.ReadWord(0x50)) > unchecked((short)task.ReadWord(0x52)))
                    {
                        task = task.WriteWord(0x50, 0);
                    }
                    CompleteWhenInactive();
                    break;
                case 3:
                    break;
                default:
                    throw new InvalidDataException("Native Scan sprite phase exceeds its original callbacks.");
            }
            if ((task.ReadByte(0x26) & 4) == 0)
            {
                Submit(Sprite84E580(task, inputs.ReadSprite(task.ReadDword(0x4C)), inputs.Projection, inputs.CameraRoll, 0x400));
            }
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        Publish();
        return new(frame, complete ? 2 : 0);

        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        void CompleteWhenInactive()
        {
            if (frame.Highlight269AA90 == 0)
            {
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                Next();
            }
        }
        void CopyParentPosition()
        {
            NativeBattleModulePoolTask parent = hierarchy.Read(task.ReadDword(0x18));
            task = task.WriteDword(0x1C, parent.ReadDword(0x1C)).WriteDword(0x20, parent.ReadDword(0x20));
        }
        void DrawLines() => Submit(Lines84DF00(task, inputs.ReadLineRecords(task.ReadDword(0x50))));
        void DrawRing(bool diagonal, bool horizontal)
        {
            NativeBattleScanReticleRing ring = NativeBattleScanReticleLines.Ring84D6D0(unchecked((short)task.ReadWord(0x1C)), unchecked((short)task.ReadWord(0x1E)), 48, 0x600, unchecked((short)task.ReadWord(0x66)), NativeBattlePilotVisualLifetimes.Identity(40, task));
            Submit(ring.Packets);
            if (diagonal)
            {
                NativeBattleScanReticlePoint start = ring.FinalPoint ?? throw new NotSupportedException("Native Scan ring diagonal requires its actual final ring endpoint.");
                NativeBattleScanReticlePoint end = new(unchecked((short)(start.X + 24)), unchecked((short)(start.Y + 24)));
                Submit(new([new(NativeBattleScanReticleLines.Line84E290(start, end, NativeBattlePilotVisualLifetimes.Identity(40, task, 100)), 0, 1)], 16));
                if (horizontal)
                {
                    Submit(new([new(NativeBattleScanReticleLines.Line84E290(end, new(unchecked((short)(end.X + 88)), end.Y), NativeBattlePilotVisualLifetimes.Identity(40, task, 101)), 0, 1)], 16));
                }
            }
        }
        void Publish() => frame = frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools } } };
        void Submit(NativeBattleModulePacketBatch packets)
        {
            hierarchy = hierarchy.Write(address, task);
            Publish();
            frame = inputs.Submit(frame, packets);
            hierarchy = new(frame.Scan.Effect.Pools);
            task = hierarchy.Read(address);
        }
    }

    private static uint Pack(int x, int y) => (uint)(ushort)x | (uint)(ushort)y << 16;
}
