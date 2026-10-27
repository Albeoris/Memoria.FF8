using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect251Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect251Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect251ControllerTests
{
    [Fact]
    public void Last_main_context_at163_publishes_actual_composed_y_and178_retains_it()
    {
        NativeBattleEffect251Frame frame = Create(163);
        byte[] mutable = new byte[17224];
        BitConverter.GetBytes(8).CopyTo(mutable, 4);
        BitConverter.GetBytes(95).CopyTo(mutable, 12);
        byte[] stationary = new byte[16];
        BitConverter.GetBytes(8).CopyTo(stationary, 4);
        BitConverter.GetBytes(20).CopyTo(stationary, 12);
        NativeBattleEffect251Resources resources = new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty.Add(0x1432448, new(0x1432448, stationary.ToImmutableArray())).Add(0x1431418, new(0x1431418, stationary.ToImmutableArray())), null!, null!, null!, null!);
        frame = frame with { State = frame.State with { ScriptSourcesBound = true, ScriptStorage = ImmutableDictionary<uint, NativeBattleEffectArena>.Empty.Add(0x1434430, NativeBattleEffectArena.Clear().Write(0, mutable)) }, Staging = NativeBattleTemporaryMemory.PublishView(NativeBattleFileStaging.Unknown(), PauseAddress, NativeBattleEffectArena.Clear(), 4) };
        frame = Write(frame, MainAddress, Read(frame, MainAddress).WriteDword(0x218, 0x1434430).WriteDword(0x21C, 80));
        List<NativeBattleEffect251Operation> operations = [];
        frame = AdvanceMain5000(frame, MainAddress, resources, Inputs(operations, false, 317)).Frame;
        Assert.Equal(unchecked((ushort)(317 - 4096)), frame.State.ReadCallbackStackWord58());
        frame = InitializeCommonPool(frame, ActDescriptor);
        (frame, uint child) = TrySpawn(frame, ActDescriptor, 0x6F6530);
        frame = BindProgramme(frame, child + 0x14, 0x1431418, 0x144, resources);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect251Lower.Advance(frame with { State = frame.State with { CallbackStackWord58 = null } }, child, resources, Inputs(operations, false, 999)));
        frame = NativeBattleEffect251Lower.Advance(frame, child, resources, Inputs(operations, false, 999)).Frame;
        Assert.Equal(unchecked((ushort)(317 - 4096)), frame.State.ReadCallbackStackWord58());
        frame = Write(frame, MainAddress, Read(frame, MainAddress).WriteWord(12, 178));
        frame = AdvanceMain5000(frame, MainAddress, resources, Inputs(operations, false, 999)).Frame;
        Assert.Equal(unchecked((ushort)(317 - 4096)), frame.State.ReadCallbackStackWord58());
    }
    [Fact]
    public void Busy_file_poll_holds_age54_after_stage_and_point_update()
    {
        NativeBattleEffect251Frame frame = Create(54);
        List<NativeBattleEffect251Operation> operations = [];
        NativeBattleEffect251Step busy = AdvanceMain5000(frame, MainAddress, null!, Inputs(operations, true));
        Assert.Equal((ushort)54, Read(busy.Frame, MainAddress).ReadWord(12));
        Assert.Equal(2300u, busy.Frame.State.ReadDword(StageAmountGlobal));
        Assert.Equal(new byte[8], Read(busy.Frame, MainAddress).ReadBytes(0x48, 8));
        Assert.Empty(operations);
        NativeBattleEffect251Step ready = AdvanceMain5000(busy.Frame, MainAddress, null!, Inputs(operations, false));
        Assert.Equal((ushort)55, Read(ready.Frame, MainAddress).ReadWord(12));
        Assert.Single(operations.OfType<NativeBattleEffect251AdvanceIo534210>());
    }

    [Fact]
    public void Terminal_main_restores_full_packed_origin_without_inventing_target_writes()
    {
        NativeBattleEffect251Frame frame = Create(220);
        byte[] origin = [1, 2, 3, 4, 5, 6, 0xAB, 0xCD];
        frame = Write(frame, MainAddress, Read(frame, MainAddress).WriteBytes(0x10, origin));
        List<NativeBattleEffect251Operation> operations = [];
        NativeBattleEffect251Step done = AdvanceMain5000(frame, MainAddress, null!, Inputs(operations, false));
        Assert.Equal(2, done.Result);
        Assert.Equal((ushort)220, Read(done.Frame, MainAddress).ReadWord(12));
        NativeBattleEffect251WriteModelBytes restore = Assert.Single(operations.OfType<NativeBattleEffect251WriteModelBytes>());
        Assert.Equal(ModelAddress(3), restore.ModelAddress);
        Assert.Equal(0x1C, restore.Offset);
        Assert.Equal(8, restore.Length);
        Assert.Equal(origin, restore.Data.Read(0, 8));
        Assert.Single(operations.OfType<NativeBattleEffect251AdvanceIo534210>());
    }

    private const uint MainAddress = CommonAddress + 0xA294;

    private static NativeBattleEffect251Frame Create(short phase)
    {
        NativeBattleEffect251State state = NativeBattleEffect251State.FromColdProcess().WriteDword(ArenaGlobal, CommonAddress).WriteDword(CasterGlobal, ModelAddress(3)).WriteDword(TargetGlobal, ModelAddress(4));
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainAddress, 0x5EC, 1);
        (main, _) = main.Append(MainCallback);
        main = main.WriteSlot(0, main.Slots[0].WriteWord(12, unchecked((ushort)phase)));
        state = state.DetachCommonQueue(MainDescriptor);
        NativeBattleOriginalEffectState effect = new(251, 250, new(251, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = [main] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        return new(NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, [main]), effect, new(42), state);
    }

    private static NativeBattleEffect251Inputs Inputs(List<NativeBattleEffect251Operation> operations, bool busy, int viewY = 0)
    {
        return new(NativeBattleGteColorInterpolation.CodeConstants45F270, (_, _, _) => NativeBattleEffectArena.Clear(), (_, _) => NativeBattleEffectArena.Clear(), (frame, _, _, _, _) => new(frame, NativeBattleEffectArena.Clear()), (frame, _, retained) => new(frame, retained), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Euler701310(new(0, 0, 0)) with { Y = viewY }, null), _ => new(null, 512, 160, 108), _ => 0, frame => new(frame, busy), (frame, operation) =>
        {
            operations.Add(operation);
            return frame;
        });
    }
}
