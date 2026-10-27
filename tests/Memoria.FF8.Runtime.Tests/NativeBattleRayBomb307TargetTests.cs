using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleRayBomb307Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRayBomb307TargetTests
{
    [Fact]
    public void Initial_target_saves_only_masked_flags_and_actual_eight_byte_views_before_pause()
    {
        Fixture fixture = new(0);
        NativeBattleRayBomb307TaskStep step = fixture.Advance(fixture.Frame, 0x201);
        Assert.Equal(0, step.Result);
        Assert.Equal((ushort)0, NativeBattleRayBomb307Controller.Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal((ushort)321, NativeBattleRayBomb307Controller.Read(step.Frame, ChildBank).ReadWord(22));
        Assert.Equal((ushort)0x1000, step.Frame.State.ReadWord(Saved));
        Assert.Equal(fixture.OriginalRotation.Read(0, 6), step.Frame.State.ReadView(Saved + 4, 8).Read(0, 6));
        Assert.Equal(fixture.OriginalPosition.Read(0, 6), step.Frame.State.ReadView(Saved + 12, 8).Read(0, 6));
        Assert.Throws<NotSupportedException>(() => step.Frame.State.ReadView(Saved + 4, 8).Read(6, 2));
        Assert.Throws<NotSupportedException>(() => step.Frame.State.ReadView(Saved + 12, 8).Read(6, 2));
        Assert.Equal(fixture.Frame.Random, step.Frame.Random);
        Assert.Equal(0xABCDEFu, step.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal((ushort)0x3062, fixture.Flags);
        Assert.Equal((short)300, Word(fixture.Rotation, 4));
        Assert.Throws<NotSupportedException>(() => fixture.Position.Read(6, 2));
        Assert.DoesNotContain(fixture.Operations, operation => operation is NativeBattleRayBomb307Contact);
    }

    [Theory]
    [InlineData(24, 16, 48)]
    [InlineData(31, 12, 36)]
    public void Actual_birth_ages_preserve_W_and_tail_and_consume_three_CRT_draws_per_row(ushort age, int count, int calls)
    {
        Fixture fixture = new(age);
        NativeBattleRayBomb307Frame frame = fixture.Seeded(fixture.Frame);
        NativeCrtRandomState expected = frame.Random;
        NativeBattleRayBomb307TaskStep step = fixture.Advance(frame);
        for (int index = 0; index < count; index++)
        {
            expected = expected.Draw(out ushort size);
            expected = expected.Draw(out ushort angle);
            expected = expected.Draw(out ushort speed);
            int at = index * 24;
            Assert.Equal(1u, Dword(step.Frame.State.Particles, at));
            Assert.Equal((short)0, Word(step.Frame.State.Particles, at + 4));
            Assert.Equal((short)(age == 24 ? size % 1024 + 2560 : size % 768 + 2048), Word(step.Frame.State.Particles, at + 6));
            Assert.Equal((short)900, Word(step.Frame.State.Particles, at + 8));
            Assert.Equal((short)0, Word(step.Frame.State.Particles, at + 10));
            Assert.Equal((short)2800, Word(step.Frame.State.Particles, at + 12));
            Assert.Throws<NotSupportedException>(() => step.Frame.State.Particles.Read(at + 14, 2));
            Assert.Throws<NotSupportedException>(() => step.Frame.State.Particles.Read(at + 22, 2));
            int magnitude = age == 24 ? speed % 90 + 120 : speed % 60 + 100;
            Assert.Equal(unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle % 4096) * magnitude) >> 12)), Word(step.Frame.State.Particles, at + 16));
            Assert.Equal((short)0, Word(step.Frame.State.Particles, at + 18));
            Assert.Equal(unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle % 4096) * magnitude) >> 12)), Word(step.Frame.State.Particles, at + 20));
        }
        Assert.Equal(expected, step.Frame.Random);
        Assert.Equal(calls, count * 3);
        Assert.Equal(age == 31 ? 2 : 0, step.Result);
        Assert.Equal(0u, Dword(step.Frame.State.Particles, count * 24));
    }

    [Fact]
    public void First_free_row80_stops_births_without_RNG_but_native_scan_still_reads_its_head()
    {
        Fixture fixture = new(24);
        NativeBattleRayBomb307Frame frame = fixture.Seeded(fixture.Frame);
        for (int index = 0; index < 80; index++)
        {
            frame = frame with { State = frame.State with { Particles = frame.State.Particles.Write(index * 24, BitConverter.GetBytes(1u)) } };
        }
        NativeBattleRayBomb307TaskStep step = fixture.Advance(frame);
        Assert.Equal(frame.Random, step.Frame.Random);
        Assert.Equal(0u, Dword(step.Frame.State.Particles, 80 * 24));
        frame = frame with { State = frame.State with { Particles = frame.State.Particles.MarkUnknown(80 * 24, 4) } };
        Assert.Throws<NotSupportedException>(() => fixture.Advance(frame));
    }

    [Fact]
    public void Late_pause_writes_only_Y_and_retains_rotation_position_padding_age_and_CRT()
    {
        Fixture fixture = new(28);
        NativeBattleRayBomb307Frame frame = fixture.Seeded(fixture.Frame);
        NativeBattleRayBomb307TaskStep step = fixture.Advance(frame, 0x201);
        Assert.Equal(0, step.Result);
        Assert.Equal(frame.Random, step.Frame.Random);
        Assert.Equal((ushort)28, NativeBattleRayBomb307Controller.Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(NativeBattleTestSnapshots.Json(fixture.OriginalRotation), NativeBattleTestSnapshots.Json(fixture.Rotation));
        Assert.Equal((short)1000, Word(fixture.Position, 0));
        Assert.Equal((short)3000, Word(fixture.Position, 4));
        Assert.Equal(unchecked((short)(unchecked(NativeBattleCameraMath.Sine(1024) * 80) >> 12)), Word(fixture.Position, 2));
        Assert.Single(fixture.Operations.OfType<NativeBattleRayBomb307ActorWrite8>());
    }

    [Fact]
    public void New_age32_restores_saved_flags_rotation_position_then_selected_contact_and_JSON_next()
    {
        Fixture first = new(31);
        NativeBattleRayBomb307Frame frame = first.Seeded(first.Frame);
        first.Flags = 0xA062;
        string json = NativeBattleTestSnapshots.Json(frame);
        NativeBattleRayBomb307TaskStep step = first.Advance(frame);
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)0xB042, first.Flags);
        Assert.Equal(first.OriginalRotation.Read(0, 6), first.Rotation.Read(0, 6));
        Assert.Equal(first.OriginalPosition.Read(0, 6), first.Position.Read(0, 6));
        Assert.Throws<NotSupportedException>(() => first.Rotation.Read(6, 2));
        Assert.Throws<NotSupportedException>(() => first.Position.Read(6, 2));
        Assert.Equal(new NativeBattleRayBomb307Contact(2), first.Operations[^1]);
        Assert.Single(first.Operations.OfType<NativeBattleRayBomb307Contact>());
        Fixture second = new(31) { Flags = 0xA062 };
        NativeBattleRayBomb307TaskStep restored = second.Advance(NativeBattleTestSnapshots.RestoreJson<NativeBattleRayBomb307Frame>(json)!);
        Assert.Equal(NativeBattleTestSnapshots.Json(step.Frame), NativeBattleTestSnapshots.Json(restored.Frame));
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Position), NativeBattleTestSnapshots.Json(second.Position));
    }

    [Fact]
    public void Age28_is_not_a_birth_boundary()
    {
        Fixture fixture = new(28);
        NativeBattleRayBomb307Frame frame = fixture.Seeded(fixture.Frame);
        NativeBattleRayBomb307TaskStep step = fixture.Advance(frame);
        Assert.Equal(frame.Random, step.Frame.Random);
        Assert.Equal(0u, Dword(step.Frame.State.Particles, 0));
        Assert.Equal(0, step.Result);
    }

    private const uint Saved = SavedActorsAddress + 60;
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private sealed class Fixture
    {
        public NativeBattleRayBomb307Frame Frame { get; }
        public NativeBattleEffectArena OriginalRotation { get; } = View(100, -200, 300);
        public NativeBattleEffectArena OriginalPosition { get; } = View(1000, 2000, 3000);
        public NativeBattleEffectArena Rotation { get; private set; }
        public NativeBattleEffectArena Position { get; private set; }
        public ushort Flags { get; set; } = 0x3042;
        public List<NativeBattleRayBomb307Operation> Operations { get; } = [];
        public Fixture(ushort age)
        {
            Rotation = NativeBattleTestSnapshots.Independent(OriginalRotation);
            Position = NativeBattleTestSnapshots.Independent(OriginalPosition);
            NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
            (children, int index) = children.Append(0x5A25E0);
            children = NativeBattleTaskFixture.EditPool(children, data => data with { Slots = children.Slots.SetItem(index, children.Slots[index].WriteWord(12, age).WriteWord(14, 3).WriteWord(16, 900).WriteWord(18, 1500).WriteWord(20, 2800).WriteWord(24, 1200).WriteWord(26, 2).WriteWord(32, 640)) });
            NativeBattleOriginalEffectState effect = new(307, 307, new(307, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = [NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2), children] };
            NativeBattleEffectArena particles = NativeBattleEffectArena.Clear().MarkUnknown(0, ParticlesLength);
            for (int row = 0; row < 100; row++)
            {
                particles = particles.Write(row * 24, BitConverter.GetBytes(0u));
            }
            Frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(123), NativeBattleRayBomb307State.FromColdProcess() with { Particles = particles });
        }
        public NativeBattleRayBomb307Frame Seeded(NativeBattleRayBomb307Frame frame) => frame with { State = frame.State.WriteWord(Saved, 0x1000).WriteView(Saved + 4, OriginalRotation, 8).WriteView(Saved + 12, OriginalPosition, 8) };
        public NativeBattleRayBomb307TaskStep Advance(NativeBattleRayBomb307Frame frame, uint graphicsFlags = 0) => NativeBattleRayBomb307Controller.AdvanceTarget(NativeBattleTestSnapshots.Independent(frame), ChildBank, null!, new(NativeBattleActionPacketsState.Fresh(), graphicsFlags, actor => new(actor, Flags), (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (current, _) => new(current with { State = current.State.WriteDword(CursorGlobal, 0xABCDEF) }, new(900, 1500, 2800, 321)), _ => Rotation, _ => Position, Execute));
        private NativeBattleRayBomb307Frame Execute(NativeBattleRayBomb307Frame frame, NativeBattleRayBomb307Operation operation)
        {
            Operations.Add(operation);
            if (operation is NativeBattleRayBomb307ActorFlags flags)
            {
                Flags = (ushort)((Flags & ~flags.ClearMask) | flags.SetMask);
            }
            if (operation is NativeBattleRayBomb307ActorWrite8 write)
            {
                Rotation = write.Rotation8 ?? Rotation;
                Position = write.Position8 ?? Position;
            }
            return frame;
        }
        private static NativeBattleEffectArena View(short x, short y, short z) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z));
    }
}
