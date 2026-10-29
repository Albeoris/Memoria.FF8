using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco99MovingProgramTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 18000, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_moving_programs_publish_real_particles_and_cold_next_without_losing_physical_tail(bool small)
    {
        using GameAssets game = Installed();
        NativeBattleGfAnimationSource source = Assert.Single(game.ReadNativeChoco99ProgramResources().Loaded, item => item.FileId == 476).ProgramSource;
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x256B9D8, 0x256B000, 0x548, 1), NativeBattleEffectTaskPool.Initialize(NativeBattleChoco99Particles.AuxiliaryDescriptor, 0x2566000, 0xB0, 64)]);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, small ? 0x720610u : 0x71FFB0u, 0x548);
        uint address = appended.Address;
        NativeBattleModulePoolTask task = appended.Hierarchy.Read(address).WriteDword(0x0C, 0x1D99A78).WriteDword(0x74, source.Address).WriteDword(0x78, 0x2C).WriteWord(0x22, 0x1234).WriteWord(0x6C, 3);
        Saved current = new(task, Enumerable.Repeat((byte)0xA5, 0x760).ToImmutableArray(), appended.Hierarchy.Write(address, task), new(123));
        int accepted = 0;
        int trails = 0;
        int radials = 0;
        bool testedTerminalRadial = false;
        bool testedFrozenMotion = false;
        int pauseCalls = 0;
        for (int call = 0; call < 50; call++)
        {
            int phase = current.Task.ReadByte(0x29);
            short mode = small ? (short)(call >= 8 ? 8 : 0) : (short)(phase == 3 && current.Task.ReadWord(0x84) >= 8 ? 10 : 0);
            Saved restored = NativeBattleSnapshotJson.Deserialize<Saved>(NativeBattleSnapshotJson.Serialize(current))!;
            StepResult next = Step(current, mode, 0);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(Step(restored, mode, 0)));
            Assert.Equal((ushort)0x1234, next.Draw.Draw.Task.ReadWord(0x22));
            Assert.Equal((ushort)3, next.Draw.Draw.Task.ReadWord(0x6C));
            if (phase == 1 || !small && phase == 3)
            {
                Assert.Equal(current.Task.ReadDword(0x1C), next.Draw.Draw.Task.ReadDword(0x540));
                Assert.Equal(current.Task.ReadDword(0x20), next.Draw.Draw.Task.ReadDword(0x544));
                if (!testedFrozenMotion)
                {
                    StepResult frozen = Step(current, 0, 1);
                    Assert.Equal(current.Task.ReadDword(0x98), frozen.Draw.Draw.Task.ReadDword(0x98));
                    Assert.NotEqual(current.Task.ReadWord(0x1C), frozen.Draw.Draw.Task.ReadWord(0x1C));
                    Assert.NotEmpty(frozen.Emissions);
                    testedFrozenMotion = true;
                }
            }
            if (!small && phase == 2)
            {
                pauseCalls++;
                Assert.Null(next.Draw.Draw.Remaining);
                Assert.Empty(next.Emissions);
                Assert.Equal(current.Task.ReadDword(0x98), next.Draw.Draw.Task.ReadDword(0x98));
                Assert.Equal(current.Task.ReadDword(0x540), next.Draw.Draw.Task.ReadDword(0x540));
                if (next.Draw.Draw.Task.ReadByte(0x29) == 3)
                {
                    Assert.Equal((ushort)1920, next.Draw.Draw.Task.ReadWord(0x1C));
                    Assert.Equal(unchecked((ushort)-1024), next.Draw.Draw.Task.ReadWord(0x1E));
                }
            }
            if (!small && phase == 3 && current.Task.ReadWord(0x84) == 3)
            {
                StepResult terminal = Step(current, 10, 0);
                Emission emitted = Assert.Single(terminal.Emissions);
                Assert.Equal(NativeBattleChoco99EmissionKind.Radial, emitted.Kind);
                Assert.Equal(current.Task.ReadDword(0x1C), emitted.Position0);
                Assert.Equal(current.Task.ReadDword(0x20), emitted.Position1);
                Assert.Null(terminal.Draw.Draw.Remaining);
                Assert.Empty(terminal.Draw.Draw.Packets.Attempts);
                Assert.Equal(current.Task.ReadDword(0x98), terminal.Draw.Draw.Task.ReadDword(0x98));
                Assert.False(terminal.Draw.Draw.ReadyToComplete);
                testedTerminalRadial = true;
            }
            foreach (Emission emission in next.Emissions)
            {
                Assert.Equal((short)(small ? 8 : 10), emission.Mode);
                if (emission.Kind == NativeBattleChoco99EmissionKind.Trail)
                {
                    trails++;
                    Assert.Equal(next.Draw.Draw.Task.ReadDword(0x1C), emission.Position0);
                    Assert.Equal(next.Draw.Draw.Task.ReadDword(0x20), emission.Position1);
                }
                else
                {
                    radials++;
                    Assert.Equal(current.Task.ReadDword(0x1C), emission.Position0);
                    Assert.Equal(current.Task.ReadDword(0x20), emission.Position1);
                }
            }
            accepted += next.Draw.Draw.Packets.Attempts.Count(attempt => attempt.Primitive is not null);
            current = new(next.Draw.Draw.Task, next.Draw.Draw.MorphScratch, next.Hierarchy, next.Random);
            if ((current.Task.ReadByte(0x26) & 1) != 0)
            {
                Assert.False(next.Draw.Draw.ReadyToComplete);
                Assert.Equal(trails + radials, current.Task.ReadByte(0x28));
                Assert.True(Step(current with { Task = current.Task.WriteByte(0x28, 0) }, mode, 0).Draw.Draw.ReadyToComplete);
                break;
            }
        }
        Assert.True(accepted > 0 && trails > 0 && testedFrozenMotion);
        Assert.True((current.Task.ReadByte(0x26) & 1) != 0);
        Assert.Equal(small ? 0 : 5, radials);
        Assert.Equal(small ? 0 : 10, pauseCalls);
        Assert.Equal(!small, testedTerminalRadial);

        StepResult Step(Saved saved, short mode, uint frozen)
        {
            NativeBattleEffectHierarchy nextHierarchy = saved.Hierarchy;
            NativeCrtRandomState random = saved.Random;
            List<Emission> emissions = [];
            NativeBattleChoco99ProgramDraw draw = small ? NativeBattleChoco99ProgramDrawing.AdvanceSmall720610(saved.Task, source, mode, saved.Scratch, View, Projection, colors, frozen, Emit) : NativeBattleChoco99ProgramDrawing.AdvanceMoving71FFB0(saved.Task, source, mode, saved.Scratch, View, Projection, colors, frozen, Emit);
            nextHierarchy = nextHierarchy.Write(address, draw.Draw.Task);
            return new(draw, nextHierarchy, random, emissions.ToImmutableArray());

            NativeBattleModulePoolTask Emit(NativeBattleModulePoolTask parent, NativeBattleChoco99EmissionKind kind, short threshold)
            {
                emissions.Add(new(kind, threshold, parent.ReadDword(0x1C), parent.ReadDword(0x20)));
                nextHierarchy = nextHierarchy.Write(address, parent);
                NativeBattleChoco99ParticleEmission emitted = kind == NativeBattleChoco99EmissionKind.Trail ? NativeBattleChoco99Particles.EmitTrail7209B0(nextHierarchy, address, (ushort)threshold, random, SelectActors) : NativeBattleChoco99Particles.EmitRadial720D70(nextHierarchy, address, (ushort)threshold, random, SelectActors);
                nextHierarchy = emitted.Hierarchy;
                random = emitted.Random;
                return nextHierarchy.Read(address);
            }
        }

        static NativeBattleEffectTaskActors SelectActors(uint parameterAddress, sbyte headerOffset, sbyte contactOffset)
        {
            Assert.Equal(0x1D99A78u, parameterAddress);
            Assert.Equal((sbyte)0, headerOffset);
            Assert.Equal((sbyte)0, contactOffset);
            return new(0, 3);
        }
    }

    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }

    private sealed record Saved(NativeBattleModulePoolTask Task, ImmutableArray<byte> Scratch, NativeBattleEffectHierarchy Hierarchy, NativeCrtRandomState Random);
    private sealed record Emission(NativeBattleChoco99EmissionKind Kind, short Mode, uint Position0, uint Position1);
    private sealed record StepResult(NativeBattleChoco99ProgramDraw Draw, NativeBattleEffectHierarchy Hierarchy, NativeCrtRandomState Random, ImmutableArray<Emission> Emissions);
}
