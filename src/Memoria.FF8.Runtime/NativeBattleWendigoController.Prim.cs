using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleWendigoController
{
    private sealed partial class Execution
    {
        private uint SpawnPrim(uint parent, uint callback, NativeBattleGfAnimationSource source, int size, ushort mode, ushort argument)
        {
            uint address = Append(4, callback, 0x118, parent);
            Store(address, Read(address).WriteDword(0x74, source.Address).WriteDword(0x78, unchecked((uint)size)).WriteWord(0x80, mode).WriteWord(0x82, argument));
            return address;
        }
        private NativeBattleGfAnimationSource PrimSource(NativeBattleModulePoolTask task)
        {
            if (resources.BallPrim is { } ball && task.ReadDword(0x74) == ball.Address)
            {
                return ball;
            }
            if (resources.KickPrim is { } kick && task.ReadDword(0x74) == kick.Address)
            {
                return kick;
            }
            throw new NotSupportedException("Native Shoot requires its initialized primitive source.");
        }
        private static NativeBattleModulePoolTask PrimWorkspace(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace)
        {
            return task.WriteRetainedBytes(0x94, workspace.Data.AsSpan());
        }
        private void InitializePrim(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(0x94 - 12, 0x84));
            workspace = workspace.Initialize7016B0(PrimSource(task), checked((int)task.ReadDword(0x78)));
            Store(address, PrimWorkspace(task, workspace));
        }
        private void Prim(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            if (task.Callback == 0x8462D0)
            {
                if (Stage(task) == 0)
                {
                    Store(address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
                }
                return;
            }
            bool kick = task.Callback == 0x846350;
            if (kick && Stage(task) == 0)
            {
                InitializePrim(address);
                Store(address, Next(Read(address)));
                return;
            }
            if (!kick)
            {
                if (Stage(task) != 0)
                {
                    return;
                }
                int state = Word(Ball + 0xDE);
                if (state == 1)
                {
                    InitializePrim(address);
                    Word(Ball + 0xDE, 2);
                }
                else if (state == 3)
                {
                    Store(address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
                    return;
                }
                else if (state != 2)
                {
                    return;
                }
            }
            else if (Stage(task) != 1)
            {
                return;
            }
            task = Read(address);
            NativeBattleActionMatrix world;
            if (kick)
            {
                world = TaskMatrix(task, 0x30);
            }
            else
            {
                Store(address, task.WriteDword(0x1C, Dword(Ball + 0x7C)).WriteDword(0x20, Dword(Ball + 0x80)));
                NativeBattleVector p = Position(Ball + 0x7C);
                world = Identity() with { X = p.X, Y = p.Y, Z = p.Z };
                world = NativeBattleCuragaMatrices.RotateX8DD7E0(world, Word(Ball + 0xA4));
                world = NativeBattleCuragaMatrices.RotateZ8DD960(world, Word(Ball + 0xA8));
            }
            NativeBattleActionMatrix view = inputs.Projection.View ?? throw new NotSupportedException("Native Shoot requires its camera matrix.");
            NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(view, world);
            NativeBattleGfAnimationSource source = PrimSource(Read(address));
            NativeBattleGfAnimationWorkspace workspace = new(Read(address).Payload.Slice(0x94 - 12, 0x84));
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (currentWorkspace, node) =>
            {
                if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
                {
                    return currentWorkspace;
                }
                int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan(8 + node.GeometryIndex * 4, 4));
                int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan(geometry + 4, 4));
                ImmutableArray<byte> vertices;
                if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
                {
                    int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
                    vertices = source.Data.Slice(checked(geometry + 12 + shape * count * 8), checked(count * 8));
                }
                else
                {
                    if (count is < 0 or > 108)
                    {
                        throw new InvalidDataException("Native Shoot primitive morph exceeds its declared vertex allocation.");
                    }
                    int scratch = checked((int)(0x267ACA8 - Base));
                    ImmutableArray<byte> morphed = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38, State.Module.Read(scratch, 0x360).ToImmutableArray());
                    SetState(State with { Module = State.Module.Write(scratch, morphed.AsSpan()) });
                    vertices = morphed.Slice(0, count * 8);
                }
                NativeBattleActionMatrix rotation = NativeBattleWendigoPrimGeometry.Compose(node, camera);
                bool fade = node.Scalar36 != 0;
                NativeBattleGfGeometryBatch batch = NativeBattleGfGeometryPackets.EmitBatch7043B0(source.Data, geometry, vertices, rotation, inputs.Projection, new(fade ? 0x20F0u : 0x2030u, fade ? node.Color32 : 0, node.Scalar36, null, null, 0, 17, 2), inputs.Colors);
                Emit(new NativeBattleWendigoPackets(batch, false));
                return currentWorkspace;
            });
            Store(address, PrimWorkspace(Read(address), advanced.Workspace));
            if (advanced.Remaining == 0)
            {
                if (kick)
                {
                    Store(address, Next(Read(address).WriteByte(0x26, (byte)(Read(address).ReadByte(0x26) | 1))));
                }
                else
                {
                    Word(Ball + 0xDE, 0);
                }
            }
        }
    }
}

public static class NativeBattleWendigoPrimGeometry
{
    public static NativeBattleActionMatrix Compose(NativeBattleGfAnimationNode node, NativeBattleActionMatrix camera)
    {
        NativeBattleActionMatrix rotation = (node.Header & 0x400) != 0 ? NativeBattleGfMatrices.Euler701430(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector offset = node.Vector8;
        int ox = offset.X;
        int oy = offset.Y;
        int oz = offset.Z;
        if ((node.Header & 0x200) == 0)
        {
            (ox, oy, oz) = NativeBattleGfMatrices.Rotate56C3D0(camera, offset);
            rotation = NativeBattlePoseMatrices.MultiplyGteRotation45E470(camera, rotation);
        }
        rotation = rotation with { X = unchecked(camera.X + ox), Y = unchecked(camera.Y + oy), Z = unchecked(camera.Z + oz) };
        if (node.Vector24 is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, node.Vector24.X, node.Vector24.Y, node.Vector24.Z);
        }
        return rotation;
    }
}
