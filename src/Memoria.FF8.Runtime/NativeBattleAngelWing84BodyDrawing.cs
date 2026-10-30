using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAngelWing84BodyDrawStorage([property: JsonRequired] NativeBattleEffectArena Context, [property: JsonRequired] NativeBattleEffectArena Parameters, [property: JsonRequired] NativeBattleEffectArena Vertices, [property: JsonRequired] ImmutableArray<byte> OrdinaryProjected, [property: JsonRequired] short CenterY)
{
    public static NativeBattleAngelWing84BodyDrawStorage FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), new byte[2560].ToImmutableArray(), 0);
}

public sealed record NativeBattleAngelWing84BodyDrawStep(NativeBattleAngelWing84Frame Frame, NativeBattleLogicalOrdering Ordering);

public static partial class NativeBattleAngelWing84Body
{
    public static NativeBattleAngelWing84Frame InitializeShader(NativeBattleAngelWing84Frame frame, uint address)
    {
        NativeBattleAngelWing84BodyDrawStorage storage = frame.State.BodyDrawStorage ?? throw new NotSupportedException("Native796CA0 requires its retained module drawing storage.");
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear();
        context = Dword(context, 64, 0x25C3948);
        context = Dword(context, 68, 0x25C9F40);
        context = Dword(context, 72, checked(address + 48));
        context = Dword(context, 76, 0x25C2354);
        context = Dword(context, 80, 0x25C2354);
        NativeBattleEffectArena parameters = Dword(storage.Parameters, 4, 0x25C3948);
        parameters = Word(parameters, 20, 0);
        parameters = Word(parameters, 22, 0);
        parameters = Word(parameters, 24, 320);
        parameters = Word(parameters, 26, 0);
        parameters = parameters.Write(28, new byte[] { 128, 128, 128 });
        parameters = Dword(parameters, 32, uint.MaxValue);
        parameters = Word(parameters, 36, 0);
        return frame with { State = frame.State with { BodyShaderAngle = 0, BodyDrawStorage = storage with { Context = context, Parameters = parameters, CenterY = 0 } } };
    }

