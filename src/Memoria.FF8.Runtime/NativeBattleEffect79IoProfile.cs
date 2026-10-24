using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect79Profile;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect79IoProfile
{
    private static NativeBattleIoProfile Profile { get; } = new(ScriptAddress, ScriptLength, 0x7B51C0, 0x7B53C0, 0x7B5420, 0x7B5430, 0x19F1018, true, 0x7B5440, 0x7B5450, false, 0x7B53F0);

    internal static NativeBattleProfiledIoState Initialize(ImmutableArray<byte> script, uint next)
    {
        NativeBattleProfiledIoState initialized = NativeBattleProfiledIoCore.Initialize(Profile, script, next);
        byte[] control = initialized.Control.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(0x128), 0x19F24C4);
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(0x12C), 0x19F36C0);
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(0x130), 0x19F4880);
        return initialized with { Control = control.ToImmutableArray() };
    }

    internal static NativeBattleProfiledIoAdvance Advance(NativeBattleModulePoolTask task, NativeBattleProfiledIoState io) => NativeBattleProfiledIoCore.Advance(Profile, task, io);
    internal static (NativeBattleProfiledIoState State, bool Released) ReleaseWait(NativeBattleProfiledIoState io) => NativeBattleProfiledIoCore.ReleaseWait(Profile, io);
}
