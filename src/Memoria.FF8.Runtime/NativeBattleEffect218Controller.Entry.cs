using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect218EntryInputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattleGraphicsModel> ReadModel, Func<ushort, NativeBattleVector> TargetCenter5020A0);

public static partial class NativeBattleEffect218Controller
{
    public static NativeBattleEffect218Frame Initialize6326D0(NativeBattleEffect218Frame frame, NativeBattleEffect218Resources resources, NativeBattleEffect218EntryInputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect218 entry requires its inactive physical task banks.");
        }
        NativeBattleEffectParameters parameters = Parameters(frame);
        int first = inputs.Packets.Headers[parameters.HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Effect218 entry requires its actual first contact.");
        int target = inputs.Packets.ReadContact(first).Primary.ActorIndex;
        _ = ModelAddress(target);
        _ = ModelAddress(parameters.Actor78);
        NativeBattleEffect218State state = frame.State.InitializeSource(resources).WriteDword(ArenaGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, parameters.Actor78).WriteDword(TargetGlobal, checked((uint)target)).WriteDword(0xE257E0, ArenaAddress + ParticleAOffset).WriteDword(0xE257E4, ArenaAddress + ParticleBOffset).WriteDword(0xE257E8, ArenaAddress + ProjectionOffset);
        ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = ImmutableArray.CreateBuilder<NativeBattleEffectTaskPool>(3);
        foreach (NativeBattleCureFamilyPool profile in Pools)
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity);
            pool = pool with { Slots = pool.Slots.Select((task, slot) => CopyTask(task, 12, state.ReadView(checked(profile.Bank + (uint)(slot * profile.Stride) + 12), profile.Stride - 12), profile.Stride - 12)).ToImmutableArray() };
            pools.Add(pool);
        }
        foreach (NativeBattleEffectTaskPool pool in pools)
        {
            state = state with { Globals = state.Globals.MarkUnknown(checked((int)(pool.BankAddress - NativeBattleEffect218State.GlobalsAddress)), pool.Stride * pool.Slots.Length) };
        }
        frame = frame with { State = state, Effect = frame.Effect with { Pools = pools.ToImmutable(), ReturnedDescriptor = RootDescriptor } };
        (frame, uint root) = AppendRequired(frame, RootDescriptor, RootCallback);
        frame = Write(frame, root, Read(frame, root).WriteWord(12, 0));
        (frame, uint main) = AppendRequired(frame, MainDescriptor, MainCallback);
        frame = Write(frame, main, Read(frame, main).WriteWord(12, 0));
        for (int index = 0; index < 200; index++)
        {
            frame = WriteBytes(frame, checked(ArenaAddress + ParticleAOffset + (uint)(index * 24)), NativeBattleEffectArena.Clear().Write(0, new byte[4]), 4);
        }
        for (int index = 0; index < 150; index++)
        {
            frame = WriteBytes(frame, checked(ArenaAddress + ParticleBOffset + (uint)(index * 32)), NativeBattleEffectArena.Clear().Write(0, new byte[4]), 4);
        }
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Effect218 entry requires its actual texture process.");
        (textures, _) = textures.EnqueueTim505E30(resources.InitialTexture);
        frame = frame with { Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) } };
        NativeBattleGraphicsModel recipient = inputs.ReadModel(target);
        short heading = recipient.Rotation14 ?? throw new NotSupportedException("Effect218 entry requires its actual recipient heading.");
        NativeBattleActionMatrix basis = NativeBattleGfMatrices.Euler56CD50(new(0, unchecked((short)(heading + 2048)), 0));
        frame = WriteMatrixFields218(frame, CameraBasis, basis);
        NativeBattleVector center = inputs.TargetCenter5020A0(parameters.TargetMask7A);
        frame = WriteBytes(frame, 0x24F3548, PointView(center), 6);
        NativeBattleActionMatrix recipientWorld = recipient.GeometryWorldMatrix ?? throw new NotSupportedException("Effect218 entry requires its current recipient world matrix.");
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(recipientWorld, new(0, 0, -4500));
        frame = Dword(frame, 0x24F235C, unchecked((uint)(int)offset.X));
        frame = Dword(frame, 0x24F2360, unchecked((uint)(int)offset.Y));
        frame = Dword(frame, 0x24F2364, unchecked((uint)(center.Z + offset.Z)));
        return Dword(frame, 0x24F3544, 0);
    }
}
