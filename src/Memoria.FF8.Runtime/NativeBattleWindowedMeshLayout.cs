using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed class NativeBattleWindowedMeshLayout
{
    public static NativeBattleWindowedMeshLayout OilShot136 { get; } = new(92, 4, 0x24, 0x34, 0x30, 0x10, 0x14, 0x1C, [(7, 0)], 0x1C, true);
    public static NativeBattleWindowedMeshLayout OilBlast137 { get; } = new(84, 0x18, 0x1C, 0x2C, 0x28, 4, 8, 0x10, [(3, 0), (4, 0), (5, 0), (7, 4)]);
    public static NativeBattleWindowedMeshLayout Saliva138 { get; } = new(92, 4, 0x24, 0x34, 0x30, 0x10, 0x14, 0x1C, [(3, 0), (7, 12)]);

    private NativeBattleWindowedMeshLayout(int contextBytes, int selectedVertices, int faceCursor, int flags, int depth, int uOffset, int windowA, int windowB, ImmutableArray<(int Group, int Skip)> groups, int initialFaceBias = 12, bool insertDrawMode = false)
    {
        ContextBytes = contextBytes;
        SelectedVertices = selectedVertices;
        FaceCursor = faceCursor;
        Flags = flags;
        Depth = depth;
        UOffset = uOffset;
        WindowA = windowA;
        WindowB = windowB;
        Groups = groups;
        InitialFaceBias = initialFaceBias;
        InsertDrawMode = insertDrawMode;
    }

    public int InitialFaceBias { get; }
    public bool InsertDrawMode { get; }
    public int ContextBytes { get; }
    public int SelectedVertices { get; }
    public int FaceCursor { get; }
    public int Flags { get; }
    public int Depth { get; }
    public int UOffset { get; }
    public int WindowA { get; }
    public int WindowB { get; }
    public ImmutableArray<(int Group, int Skip)> Groups { get; }
}
