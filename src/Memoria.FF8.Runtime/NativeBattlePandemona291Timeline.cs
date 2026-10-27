using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePandemona291TimelineServices(Func<NativeBattlePandemona291Frame, NativeBattleEffectFileRequest, NativeBattlePandemona291Frame> QueueFile, Func<NativeBattlePandemona291Frame, uint, NativeBattlePandemona291Frame> PlayOpeningSound, Func<NativeBattlePandemona291Frame, int, int, NativeBattlePandemona291Frame> ApplyOpeningCamera)
{
    public Func<NativeBattlePandemona291Frame, uint, NativeBattlePandemona291Frame>? DrawTrails { get; init; }
    public Func<NativeBattlePandemona291Frame, uint, int, NativeBattlePandemona291Frame>? DrawSurface { get; init; }
    public Func<NativeBattlePandemona291Frame, int, NativeBattlePandemona291Frame>? DrawLayers { get; init; }
    public Func<NativeBattlePandemona291Frame, uint, int, NativeBattlePandemona291Frame>? DrawVortex { get; init; }
    public Func<NativeBattlePandemona291Frame, uint, NativeBattleModulePoolTask, NativeBattlePandemona291ChildStep>? InitializeBody { get; init; }
    public Func<NativeBattlePandemona291Frame, uint, int, NativeBattlePandemona291Frame>? DrawRising { get; init; }
    public Func<NativeBattlePandemona291Frame, uint, NativeBattlePandemona291Frame>? DrawBody { get; init; }
    public Func<NativeBattlePandemona291Frame, uint, int, NativeBattlePandemona291Frame>? DrawLate { get; init; }
    public Func<NativeBattlePandemona291Frame, uint, int, NativeBattlePandemona291Frame>? DrawTargets { get; init; }
    public Func<NativeBattlePandemona291Frame, short, NativeBattlePandemona291Frame>? LoadEndingTim { get; init; }
    public Func<NativeBattlePandemona291Frame, uint, NativeBattlePandemona291Frame>? StopVibration { get; init; }
    public NativeBattlePandemona291CompletionServices? Completion { get; init; }
    public Action<NativeBattlePandemona291Frame>? PlayStream { get; init; }
}

