using static Memoria.FF8.Runtime.NativeBattleEffect218Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect218RayTests
{
    [Fact]
    public void Billboard_divides_signed_screen_WORDs_toward_zero_and_preserves_retained_matrix_padding()
    {
        NativeBattleOriginalEffectState effect = new(218, 218, new(218, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleEffect218State state = NativeBattleEffect218State.FromColdProcess().WriteWord(0x24F360A, 0xBEEF);
        NativeBattleEffect218Frame frame = new(memory, effect, new(1), state);
        NativeBattlePackedMatrix32 view = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
        frame = PrepareBillboard633440(frame, new(-1601, -1081, 1000), null, 1536, -5, view, new(null, 100, 160, 108), 321);
        Assert.Equal(unchecked((uint)-160), frame.State.ReadDword(0x24F360C));
        Assert.Equal(unchecked((uint)-108), frame.State.ReadDword(0x24F3610));
        Assert.Equal(316u, frame.State.ReadDword(0x24F3614));
        Assert.Equal((ushort)1536, frame.State.ReadWord(0x24F35F8));
        Assert.Equal((ushort)1536, frame.State.ReadWord(0x24F3600));
        Assert.Equal((ushort)1536, frame.State.ReadWord(0x24F3608));
        Assert.Equal((ushort)0xBEEF, frame.State.ReadWord(0x24F360A));
        Assert.Equal((byte?)0xEF, frame.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Equal((byte?)0xBE, frame.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Equal(0xFFFFFFFFu, frame.Memory.GeometryRegisters.Sxy[2]);
    }
}