    public static NativeBattleAngelWing84BodyDrawStep Draw(NativeBattleAngelWing84Frame frame, uint address, NativeBattleAngelWing84BodyResources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleShadowSource shadow, NativeBattlePrimitiveCapabilities capabilities, NativeBattleSummonColors colors, NativeBattleLogicalOrdering ordering, bool capturePresentation = false)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        NativeBattleEffectOwnedModelState model = frame.State.BodyModel ?? throw new NotSupportedException("Native796390 requires its actual owned body.");
        NativeBattleAngelWing84BodyDrawStorage storage = frame.State.BodyDrawStorage ?? throw new NotSupportedException("Native796390 requires its retained drawing storage.");
        NativeBattleEffectOwnedModel.Validate(task, model, resources.Model);
        if ((task.ReadByte(38) & 4) != 0)
        {
            return new(frame, ordering);
        }
        if ((task.ReadByte(38) & 8) == 0)
        {
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native796440 requires its retained renderer process.");
            var gate = process.Enter5099D0(capabilities.HardwareEnabled ?? throw new NotSupportedException("Native796440 requires its current hardware capability."));
            int start = frame.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native796440 requires its current private packet cursor.");
            NativeBattleEffectOwnedModelDraw ordinary = NativeBattleEffectOwnedModelDrawing.Draw7558A0(task, model, resources.Model, view, projection, shadow, capabilities, colors, storage.OrdinaryProjected, () => gate.Draw, capturePresentation ? NativeBattlePilotVisualLifetimes.Identity(84, task) with { ResourceGeneration = resources.Model.Address } : null);
            process = gate.Process;
            if (gate.Draw)
            {
                process = process.PublishModelColor(task.ReadDword(88)).Publish50FDF0(checked(0x20DFAB8u + (uint)(start + ordinary.BodyAllocationBytes)), null);
            }
            frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process } };
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, ordinary.Task).Pools }, State = frame.State with { BodyDrawStorage = storage with { OrdinaryProjected = ordinary.ProjectedScratch }, BodyBones = NativeBattlePoseMatrices.StageBoneMatrices(resources.Model.PoseSource, model.DecodedPose!) } };
            Append(ordinary.Packets);
            return new(frame, ordering);
        }
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CE30(new(Signed(60), Signed(62), Signed(64)));
        world = NativeBattlePoseMatrices.Scale56BEF0(world, unchecked((int)task.ReadDword(276)), unchecked((int)task.ReadDword(280)), unchecked((int)task.ReadDword(284))) with { X = Signed(76), Y = Signed(78), Z = Signed(80) };
        for (int lane = 0; lane < 9; lane++)
        {
            task = task.WriteWord(112 + lane * 2, unchecked((ushort)world.Rotation[lane]));
        }
        task = task.WriteDword(132, unchecked((uint)world.X)).WriteDword(136, unchecked((uint)world.Y)).WriteDword(140, unchecked((uint)world.Z));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, task).Pools }, State = frame.State with { Scene = NativeBattleAngelWing84Scene.Read(frame).WriteDword(24, address) } };
        short centerY = ReadBodyBone(frame, resources, 241, world).Y;
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.StageBoneMatrices(resources.Model.PoseSource, model.DecodedPose ?? throw new NotSupportedException("Native796620 requires its current decoded body pose."));
        NativeBattleActionMatrix modelView = ShaderModelView(view, world);
        NativeBattleEffectLocalBounds bounds = new(new(Signed(100), Signed(102), Signed(104)), new(Signed(106), Signed(108), Signed(110)));
        NativeBattleShadowGeometry shape = NativeBattlePoseMatrices.ShadowGeometry5088A0(world, Signed(84), bounds, view, shadow, capabilities.GouraudAlpha);
        NativeBattleModuleProjectedVertex[] shadowVertices = shape.Vertices.Select(vertex => NativeBattleModuleProjection.Project45FE10(shape.CameraMatrix, vertex, projection)).ToArray();
        Append(NativeBattleShadowPackets.EmitBatch5088A0(shadowVertices, task.ReadDword(92), capabilities.GouraudAlpha, 4112, 16, capturePresentation ? shape.Vertices.Select(vertex => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(shape.CameraMatrix, vertex), projection)).ToArray() : null, capturePresentation ? NativeBattlePilotVisualLifetimes.Identity(84, task, int.MinValue) with { ResourceGeneration = resources.Model.Address } : null));
        NativeBattleAngelWing84ShaderDraw shader = NativeBattleAngelWing84ShaderMesh.Draw(resources.Model.Geometry, bones, modelView, projection, task.ReadDword(172), unchecked((short)(frame.State.BodyShaderAngle ?? throw new NotSupportedException("Native796720 requires its current shader radius."))), centerY, storage.Vertices, capturePresentation ? NativeBattlePilotVisualLifetimes.Identity(84, task, 1) with { ResourceGeneration = resources.Model.Address } : null);
        NativeBattleEffectArena context = StoreMatrix(storage.Context, 0, world);
        context = StoreMatrix(context, 32, modelView);
        frame = frame with { State = frame.State with { BodyBones = bones, BodyDrawStorage = storage with { Context = context, Vertices = shader.Vertices, CenterY = centerY } } };
        Append(shader.Packets);
        return new(frame, ordering);

        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
        void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, frame.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native796390 requires its current private draw cursor."), ordering, NativeBattleRetainedPolygonDepths.Capture(batch));
            frame = frame with { Memory = written.Memory, State = frame.State with { PrimaryDrawCursor = written.Cursor } };
            ordering = written.Ordering;
        }
    }

    public static NativeBattleVector ReadBodyBone(NativeBattleAngelWing84Frame frame, NativeBattleAngelWing84BodyResources resources, byte selector, NativeBattleActionMatrix? actualWorld = null)
    {
        uint address = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleAngelWing84Scene.Read(frame).Settings.Read(24, 4));
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        ImmutableArray<NativeBattleActionMatrix> bones = frame.State.BodyBones ?? throw new NotSupportedException("Native502170 requires its retained effect-owned body matrices.");
        int bone = selector >= 240 ? resources.Model.PoseSource.Skeleton[selector - 236] : selector;
        int fraction = (bone & 128) != 0 ? 4096 : 0;
        bone &= 127;
        if (bone >= bones.Length)
        {
            throw new InvalidDataException("Native Angel Wing body selector leaves its original rig.");
        }
        NativeBattleActionMatrix world = actualWorld ?? ReadMatrix(task);
        NativeBattleActionMatrix selected = NativeBattlePoseMatrices.Compose56C2F0(world, bones[bone]);
        short parent = BinaryPrimitives.ReadInt16LittleEndian(resources.Model.PoseSource.Skeleton.AsSpan().Slice(16 + bone * 48));
        int distance = parent < 0 ? 0 : fraction * BinaryPrimitives.ReadInt16LittleEndian(resources.Model.PoseSource.Skeleton.AsSpan().Slice(18 + bone * 48)) >> 12;
        return new(unchecked((short)(selected.X + ((long)selected.Rotation[2] * distance >> 12))), unchecked((short)(selected.Y + ((long)selected.Rotation[5] * distance >> 12))), unchecked((short)(selected.Z + ((long)selected.Rotation[8] * distance >> 12))));
    }

    private static NativeBattleActionMatrix ShaderModelView(NativeBattleActionMatrix view, NativeBattleActionMatrix world)
    {
        short[] rotation = new short[9];
        for (int column = 0; column < 3; column++)
        {
            NativeBattleVector vector = new(world.Rotation[column], world.Rotation[3 + column], world.Rotation[6 + column]);
            NativeBattleVector transformed = NativeBattleGfMatrices.Transform460840(view with { X = 0, Y = 0, Z = 0 }, vector);
            rotation[column] = transformed.X;
            rotation[column + 3] = transformed.Y;
            rotation[column + 6] = transformed.Z;
        }
        NativeBattleFullMacVertex origin = NativeBattleGfMatrices.TransformMac460840(view, new(unchecked((short)world.X), unchecked((short)world.Y), unchecked((short)world.Z)));
        return new(rotation.ToImmutableArray(), origin.X, origin.Y, origin.Z, 4096);
    }

    private static NativeBattleActionMatrix ReadMatrix(NativeBattleModulePoolTask task) => new(Enumerable.Range(0, 9).Select(lane => unchecked((short)task.ReadWord(112 + lane * 2))).ToImmutableArray(), unchecked((int)task.ReadDword(132)), unchecked((int)task.ReadDword(136)), unchecked((int)task.ReadDword(140)), 4096);

    private static NativeBattleEffectArena StoreMatrix(NativeBattleEffectArena storage, int offset, NativeBattleActionMatrix matrix)
    {
        for (int lane = 0; lane < 9; lane++)
        {
            storage = Word(storage, offset + lane * 2, unchecked((ushort)matrix.Rotation[lane]));
        }
        storage = Dword(storage, offset + 20, unchecked((uint)matrix.X));
        storage = Dword(storage, offset + 24, unchecked((uint)matrix.Y));
        return Dword(storage, offset + 28, unchecked((uint)matrix.Z));
    }

    private static NativeBattleEffectArena Word(NativeBattleEffectArena storage, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return storage.Write(offset, bytes);
    }

    private static NativeBattleEffectArena Dword(NativeBattleEffectArena storage, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return storage.Write(offset, bytes);
    }
}
