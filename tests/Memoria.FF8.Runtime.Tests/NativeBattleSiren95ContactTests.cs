using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSiren95ContactTests
{
    [Fact]
    public void Contact_initialization_reads_signed_bounds_before_position_and_starts_the_real_selected_sequence()
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        var appended = NativeBattleSiren95Actors.AppendTask(fixture.Frame, 0x257FA90, 0x7447C0, 0xE0, NativeBattleSiren95Controller.MainBank, fixture.Inputs.Packets);
        int selections = 0;
        NativeBattleGraphicsModel model = Model() with { GeometryBounds52 = new(new(0, 0, 0), new(0, -512, 0)), Transform = null, Flags8 = null };
        NativeBattleSiren95ContactServices services = new(fixture.Inputs.Packets, _ => model, (current, actor, sequence) =>
        {
            selections++;
            Assert.Equal((byte)3, actor);
            Assert.InRange(sequence, 4, 6);
            return current;
        });
        var rejected = NativeBattleSiren95Contacts.Advance7447C0(appended.Frame, appended.Address, services);
        Assert.True(rejected.Complete);
        Assert.Equal(0, selections);
        model = Model();
        var initialized = NativeBattleSiren95Contacts.Advance7447C0(appended.Frame, appended.Address, services);
        Assert.False(initialized.Complete);
        Assert.Equal(1, selections);
        NativeBattleModulePoolTask task = Read(initialized.Frame, appended.Address);
        Assert.Equal((byte)1, task.ReadByte(0x29));
        Assert.Equal((ushort)8, task.ReadWord(0xDC));
        NativeCrtRandomState expected = appended.Frame.Random.Draw(out _);
        Assert.Equal(expected, initialized.Frame.Random);
    }

    [Fact]
    public void Odd_contact_tick_preserves_native_child_order_and_all_forty_random_operands()
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        var appended = NativeBattleSiren95Actors.AppendTask(fixture.Frame, 0x257FA90, 0x7447C0, 0xE0, NativeBattleSiren95Controller.MainBank, fixture.Inputs.Packets);
        var frame = Put(appended.Frame, appended.Address, Read(appended.Frame, appended.Address).WriteByte(0x29, 1).WriteWord(0x24, 1).WriteWord(0xDC, 8));
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 40; index++)
        {
            expected = expected.Draw(out _);
        }
        string saved = JsonSerializer.Serialize(frame);
        NativeBattleSiren95ContactServices services = new(fixture.Inputs.Packets, _ => Model(), (_, _, _) => throw new InvalidOperationException());
        var result = NativeBattleSiren95Contacts.Advance7447C0(frame, appended.Address, services);
        Assert.Equal(expected, result.Frame.Random);
        var children = result.Frame.Effect.Pools.SelectMany(pool => pool.Slots).Where(task => task.Callback is 0x744AA0 or 0x744C30).ToArray();
        Assert.Equal(new uint[] { 0x744AA0, 0x744C30 }, children.Select(task => task.Callback));
        Assert.Equal((ushort)7, Read(result.Frame, appended.Address).ReadWord(0xDC));
        Assert.All(children, child => Assert.Equal(appended.Address, child.ReadDword(0x18)));
        Assert.Equal((ushort)0, children[0].ReadWord(0x8C));
        Assert.Equal((ushort)0, children[0].ReadWord(0x90));
        Assert.InRange(unchecked((short)children[1].ReadWord(0x8E)), -3583, -1536);
        var restored = NativeBattleSiren95Contacts.Advance7447C0(JsonSerializer.Deserialize<NativeBattleSiren95Frame>(saved)!, appended.Address, services);
        Assert.Equal(JsonSerializer.Serialize(result.Frame), JsonSerializer.Serialize(restored.Frame));
    }

    [Theory]
    [InlineData(false, 4, -7, 0)]
    [InlineData(true, 1, -9, 10)]
    public void Particle_updates_every_corner_but_gravity_draws_only_the_first_and_preserves_padding(bool gravity, int count, short vx, short vy)
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        var appended = NativeBattleSiren95Actors.AppendTask(fixture.Frame, 0x257FA90, gravity ? 0x744C30u : 0x744AA0u, 0xE0, NativeBattleSiren95Controller.MainBank, fixture.Inputs.Packets);
        NativeBattleModulePoolTask task = Read(appended.Frame, appended.Address);
        for (int corner = 0; corner < 4; corner++)
        {
            task = task.WriteWord(0x6C + corner * 8, 100).WriteWord(0x6E + corner * 8, 200).WriteWord(0x70 + corner * 8, 300).WriteWord(0x72 + corner * 8, 0xABCD).WriteWord(0x8C + corner * 8, unchecked((ushort)-9)).WriteWord(0x8E + corner * 8, unchecked((ushort)-17)).WriteWord(0x90 + corner * 8, 33);
        }
        NativeBattleSiren95Frame frame = Put(appended.Frame, appended.Address, task);
        int calls = 0;
        var result = NativeBattleSiren95Contacts.AdvanceParticle(frame, appended.Address, (current, pointer, scale) =>
        {
            Assert.Equal(gravity ? (short)0x3000 : (short)4096, scale);
            Assert.Equal((ushort)0xABCD, Read(current, pointer).ReadWord(0x22));
            calls++;
            return current;
        });
        Assert.Equal(count, calls);
        task = Read(result.Frame, appended.Address);
        Assert.Equal(unchecked((ushort)vx), task.ReadWord(0x8C));
        Assert.Equal(unchecked((ushort)(gravity ? 175 : -13)), task.ReadWord(0x8E));
        Assert.Equal((ushort)(200 + vy), task.ReadWord(0x6E));
        Assert.Equal((ushort)0xABCD, task.ReadWord(0x8A));
    }

    private static NativeBattleGraphicsModel Model() => new(3, 3) { GeometryBounds52 = new(new(-100, -200, -300), new(100, 0, 300)), Transform = new(new(0, 0, 0), new(10, 20, -1000), 0, 0, 4096), Flags8 = 0 };
    private static NativeBattleModulePoolTask Read(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Put(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
