using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHoly175DrawingResult(NativeBattleHoly175State State, NativeBattleGfGeometryBatch? Geometry, NativeBattleModulePacketBatch? Sprites, bool Common, short? NextOffset);
internal sealed record NativeBattleHoly175MeshDraw(NativeBattleHoly175State State, NativeBattleStaticMeshResource Resource, NativeBattleActionMatrix Matrix, NativeBattleModuleMeshContext Context);

public static partial class NativeBattleHoly175Drawing
{
    public static NativeBattleHoly175DrawingResult DrawTask(NativeBattleModulePoolTask task, NativeBattleHoly175State state, NativeBattleHoly175Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleVector cameraEye, NativeBattleSummonColors colors)
    {
        if (task.Callback == 0x5D3590)
        {
            NativeBattleActionMatrix driver = state.ReadMatrix(S(task, 0xE));
            int radius = S(task, 0x16) + S(task, 0x20);
            short angle = S(task, 0x18);
            NativeBattleVector local = new(unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 12)), 0, unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12)));
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(driver, local);
            NativeBattleVector point = new(unchecked((short)(rotated.X + driver.X)), unchecked((short)(rotated.Y + driver.Y)), unchecked((short)(rotated.Z + driver.Z)));
            short scale = S(task, 0x1C);
            NativeBattleHoly175SpriteResource resource = resources.Sprite(0xD523E8);
            NativeBattleModuleDraw draw = Sprite(resource, task.ReadByte(0xC) & 7, point, scale, -(scale >> 4), 0, 0x808080) with { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(175, task) };
            return new(state, null, NativeBattleModulePackets.EmitSprite571DC0(draw, projection with { View = view }, cameraRoll, 17, 2), false, draw.SpriteFrame!.NextOffset);
        }
        if (task.Callback == 0x5D3FB0)
        {
            return DrawRays(task, state, resources, view, projection, cameraRoll, cameraEye);
        }
        if (task.Callback == 0x5D3B80)
        {
            ImmutableArray<byte> table = resources.WindowColors?.Data ?? throw new NotSupportedException("Native Holy window drawing requires its actual color table.");
            if (table.Length != 64)
            {
                throw new InvalidDataException("Native Holy window color table requires sixteen complete DWORDs.");
            }
            uint color = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan().Slice((task.ReadWord(0x22) & 15) * 4));
            return new(state, NativeBattleHoly175WindowedMeshPackets.Draw5D3B80(task, resources.Mesh(0xD56C58), color, view, projection, colors), null, true, null);
        }
        if (task.Callback == 0x5D3E20 && S(task, 0xE) > 0)
        {
            return new(state, new([], 0), null, false, null);
        }
        NativeBattleHoly175MeshDraw mesh = DescribeStandardMesh(task, state, resources, view, projection);
        NativeBattleGfGeometryBatch packets = NativeBattleModulePackets.EmitBatch572200(mesh.Resource.Bytes, 0, default, mesh.Matrix, projection, mesh.Context, colors, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(175, task));
        if (task.Callback == 0x5D43B0)
        {
            packets = packets with
            {
                Attempts = packets.Attempts.Select(attempt =>
                {
                    if (attempt.Primitive is not { PilotGeometry: { } geometry } primitive)
                    {
                        return attempt;
                    }
                    uint command = primitive.Words[1] >> 24;
                    int stride = 1 + ((command & 4) != 0 ? 1 : 0) + ((command & 0x10) != 0 ? 1 : 0);
                    System.Numerics.Vector3 Screen(int corner)
                    {
                        uint value = primitive.Words[2 + corner * stride];
                        return new(unchecked((short)value), unchecked((short)(value >> 16)), primitive.VertexPayload4[corner]);
                    }
                    NativeBattlePilotVisualGeometry screen = new(geometry.Identity, Screen(0), Screen(1), Screen(2), geometry.CornerCount == 4 ? Screen(3) : default, geometry.CornerCount) { ScreenSpace = true, SourceDepths = new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), UseSourceDepths = true };
                    return attempt with { Primitive = primitive with { PilotGeometry = screen } };
                }).ToImmutableArray()
            };
        }
        return new(mesh.State, packets, null, false, null);
    }

    internal static NativeBattleHoly175MeshDraw DescribeStandardMesh(NativeBattleModulePoolTask task, NativeBattleHoly175State state, NativeBattleHoly175Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection)
    {
        int counter = S(task, 0xC);
        short scale = S(task, 0x1C);
        NativeBattleVector point = Position(task);
        NativeBattleVector angles = default;
        uint address;
        int? fade = null;
        byte shift = 2;
        NativeBattleActionMatrix matrix;
        switch (task.Callback)
        {
            case 0x5D3A50:
                address = 0xD54588;
                angles = new(0, S(task, 0x18), 0);
                fade = counter < 8 ? (8 - counter) << 9 : null;
                break;
            case 0x5D3CE0:
                address = 0xD53BA0;
                fade = counter >= 8 ? (counter - 8) << 9 : null;
                break;
            case 0x5D3E20:
                address = 0xD53688;
                angles = new(S(task, 0x18), S(task, 0x16), 0);
                fade = counter < 8 ? (8 - counter) << 9 : counter >= 16 ? unchecked(409 * (counter - 16)) : null;
                break;
            case 0x5D42B0:
                NativeBattleModuleDraw billboard = new(NativeBattleModuleDrawKind.Sprite, 0) { Anchor = new(point.X, point.Y, point.Z, 0), Scale = scale, DepthOffset = 0, SpriteFlags = 0 };
                matrix = NativeBattlePoseMatrices.ModuleSpriteCamera(billboard, view, 0);
                NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Multiply56C090(matrix, NativeBattleGfMatrices.Euler56CE30(new(0, 0, S(task, 0x18))));
                matrix = matrix with { Rotation = rotation.Rotation };
                return new(state, resources.Mesh(0xD52DD8), matrix, new(0x33, 0, null, null, null, null, 17, 13));
            case 0x5D43B0:
                (state, matrix) = ScreenAnchor5D41F0(state, point, scale, view, projection);
                NativeBattleEffectArena retained = WriteWord(state.StaticMatrix, 0, scale);
                retained = WriteWord(retained, 8, 4096);
                state = state with { StaticMatrix = retained };
                matrix = NativeBattleHoly175State.LoadMatrix(retained, 0);
                fade = counter < 4 ? (4 - counter) << 10 : counter >= 18 ? unchecked(682 * (counter - 18)) : null;
                return new(state, resources.Mesh(0xD527F8), matrix, new(fade is null ? 0x33u : 0xF3u, 0, fade, null, null, null, 17, 14));
            default:
                throw new NotSupportedException("Native Holy drawing requires its actual reached callback.");
        }
        matrix = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(angles), scale, scale, scale) with { X = point.X, Y = point.Y, Z = point.Z };
        matrix = NativeBattlePoseMatrices.Compose56C2F0(view, matrix);
        return new(state, resources.Mesh(address), matrix, new(fade is null ? 0x33u : 0xF3u, 0, fade, null, null, null, 17, shift));
    }

    internal static (NativeBattleHoly175State State, NativeBattleActionMatrix Matrix) ScreenAnchor5D41F0(NativeBattleHoly175State state, NativeBattleVector point, short scale, NativeBattleActionMatrix view, NativeBattleCameraProjection projection)
    {
        NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(view, point, projection);
        int x = unchecked((short)projected.Screen) / 8 - 160;
        int y = unchecked((short)(projected.Screen >> 16)) / 8 - 108;
        short focal = unchecked((short)(projection.FocalLength ?? throw new NotSupportedException("Native Holy screen anchor requires its actual focal WORD.")));
        NativeBattleEffectArena retained = WriteWord(WriteWord(WriteWord(state.StaticMatrix, 0, scale), 8, scale), 16, scale);
        byte[] translation = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(translation, x);
        BinaryPrimitives.WriteInt32LittleEndian(translation.AsSpan(4), y);
        BinaryPrimitives.WriteInt32LittleEndian(translation.AsSpan(8), focal);
        retained = retained.Write(20, translation);
        state = state with { StaticMatrix = retained };
        return (state, NativeBattleHoly175State.LoadMatrix(retained, 0));
    }

    internal static NativeBattleModulePacketBatch EmitLoadedSprite(NativeBattleHoly175SpriteResource resource, int frame, NativeBattleActionMatrix loadedMatrix, NativeBattleCameraProjection projection, short cameraRoll, ushort flags, uint color, int bucket = 17, byte shift = 2, NativeBattlePilotVisualIdentity? pilotIdentity = null, bool pilotScreenSpace = true)
    {
        NativeBattleModuleDraw draw = Sprite(resource, frame, default, 4096, 0, flags, color) with { SpriteWorldRotation = loadedMatrix.Rotation, PilotIdentity = pilotIdentity };
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], loadedMatrix.X, loadedMatrix.Y, loadedMatrix.Z, 4096);
        NativeBattleModulePacketBatch packets = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, cameraRoll, bucket, shift, NativeBattlePoseMatrices.ModuleSpriteCamera(draw, camera, cameraRoll));
        if (!pilotScreenSpace)
        {
            return packets;
        }
        return packets with
        {
            Primitives = packets.Primitives.Select(item =>
            {
                if (item.Primitive.PilotGeometry is not { } geometry)
                {
                    return item;
                }
                System.Numerics.Vector3 Screen(int corner)
                {
                    uint value = item.Primitive.Words[2 + corner * 2];
                    return new(unchecked((short)value), unchecked((short)(value >> 16)), item.Primitive.VertexPayload4[corner]);
                }
                NativeBattlePilotVisualGeometry screen = new(geometry.Identity, Screen(0), Screen(1), Screen(2), Screen(3), 4) { ScreenSpace = true };
                return item with { Primitive = item.Primitive with { PilotGeometry = screen } };
            }).ToImmutableArray()
        };
    }

    private static NativeBattleHoly175DrawingResult DrawRays(NativeBattleModulePoolTask task, NativeBattleHoly175State state, NativeBattleHoly175Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short roll, NativeBattleVector cameraEye)
    {
        NativeBattleVector anchor = Position(task);
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(cameraEye.X - anchor.X, cameraEye.Y - anchor.Y, cameraEye.Z - anchor.Z);
        NativeBattleModuleAxisAngle aligned = NativeBattleModuleAlignment.Between571480(0, 0, -4096, direction.X, direction.Y, direction.Z);
        NativeBattleActionMatrix look = new(NativeBattleModuleAlignment.Rotation5714F0(aligned.Angle, aligned.Axis), 0, 0, 0, 4096);
        int counter = S(task, 0xC);
        var ray = NativeBattleActionGeometryRules.NormalizeVector56BC50(unchecked(-512 - 300 * counter), unchecked(-256 - 30 * counter), unchecked(-3584 + 25 * counter));
        ushort flags = 0;
        uint color = 0x808080;
        if (counter < 2 || counter >= 16)
        {
            flags = 4;
            int brightness = counter < 2 ? counter << 6 : unchecked((24 - counter) << 4);
            color = unchecked((uint)((brightness << 16) | (brightness << 8)));
        }
        ImmutableArray<byte> rows = resources.RayRows?.Data ?? throw new NotSupportedException("Native Holy rays require their eight original records.");
        if (rows.Length != 64)
        {
            throw new InvalidDataException("Native Holy rays require eight complete original records.");
        }
        ImmutableArray<NativeBattleAllocatedPrimitive>.Builder primitives = ImmutableArray.CreateBuilder<NativeBattleAllocatedPrimitive>();
        int allocated = 0;
        for (int index = 0; index < 8; index++)
        {
            int offset = index * 8;
            int frame = BinaryPrimitives.ReadUInt16LittleEndian(rows.AsSpan().Slice(offset));
            short scale = BinaryPrimitives.ReadInt16LittleEndian(rows.AsSpan().Slice(offset + 2));
            short factor = BinaryPrimitives.ReadInt16LittleEndian(rows.AsSpan().Slice(offset + 4));
            int t = unchecked(factor * 2000) >> 12;
            NativeBattleVector relative = new(unchecked((short)(unchecked(ray.X * t) >> 12)), unchecked((short)(unchecked(ray.Y * t) >> 12)), unchecked((short)(unchecked(ray.Z * t) >> 12)));
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(look, relative);
            NativeBattleVector point = new(unchecked((short)(anchor.X + rotated.X)), unchecked((short)(anchor.Y + rotated.Y)), unchecked((short)(anchor.Z + rotated.Z)));
            (NativeBattleHoly175State nextState, NativeBattleActionMatrix matrix) = ScreenAnchor5D41F0(state, point, scale, view, projection);
            state = nextState;
            NativeBattleModulePacketBatch batch = EmitLoadedSprite(resources.Sprite(0xD522CC), frame, matrix, projection, roll, flags, color, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(175, task, index));
            primitives.AddRange(batch.Primitives.Select(primitive => primitive with { ByteOffset = checked(primitive.ByteOffset + allocated) }));
            allocated = checked(allocated + batch.AllocationBytes);
        }
        return new(state, null, new(primitives.ToImmutable(), allocated), false, null);
    }

    private static NativeBattleModuleDraw Sprite(NativeBattleHoly175SpriteResource resource, int frame, NativeBattleVector point, short scale, int depthOffset, ushort flags, uint color)
    {
        NativeBattleModuleResource module = new(0, resource.Address, 0, 0, resource.Bytes);
        return new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = module, SpriteFrame = module.ProjectedFrame571C80(frame), Phase = unchecked((ushort)frame), Anchor = new(point.X, point.Y, point.Z, 0), Scale = scale, DepthOffset = depthOffset, SpriteFlags = flags, SpriteColor = color };
    }

    private static NativeBattleVector Position(NativeBattleModulePoolTask task) => new(S(task, 0x10), S(task, 0x12), S(task, 0x14));
    private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleEffectArena WriteWord(NativeBattleEffectArena owner, int offset, short value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
        return owner.Write(offset, bytes);
    }
}
