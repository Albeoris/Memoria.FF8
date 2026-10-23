using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleGatlingGun150Controller;
using static Memoria.FF8.Runtime.NativeBattleGatlingGun150Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGatlingGun150ControllerTests
{
    [Fact]
    public void Director_age_one_uses_unshifted_bone_anchor_signed_sixth_steps_and_five_delayed_births()
    {
        NativeBattleGatlingGun150Frame frame = Frame(MainCallback);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, 1));
        List<NativeBattleGatlingGun150Operation> operations = [];
        var result = AdvanceDirector(frame, ChildBank, Resources(), Inputs(operations));
        NativeBattleModulePoolTask[] children = result.Frame.Effect.Pools[1].Slots.Skip(1).Take(5).ToArray();
        Assert.All(children, task => Assert.Equal(0x60EAE0u, task.Callback));
        Assert.Equal(new ushort[] { 2, 4, 6, 8, 10 }, children.Select(task => task.ReadWord(14)));
        Assert.Equal(new short[] { 0, -100, -200, -300, -400 }, children.Select(task => Signed(task, 16)));
        Assert.Equal(new short[] { 400, 500, 600, 700, 800 }, children.Select(task => Signed(task, 20)));
        Assert.All(children, task => Assert.Equal((ushort)0, task.ReadWord(18)));
        Assert.Equal(new ushort[] { 1, 2, 3, 4, 5 }, children.Select(task => task.ReadWord(24)));
        NativeBattleGatlingGun150Sound sound = Assert.IsType<NativeBattleGatlingGun150Sound>(Assert.Single(operations));
        Assert.Equal(0x61AD6u, sound.Packed);
        Assert.Equal(0u, sound.Flags);
        Assert.Equal((byte)128, sound.Pan);
        Assert.Equal((ushort)2, Read(result.Frame, ChildBank).ReadWord(12));
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Throws<NotSupportedException>(() => result.Frame.State.ReadWord(CasterPointGlobal + 6));
    }

    [Theory]
    [InlineData(2, 0, 0)]
    [InlineData(3, 1, 2)]
    [InlineData(24, 0, 0)]
    [InlineData(25, 1, 2)]
    [InlineData(26, 0, 0)]
    public void Director_emits_flash_only_on_original_odd_age_window(ushort age, int flashes, int randomDraws)
    {
        NativeBattleGatlingGun150Frame frame = Frame(MainCallback);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, age));
        var result = AdvanceDirector(frame, ChildBank, Resources(), Inputs([]));
        Assert.Equal(flashes, result.Frame.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0 && task.Callback == 0x60E6D0));
        if (age == 2)
        {
            Assert.Single(result.Frame.Effect.Pools[1].Slots, task => (task.Flags & 1) != 0 && task.Callback == 0x60E820);
        }
        Assert.Equal(AfterDraws(frame.Random, randomDraws), result.Frame.Random);
    }

    [Fact]
    public void Director_pause_precedes_anchor_publication_and_contacts_finish_at_original_ages()
    {
        NativeBattleGatlingGun150Frame frame = Frame(MainCallback);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, 30));
        List<NativeBattleGatlingGun150Operation> operations = [];
        NativeBattleGatlingGun150Inputs paused = Inputs(operations) with { GraphicsFlags = 0x201, ReadBone502170 = (_, _, _, _) => throw new InvalidOperationException() };
        Assert.Equal(frame, AdvanceDirector(frame, ChildBank, Resources(), paused).Frame);
        var active = AdvanceDirector(frame, ChildBank, Resources(), Inputs(operations));
        Assert.Equal(0, active.Result);
        Assert.Equal(3, Assert.IsType<NativeBattleGatlingGun150ContactsAll>(Assert.Single(operations)).HeaderIndex);
        frame = Write(active.Frame, ChildBank, Read(active.Frame, ChildBank).WriteWord(12, 36));
        var finished = AdvanceDirector(frame, ChildBank, Resources(), Inputs(operations));
        Assert.Equal(2, finished.Result);
        Assert.Equal((ushort)37, Read(finished.Frame, ChildBank).ReadWord(12));
        Assert.Single(operations);
    }

    [Fact]
    public void Director_target_bursts_follow_flash_draws_and_use_signed_original_bounds()
    {
        NativeBattleGatlingGun150Frame frame = Frame(MainCallback);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, 11));
        var result = AdvanceDirector(frame, ChildBank, Resources(), Inputs([]));
        NativeBattleModulePoolTask[] targets = result.Frame.Effect.Pools[1].Slots.Skip(2).Take(7).ToArray();
        Assert.All(targets, task => Assert.Equal(0x60EFF0u, task.Callback));
        Assert.Equal(new short[] { -127, -299, -300, -459, -510, -657, -783 }, targets.Select(task => Signed(task, 18)));
        Assert.All(targets, task =>
        {
            Assert.Equal((short)-501, Signed(task, 16));
            Assert.Equal((short)901, Signed(task, 20));
        });
        Assert.Equal(AfterDraws(frame.Random, 9), result.Frame.Random);
    }

    [Fact]
    public void Delayed_birth_preserves_W_and_copies_actual_mesh_position_to_sprite_and_masked_slot_task()
    {
        NativeBattleGatlingGun150Frame frame = Frame(0x60EAE0);
        NativeBattleModulePoolTask source = Read(frame, ChildBank).WriteWord(14, 2).WriteWord(16, 100).WriteWord(18, 50).WriteWord(20, 300).WriteWord(22, 0xCAFE).WriteWord(24, 5);
        frame = Write(frame, ChildBank, source);
        NativeBattleGatlingGun150Inputs inputs = Inputs([]);
        var paused = AdvanceSpawner(frame, ChildBank, inputs with { GraphicsFlags = 1 });
        Assert.Equal(frame, paused.Frame);
        var waited = AdvanceSpawner(frame, ChildBank, inputs);
        Assert.Equal((ushort)1, Read(waited.Frame, ChildBank).ReadWord(14));
        Assert.Equal(frame.Random, waited.Frame.Random);
        frame = Write(waited.Frame, ChildBank, Read(waited.Frame, ChildBank).WriteWord(14, 0));
        var result = AdvanceSpawner(frame, ChildBank, inputs);
        Assert.Equal(2, result.Result);
        NativeBattleModulePoolTask mesh = Read(result.Frame, ChildBank + 36);
        NativeBattleModulePoolTask sprite = Read(result.Frame, ChildBank + 72);
        NativeBattleModulePoolTask slots = Read(result.Frame, ChildBank + 108);
        Assert.Equal(0x60EBD0u, mesh.Callback);
        Assert.Equal(0x60ED10u, sprite.Callback);
        Assert.Equal(0x60EDA0u, slots.Callback);
        Assert.Equal((ushort)1024, mesh.ReadWord(28));
        Assert.Equal((ushort)256, mesh.ReadWord(30));
        Assert.Equal((short)-200, Signed(sprite, 18));
        Assert.Equal((ushort)32, slots.ReadWord(14));
        Assert.All(new[] { mesh, sprite, slots }, task => Assert.Equal((ushort)0xCAFE, task.ReadWord(22)));
        Assert.Equal(TaskView(mesh, 16, 8).Read(0, 8), TaskView(slots, 16, 8).Read(0, 8));
        Assert.Equal(AfterDraws(frame.Random, 2), result.Frame.Random);
    }

    [Fact]
    public void Target_burst_uses_six_random_draws_and_original_three_callbacks()
    {
        NativeBattleGatlingGun150Frame frame = Frame(0x60EFF0);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(16, 100).WriteWord(18, 50).WriteWord(20, 300).WriteWord(24, 7));
        var result = AdvanceSpawner(frame, ChildBank, Inputs([]));
        Assert.Equal(2, result.Result);
        Assert.Equal(new uint[] { 0x60F1C0, 0x60ED10, 0x60F330 }, result.Frame.Effect.Pools[1].Slots.Skip(1).Take(3).Select(task => task.Callback));
        Assert.Equal(AfterDraws(frame.Random, 6), result.Frame.Random);
        NativeBattleModulePoolTask mesh = Read(result.Frame, ChildBank + 36);
        Assert.Equal((ushort)2148, mesh.ReadWord(24));
        Assert.Equal((ushort)(mesh.ReadWord(28) / 4), mesh.ReadWord(30));
        Assert.Equal((ushort)128, Read(result.Frame, ChildBank + 108).ReadWord(14));
    }

    [Theory]
    [InlineData(0, 0x2451E80u)]
    [InlineData(1, 0x2459E80u)]
    public void Drained_root_selects_bank_then_transfers_current_task_bytes_to_inactive_owners_and_restores(ushort age, uint cursor)
    {
        NativeBattleGatlingGun150Frame frame = Frame(MainCallback);
        NativeBattleEffectTaskPool children = frame.Effect.Pools[1];
        children = children with { Slots = children.Slots.SetItem(0, children.Slots[0] with { Flags = 0 }), Head = null, Tail = null };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children) } };
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, age));
        NativeBattleGatlingGun150Frame result = Advance(frame, Resources(), Inputs([]));
        Assert.Equal(cursor, result.State.ReadDword(CursorGlobal));
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.Empty(result.Effect.Pools);
        Assert.Equal((ushort)(age + 1), BitConverter.ToUInt16(result.State.RootBankStorage!.Read(12, 2)));
        NativeBattleGatlingGun150Frame restored = JsonSerializer.Deserialize<NativeBattleGatlingGun150Frame>(JsonSerializer.Serialize(result))!;
        Assert.Equal(cursor, restored.State.ReadDword(CursorGlobal));
        Assert.Equal(result.State.ChildBankStorage!.Read(0, 36), restored.State.ChildBankStorage!.Read(0, 36));
    }

    private static NativeBattleGatlingGun150Frame Frame(uint callback)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, RootCount);
        (root, _) = root.Append(RootCallback);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (children, _) = children.Append(callback);
        NativeBattleOriginalEffectState effect = new(150, 149, new(150, Entry, Initializer), new(3, 0, 1, 3, 0, 0), RootDescriptor) { Pools = [root, children] };
        NativeBattleGatlingGun150State state = NativeBattleGatlingGun150State.FromColdProcess().WriteDword(CasterGlobal, 3).WriteDword(FirstTargetGlobal, 0) with { RootBankStorage = null, ChildBankStorage = null };
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, new(55), state);
    }

    private static NativeCrtRandomState AfterDraws(NativeCrtRandomState state, int count)
    {
        for (int index = 0; index < count; index++)
        {
            state = state.Draw(out _);
        }
        return state;
    }

    private static NativeBattleGatlingGun150Resources Resources() => new(new(0, MeshAAddress, []), new(0, MeshBAddress, []), new(0, MeshCAddress, []), new(0, SpriteAAddress, 0, 0, []), new(0, SpriteBAddress, 0, 0, []), new(0, SpriteCAddress, 0, 0, []), new("unused", []), 0x61AD6);
    private static NativeBattleEffectArena Point(short x, short y, short z) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z));
    private static NativeBattleGatlingGun150Inputs Inputs(List<NativeBattleGatlingGun150Operation> operations) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => throw new InvalidOperationException(), (_, actor) => new(actor, 3) { Rotation14 = 100, GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770(), GeometryBounds52 = new(new(0, -800, 0), new(0, 0, 0)) }, (frame, actor, selector, offset) =>
    {
        Assert.Equal(3, actor);
        Assert.Equal(0xF0, selector);
        Assert.Equal(0, offset);
        return new(frame, Point(100, 200, 300));
    }, (frame, actor) =>
    {
        Assert.Equal(0, actor);
        return new(frame, Point(-501, 400, 901));
    }, (frame, operation) =>
    {
        operations.Add(operation);
        return frame;
    });
}
