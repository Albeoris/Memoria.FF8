using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleEffectArena Module) DrawNativeWendigoBall(NativeBattleGraphicsState graphics, int index, NativeBattleEffectArena module, uint moduleBase, uint ballAddress, bool shoot, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        int ball = Relative(ballAddress);
        if (ReadShort(ball + (shoot ? 0xD0 : 0xB0)) != index)
        {
            throw new InvalidDataException("Native ball renderer disagrees with its physical actor slot.");
        }
        NativeBattleGraphicsModel actor = graphics.Models[index];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new NotSupportedException("Native ball renderer requires its original body rig.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native ball renderer requires its published camera.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native ball renderer requires its published view matrix.");
        NativeBattleActionMatrix world = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native ball renderer requires its actual model matrix.");
        WriteMatrix(ball + 0x20, world, true);
        NativeBattleActionMatrix camera = NativeBattleWendigoBallGeometry.Camera(view, world);
        WriteMatrix(ball + 0x40, camera, false);
        NativeBattleActionMatrix rotation = NativeBattleWendigoBallGeometry.Rotation(ReadMatrix(ball), new(ReadShort(ball + 0x9C), ReadShort(ball + 0x9E), ReadShort(ball + 0xA0)));
        NativeBattleVector pivot = Vector(ball + 0x74);
        int morph = ball + (shoot ? 0xC8 : 0xA8);
        short weight = ReadShort(morph);
        NativeBattleVector radii = Vector(morph + 2);
        if (ReadUInt(ball + 0x6C) != 0x1D8E054)
        {
            throw new NotSupportedException("Native ball renderer requires its actual FRAME cursor pointer.");
        }
        int scratch = Relative(ReadUInt(Relative(ReadUInt(ball + 0x64)) + 4));
        NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native ball renderer requires its current ordering table.");
        if ((actor.Flags0 & 0x20) == 0)
        {
            NativeBattleGraphicsDefinition graphicsDefinition = definition.NativeGraphics!;
            bool gouraud = graphicsDefinition.PrimitiveCapabilities!.GouraudAlpha;
            NativeBattleEffectLocalBounds bounds = actor.GeometryBounds52 ?? throw new NotSupportedException("Native ball shadow requires its original model bounds.");
            NativeBattleShadowGeometry shape = NativeBattlePoseMatrices.ShadowGeometry5088A0(actor, bounds, view, graphicsDefinition.Rules.ShadowSource!, gouraud);
            NativeBattleModuleProjectedVertex[] vertices = shape.Vertices.Select(vertex => NativeBattleModuleProjection.Project45FE10(shape.CameraMatrix, vertex, projection)).ToArray();
            Append(NativeBattleShadowPackets.EmitBatch5088A0(vertices, actor.Color44!.Value, gouraud, 4112, 16, pilotWorldVertices: pilotIdentity.HasValue ? shape.Vertices.Select(vertex => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(shape.CameraMatrix, vertex), projection)).ToArray() : null, pilotIdentity: pilotIdentity is { } identity ? identity with { Primitive = 0x5088A0 } : null));
        }
        actor = NativeBattleBodyBoneMatrices.Publish508C90(actor, pose);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(actor, pose);
        Draw(DiablosActorGeometry(resource, actor), bones, false, true);
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native ball renderer requires its actual linked geometry presence.");
        if (!linked.IsEmpty && resource.LinkedGeometryUsesBodySkeleton != true)
        {
            NativeBattlePoseSource linkedPose = resource.LinkedPoseSource ?? throw new NotSupportedException("Native ball renderer requires its linked original rig.");
            NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadLinkedDecodedPose(actor.SequenceState!, linkedPose);
            Draw(linked, NativeBattlePoseMatrices.StageBoneMatrices(linkedPose, decoded), true, resource.GeometryRelocated507550);
        }
        actor = NativeBattleBodyBoneMatrices.Publish508C90(actor, pose);
        return (graphics with { Models = graphics.Models.SetItem(index, actor), PrimitiveFrame = frame }, module);

        void Draw(ImmutableArray<NativeBattleRawMesh> meshes, ImmutableArray<NativeBattleActionMatrix> matrices, bool linkedGeometry, bool alreadyRelocated)
        {
            uint mask = actor.SequenceState!.MeshMask124;
            for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
            {
                if ((mask & (1u << (meshIndex & 31))) == 0)
                {
                    continue;
                }
                NativeBattleRawMesh mesh = meshes[meshIndex];
                ImmutableArray<NativeBattleModuleProjectedVertex>.Builder projected = ImmutableArray.CreateBuilder<NativeBattleModuleProjectedVertex>(mesh.Vertices.Length);
                System.Numerics.Vector3[]? worldVertices = pilotIdentity.HasValue ? new System.Numerics.Vector3[mesh.Vertices.Length] : null;
                for (int vertex = 0; vertex < mesh.Vertices.Length; vertex++)
                {
                    NativeBattleRawVertex raw = mesh.Vertices[vertex];
                    NativeBattleVector point = NativeBattleWendigoBallGeometry.Morph(raw, matrices[raw.Bone], rotation, pivot, weight, radii);
                    if (worldVertices is not null)
                    {
                        worldVertices[vertex] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(camera, point), projection);
                    }
                    int offset = checked(scratch + vertex * 20);
                    WriteVector(offset, point);
                    // 45E3E0 writes XYZ only; the copied heap WORD at +6 has no initialized producer.
                    module = module.MarkUnknown(offset + 6, 2);
                    NativeBattleModuleProjectedVertex screen = NativeBattleModuleProjection.Project45FE10(camera, point, projection);
                    WriteUInt(offset + 8, screen.Screen);
                    WriteUInt(offset + 12, screen.Depth);
                    projected.Add(screen);
                }
                NativeBattleModelTextureBinding texture = linkedGeometry || resource.LinkedGeometryUsesBodySkeleton == true && meshIndex == 1 ? actor.LinkedTextureSlots! : actor.BodyTextureSlots!;
                ImmutableArray<NativeBattleRawPolygon> polygons = alreadyRelocated ? mesh.Polygons : mesh.Polygons.Select(polygon => polygon with { Bytes = NativeBattleRawGeometryPackets.Relocate507550(polygon, texture) }).ToImmutableArray();
                Append(NativeBattleWendigoBallGeometry.Packets(polygons, projected.ToImmutable(), actor.Color40!.Value, worldVertices, pilotIdentity is { } identity ? identity with { Primitive = (linkedGeometry ? 0x10000 : 0) + meshIndex } : null));
            }
        }

        void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native ball renderer requires its retained FRAME owner.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(memory.PrimitiveProcess!, frame.Pending, batch);
            NativeEffectMemory = memory with { PrimitiveProcess = written.Process };
            frame = frame with { Pending = written.Ordering };
        }

        int Relative(uint address)
        {
            if (address < moduleBase || address - moduleBase >= NativeBattleEffectArena.Length)
            {
                throw new InvalidDataException("Native ball pointer leaves its retained module bank.");
            }
            return checked((int)(address - moduleBase));
        }

        short ReadShort(int offset) => BinaryPrimitives.ReadInt16LittleEndian(module.Read(offset, 2));
        uint ReadUInt(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(module.Read(offset, 4));
        NativeBattleVector Vector(int offset) => new(ReadShort(offset), ReadShort(offset + 2), ReadShort(offset + 4));
        NativeBattleActionMatrix ReadMatrix(int offset) => new(Enumerable.Range(0, 9).Select(lane => ReadShort(offset + lane * 2)).ToImmutableArray(), 0, 0, 0, 4096);

        void WriteVector(int offset, NativeBattleVector vector)
        {
            byte[] data = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(data, vector.X);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(2), vector.Y);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(4), vector.Z);
            module = module.Write(offset, data);
        }

        void WriteUInt(int offset, uint value)
        {
            byte[] data = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(data, value);
            module = module.Write(offset, data);
        }

        void WriteMatrix(int offset, NativeBattleActionMatrix matrix, bool copiedPadding)
        {
            byte[] data = new byte[18];
            for (int lane = 0; lane < 9; lane++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(lane * 2), matrix.Rotation[lane]);
            }
            module = module.Write(offset, data);
            if (copiedPadding)
            {
                module = module.MarkUnknown(offset + 18, 2);
            }
            WriteUInt(offset + 20, unchecked((uint)matrix.X));
            WriteUInt(offset + 24, unchecked((uint)matrix.Y));
            WriteUInt(offset + 28, unchecked((uint)matrix.Z));
        }
    }
}
