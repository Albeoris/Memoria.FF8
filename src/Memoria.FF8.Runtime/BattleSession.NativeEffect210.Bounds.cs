using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect210Controller;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect210Frame ReadNativeEffect210Bounds(ref NativeBattleGraphicsState graphics, NativeBattleEffect210Frame current, NativeBattleEffect210Bounds9800 operation)
    {
        int actor = Actor(operation.ModelAddress);
        NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect210 bounds require their current addressed memory.");
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Effect210 bounds require their current primary skeleton.");
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect210 bounds require their current actor world matrix.");
        model = NativeBattleBodyBoneMatrices.Compose5095B0(model, world);
        graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(model, source);
        ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new NotSupportedException("Effect210 bounds require their current primary geometry.");
        uint mask = model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Effect210 bounds require their current object mask.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Effect210 bounds require their current projection.");
        NativeBattleScriptedGraphicsMemory controls = current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect210 bounds require their current projection controls.");
        (current, uint temporaryAddress) = Allocate(current, 24);
        NativeBattleEffectArena temporary = Context(current, temporaryAddress, 24).Write(0, BitConverter.GetBytes(65536)).Write(4, BitConverter.GetBytes(-65536)).Write(8, BitConverter.GetBytes(0)).Write(12, BitConverter.GetBytes((ushort)4096)).Write(20, BitConverter.GetBytes(mask));
        current = PublishContext(current, temporaryAddress, temporary, 24);
        int minimum = 65536;
        int maximum = -65536;
        for (int objectIndex = 0; objectIndex < geometry.Length; objectIndex++)
        {
            if ((mask & (1u << (objectIndex & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = geometry[objectIndex];
            mesh.Validate();
            ImmutableArray<NativeBattleRawVertexGroup> groups = mesh.PackedVertexGroups ?? throw new NotSupportedException("Effect210 bounds require their actual ordered packed vertex groups.");
            foreach (NativeBattleRawVertexGroup group in groups)
            {
                if (group.Bone >= matrices.Length || group.Count < 0 || group.Bytes.IsDefault || group.Bytes.Length < group.Count * 6)
                {
                    throw new InvalidDataException("Effect210 bounds vertex group leaves its current primary source.");
                }
                NativeBattleActionMatrix matrix = matrices[group.Bone];
                current = LoadMatrix(current, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
                for (int vertexIndex = 0; vertexIndex < group.Count; vertexIndex++)
                {
                    ReadOnlySpan<byte> packed = group.Bytes.AsSpan().Slice(vertexIndex * 6, 6);
                    NativeBattleVector vertex = new(BinaryPrimitives.ReadInt16LittleEndian(packed), BinaryPrimitives.ReadInt16LittleEndian(packed[2..]), BinaryPrimitives.ReadInt16LittleEndian(packed[4..]));
                    NativeBattleScriptedColorRegisters registers = current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect210 bounds require their current packed vertex owner.");
                    byte[] input = registers.NormalInput?.ToArray() ?? new byte[8];
                    bool[] known = registers.NormalInputKnown?.ToArray() ?? Enumerable.Repeat(registers.NormalInput is not null, 8).ToArray();
                    packed.CopyTo(input);
                    Array.Fill(known, true, 0, 6);
                    NativeBattleModuleDepthCueVertex projected = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix, vertex, projection, controls.Dqa, controls.Dqb);
                    registers = registers with { NormalInput = input.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray(), Mac = [null, null, null], Ir = [null, projected.Ir2, null], Flags = projected.Projected.Flags };
                    current = current with { Memory = current.Memory with { ScriptedColorRegisters = registers } };
                    short x = unchecked((short)projected.Projected.Screen);
                    temporary = temporary.Write(16, BitConverter.GetBytes(projected.Projected.Screen));
                    if (x < minimum)
                    {
                        minimum = x;
                        temporary = temporary.Write(0, BitConverter.GetBytes(minimum));
                    }
                    if (x > maximum)
                    {
                        maximum = x;
                        temporary = temporary.Write(4, BitConverter.GetBytes(maximum));
                    }
                    current = PublishContext(current, temporaryAddress, temporary, 24);
                }
            }
        }
        NativeBattleEffectArena output = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((short)maximum))).Write(2, BitConverter.GetBytes(unchecked((short)minimum)));
        current = WriteBytes(current, operation.DestinationAddress, output, 4);
        current = Release(current, 24);
        (current, uint restoreAddress) = Allocate(current, 88);
        NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(graphics.Models[actor], source, Context(current, restoreAddress, 88));
        graphics = graphics with { Models = graphics.Models.SetItem(actor, restored.Model) };
        current = PublishContext(current, restoreAddress, restored.Temporary58, 88);
        current = Release(current, 88);
        NativeFileStaging = current.Staging;
        return current;
    }
}
