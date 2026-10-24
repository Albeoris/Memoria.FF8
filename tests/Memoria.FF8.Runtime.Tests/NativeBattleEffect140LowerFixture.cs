using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect140Controller;

namespace Memoria.FF8.Runtime.Tests;

internal static class NativeBattleEffect140LowerFixture
{
    internal static NativeBattleEffect140Frame Frame(int z = 2000)
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        return new(memory, new(140, 139, new(140, 0x6A6300, 0x6A6310), null, null) { Pools = [] }, new(123), NativeBattleEffect140State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    internal static NativeBattleEffect140Inputs Inputs(Func<NativeBattleEffect140Frame, NativeBattleEffect140Operation, NativeBattleEffect140Frame> execute) => new(Colors: NativeBattleGteColorInterpolation.CodeConstants45F270, ReadFlags: _ => 0, ReadPackets: null!, ReadModel: null!, ReadModelPosition8: null!, ReadView: _ => NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 2000, 4096), null), ReadProjection: _ => new(null, 100, 160, 108), ReadCameraPose: _ => new(new(0, 0, 0), new(0, 0, 4096), 100, 0), ReadEyePadding: null!, ReadTargetPadding: null!, Execute: execute);
    internal static NativeBattleEffect140Resources Resources(params NativeBattleStaticMeshResource[] meshes) => new(new("texture", []), null!, new("vibration", []), [], ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, meshes.ToImmutableDictionary(mesh => mesh.Address), null!, ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
    internal static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    internal static short S(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    internal static NativeBattleEffect140Frame WithPool(NativeBattleEffect140Frame frame, ushort capacity = 1) => frame with { Effect = frame.Effect with { Pools = [NativeBattleEffectTaskPool.Initialize(0x2517AC8, 0x1000, 28, capacity)] } };
    internal static NativeBattleStaticMeshResource Mesh(int vertices = 4, bool degenerate = false)
    {
        int polygon = 8 + vertices * 8;
        byte[] bytes = new byte[polygon + 32 + 36];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, polygon);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), vertices);
        for (int index = 0; index < vertices; index++)
        {
            short x = vertices == 4 ? (short)((index & 1) == 0 ? -100 : 100) : (short)(1000 + index);
            short y = vertices == 4 ? (short)(index < 2 ? -100 : 100) : (short)0;
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), x);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 8), 4096);
        }
        int face = polygon + 32;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(polygon + 28), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), 0x3E808080);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), degenerate ? (ushort)0 : (ushort)(corner * 2));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x123402FA);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x004006FB);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0x08FD0AFC);
        for (int corner = 1; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20 + corner * 4), 0x00808080);
        }
        return new(0, 0x11D04B4, bytes.ToImmutableArray());
    }

    internal static NativeBattleEffectArena Context(NativeBattleStaticMeshResource mesh, int cue = 0)
    {
        return NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(mesh.Address)).Write(4, BitConverter.GetBytes(mesh.Address + 8)).Write(0x10, BitConverter.GetBytes((short)8)).Write(0x14, new byte[4]).Write(0x18, BitConverter.GetBytes((short)256)).Write(0x1A, BitConverter.GetBytes((short)256)).Write(0x1C, new byte[4]).Write(0x20, BitConverter.GetBytes((short)128)).Write(0x22, BitConverter.GetBytes((short)256)).Write(0x38, BitConverter.GetBytes(cue)).Write(0x30, BitConverter.GetBytes(0xDEADBEEFu)).MarkUnknown(0x78, 32);
    }
}
