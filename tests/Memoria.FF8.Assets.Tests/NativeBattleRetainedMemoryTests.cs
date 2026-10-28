using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRetainedMemoryTests
{
    [Fact]
    public void AuthenticatedColdImageSuppliesExactlySevenPhysicalPresentationMirrorPairs()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for the authenticated native presentation mirror bank.");
        byte[] executable = File.ReadAllBytes(Path.Combine(directory!, "FF8_EN.exe"));
        NativeExecutableMemory image = new(executable);
        NativeBattlePresentationStatusMemory mirrors = NativeBattleRetainedMemoryReader.ReadColdProcess(image).PresentationStatusMemory!;

        Assert.Equal(7, mirrors.Rows.Length);
        for (int index = 0; index < mirrors.Rows.Length; index++)
        {
            ushort saved = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(image.ReadLoader(checked(0x1D27B92u + 208u * (uint)index), 2));
            uint battle = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(image.ReadLoader(checked(0x1D27B1Cu + 208u * (uint)index), 4));
            Assert.Equal(new NativeBattleStatusMirror(saved, battle), mirrors.Rows[index]);
            Assert.Equal(NativeBattlePresentationStatusMemory.FromColdProcess().Rows[index], mirrors.Rows[index]);
        }
        Assert.Equal(0x1D280C0u, 0x1D27B10u + 7u * 208u);
    }

    [Fact]
    public void AuthenticatedLoaderImageProvesTheRuntimeColdProcessContract()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleRetainedMemory actual = assets.ReadColdProcessNativeBattleMemory();
        NativeBattleRetainedMemory generic = NativeBattleRetainedMemory.FromColdProcess();
        NativeBattleEffectMemory detachedEffect = NativeBattleStorageSnapshots.Capture(actual.EffectMemory!);
        Assert.Equal((byte?)0, actual.EffectMemory!.Parameter80);
        Assert.Equal((byte?)0, actual.EffectMemory.Parameter82);
        Assert.Equal((byte?)0, generic.EffectMemory!.Parameter82);
        Assert.Equal((short?)0, actual.EffectMemory.CuraContextF4);
        Assert.Equal((int?)0, actual.EffectMemory.CuraSceneFade);
        Assert.Equal(NativeBattleCuragaExecutableState.FromColdProcess(), actual.EffectMemory.CuragaExecutable);
        var oldEffect = JsonSerializer.SerializeToNode(detachedEffect)!.AsObject();
        Assert.True(oldEffect.Remove(nameof(NativeBattleEffectMemory.CuraContextF4)));
        Assert.True(oldEffect.Remove(nameof(NativeBattleEffectMemory.CuraSceneFade)));
        Assert.True(oldEffect.Remove(nameof(NativeBattleEffectMemory.CuragaExecutable)));
        Assert.Null(JsonSerializer.Deserialize<NativeBattleEffectMemory>(oldEffect)!.CuraContextF4);
        Assert.Null(JsonSerializer.Deserialize<NativeBattleEffectMemory>(oldEffect)!.CuraSceneFade);
        Assert.Null(JsonSerializer.Deserialize<NativeBattleEffectMemory>(oldEffect)!.CuragaExecutable);
        Assert.Null(generic.EffectMemory!.QuezacotlExecutableMemory);
        Assert.Null(generic.EffectMemory.DoubleMorph);
        Assert.Null(generic.EffectMemory.TripleMorph);
        Assert.Null(generic.EffectMemory.Effect57Executable);
        Assert.Null(generic.EffectMemory.RemedyExecutable);
        Assert.Null(generic.EffectMemory.RecoveryExecutables);
        Assert.Equal(new ushort[] { 7, 53, 58, 59 }, actual.EffectMemory.RecoveryExecutables!.Value.Select(bank => bank.Effect));
        Assert.Equal(NativeBattleRestorationExecutableState.FromColdProcess(57), actual.EffectMemory.Effect57Executable);
        Assert.Equal(NativeBattleRestorationExecutableState.FromColdProcess(67), actual.EffectMemory.RemedyExecutable);
        NativeBattleQuezacotlExecutableMemory executableMemory = Assert.IsType<NativeBattleQuezacotlExecutableMemory>(detachedEffect.QuezacotlExecutableMemory);
        byte[] executable = File.ReadAllBytes(Path.Combine(assets.GameDirectory, "FF8_EN.exe"));
        Assert.Null(generic.EffectMemory.DrinkMagic37);
        Assert.Null(generic.EffectMemory.MeltingBubble43);
        Assert.Null(generic.EffectMemory.MeltingBubble43Executable);
        Assert.Null(generic.EffectMemory.WindBlast29);
        Assert.Null(generic.EffectMemory.WindBlast29Executable);
        Assert.Null(generic.EffectMemory.Effect60);
        Assert.Null(generic.EffectMemory.Effect60PrimaryExecutable);
        Assert.Null(generic.EffectMemory.Effect60SecondaryExecutable);
        Assert.Null(generic.EffectMemory.PsychoBlast23);
        Assert.Null(generic.EffectMemory.PsychoBlast23Executable);
        Assert.Null(generic.EffectMemory.Raldo19);
        Assert.Null(generic.EffectMemory.Effect20);
        Assert.Null(generic.EffectMemory.Raldo19Executable);
        NativeBattleDrinkMagic37State drink = actual.EffectMemory.RequireDrinkMagic37();
        Assert.Null(drink.Nodes);
        Assert.Equal(NativeBattleEffectStaticResourcesReader.Read(executable, NativeBattleDrinkMagic37Profile.OrbAddress, NativeBattleDrinkMagic37Profile.OrbLength).ToArray(), drink.OrbMesh.Read(0, NativeBattleDrinkMagic37Profile.OrbLength));
        Assert.All(drink.CapturedAnchor8, value => Assert.Equal((byte)0, value));
        Assert.Equal(0u, drink.Counter26A8DA8);
        Assert.Equal(0u, drink.Counter26A7D9C);
        Assert.Equal(NativeBattleDrinkMagic37Profile.ExecutableAddress, actual.EffectMemory.DrinkMagic37Executable!.ResourceAddress);
        Assert.Equal(0u, actual.EffectMemory.DrinkMagic37Executable.InitializationFlag);
        Assert.Null(actual.EffectMemory.DrinkMagic37Executable.Data);
        foreach (ushort effect in new ushort[] { 73, 78 })
        {
            NativeBattleMightyGuardState mighty = actual.EffectMemory.RequireMightyGuard(effect);
            Assert.Equal(effect, mighty.Effect);
            Assert.Equal(0u, mighty.InitializationFlag);
            Assert.Null(mighty.Executable);
            Assert.Null(mighty.SourceArenaOffset);
            Assert.Null(mighty.Emitters);
            Assert.Null(mighty.Geometry);
        }
        Assert.Equal(NativeBattleEffectStaticResourcesReader.Read(executable, 0x1298C68, 0x109FC).ToArray(), executableMemory.Data.AsSpan().Slice(0, 0x109FC).ToArray());
        Assert.Equal(NativeBattleEffectStaticResourcesReader.Read(executable, 0x12A9664, 0x4D9C).ToArray(), executableMemory.Data.AsSpan().Slice(0x109FC, 0x4D9C).ToArray());
        NativeBattleGfAnimationSource animationSource = NativeBattleQuezacotlWorkspace.Source(actual.EffectMemory);
        Assert.Equal(0x1298C68u, animationSource.Address);
        byte[] animationBytes = new byte[animationSource.Length];
        using (NativeBattleGfSourceReader reader = animationSource.OpenReader())
        {
            reader.ReadInto(0, animationBytes);
        }
        Assert.Equal(executableMemory.Data.ToArray(), animationBytes);
        NativeBattleDoubleMorph doubleMorph = Assert.IsType<NativeBattleDoubleMorph>(detachedEffect.DoubleMorph);
        Assert.Equal(NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 0x163FFD0u).Bytes.ToArray(), doubleMorph.Bytes.ToArray());
        NativeBattleTripleMorph tripleMorph = Assert.IsType<NativeBattleTripleMorph>(detachedEffect.TripleMorph);
        Assert.Equal(NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 0x1627970u).Bytes.ToArray(), tripleMorph.Bytes.ToArray());
        Assert.Null(generic.EffectMemory.Bio22Geometry);
        Assert.Null(generic.EffectMemory.Bio22SecondaryGeometry);
        Assert.Null(generic.EffectMemory.Bio22ThirdGeometry);
        Assert.Null(generic.EffectMemory.Bio22FourthGeometry);
        Assert.Null(generic.EffectMemory.Bio22Palette);
        Assert.Empty(generic.EffectMemory.Scan!.MutableMeshes);
        NativeExecutableMemory image = new(executable);
        Assert.Equal(uint.MaxValue, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(image.ReadLoader(0xD32508, 4)));
        Assert.Equal(uint.MaxValue, generic.EffectMemory.Shiva!.SpawnedChildD32508);
        NativeBattleEffectMemory authenticatedEffect = generic.EffectMemory with
        {
            GeometryRegisters = generic.EffectMemory.GeometryRegisters! with { AnchorScratch8 = image.ReadLoader(0x1D97708, 8).Select(value => (byte?)value).ToImmutableArray() },
            ModuleSpriteBillboard21DFED0 = new(image.ReadLoader(0x21DFED0, 32).ToImmutableArray(), Enumerable.Repeat(true, 32).ToImmutableArray()),
            Aqua55 = new(55, image.ReadLoader(0x26163B0, 84).ToImmutableArray(), NativeBattleEffectStaticResourcesReader.Read(executable, 0x15A4168, 10600)),
            Aqua72 = new(72, image.ReadLoader(0x25FA370, 84).ToImmutableArray(), NativeBattleEffectStaticResourcesReader.Read(executable, 0x158A4BC, 10600)),
            DrinkMagic37 = new(null, NativeBattleEffectArena.Clear().Write(0, image.ReadLoader(NativeBattleDrinkMagic37Profile.OrbAddress, NativeBattleDrinkMagic37Profile.OrbLength)), image.ReadLoader(0x26A8D80, 8).ToImmutableArray()),
            DrinkMagic37Executable = NativeBattleCuragaExecutableState.FromColdProcess(37),
            Raldo19 = new(null, image.ReadLoader(NativeBattleRaldo19Profile.SharedAddress, 84).ToImmutableArray(), NativeBattleEffectArena.Clear().Write(0, image.ReadLoader(NativeBattleRaldo19Profile.BallAddress, 0xC4)), NativeBattleEffectArena.Clear().Write(0, image.ReadLoader(NativeBattleRaldo19Profile.BodyScratchAddress, NativeBattleRaldo19Profile.BodyScratchLength)), NativeBattleEffectArena.Clear().Write(0, image.ReadLoader(NativeBattleRaldo19Profile.DescriptorAddress, 48)), NativeBattleEffectArena.Clear().Write(0, image.ReadLoader(NativeBattleRaldo19Profile.VertexScratchAddress, NativeBattleRaldo19Profile.VertexScratchLength)))
            {
                Counter273048C = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(image.ReadLoader(0x273048C, 4)),
                Counter272DDDC = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(image.ReadLoader(0x272DDDC, 4))
            },
            Raldo19Executable = NativeBattleCuragaExecutableState.FromColdProcess(19),
            Effect20 = NativeBattleEffect20State.FromColdProcess(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(image.ReadLoader(0x27268F8, 4)), System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(image.ReadLoader(0x2722960, 4))),
            MeltingBubble43 = NativeBattleMeltingBubble43State.FromColdProcess(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(image.ReadLoader(0x2656220, 4)), System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(image.ReadLoader(0x265587C, 4)), NativeBattleMeltingBubble43Profile.ChannelRegions.SelectMany(region => image.ReadLoader(region.Address, region.Length)).ToImmutableArray()),
            MeltingBubble43Executable = new(1, image.ReadLoader(0x15C6080, 260).ToImmutableArray()) { ResourceAddress = 0x15C6080 },
            WindBlast29 = NativeBattleWindBlast29State.FromColdProcess(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(image.ReadLoader(0x26D74E8, 4)), System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(image.ReadLoader(0x26D6B44, 4))),
            WindBlast29Executable = NativeBattleCuragaExecutableState.FromColdProcess(29),
            Effect60 = NativeBattleEffect60State.FromColdProcess(image.ReadLoader(NativeBattleEffect60Profile.SharedAddress, 84).ToImmutableArray(), image.ReadLoader(NativeBattleEffect60Profile.SavedRotationAddress, 8).ToImmutableArray(), image.ReadLoader(NativeBattleEffect60Profile.MorphScratchAddress, NativeBattleEffect60Profile.MorphScratchLength).ToImmutableArray(), System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(image.ReadLoader(0x2607050, 4)), System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(image.ReadLoader(0x2605DD4, 4))),
            Effect60PrimaryExecutable = new(0, null) { ResourceAddress = 0x1797CC0 },
            Effect60SecondaryExecutable = new(0, null) { ResourceAddress = 0x179C998 },
            PsychoBlast23 = NativeBattlePsychoBlast23State.FromColdProcess(image.ReadLoader(NativeBattlePsychoBlast23Profile.SharedAddress, 84).ToImmutableArray(), image.ReadLoader(NativeBattlePsychoBlast23Profile.BasisAddress, 56).ToImmutableArray(), image.ReadLoader(NativeBattlePsychoBlast23Profile.MorphScratchAddress, NativeBattlePsychoBlast23Profile.MorphScratchLength).ToImmutableArray(), System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(image.ReadLoader(0x27044D0, 4)), System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(image.ReadLoader(0x2703654, 4))),
            PsychoBlast23Executable = NativeBattleCuragaExecutableState.FromColdProcess(23),
            Scan = generic.EffectMemory.Scan with
            {
                MutableMeshes = new[] { (Address: 0x15CDDECu, Length: 0x3E8), (Address: 0x15CE1D4u, Length: 0x768) }.ToImmutableDictionary(item => item.Address, item => new NativeBattleScanByteOwner(image.ReadLoader(item.Address, item.Length).ToImmutableArray(), Enumerable.Repeat(true, item.Length).ToImmutableArray()))
            },
            Bio22Geometry = NativeBattleBio22Geometry.FromColdProcess(NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 0x1615CE4)),
            Bio22SecondaryGeometry = NativeBattleBio22Geometry.FromColdProcess(NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 0x1617E3C)),
            Bio22ThirdGeometry = NativeBattleBio22Geometry.FromColdProcess(NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 0x1619888)),
            Bio22FourthGeometry = NativeBattleBio22Geometry.FromColdProcess(NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 0x161F750)),
            Bio22Palette = NativeBattleBio22Palette.FromColdProcess(new("FF8_EN:16146E8", NativeBattleEffectStaticResourcesReader.Read(executable, 0x16146E8, 0x220)))
        };
        byte[] temporary = image.ReadLoader(NativeBattleTemporaryMemory.Address, NativeBattleTemporaryMemory.ReachedLength);
        byte[] cursor = image.ReadLoader(NativeBattleTemporaryMemory.Cursor, 4);
        byte[] shadow = image.ReadLoader(0x1D999D0, 80);
        Assert.Equal(shadow, actual.FileStaging!.Read(0x1D999D0, shadow.Length));
        Assert.Equal(shadow, generic.FileStaging!.Read(0x1D999D0, shadow.Length));
        Assert.Equal(temporary, actual.FileStaging!.Read(NativeBattleTemporaryMemory.Address, temporary.Length));
        Assert.Equal(cursor, actual.FileStaging.Read(NativeBattleTemporaryMemory.Cursor, cursor.Length));
        Assert.Equal(temporary, generic.FileStaging!.Read(NativeBattleTemporaryMemory.Address, temporary.Length));
        Assert.Equal(cursor, generic.FileStaging!.Read(NativeBattleTemporaryMemory.Cursor, cursor.Length));
        NativeBattleRetainedMemory authenticated = generic with { EffectMemory = authenticatedEffect, FileStaging = NativeBattleStorageSnapshots.Restore(generic.FileStaging!).Write(NativeBattleTemporaryMemory.Address, temporary).Write(NativeBattleTemporaryMemory.Cursor, cursor).Write(0x1D999D0, shadow) };
        JsonObject expectedCold = JsonSerializer.SerializeToNode(NativeBattleStorageSnapshots.Capture(authenticated))!.AsObject();
        JsonObject actualCold = JsonSerializer.SerializeToNode(NativeBattleStorageSnapshots.Capture(actual with { EffectMemory = actual.EffectMemory with { QuezacotlExecutableMemory = null, DoubleMorph = null, TripleMorph = null, Effect57Executable = null, RemedyExecutable = null, RecoveryExecutables = null } }))!.AsObject();
        AssertDefinedEffectProperties(expectedCold[nameof(NativeBattleRetainedMemory.EffectMemory)], actualCold[nameof(NativeBattleRetainedMemory.EffectMemory)], "EffectMemory");
        expectedCold.Remove(nameof(NativeBattleRetainedMemory.EffectMemory));
        actualCold.Remove(nameof(NativeBattleRetainedMemory.EffectMemory));
        Assert.True(JsonNode.DeepEquals(expectedCold, actualCold), "All other retained cold owners must match the authenticated image exactly.");
        Assert.Same(actual, assets.ReadColdProcessNativeBattleMemory());
        Assert.Equal(0, actual.ActionPackets.Headers[31].ReadByte(19));
        Assert.Equal(0, actual.ActionPackets.ReadContact(48).Secondary.Amount6);
        Assert.Equal((byte)0, actual.ActionScratch.LastPublishedTargetCountE05);
        Assert.Equal((byte)0, actual.ActionScratch.FinisherActionIndexE2E);
        Assert.Equal(new byte?[] { 0, 0, 0, 0, 0, 0, 0 }, actual.ElementAttack);
        Assert.All(actual.ActionMemory.Rows, row => Assert.All(row, value => Assert.Equal((byte)0, value)));
    }

    private static void AssertDefinedEffectProperties(JsonNode? expected, JsonNode? actual, string path)
    {
        if (expected is null)
        {
            Assert.Null(actual);
        }
        else if (expected is JsonObject expectedObject)
        {
            JsonObject actualObject = Assert.IsType<JsonObject>(actual);
            foreach ((string name, JsonNode? value) in expectedObject)
            {
                Assert.True(actualObject.ContainsKey(name), "Missing authenticated cold property: " + path + "." + name);
                AssertDefinedEffectProperties(value, actualObject[name], path + "." + name);
            }
        }
        else if (expected is JsonArray expectedArray)
        {
            JsonArray actualArray = Assert.IsType<JsonArray>(actual);
            Assert.Equal(expectedArray.Count, actualArray.Count);
            for (int index = 0; index < expectedArray.Count; index++)
            {
                AssertDefinedEffectProperties(expectedArray[index], actualArray[index], path + "[" + index + "]");
            }
        }
        else
        {
            Assert.True(JsonNode.DeepEquals(expected, actual), "Authenticated cold value differs at " + path);
        }
    }

    [Fact]
    public void UnauthenticatedImageCannotSupplyColdProcessFacts()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleRetainedMemoryReader.ReadColdProcess(new byte[1024]));
    }
}
