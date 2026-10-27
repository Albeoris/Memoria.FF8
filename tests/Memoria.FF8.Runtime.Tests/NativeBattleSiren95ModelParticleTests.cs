using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSiren95ModelParticleTests
{
    [Theory]
    [InlineData(0x746FD0u, 1024)]
    [InlineData(0x747220u, 2048)]
    public void Model_particles_ignore_contact_stage_gate_damp_signed_words_and_draw_all_four_corners(uint callback, short scale)
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        var added = NativeBattleSiren95Actors.AppendTask(fixture.Frame, 0x257FA90, callback, 0xE0, NativeBattleSiren95Controller.MainBank, fixture.Inputs.Packets);
        uint director = added.Frame.Memory.Siren95!.DirectorAddress1533010!.Value;
        NativeBattleSiren95Frame frame = added.Frame with { Memory = added.Frame.Memory with { Siren95 = added.Frame.Memory.Siren95!.Write(director + 0x40, BitConverter.GetBytes((short)6)) } };
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(added.Address);
        for (int corner = 0; corner < 4; corner++)
        {
            task = task.WriteWord(0x6C + corner * 8, 100).WriteWord(0x6E + corner * 8, 200).WriteWord(0x70 + corner * 8, 300).WriteWord(0x8C + corner * 8, unchecked((ushort)-67)).WriteWord(0x8E + corner * 8, 67);
        }
        frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(added.Address, task).Pools } };
        var seen = new List<uint>();
        NativeBattleSiren95Frame Draw(NativeBattleSiren95Frame current, uint pointer, short actualScale)
        {
            var child = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(pointer);
            Assert.Equal(scale, actualScale);
            Assert.Equal((ushort)97, child.ReadWord(0x1C));
            Assert.Equal((ushort)203, child.ReadWord(0x1E));
            Assert.Equal((ushort)300, child.ReadWord(0x20));
            Assert.Equal((byte)0, (byte)(child.ReadByte(0x26) & 4));
            seen.Add(child.ReadDword(0x4C));
            return current;
        }
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleSiren95Frame>(NativeBattleTestSnapshots.Json(frame))!;
        var step = NativeBattleSiren95Contacts.AdvanceParticle(frame, added.Address, Draw);
        Assert.Equal(callback == 0x746FD0 ? new uint[] { 0x15336C4, 0x15334A0, 0x15334A0, 0x15334A0 } : new uint[] { 0x15334A0, 0x15334A0, 0x15334A0, 0x15334A0 }, seen);
        Assert.False(step.Complete);
        Assert.Equal(NativeBattleTestSnapshots.Json(step), NativeBattleTestSnapshots.Json(NativeBattleSiren95Contacts.AdvanceParticle(restored, added.Address, (current, _, _) => current)));
    }
}
