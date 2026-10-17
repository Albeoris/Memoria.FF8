using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCuragaNodeKinematics
{
    internal static NativeBattleEffectNodeBank CurrentBank(NativeBattleCuragaExecutableState? source)
    {
        NativeBattleCuragaExecutableState retained = source ?? throw new NotSupportedException("Native Curaga node requires its retained current executable bank.");
        retained.Validate();
        return new(NativeBattleCuragaExecutableState.Address, retained.Data ?? throw new NotSupportedException("Native Curaga node requires its materialized executable bytes."));
    }

    internal static NativeBattleEffectNodeLifecycle CreateLifecycle(NativeBattleCuragaNodeMemory memory, NativeBattleEffectNodeBank bank, Func<int, NativeBattleGraphicsModel>? readModel = null)
    {
        return new(memory, bank, new(memory.State.CopiedCamera, readModel ?? (_ => throw new NotSupportedException("Native node orientation requires its actual model reader.")), (parent, source, target) => NativeBattleCuragaNodes.AllocateEmitter(memory, parent, source, target), (emitter, source) => NativeBattleCuragaNodes.AllocateGeometry(memory, emitter, source)));
    }

    internal static NativeBattleCuragaFrame FlushCurrent(NativeBattleCuragaNodeMemory memory, NativeBattleEffectNodeBank bank)
    {
        NativeBattleCuragaFrame result = memory.Flush();
        NativeBattleCuragaExecutableState retained = result.Memory.CuragaExecutable ?? throw new NotSupportedException("Native Curaga node cannot publish its current bytes without the same retained bank owner.");
        return result with { Memory = result.Memory with { CuragaExecutable = retained with { Data = bank.Data } } };
    }

    private static NativeBattleEffectNodePrepareMesh PrepareMeshes(NativeBattleCuragaNodeMemory memory, NativeBattleCameraProjection projection, NativeBattleSummonColors? colors)
    {
        return (node, source) =>
        {
            uint flags = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(0x3A)) == 0 ? 0x30u : 0;
            short fade = unchecked((short)memory.Word(node + 0x1CE));
            uint? farColor = null;
            if (fade != 0)
            {
                flags |= 0xC0;
                farColor = memory.Dword(node + 0x16C);
            }
            NativeBattleCuragaMeshContext context = new(flags, farColor, fade == 0 ? null : fade, null, null, null, 17, 2);
            return (ImmutableArray<byte> bytes, NativeBattleActionMatrix matrix) => NativeBattleCuragaMeshPackets.EmitBatch87EEB0(bytes, matrix, projection, context, colors: colors);
        };
    }
}
