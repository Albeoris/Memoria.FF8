using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectPresentationFileIoTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Async_full_TIM_completion_survives_host_ingress_and_drains_palette_and_pixels_once(bool firstAdvance)
    {
        NativeBattleEffectTextureState incoming = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattleEffectHeapAllocation tim = Tim();
        BattleSession owner = Create(incoming, tim, firstAdvance);
        NativeBattleCameraProjection projection = owner.Presentation!.Camera.Projection!;
        NativeBattleGraphicsModel source = ReadOriginalModelCell(owner.Presentation!.Graphics, 0);
        Assert.Equal(0, incoming.Count);
        Assert.Equal(0, incoming.ReadVramWord(33, 16));
        Assert.Equal((short)1, owner.Presentation!.NativeFileIo!.ActiveCount);

        NativeBattleEffectPresentationStep step = owner.Presentation!.AdvanceEffectPresentation(source, new(7), projection, [], incoming);

        Assert.Empty(step.Contacts);
        Assert.Empty(step.Sounds);
        NativeBattleEffectTextureState completed = owner.Presentation!.Memory.Textures!;
        Assert.Equal((ushort)31, completed.ReadVramWord(33, 16));
        Assert.Equal((ushort)0x3210, completed.ReadVramWord(64, 256));
        Assert.Equal(0, completed.Count);
        Assert.Equal(0x100, completed.Slots[0]!.ArenaOffset);
        Assert.Equal((short)0, owner.Presentation!.NativeFileIo!.ActiveCount);
        Assert.All(owner.Presentation!.NativeFileIo.Rows, row => Assert.Equal(0u, row.SchedulerCallback));
        Assert.Equal((byte)1, owner.Presentation!.Memory.Arena!.Read(4, 1)[0]);
        object publication = completed.VramPublicationIdentity;

        owner.Presentation!.AdvanceEffectPresentation(source, new(7), projection, [], completed);

        Assert.Same(publication, owner.Presentation!.Memory.Textures!.VramPublicationIdentity);
        Assert.Equal(0, owner.Presentation!.Memory.Textures.Count);
        Assert.Equal((byte)1, owner.Presentation!.Memory.Arena!.Read(4, 1)[0]);
        Assert.Equal((short)0, owner.Presentation!.NativeFileIo!.ActiveCount);
    }

    private static BattleSession Create(NativeBattleEffectTextureState incoming, NativeBattleEffectHeapAllocation tim, bool firstAdvance)
    {
        BattleSession owner = new(new BattleDefinition
        {
            Name = "Presentation asynchronous TIM publication",
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCrtRandom = new(7),
            Combatants =
            [
                new() { Id = "source", Name = "Source", IsPlayer = true },
                new() { Id = "target", Name = "Target" }
            ]
        }, BattleAtbMode.TurnBased);
        NativeBattleEffectMemory memory = owner.Presentation!.Memory with
        {
            Arena = NativeBattleEffectArena.Clear().Write(0x100, tim.Data.AsSpan()),
            Textures = incoming,
            EffectIo = new(0x6C3580, null, null),
            PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400()
        };
        memory = NativeBattleEffectIo.Initialize534110(memory, 0);
        memory = NativeBattleEffectIo.Queue5341D0(memory, 1, NativeBattleEffectNodeMemory.ArenaAddress + 0x100, 1);
        NativeBattleEffectIoAdvance queued = NativeBattleEffectIo.Advance534210(memory);
        Assert.NotNull(queued.Request);
        NativeBattleHostedTestFixture.SetProperty(owner.Presentation!, nameof(BattlePresentationProcess.NativeEffectMemory), queued.Memory);
        NativeBattleHostedTestFixture.SetProperty(owner.Presentation!, nameof(BattlePresentationProcess.NativeFileIo), NativeBattleFileIoState.FromColdProcess().Queue482610(tim.Data.Length, queued.Request!.CompletionCallback, null));
        NativeBattleHostedTestFixture.SetProperty(owner.Presentation!, nameof(BattlePresentationProcess.NativeStage), NativeBattleStageState.FromColdProcess());
        NativeBattleCameraProjection projection = new(NativeBattleForeignModelGeometry.Identity, 216, 160, 108);
        NativeBattleCameraState camera = BattlePresentationProcess.BindEffectPresentationHostProjection(NativeBattleCameraState.FromColdProcess(), projection);
        SetProperty(owner, nameof(BattlePresentationProcess.NativeCamera), camera);
        SetField(owner, "presentationHostCamera", camera);
        SetField(owner, "presentationForeignActors", new Dictionary<BattleModelOwner, NativeBattleForeignActor>());
        SetField(owner, "presentationFirstAdvance", firstAdvance);
        BattleModelOwner source = new BattlePartyActorModel(new("source"));
        SetField(owner, "presentationSourceOwner", source);
        NativeBattleGraphicsState graphics = OriginalGraphics([(0, source, new(0))]) with { PrimitiveFrame = NativeBattlePrimitiveFrame.Clear() };
        SetProperty(owner, nameof(BattlePresentationProcess.NativeGraphics), graphics);
        return owner;
    }

    private static NativeBattleEffectHeapAllocation Tim()
    {
        byte[] data = new byte[66];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(12), 32);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(14), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(22), 31);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(52), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(56), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(58), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(60), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(62), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(64), 0x3210);
        return new("fixture.tim", data.ToImmutableArray());
    }

    private static void SetProperty(BattleSession owner, string name, object value)
    {
        typeof(BattleSession).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(owner, value);
    }

    private static void SetField(BattleSession owner, string name, object value)
    {
        typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, value);
    }
}
