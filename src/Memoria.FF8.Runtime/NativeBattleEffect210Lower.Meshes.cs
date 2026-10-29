using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect210Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect210Lower
{
    public static NativeBattleEffect210Frame Draw8BF0(NativeBattleEffect210Frame frame, NativeBattleEffectArena point8, short angle, int rotation, int fade, int uvScroll, int scale, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleVector point = Vector(point8);
        if (scale > 0)
        {
            (frame, uint temporary) = Allocate(frame, 108);
            NativeBattleActionMatrix local = NativeBattleDuel215Controller.ParticleBasis6ED1E0(angle, 8192) with { X = point.X, Y = 0, Z = point.Z };
            frame = LoadCpuMatrix(frame, NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local));
            NativeBattleEffectArena context = Windows(Context(frame, temporary, 108), 0, uvScroll & 127, 0, 128, 256, 128);
            context = Put(Put(Put(Put(Put(context, 0, 0x145E1C4), 8, 0), 12, unchecked((uint)scale)), 24, 0), 28, 0xF0);
            frame = PublishContext(frame, temporary, context, 108);
            frame = DrawWindowed108(frame, temporary, 0x145E1C4, resources, inputs, pilotIdentity: pilotIdentity is { } firstOwner ? firstOwner with { Primitive = unchecked(firstOwner.Primitive * 397) } : null);
            return Release(frame, 108);
        }
        if (rotation < 4096)
        {
            (frame, uint temporary) = Allocate(frame, 180);
            NativeBattleEffectArena context = Context(frame, temporary, 180);
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, angle, 0));
            NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(local, new(0, 0, unchecked((short)((2048 - rotation) * 2))));
            local = local with { X = unchecked(offset.X + point.X), Y = offset.Y, Z = unchecked(offset.Z + point.Z) };
            if (rotation != 0)
            {
                NativeBattleActionMatrix zRotation = NativeBattleGfMatrices.Euler56CE30(new(0, 0, unchecked((short)rotation)));
                context = CopyBytes(PackedArena(NativeBattlePackedMatrix32.FromMatrix(zRotation, 0)), 0, context, 0x8C, 20);
                NativeBattleActionMatrix combined = NativeBattleGfMatrices.Compose56C2F0(local, zRotation);
                local = combined with { X = local.X, Y = local.Y, Z = local.Z };
            }
            context = PutPoint(context, 0xAC, new(0, 0, unchecked((short)((2048 - rotation) * 2))));
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
            context = CopyBytes(PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)), 0, context, 0x6C, 32);
            context = Windows(context, uvScroll & 63, 0, 0, 0, 64, 256);
            context = Put(Put(Put(Put(Put(context, 0, 0x14603F4), 8, 0), 12, unchecked((uint)fade)), 24, 0), 28, 0xF0);
            frame = PublishContext(frame, temporary, context, 180);
            frame = LoadCpuMatrix(frame, matrix);
            frame = DrawWindowed108(frame, temporary, 0x14603F4, resources, inputs, pilotIdentity: pilotIdentity is { } secondOwner ? secondOwner with { Primitive = unchecked(secondOwner.Primitive * 397 + 1) } : null);
            frame = Release(frame, 180);
        }
        (frame, uint customTemporary) = Allocate(frame, 152);
        NativeBattleActionMatrix customLocal = NativeBattleDuel215Controller.ParticleBasis6ED1E0(angle, 8192) with { X = point.X, Y = 0, Z = point.Z };
        frame = LoadCpuMatrix(frame, NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), customLocal));
        NativeBattleEffectArena custom = Context(frame, customTemporary, 152);
        custom = Put(Put(Put(custom, 0, 0x145E1C4), 4, 0x145E1CC), 0x38, unchecked((uint)(fade - 2048)));
        custom = PutWord(custom, 0x12, uvScroll & 127);
        custom = PutWord(PutWord(PutWord(PutWord(custom, 0x14, 0), 0x16, 0), 0x18, 256), 0x1A, 256);
        custom = PutWord(PutWord(PutWord(PutWord(custom, 0x1C, 0), 0x1E, 128), 0x20, 256), 0x22, 128);
        frame = PublishContext(frame, customTemporary, custom, 152);
        NativeBattleEffect210CustomMeshStep drawn = NativeBattleEffect210CustomMesh.DrawLoaded(frame.Memory, resources.ReadMesh(0x145E1C4), inputs.ReadProjection(frame), custom, inputs.Colors, pilotIdentity: pilotIdentity is { } customOwner ? customOwner with { Primitive = unchecked(customOwner.Primitive * 397 + 2), ResourceGeneration = 0x145E1C4 } : null);
        frame = frame with { Memory = drawn.Memory };
        frame = PublishContext(frame, customTemporary, drawn.Context152, 152);
        frame = inputs.Execute(frame, new NativeBattleEffect210DrawBatch(drawn.Batch, CursorGlobal));
        return Release(frame, 152);
    }

    private static NativeBattleEffect210Frame DrawWindowed108(NativeBattleEffect210Frame frame, uint temporary, uint source, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleStaticMeshResource mesh = resources.ReadMesh(source);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        NativeBattleWindowedMeshContext108Step result = NativeBattleWindowedMeshContext108.DrawLoaded(frame.Memory, mesh, inputs.ReadProjection(frame), Context(frame, temporary, 108), inputs.Colors, 17, 2, count, index =>
        {
            ReadOnlySpan<byte> row = mesh.Bytes.AsSpan().Slice(8 + index * 8, 8);
            return (new(BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(row.Slice(4))), BinaryPrimitives.ReadInt16LittleEndian(row.Slice(6)));
        }, pilotIdentity: pilotIdentity is { } identity ? identity with { ResourceGeneration = source } : null);
        frame = frame with { Memory = result.Memory };
        frame = PublishContext(frame, temporary, result.Context108, 108);
        return inputs.Execute(frame, new NativeBattleEffect210DrawBatch(result.Batch, CursorGlobal));
    }

    private static NativeBattleEffectArena Windows(NativeBattleEffectArena context, int u, int v, int x, int y, int width, int height)
    {
        context = PutWord(PutWord(context, 0x20, u), 0x22, v);
        context = PutWord(PutWord(PutWord(PutWord(context, 0x24, 0), 0x26, 0), 0x28, 256), 0x2A, 256);
        return PutWord(PutWord(PutWord(PutWord(context, 0x2C, x), 0x2E, y), 0x30, width), 0x32, height);
    }
}
