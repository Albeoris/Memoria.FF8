using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLevelResources(ushort Effect, NativeBattleGfAnimationSource Animation, NativeBattleEffectHeapAllocation Camera, uint Sound);
public sealed record NativeBattleLevelState([property: JsonRequired] int BufferOffset);

public sealed partial record NativeBattleOriginalEffectState
{
    public NativeBattleLevelState? Level { get; init; }
}

public sealed record NativeBattleLevelProfile(ushort Effect, uint RootDescriptor, uint RootBank, uint RootCallback, uint ChildDescriptor, uint ChildCallback, int WorkspaceBytes, int TaskBytes, uint ProgrammeAddress, int ProgrammeBytes, uint CameraAddress, uint SoundAddress)
{
    public static NativeBattleLevelProfile ForEffect(int effect) => effect switch
    {
        271 => new(271, 0x2508600, 0x25085B8, 0x68F600, 0x25085F0, 0x68F830, 0x4A0, 0x4CC, 0x1116304, 18216, 0x111AA30, 0x111AA2C),
        272 => new(272, 0x2508598, 0x2508550, 0x68DD20, 0x2508588, 0x68E090, 0x4D0, 0x4FC, 0x111122C, 18232, 0x1115968, 0x1115964),
        _ => throw new NotSupportedException("Native level effect requires its actual profile.")
    };
}