public static class NativeBattlePandemona291Timeline
{
    public static NativeBattlePandemona291ChildStep Advance6ED900(NativeBattlePandemona291Frame frame, uint address, NativeBattleModulePoolTask task, NativeBattlePandemona291TimelineServices services)
    {
        int age = unchecked((short)task.ReadWord(0xC));
        if (age < 0)
        {
            throw new NotSupportedException($"Native Pandemona6ED900 age{age} requires its next scene contract.");
        }
        NativeBattlePandemona291State state = frame.Memory.Pandemona291 ?? throw new NotSupportedException("Native Pandemona timeline requires its current module owner.");
        frame = frame with { Memory = frame.Memory with { Pandemona291 = state with { Flash25562F0 = unchecked((uint)(age < 8 ? age * 350 : 2800)) } } };
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona timeline requires its current arena.");
        int start = checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress));
        if (age is >= 10 and < 40)
        {
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
            if (age == 10)
            {
                NativeBattleGfAnimationSource source = frame.Effect.Binding.Pandemona291Resources?.SurfaceProgram ?? throw new NotSupportedException("Native Pandemona requires its original surface programme.");
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(arena, start + 0x1F0, 0x12C);
                workspace = workspace.Initialize7016B0(source, 0x12C);
                arena = workspace.CopyTo(arena, start + 0x1F0);
            }
            else if (age >= 18)
            {
                frame = (services.DrawSurface ?? throw new NotSupportedException("Native Pandemona requires its full surface callback6F06F0."))(frame, address, age);
                arena = frame.Memory.Arena!;
            }
            uint pause = state.Pause2556258 ?? throw new NotSupportedException("Native Pandemona requires its current pause DWORD.");
            if (pause == 0)
            {
                int trailCenter = state.Center25562E8 ?? throw new NotSupportedException("Native Pandemona requires its current trail center.");
                NativeBattlePandemona291TrailAllocation spawned = NativeBattlePandemona291Trails.SpawnOpening(arena, frame.Random, address, trailCenter);
                arena = NativeBattlePandemona291Trails.Advance6F19C0(spawned.Arena, address);
                frame = frame with { Random = spawned.Random };
            }
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
        }
        if (age is >= 25 and < 40)
        {
            frame = (services.DrawLayers ?? throw new NotSupportedException("Native Pandemona requires its animated layers6F2A30."))(frame, age);
            uint pause = frame.Memory.Pandemona291!.Pause2556258 ?? throw new NotSupportedException("Native Pandemona requires its current pause DWORD.");
            if (pause == 0)
            {
                frame = NativeBattlePandemona291LayerParticles.Spawn6EDBCF(frame, age);
            }
            arena = frame.Memory.Arena!;
        }
        if (age is >= 40 and < 100)
        {
            frame = (services.DrawVortex ?? throw new NotSupportedException("Native Pandemona requires its complete vortex scene."))(frame, address, age);
            arena = frame.Memory.Arena!;
        }
        if (age is >= 100 and < 154)
        {
            frame = (services.DrawRising ?? throw new NotSupportedException("Native Pandemona requires its complete rising scene."))(frame, address, age);
            arena = frame.Memory.Arena!;
        }
        if (age >= 154)
        {
            frame = (services.DrawLate ?? throw new NotSupportedException("Native Pandemona requires its full late scene."))(frame, address, age);
            arena = frame.Memory.Arena!;
        }
        if (age is >= 144 and < 174)
        {
            if (age == 144)
            {
                NativeBattleGfAnimationSource source = frame.Effect.Binding.Pandemona291Resources?.RisingProgram ?? throw new NotSupportedException("Native Pandemona requires its original rising surface programme.");
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(arena, start + 0x1F0, 0xE0);
                workspace = workspace.Initialize7016B0(source, 0xE0);
                arena = workspace.CopyTo(arena, start + 0x1F0);
                frame = frame with { Memory = frame.Memory with { Arena = arena } };
            }
            frame = (services.DrawSurface ?? throw new NotSupportedException("Native Pandemona requires its full rising surface callback."))(frame, address, age);
            arena = frame.Memory.Arena!;
        }
        bool activeTrails = false;
        for (int index = 0; index < 128; index++)
        {
            int record = checked(start + 0x42C + index * 24);
            if (BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(record + 6, 2)) != 0)
            {
                activeTrails = true;
                break;
            }
        }
        if (activeTrails)
        {
            frame = (services.DrawTrails ?? throw new NotSupportedException("Native Pandemona requires its complete active trail transform and renderer."))(frame, address);
        }
        if (age >= 224)
        {
            frame = (services.DrawTargets ?? throw new NotSupportedException("Native Pandemona requires its current scaled targets."))(frame, address, age);
        }
        if (age is >= 100 and < 377 && age is not (>= 224 and < 254))
        {
            frame = (services.DrawBody ?? throw new NotSupportedException("Native Pandemona requires its complete body renderer6F0AE0."))(frame, address);
        }
        uint buffer = state.ModelBuffer25562F4 ?? throw new NotSupportedException("Native Pandemona requires its model buffer publication.");
        if (age == 0)
        {
            NativeBattleEffectMemory memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, 719, unchecked(buffer + 0x2321C), 2);
            memory = NativeBattleEffectIo.Queue5341D0(memory, 720, unchecked(buffer + 0x2321C), 1);
            memory = NativeBattleEffectIo.Queue5341D0(memory, 721, unchecked(buffer + 0x2321C), 3);
            memory = NativeBattleEffectIo.Queue5341D0(memory, 722, unchecked(buffer + 0x31220), 5);
            frame = frame with { Memory = memory };
        }
        if (age is 99 or 183 or 253)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Poll534270(frame.Memory);
            frame = frame with { Memory = poll.Memory };
            if (poll.Busy)
            {
                if (poll.Request is { } pending)
                {
                    frame = services.QueueFile(frame, pending);
                }
                return new(frame, task, 0);
            }
            if (age == 99)
            {
                frame = frame with { Memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, 723, unchecked(buffer + 0x2321C), 3) };
                frame = frame with { Memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, 724, unchecked(buffer + 0x23220), 5) };
            }
        }
        if (age == 224)
        {
            frame = frame with { Memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, 725, unchecked(buffer + 0x2321C), 3) };
            frame = frame with { Memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, 726, unchecked(buffer + 0x23220), 5) };
        }
        if (age == 480)
        {
            if (frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x2556268).Head is not null)
            {
                return new(frame, task, 0);
            }
            foreach (short fileId in new short[] { 360, 361, 362 })
            {
                frame = frame with { Memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, fileId, unchecked(buffer + 0x31220), 1) };
            }
            frame = frame with { Memory = frame.Memory with { Pandemona291 = frame.Memory.Pandemona291! with { StaticLoadState13BBBE8 = 0 } } };
        }
        NativeBattleEffectIoAdvance advance = NativeBattleEffectIo.Advance534210(frame.Memory);
        frame = frame with { Memory = advance.Memory };
        if (advance.Request is { } request)
        {
            frame = services.QueueFile(frame, request);
        }
        uint load = frame.Memory.Pandemona291!.StaticLoadState13BBBE8 ?? throw new NotSupportedException("Native Pandemona requires its current static load-state DWORD.");
        if (load is 0 or 1 or 2)
        {
            frame = (services.LoadEndingTim ?? throw new NotSupportedException("Native Pandemona requires its direct48D0A0 ending TIM load."))(frame, unchecked((short)(360 + load)));
            frame = frame with { Memory = frame.Memory with { Pandemona291 = frame.Memory.Pandemona291! with { StaticLoadState13BBBE8 = load + 1 } } };
        }
        if (age is >= 130 and < 377 && age is not (>= 224 and < 254))
        {
            NativeBattlePandemona291ChildStep posed = NativeBattlePandemona291Body.Advance6FBDB0(frame, address, task);
            frame = posed.Frame;
            task = posed.Task;
        }
        if (age == 0)
        {
            uint sound = frame.Effect.Binding.Pandemona291Resources?.OpeningSound ?? throw new NotSupportedException("Native Pandemona requires its original opening sound descriptor.");
            frame = services.PlayOpeningSound(frame, sound);
        }
        int center = frame.Memory.Pandemona291!.Center25562E8 ?? throw new NotSupportedException("Native Pandemona requires its current camera center.");
        frame = services.ApplyOpeningCamera(frame, center, age);
        if (age == 99)
        {
            NativeBattlePandemona291ChildStep initialized = (services.InitializeBody ?? throw new NotSupportedException("Native Pandemona requires its complete model initialization6F0A70."))(frame, address, task);
            frame = initialized.Frame;
            task = initialized.Task;
        }
        if (age == 107)
        {
            (services.PlayStream ?? throw new NotSupportedException("Native Pandemona requires its current summon-stream playback."))(frame);
        }
        if (age == 476)
        {
            uint handle = BinaryPrimitives.ReadUInt32LittleEndian(frame.Memory.Arena!.Read(start + 0x10, 4));
            frame = (services.StopVibration ?? throw new NotSupportedException("Native Pandemona requires its current vibration handle."))(frame, handle);
        }
        ushort next = unchecked((ushort)(task.ReadWord(0xC) + 1));
        task = task.WriteWord(0xC, next);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(start + 0xC, BitConverter.GetBytes(next)) } };
        if (unchecked((short)next) <= 480)
        {
            return new(frame, task, 0);
        }
        return NativeBattlePandemona291Completion.AfterCounter6F0626(frame, address, task, services.Completion ?? throw new NotSupportedException("Native Pandemona requires its complete ending IO and target restoration."));
    }

    public static NativeBattleCameraState AdvanceOpeningCamera(NativeBattleCameraState camera, int center, int age)
    {
        if (age is < 0 or >= 154)
        {
            throw new ArgumentOutOfRangeException(nameof(age));
        }
        if (age is >= 100 and < 123)
        {
            return camera;
        }
        if (age >= 123)
        {
            NativeBattleCameraPose retained = camera.CurrentPose ?? throw new NotSupportedException("Native Pandemona rising camera requires its current eye and target.");
            NativeBattleCameraPose rising = retained with { FocalLength = camera.FocalLength, Roll = camera.Roll };
            if (age == 123)
            {
                rising = rising with { Eye = new(0x5F9, unchecked((short)0xEEEA), unchecked((short)(center + 0xA08))), Target = new(unchecked((short)0xFB6E), unchecked((short)0xEA57), unchecked((short)(center - 0x484))) };
            }
            if (age is >= 124 and < 154)
            {
                rising = NativeBattleCarbuncle278Camera.Orbit683D10(rising, 0, 0x1054);
                rising = rising with { Target = rising.Target with { Y = unchecked((short)(rising.Target.Y + 130)) } };
            }
            if (age == 153)
            {
                rising = rising with { Eye = new(0x76B, unchecked((short)0xF7B7), unchecked((short)(center - 0x557))), Target = new(unchecked((short)0xFDEC), unchecked((short)0xF768), unchecked((short)(center - 0xEA))) };
            }
            camera = camera.WriteEffectGlobals(rising);
            if (age is >= 144 and < 150)
            {
                int elapsed = age - 144;
                short shake = unchecked((short)(unchecked(100 * NativeBattleCameraMath.Sine(unchecked(elapsed * 0x155 + 0xAA))) >> 12));
                if ((elapsed & 1) != 0)
                {
                    shake = unchecked((short)-shake);
                }
                camera = camera with { ViewTranslationScratch = (camera.ViewTranslationScratch ?? throw new NotSupportedException("Native Pandemona rising camera requires its retained view scratch.")) with { Y = shake } };
            }
            return camera;
        }
        if (age >= 40)
        {
            NativeBattleCameraPose current = camera.CurrentPose ?? throw new NotSupportedException("Native Pandemona vortex camera requires its current eye and target WORDs.");
            return camera.WriteEffectGlobals(NativeBattleCarbuncle278Camera.Orbit683D10(current with { FocalLength = camera.FocalLength, Roll = camera.Roll }, 16, 4096));
        }
        NativeBattleCameraPose pose = age == 0 ? new(new(0, -1200, unchecked((short)(center - 200))), new(0, -900, unchecked((short)(center - 2000))), 240, 0) : (camera.CurrentPose ?? throw new NotSupportedException("Native Pandemona requires its current canonical eye and target WORDs.")) with { FocalLength = camera.FocalLength, Roll = camera.Roll };
        pose = NativeBattleCarbuncle278Camera.Orbit683D10(pose, 0, unchecked((552 - age) << 3));
        pose = pose with { Eye = pose.Eye with { Y = -1200 } };
        if (age == 39)
        {
            pose = FinalOpeningCamera39(center, camera.Roll);
        }
        return camera.WriteEffectGlobals(pose);
    }
    public static NativeBattleCameraPose FinalBodyCamera99(int center, short? focalLength, short roll)
    {
        return new(new(0, -1674, unchecked((short)(center - 2358))), new(0, -4511, unchecked((short)center)), focalLength, roll);
    }

    public static NativeBattleCameraPose FinalOpeningCamera39(int center, short roll)
    {
        return new(new(-4096, -1208, unchecked((short)(center - 9110))), new(0, -2984, unchecked((short)(center - 2112))), 288, roll);
    }

}
