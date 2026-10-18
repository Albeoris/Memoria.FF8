using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleEffectNodeLifecycle
{
    internal NativeBattleGfGeometryBatch AdvanceAndDraw(ref NativeCrtRandomState random, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleEffectNodePrepareMesh prepareMesh, Action<bool, NativeBattleGfGeometryBatch>? deliver = null)
    {
        Birth();
        AdvanceList(ref random);
        return DrawNodes(projection, cameraRoll, prepareMesh, deliver);
    }

    internal NativeBattleGfGeometryBatch DrawNodes(NativeBattleCameraProjection projection, short cameraRoll, NativeBattleEffectNodePrepareMesh prepareMesh, Action<bool, NativeBattleGfGeometryBatch>? deliver = null)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        Dictionary<uint, ImmutableArray<byte>> resources = [];
        int allocated = 0;
        DrawList(false);
        DrawList(true);
        return new(attempts.ToImmutable(), allocated);

        void DrawList(bool meshes)
        {
            NativeBattleEffectNodeAllocation.WalkList(() => memory, (node, kind) =>
            {
                if (kind != 1)
                {
                    return;
                }
                byte[] source = ReadSource(unchecked((sbyte)memory.Byte(node + 0x1D6)));
                bool selected = meshes ? source[0x16] is 4 or 5 : source[0x16] is 0 or 1 or 2;
                if (!selected)
                {
                    return;
                }
                uint resource = memory.Dword(node + 0x170);
                if (resource == 0)
                {
                    return;
                }
                NativeBattleEffectNodeMeshInstance? emitMesh = meshes ? prepareMesh(node, source) : null;
                int count = unchecked((sbyte)memory.Byte(node + 0x1D8));
                for (int instance = 0; instance < count; instance++)
                {
                    if (count != 1)
                    {
                        for (int axis = 0; axis < 3; axis++)
                        {
                            memory.Dword(node + 0xC0 + axis * 4, unchecked((uint)(int)(short)memory.Word(node + 0x19C + instance * 8 + axis * 2)));
                        }
                    }
                    NativeBattleActionMatrix matrix = ReadMatrix(node + 0xAC);
                    if (!resources.TryGetValue(resource, out ImmutableArray<byte> bytes))
                    {
                        bytes = bank.Read(resource, checked(bank.Length - (int)(resource - bank.Address))).ToImmutableArray();
                        resources.Add(resource, bytes);
                    }
                    NativeBattleGfGeometryBatch batch;
                    if (meshes)
                    {
                        batch = emitMesh!(bytes, matrix);
                    }
                    else
                    {
                        ushort phase = unchecked((ushort)(short)(sbyte)memory.Byte(node + 0x1D0));
                        batch = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(bytes, phase, matrix, projection, cameraRoll, 17, 2);
                    }
                    deliver?.Invoke(meshes, batch);
                    foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
                    {
                        attempts.Add(attempt with { ByteOffsetRelative = checked(allocated + attempt.ByteOffsetRelative) });
                    }
                    allocated = checked(allocated + batch.AllocationBytes);
                }
            });
        }
    }
}
