using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldEffectsTests
{
    public static void Run()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(game))
        {
            Console.WriteLine("SKIP original world particles: configure MEMORIA_FF7_GAME_DIR.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(game);
        Ff7WorldEffectTable table = Ff7PcExecutableData.LoadWorldEffects(installation);
        Check(table.Sequences.Count == 60 && table.Sprites.Count == 145 && table.Sprites.Max(sprite => sprite.Palette) == 25, "Original world effects retain60 sequence entries,145 sprite rows and all26 palettes.");
        Check(table.Sequences[7] == new Ff7WorldEffectSequence(0x13, true) && table.Sprites[0x13] is { Duration: 0x8A, Flags: 5, Palette: 25 }, "Original expansion sprite retains its native duration, mutable dimensions and last palette.");
        byte[] persistent = new byte[1280];
        persistent[0] = 152;
        persistent[29] = 255;
        using Ff7WorldRuntime world = new(installation, persistent, 1, new byte[] { 0, 1, 2 });
        for (int callback = 0; callback < 700 && !world.CanSave; callback++)
        {
            if (world.Process.Dialogue is { AwaitingInput: true, Completed: false } dialogue)
            {
                world.CompleteDialogue(dialogue.RequestId, dialogue.Selected);
            }
            world.Advance();
        }
        Check(world.CanSave, "Original world entry must establish the particle fixture.");
        Ff7WorldProcess ready = world.Process;
        int leader = ready.ControlledModel;
        Ff7WorldEntity actor = ready.Entities.Single(entity => entity.Model == leader) with { Flags = 8, Direction = 1024, TargetDirection = 1024, TurnOffset = 64 };
        ready = ready with { Effects = null, Entities = ready.Entities.Select(entity => entity.Model == leader ? actor : entity).ToArray(), EncounterRate = 0 };
        ImmediateEffects(world, ready, actor, table);
        PhaseAndPool(world, ready, actor, table);
        EmittersAndCold(world, ready, actor, table, installation);
        TerrainProducers(world, ready, actor, table);
        CorruptionAndNativeWidths(world, ready, actor, table);
        Console.WriteLine("PASS original world sprite table, immediate effects, render/update phase, pool quarantine, emitters, gravity, mutable dimensions, terrain producers and cold continuation");
    }

    private static void ImmediateEffects(Ff7WorldRuntime world, Ff7WorldProcess ready, Ff7WorldEntity actor, Ff7WorldEffectTable table)
    {
        foreach (int type in new[] { 1, 2, 3, 4, 5, 6, 20, 21, 22, 23 })
        {
            world.Restore(ready);
            world.ReadSpecial(16, actor.Model);
            int random = world.Process.BattleState!.RandomIndex;
            Command(world, actor.Model, WorldOperation.Effect, [type]);
            Ff7WorldEffectsState state = world.Process.Effects!;
            Check(state.Particles.Length == (type == 1 ? 8 : 1), "Every recognized immediate world effect must emit its original particle count.");
            Check(world.Process.BattleState!.RandomIndex == (random + (type is 2 or 3 ? 1 : 0)) % 0x209, "Immediate effect RNG belongs only to native randomized debris and smoke producers.");
            if (type == 4)
            {
                Check(state.Widths[table.Sequences[57].FirstSprite] == 11 && state.Particles[0].X == actor.PreviousX + 500 && state.Particles[0].Z == actor.PreviousZ - 30, "Type4 resets its shared11-pixel dimensions and rotates its original previous-position offset with model direction.");
            }
            if (type == 6)
            {
                Check(state.Widths[0x13] == 16 && state.Heights[0x13] == 16 && state.Particles[0] is { VelocityY: 50, GroundAligned: true, Angle: 2048 }, "Type6 selects original ground-aligned expansion, neutral heading and mutable16-pixel dimensions.");
            }
            if (type is 20 or 21 or 22 or 23)
            {
                Check(state.Particles[0].Sprite == table.Sequences[type].FirstSprite && state.Particles[0].Y == (actor.PreviousY ?? actor.Y) + 600 && state.Particles[0].X == actor.PreviousX - 200, "Immediate labels retain their original fixed-view height and heading-relative offset.");
            }
        }
    }

    private static void PhaseAndPool(Ff7WorldRuntime world, Ff7WorldProcess ready, Ff7WorldEntity actor, Ff7WorldEffectTable table)
    {
        byte[] widths = table.Sprites.Select(sprite => (byte)sprite.Width).ToArray();
        byte[] heights = table.Sprites.Select(sprite => (byte)sprite.Height).ToArray();
        Ff7WorldEffectParticle particle = new(1, actor.X, actor.Y, actor.Z, 3, 20, -4, 0, 0x13, 10, true, 5, 128, false, 0x80808080);
        widths[0x13] = 3;
        heights[0x13] = 5;
        Ff7WorldEffectsState effects = new([particle, particle with { Id = 2 }], new Ff7WorldEffectEmitter?[2], widths, heights, NextId: 3, Counter: 12);
        world.Restore(ready with { Effects = effects });
        world.Advance();
        Ff7WorldEffectsState advanced = world.Process.Effects!;
        Check(advanced.DisplayParticles is { Length: 2 } && advanced.DisplayParticles[0] == particle && advanced.DisplayWidths![0x13] == 3 && advanced.DisplayHeights![0x13] == 5, "The committed display frame retains native pre-integration particles and shared sprite dimensions.");
        Check(advanced.Particles[0] is { VelocityY: 15, Intensity: 116, Remaining: 9 } moved && moved.X == particle.X + 3 && moved.Y == particle.Y + 20 && moved.Z == particle.Z - 4, "Native effect integration moves with old velocity before subtracting byte gravity and applying fade.");
        Check(advanced.Widths[0x13] == 37 && advanced.Heights[0x13] == 37 && advanced.Counter == 13, "Two expansion particles mutate the same original sprite dimensions in linked-list order and increment the callback counter once.");
        world.Restore(ready with { Effects = effects with { Particles = [particle with { Sprite = 0x7C, Remaining = 2 }, particle with { Id = 2, Sprite = 10, Remaining = 2 }] } });
        world.Advance();
        Check(world.Process.Effects!.Particles[0] is { Color: 0x807F7F7F, Intensity: 127 } && world.Process.Effects.Particles[1] is { Color: 0x7F000000, Intensity: 127 }, "Native flags2 preserves the special7C RGB fade while ordinary footprints decrement packed alpha with black RGB.");

        Ff7WorldEffectParticle[] terminal = Enumerable.Range(1, 32).Select(id => particle with { Id = id, Sprite = 4, Remaining = 1 }).ToArray();
        world.Restore(ready with { Effects = effects with { Particles = terminal, NextId = 33, Emitters = [new(actor.Model, 4, 1), null] } });
        Command(world, actor.Model, WorldOperation.Effect, [1]);
        Check(world.Process.Effects!.Particles.Length == 32 && world.Process.Effects.NextId == 33, "Native32-slot exhaustion drops new allocations without consuming identities.");
        world.Advance();
        Check(world.Process.Effects is { Particles.Length: 0, RetiredCount: 32, DisplayParticles.Length: 32 }, "Retiring particles remain visible this callback and their slots stay quarantined from scheduled emitters.");
        Command(world, actor.Model, WorldOperation.Effect, [4]);
        Check(world.Process.Effects!.Particles.Length == 0, "Scripts before the next refresh cannot prematurely reuse its quarantine list.");
        world.Advance();
        Check(world.Process.Effects is { Particles.Length: 1, RetiredCount: 0, DisplayParticles.Length: 0 }, "The next refresh releases retired slots, then schedules its first new particle after display sampling.");
    }

    private static void EmittersAndCold(Ff7WorldRuntime world, Ff7WorldProcess ready, Ff7WorldEntity actor, Ff7WorldEffectTable table, Ff7Installation installation)
    {
        world.Restore(ready);
        Command(world, actor.Model, WorldOperation.EffectStart, [0, 2, 2]);
        Command(world, actor.Model, WorldOperation.EffectStart, [1, 3, 128]);
        Check(world.Process.Effects!.Emitters[1] is { Delay: -128 }, "Scheduled parameters retain native signed-byte storage.");
        using Ff7WorldRuntime cold = new(installation, world.Machine.CopyPersistent(), 1, new byte[] { 0, 1, 2 });
        cold.Restore(Cold(world.Process));
        for (int callback = 0; callback < 6; callback++)
        {
            world.Advance();
            cold.Advance();
            Check(JsonSerializer.Serialize(world.Process) == JsonSerializer.Serialize(cold.Process), "Scheduled effects preserve independent cold RNG, emitter countdown, particle order and display state each callback.");
            Check(world.Process.Effects!.Particles.Length == callback / 2 + 1, "A delay2 emitter uses the native post-decrement cadence and emits on callbacks1,3,5.");
        }
        Command(world, actor.Model, WorldOperation.EffectStop, [0]);
        int count = world.Process.Effects!.Particles.Length;
        world.Advance();
        Check(world.Process.Effects!.Particles.Length == count, "Stopping a scheduled producer preserves its live particles and prevents further emission.");
        world.Restore(world.Process with { Effects = world.Process.Effects with { Particles = [], Emitters = [new(actor.Model, 4, 2, -128), null] } });
        world.Advance();
        Check(world.Process.Effects is { Particles.Length: 1 } wrapped && wrapped.Emitters[0] is { Countdown: 2 }, "Native signed-byte countdown overflow still uses its original pre-decrement predicate before resetting the delay.");

        Ff7WorldEffectParticle looping = new(1, actor.X, actor.Y, actor.Z, 0, 9, 0, 0, table.Sprites.Select((sprite, index) => (sprite, index)).First(value => (value.sprite.Flags & 28) == 24).index, 1, false, 2, 128, false, 0x80808080);
        world.Restore(ready with { Effects = new([looping], new Ff7WorldEffectEmitter?[2], table.Sprites.Select(sprite => (byte)sprite.Width).ToArray(), table.Sprites.Select(sprite => (byte)sprite.Height).ToArray(), NextId: 2) });
        world.Advance();
        Check(world.Process.Effects!.Widths[looping.Sprite] == 11 && world.Process.Effects.Particles[0].Y == looping.Y + 9, "The original looping effect first shrinks a16-pixel width to11 after its current position integration.");
        for (int callback = 0; callback < 20; callback++)
        {
            world.Advance();
        }
        Check(world.Process.Effects is { Particles.Length: 1, RetiredCount: 0 }, "Native flags24 loops backward to its marked sequence start instead of retiring at the next terminal marker.");
    }

    private static void TerrainProducers(Ff7WorldRuntime world, Ff7WorldProcess ready, Ff7WorldEntity actor, Ff7WorldEffectTable table)
    {
        world.Restore(ready);
        Ff7WorldEntity footprint = actor with { Flags = 3, Terrain = 10, PoseFrame = 1 };
        ProduceTerrain(world, footprint);
        Check(world.Process.Effects!.Particles is [{ Angle: 3136 }], "Terrain footprints retain smoothed model direction plus native turn offset in their ground orientation.");
        world.Restore(ready with { Vehicle = new([]) { TinyBroncoFlightEnabled = true } });
        ProduceTerrain(world, actor with { Model = 5, Flags = 3, PoseFrame = 0 });
        Check(world.Process.Effects!.Particles.Length == 2 && world.Process.Effects.Particles.All(particle => particle.Sprite == table.Sequences[9].FirstSprite), "Tiny Bronco wake permission follows the controlled flight flag, independently of its available flight upgrade.");
        world.Restore(ready with { Vehicle = new([]) { BuggyTiltX = 1024 } });
        ProduceTerrain(world, actor with { Model = 6, Flags = 3, Terrain = 0, PoseFrame = 0, Direction = 0, TurnOffset = 0 });
        Check(world.Process.Effects!.Particles.Length == 2 && world.Process.Effects.Particles.All(particle => particle.Y == (actor.PreviousY ?? actor.Y) + 300 && particle.Z == actor.PreviousZ + 100), "Buggy wheel emitters follow the native yaw-then-slope matrix instead of remaining level with the terrain.");
        world.Restore(ready with { Effects = new([], new Ff7WorldEffectEmitter?[2], table.Sprites.Select(sprite => (byte)sprite.Width).ToArray(), table.Sprites.Select(sprite => (byte)sprite.Height).ToArray(), Counter: uint.MaxValue) });
        ProduceTerrain(world, actor with { Model = 13, Flags = 3, PoseFrame = 0 });
        Check(world.Process.Effects!.Particles is [{ } wake] && wake.Sprite == table.Sequences[48].FirstSprite, "Surface wake variants use their persisted unsigned native effect counter through its full32-bit range.");
    }

    private static void ProduceTerrain(Ff7WorldRuntime world, Ff7WorldEntity entity)
    {
        typeof(Ff7WorldRuntime).GetMethod("EmitWorldTerrainEffect", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(world, [entity]);
    }

    private static void CorruptionAndNativeWidths(Ff7WorldRuntime world, Ff7WorldProcess ready, Ff7WorldEntity actor, Ff7WorldEffectTable table)
    {
        Ff7WorldEffectParticle particle = new(1, actor.X, actor.Y, actor.Z, 0, 0, 0, 0, 10, 3, false, 0, 128, false, 0x80808080);
        Ff7WorldEffectsState valid = new([particle], new Ff7WorldEffectEmitter?[2], table.Sprites.Select(sprite => (byte)sprite.Width).ToArray(), table.Sprites.Select(sprite => (byte)sprite.Height).ToArray(), NextId: 2);
        foreach (Ff7WorldEffectParticle invalid in new[] { particle with { VelocityX = 32768 }, particle with { VelocityY = -32769 }, particle with { VelocityZ = 32768 }, particle with { Angle = 32768 }, particle with { Gravity = 256 }, particle with { Remaining = -1 }, particle with { Intensity = 32768 } })
        {
            Reject(world, ready with { Effects = valid with { Particles = [invalid] } });
        }
        Reject(world, ready with { Effects = valid with { Particles = [particle, particle] } });
        Reject(world, ready with { Effects = valid with { DisplayParticles = [particle, particle] } });
        Reject(world, ready with { Effects = valid with { DisplayParticles = [particle with { Angle = -32769 }] } });
        foreach (Ff7WorldEffectEmitter emitter in new[] { new Ff7WorldEffectEmitter(-129, 1, 1), new Ff7WorldEffectEmitter(0, 128, 1), new Ff7WorldEffectEmitter(0, 1, -129), new Ff7WorldEffectEmitter(0, 1, 1, 128) })
        {
            Reject(world, ready with { Effects = valid with { Emitters = [emitter, null] } });
        }
        Ff7WorldEffectParticle boundary = particle with { X = int.MinValue, Y = int.MaxValue, VelocityX = short.MinValue, VelocityY = short.MaxValue, VelocityZ = short.MinValue, Angle = short.MinValue, Gravity = byte.MaxValue, Remaining = byte.MaxValue, Intensity = short.MinValue, Color = uint.MaxValue };
        world.Restore(ready with { Effects = valid with { Particles = [boundary], DisplayParticles = [boundary], Emitters = [new(sbyte.MinValue, sbyte.MaxValue, sbyte.MinValue, sbyte.MaxValue), null] } });
        world.Advance();
        Check(world.Process.Effects!.Particles[0] is { Intensity: short.MaxValue, VelocityY: 32512 }, "Legitimate native signed-short intensity wraps before color processing; maximum byte gravity and signed velocities remain supported.");
        world.Restore(ready with { Effects = valid with { Particles = [particle with { Sprite = 0x13, Intensity = short.MinValue }] } });
        world.Advance();
        Check(world.Process.Effects!.Particles[0] is { Intensity: 32756, Color: 0x80F4F4F4 }, "Native short storage wraps before the negative clamp and RGB assignment narrows independently to a byte.");
    }

    private static void Reject(Ff7WorldRuntime world, Ff7WorldProcess state)
    {
        try
        {
            world.Restore(state);
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("Corrupt world particle or emitter storage was accepted.");
    }

    private static void Command(Ff7WorldRuntime world, int model, WorldOperation operation, int[] arguments)
    {
        world.Execute(new(world.Process.Vm.NextRequestId, model, model, 0, new(operation, arguments)));
    }

    private static Ff7WorldProcess Cold(Ff7WorldProcess state) => JsonSerializer.Deserialize<Ff7WorldProcess>(JsonSerializer.Serialize(state))!;

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
