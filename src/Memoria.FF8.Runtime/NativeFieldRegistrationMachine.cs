using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeFieldRetainedActor(int ObjectId, int ModelId, int FirstScript, int LastScript);
public sealed record NativeFieldRetainedPresentation(int ObjectId, byte Animation, ushort? Frame, bool AutoAnimation, byte BaseAnimation, bool Visible, byte RendererMode, short RootTranslationZ, byte Red, byte Green, byte Blue, bool AdditionalControlDisabled, bool PrimitiveTransparency, uint EyeMode)
{
    public byte? EyeBlinkParameter611 { get; init; }
}

public sealed record NativeBattleFieldResourceReloadResult(NativeBattleRetainedMemory Memory, NativeCrtRandomState? CrtRandomBefore, NativeCrtRandomState? CrtRandomAfter);
public sealed record NativeFieldRegisteredTexture(byte Mode, short X, short Y, short WidthWords, short Height, short PaletteX, short PaletteY, ushort TexturePage, ushort Clut)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public uint? PlaneIdentity { get; init; }
}
public sealed record NativeFieldRegisteredResource(int ModelId, int Index, uint Address, uint GeometryAddress, uint AnimationAddress, byte Scale, string SourceHash, int SourceOffset, ImmutableArray<NativeFieldRegisteredTexture> Textures);
public sealed record NativeFieldRegisteredInstance(int ObjectId, int ModelId, uint Address, ushort? EmptyTerminal);
public sealed record NativeFieldRegistrationState(int Version, int FieldId, string ProgramHash, string OneHash, uint PreModelAddress, uint RelocatedSourceAddress, int RelocatedSourceLength, uint EndAddress, ImmutableArray<NativeFieldRegisteredResource> Resources, ImmutableArray<NativeFieldRegisteredInstance> Instances)
{
    public void Validate(NativeBattleRetainedMemory retained)
    {
        retained.Validate();
        NativeBattleFileStaging memory = retained.FileStaging ?? throw new InvalidDataException("Native field registration lost its physical shared memory.");
        if (Version != 1 || FieldId is < 72 or > ushort.MaxValue || ProgramHash is not { Length: 64 } || OneHash is not { Length: 64 } || PreModelAddress < NativeFieldRegistrationMachine.FieldArena || EndAddress < PreModelAddress || EndAddress > NativeFieldRegistrationMachine.Kind3Limit || RelocatedSourceAddress < PreModelAddress || RelocatedSourceLength < 4 || (ulong)RelocatedSourceAddress + (uint)RelocatedSourceLength > NativeFieldRegistrationMachine.Kind3Limit || Resources.IsDefault || Instances.IsDefault || Resources.Length > 64 || Instances.Length > 32 || Resources.Select(resource => resource.ModelId).Distinct().Count() != Resources.Length || Instances.Select(instance => instance.ObjectId).Distinct().Count() != Instances.Length)
        {
            throw new InvalidDataException("Native field registration has invalid source identities, allocation layout or retained bindings.");
        }
        if (retained.FieldTextureAnimations is { } animations && (animations.OneHash != OneHash || !animations.Textures.Select(animation => animation.Texture).SequenceEqual(Resources.SelectMany(resource => resource.Textures).DistinctBy(texture => (texture.X, texture.Y)))))
        {
            throw new InvalidDataException("Field texture copy history does not belong to the retained resource registration.");
        }
        foreach (NativeFieldRegisteredResource resource in Resources)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0x1DCB0F0 + (uint)resource.Index * 4), 4));
            uint marker = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(resource.Address + 76, 4));
            if (resource.ModelId is < 0 or >= 64 || resource.Index < 0 || resource.Index >= Resources.Length || resource.Address < PreModelAddress || resource.Address >= EndAddress || pointer != resource.Address || marker != 0x12345678 || memory.Read(0x1DCAF28 + (uint)resource.ModelId, 1)[0] != resource.Index)
            {
                throw new InvalidDataException("Native field resource registration disagrees with its actual physical pointer/header.");
            }
        }
        for (int index = 0; index < Instances.Length; index++)
        {
            NativeFieldRegisteredInstance instance = Instances[index];
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0x1DCB340 + (uint)index * 4), 4));
            uint resource = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(instance.Address, 4));
            uint expected = instance.ModelId < 0 ? uint.MaxValue : Resources.Single(item => item.ModelId == instance.ModelId).Address;
            if (instance.Address < PreModelAddress || instance.Address >= EndAddress || pointer != instance.Address || resource != expected)
            {
                throw new InvalidDataException("Native field instance registration disagrees with its actual retained model pointer.");
            }
        }
    }
}
public sealed record NativeFieldRegistrationStep(NativeFieldRegistrationState State, NativeBattleRetainedMemory Memory, ImmutableArray<NativeBattleEffectVramUpload> Uploads);

/// <summary>Retained52BC00 instruction binding followed by532A40's actual field model registration.</summary>
public static partial class NativeFieldRegistrationMachine
{
    public const uint FieldArena = 0x18DD018;
    public const uint Kind3Limit = 0x19EF018;
    private const uint ProgramSource = 0x19AB018;
    private const uint CacheRows = 0x1DCA328;
    private const uint ResourcePointers = 0x1DCB0F0;
    private const uint InstancePointers = 0x1DCB340;
    private const uint ModelMap = 0x1DCAF28;
    private const uint TexturePages = 0x1DC3CEC;
    private const uint Cluts = 0x1DC6014;
    private const uint UvOffsets = 0x1DCAF90;

    private sealed class SelectedModel(int modelId, int index, NativeFieldOneModelEntry entry)
    {
        internal readonly int ModelId = modelId;
        internal readonly int Index = index;
        internal readonly NativeFieldOneModelEntry Entry = entry;
        internal readonly List<uint> Textures = [];
        internal readonly List<NativeFieldRegisteredTexture> RegisteredTextures = [];
        internal int UploadedTextures;
        internal uint Geometry;
        internal uint Animation;
        internal string SourceHash = string.Empty;
        internal int SourceOffset;
        internal byte Scale = (entry.Flag >> 28) == 0xD ? (byte)(entry.Flag >> 16) : (byte)16;
    }

    private sealed class Context(NativeFieldRegistrationMemory memory, NativeBattleEffectMemory effects, NativeFieldTextureAnimations? animations = null)
    {
        internal readonly NativeFieldRegistrationMemory Memory = memory;
        internal NativeBattleEffectMemory Effects = effects;
        internal NativeFieldTextureAnimations? Animations = animations;
        internal readonly List<NativeBattleEffectVramUpload> Uploads = [];

        internal void Upload(short x, short y, short width, short height, byte[] bytes)
        {
            NativeBattleEffectTextureState textures = Effects.Textures ?? throw new NotSupportedException("Native field texture registration requires the process VRAM owner.");
            NativeBattlePrimitiveProcess? process = Effects.PrimitiveProcess;
            if (process?.TextureWindowCache is { } cache)
            {
                process = process with { TextureWindowCache = cache.Invalidate464850(x, y, x + width - 1, y + height - 1, textures) };
            }
            Effects = Effects with { Textures = textures.Upload45BD30(x, y, width, height, bytes), PrimitiveProcess = process };
            Uploads.Add(new(x, y, width, height, bytes.ToImmutableArray()));
        }
    }

    public static NativeFieldRegistrationStep ReloadKind3(NativeFieldRegistrationResources source, IReadOnlyList<NativeFieldRetainedActor> actors, NativeBattleRetainedMemory retained)
    {
        source.Validate();
        retained.Validate();
        if (source.ExecutableHash == NativeFieldRuntimeProfile.RemasteredEnImageHash)
        {
            if (retained.FieldTextureAnimations is { Planes: null } || retained.FieldTextureAnimations is null && retained.FieldTexturePlaneCacheFresh != true)
            {
                throw new NotSupportedException("Remastered registration requires retained native plane state or an explicitly fresh cold renderer cache; older state requires migration before registration reload.");
            }
        }
        NativeFieldRegistrationLoaderInputs loader = source.Loader ?? throw new NotSupportedException("Native kind3 requires its raw PCB, original JSM, INF selector and initialized placement tables.");
        if (actors.Count > 32 || actors.Select(actor => actor.ObjectId).Distinct().Count() != actors.Count || actors.Any(actor => actor.ModelId < -1 || actor.ModelId >= 64 || actor.ModelId >= source.Directory.Length || actor.FirstScript < 0 || actor.LastScript < actor.FirstScript))
        {
            throw new InvalidDataException("Native field registration requires retained physical actor order and bounded original models/script ranges.");
        }
        NativeBattleFileStaging staging = retained.FileStaging ?? NativeBattleFileStaging.Unknown();
        NativeFieldRegistrationMemory memory = new(staging, FieldArena, 0x1DCD6E4);
        memory.ObserveCpuBanks(retained.ModelCpuResources);
        Context context = new(memory, retained.EffectMemory ?? throw new NotSupportedException("Native kind3 requires the retained shared VRAM and texture process."));
        memory.Fill(CacheRows, 0xC00, 0);
        memory.Fill(ResourcePointers, 0x100, 0);
        memory.Fill(InstancePointers, 0x80, 0);
        memory.Fill(0x1DC308C, 64, 0);
        memory.Fill(ModelMap, 64, 255);
        uint cursor = RebindProgram(memory, loader, actors);
        memory.Write(cursor, loader.Colors.Data.AsSpan());
        cursor = checked(cursor + (uint)loader.Colors.Data.Length);
        uint preModel = cursor;
        uint temporary = checked(cursor + 0x20000);
        uint inputEnd = checked(temporary + 4);
        List<SelectedModel> selected = [];
        foreach (int model in actors.Where(actor => actor.ModelId >= 0).Select(actor => actor.ModelId).Distinct())
        {
            if (selected.Count == 64)
            {
                throw new NotSupportedException("Native field registration exceeds its64 resource slots.");
            }
            NativeFieldOneModelEntry entry = source.Directory[model];
            if (entry.SourceWord8 is null || entry.RawName.IsDefault || entry.RawName.Length != 8)
            {
                throw new NotSupportedException("Native ONE registration requires all original directory DWORDs and raw name/color bytes.");
            }
            SelectedModel item = new(model, selected.Count, entry);
            item.SourceHash = source.One.Sha256;
            item.SourceOffset = checked(entry.Offset + entry.ModelOffset);
            memory.Write(inputEnd, source.One.Data.AsSpan().Slice(entry.Offset, entry.Size));
            if ((entry.Flag >> 28) == 0xD)
            {
                item.Animation = inputEnd;
                memory.Write8(0x1DC308C + (uint)item.Index, 13);
            }
            else
            {
                item.Geometry = checked(inputEnd + (uint)entry.ModelOffset);
                item.Animation = checked(item.Geometry + memory.Read32(item.Geometry + 56));
                memory.Write32(item.Geometry + 56, item.Animation);
                memory.Write8(0x1DC308C + (uint)item.Index, 255);
                if ((entry.Flag >> 28) == 0xA)
                {
                    item.Textures.Add(entry.Flag);
                }
                else
                {
                    item.Textures.AddRange(entry.RawTextureWords.Select(word => checked(inputEnd + (word & 0x0FFFFFFF))));
                }
            }
            selected.Add(item);
            if (item.Textures.Count > 6)
            {
                throw new NotSupportedException("Native model registration exceeds its six texture slots.");
            }
            inputEnd = checked(inputEnd + (uint)entry.Size);
        }
        foreach (SelectedModel item in selected.Where(item => (item.Entry.Flag >> 28) == 0xD))
        {
            string member = "/" + item.Entry.Name[..4] + ".mch";
            NativeFieldModelSource main = source.MainCharacters.Single(model => model.Member.EndsWith(member, StringComparison.OrdinalIgnoreCase));
            uint beginning = inputEnd;
            memory.Write(beginning, main.Data.AsSpan());
            uint pointer = beginning;
            while (unchecked((int)memory.Read32(pointer)) >= 0)
            {
                if (item.Textures.Count == 6)
                {
                    throw new NotSupportedException("Native model registration exceeds its six texture slots.");
                }
                item.Textures.Add(checked(beginning + (memory.Read32(pointer) & 0x0FFFFFFF)));
                pointer += 4;
            }
            item.Geometry = checked(beginning + (memory.Read32(pointer + 4) & 0x0FFFFFFF));
            item.SourceHash = main.Sha256;
            item.SourceOffset = checked((int)(item.Geometry - beginning));
            memory.Write32(item.Geometry + 56, item.Animation);
            inputEnd = checked(inputEnd + (uint)main.Data.Length);
        }
        int sourceLength = checked((int)(inputEnd - temporary));
        if (inputEnd > Kind3Limit)
        {
            throw new NotSupportedException("Native kind3 source staging exceeds its original19EF018 limit.");
        }
        uint shift = (Kind3Limit - inputEnd) & ~3u;
        uint relocated = checked(temporary + shift);
        memory.Copy(temporary, relocated, sourceLength);
        foreach (SelectedModel item in selected)
        {
            item.Geometry = checked(item.Geometry + shift);
            item.Animation = checked(item.Animation + shift);
            memory.Write32(item.Geometry + 56, item.Animation);
            for (int texture = 0; texture < item.Textures.Count; texture++)
            {
                if ((item.Textures[texture] >> 28) != 0xA)
                {
                    item.Textures[texture] = checked(item.Textures[texture] + shift);
                }
            }
        }
        int uploadIndex = 0;
        short?[] lastX = new short?[6];
        short?[] lastY = new short?[6];
        List<NativeFieldRegisteredResource> resources = [];
        foreach (SelectedModel item in selected)
        {
            uint row = checked(CacheRows + (uint)item.Index * 48);
            memory.Write32(row, checked((uint)item.Entry.Offset));
            memory.Write32(row + 4, checked((uint)item.Entry.Size));
            memory.Write32(row + 8, item.Entry.SourceWord8!.Value);
            memory.Write32(row + 12, (item.Entry.Flag >> 28) == 0xD ? item.Entry.Flag & 0xFFFF : uint.MaxValue);
            memory.Fill(row + 16, 24, 255);
            memory.Write32(row + 40, item.Geometry);
            memory.Write32(row + 44, item.Animation);
            memory.Write8(ModelMap + (uint)item.ModelId, (byte)item.Index);
            for (int ordinal = 0; ordinal < item.Textures.Count; ordinal++)
            {
                uint texture = item.Textures[ordinal];
                memory.Write32(row + 16 + (uint)ordinal * 4, texture);
                NativeFieldRegisteredTexture registration;
                if ((texture >> 28) == 0xA)
                {
                    int alias = (byte)(texture >> 20);
                    SelectedModel prior = selected.Take(item.Index).SingleOrDefault(model => model.ModelId == alias) ?? throw new NotSupportedException("Native texture alias requires an already registered original model.");
                    if (ordinal >= prior.RegisteredTextures.Count)
                    {
                        throw new InvalidDataException("Native texture alias has no previously registered matching ordinal.");
                    }
                    registration = prior.RegisteredTextures[ordinal];
                }
                else
                {
                    registration = RegisterTexture(context, loader, item.Entry, texture, uploadIndex++);
                    item.UploadedTextures++;
                    if (source.ExecutableHash == NativeFieldRuntimeProfile.RemasteredEnImageHash)
                    {
                        registration = registration with { PlaneIdentity = NativeFieldTexturePlanes.SourceIdentity(item.Entry.RawName.AsSpan(), item.UploadedTextures) };
                    }
                    lastX[ordinal] = registration.X;
                    lastY[ordinal] = registration.Y;
                    ushort offset = unchecked((ushort)((registration.Y & 255) << 8 | (registration.X & 63) << (2 - registration.Mode)));
                    memory.Write16(UvOffsets + (uint)ordinal * 2, offset);
                }
                item.RegisteredTextures.Add(registration);
                memory.Write16(TexturePages + (uint)(item.Index * 6 + ordinal) * 2, registration.TexturePage);
                memory.Write16(Cluts + (uint)(item.Index * 6 + ordinal) * 2, registration.Clut);
            }
            uint address = cursor;
            cursor = RegisterResource5318D0(memory, item, cursor);
            RebaseEyes(memory, address, lastX[0], lastY[0]);
            resources.Add(new(item.ModelId, item.Index, address, item.Geometry, item.Animation, item.Scale == 0 ? (byte)16 : item.Scale, item.SourceHash, item.SourceOffset, item.RegisteredTextures.ToImmutableArray()));
        }
        List<NativeFieldRegisteredInstance> instances = [];
        for (int index = 0; index < actors.Count; index++)
        {
            NativeFieldRetainedActor actor = actors[index];
            uint address = cursor;
            if (actor.ModelId < 0)
            {
                cursor = RegisterEmpty531D40(memory, index, actor.ModelId, cursor);
            }
            else
            {
                NativeFieldRegisteredResource resource = resources.Single(resource => resource.ModelId == actor.ModelId);
                cursor = RegisterInstance531980(memory, index, resource.Index, cursor);
            }
            instances.Add(new(actor.ObjectId, actor.ModelId, address, actor.ModelId < 0 ? memory.ReadOptional16(address + 12) : null));
        }
        //532A40 constructs into the same arena after relocating its input. Consumed source bytes can be reused.
        if (cursor > Kind3Limit)
        {
            throw new NotSupportedException("Native field constructed resources exceed their original kind3 arena.");
        }
        NativeFieldRegistrationState state = new(1, source.FieldId, source.ProgramHash, source.One.Sha256, preModel, relocated, sourceLength, cursor, resources.ToImmutableArray(), instances.ToImmutableArray());
        NativeBattleRetainedMemory result = retained with { FileStaging = memory.Finish(), EffectMemory = context.Effects, ModelCpuResources = memory.ReconcileCpuBanks(retained.ModelCpuResources), FieldTextureAnimations = NativeFieldTextureAnimations.Register(source.One.Sha256, resources.SelectMany(resource => resource.Textures), source.ExecutableHash) };
        if (source.ExecutableHash == NativeFieldRuntimeProfile.RemasteredEnImageHash)
        {
            result = result with { FieldTexturePlaneCacheFresh = false, FieldTextureAnimations = result.FieldTextureAnimations! with { Planes = retained.FieldTextureAnimations?.Planes ?? result.FieldTextureAnimations!.Planes } };
        }
        state.Validate(result);
        return new(state, result, context.Uploads.ToImmutableArray());
    }

    private static uint RebindProgram(NativeFieldRegistrationMemory memory, NativeFieldRegistrationLoaderInputs loader, IReadOnlyList<NativeFieldRetainedActor> actors)
    {
        ReadOnlySpan<byte> program = loader.Program.Data.AsSpan();
        if (program.Length < 8)
        {
            throw new InvalidDataException("Native retained JSM has no original header.");
        }
        int table = BinaryPrimitives.ReadUInt16LittleEndian(program[4..]);
        int code = BinaryPrimitives.ReadUInt16LittleEndian(program[6..]);
        if (table > short.MaxValue || code > short.MaxValue || code < table + 4 || code > program.Length || (code - table) % 2 != 0)
        {
            throw new NotSupportedException("Native retained JSM pointer table is outside its original signed offset domain.");
        }
        memory.Write(ProgramSource, program);
        uint tableAddress = checked(FieldArena + (uint)loader.AllocationPrefixBytes);
        int pointers = (code - table) / 2;
        uint operations = checked((tableAddress + (uint)pointers * 2 + 3) & ~3u);
        int copied = 0;
        for (int index = 0; index < pointers; index++)
        {
            ushort raw = BinaryPrimitives.ReadUInt16LittleEndian(program.Slice(table + index * 2, 2));
            memory.Write16(tableAddress + (uint)index * 2, (ushort)(raw & 0x7FFF));
        }
        for (int index = 0; index < pointers - 1; index++)
        {
            int raw = BinaryPrimitives.ReadUInt16LittleEndian(program.Slice(table + index * 2, 2));
            bool admitted = (raw & 0x8000) != 0 || actors.Any(actor => index >= actor.FirstScript && index <= actor.LastScript);
            if (!admitted)
            {
                memory.Write16(tableAddress + (uint)index * 2, 0);
                continue;
            }
            int first = raw & 0x7FFF;
            int last = BinaryPrimitives.ReadUInt16LittleEndian(program.Slice(table + index * 2 + 2, 2)) & 0x7FFF;
            memory.Write16(tableAddress + (uint)index * 2, checked((ushort)copied));
            for (int count = Math.Max(1, last - first); count > 0; count--)
            {
                memory.Copy(checked(ProgramSource + (uint)code + (uint)first++ * 4), checked(operations + (uint)copied++ * 4), 4);
            }
        }
        memory.Write32(0x1D9D0E4, tableAddress);
        memory.Write32(0x1D9CF50, operations);
        memory.Write32(0x1D9CDF8, checked((uint)copied));
        return checked(operations + (uint)copied * 4);
    }

    private static NativeFieldRegisteredTexture RegisterTexture(Context context, NativeFieldRegistrationLoaderInputs loader, NativeFieldOneModelEntry entry, uint texture, int index)
    {
        NativeFieldRegistrationMemory memory = context.Memory;
        if ((uint)index >= loader.ImageCoordinates.Length || (uint)index >= loader.PaletteCoordinates.Length || memory.Read32(texture) != 0x10)
        {
            throw new NotSupportedException("Native field texture registration requires a reached original TIM and available initialized placement slots.");
        }
        uint mode = memory.Read32(texture + 4);
        if ((mode & 8) == 0 || (mode & 7) > 2)
        {
            throw new NotSupportedException("Native field registration requires its original indexed TIM palette contract.");
        }
        uint paletteBlock = texture + 8;
        uint imageBlock = checked(paletteBlock + memory.Read32(paletteBlock));
        short width = memory.ReadSigned16(imageBlock + 8);
        short height = memory.ReadSigned16(imageBlock + 10);
        uint imagePosition = loader.ImageCoordinates[index];
        uint palettePosition = loader.PaletteCoordinates[index];
        short x = (short)imagePosition;
        short y = (short)(imagePosition >> 16);
        short paletteX = (short)palettePosition;
        short paletteY = (short)(palettePosition >> 16);
        context.Upload(x, y, width, height, memory.Read(imageBlock + 12, checked(2 * width * height)));
        ReadOnlySpan<byte> colors = loader.Colors.Data.AsSpan();
        int color = 0;
        for (int current = 0; current < Math.Min(colors.Length / 8, 64); current++)
        {
            if (entry.RawName.AsSpan(0, 4).SequenceEqual(colors.Slice(current * 8, 4)) && colors[current * 8 + 4] == 0)
            {
                color = current * 8;
                break;
            }
        }
        byte[] palette = memory.Read(paletteBlock + 12, 512);
        for (int word = 0; word < 256; word++)
        {
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(palette.AsSpan(word * 2, 2));
            if (value != 0)
            {
                int red = Math.Clamp((value & 31) + (colors[color + 5] - 128) / 8, 1, 31);
                int green = Math.Clamp((value >> 5 & 31) + (colors[color + 6] - 128) / 8, 1, 31);
                int blue = Math.Clamp((value >> 10 & 31) + (colors[color + 7] - 128) / 8, 1, 31);
                BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(word * 2, 2), (ushort)(0x8000 | red | green << 5 | blue << 10));
            }
        }
        memory.Write(paletteBlock + 12, palette);
        context.Upload(paletteX, paletteY, 256, 1, palette);
        ushort tpage = unchecked((ushort)(((y & 0x100 | x >> 2 & 0xF0) >> 4) | 4 * (y & 0x200 | 8 * (1 | 4 * ((int)mode & 3)))));
        ushort clut = unchecked((ushort)(paletteY << 6 | paletteX >> 4 & 63));
        return new((byte)(mode & 3), x, y, width, height, paletteX, paletteY, tpage, clut);
    }

    private static void RebaseEyes(NativeFieldRegistrationMemory memory, uint resource, short? x, short? y)
    {
        if ((memory.Read8(resource + 95) & 1) == 0)
        {
            return;
        }
        if (x is not short imageX || y is not short imageY)
        {
            throw new NotSupportedException("Native eye rebasing requires texture0's actual reached image placement.");
        }
        uint eyes = memory.Read32(resource + 20);
        int divisor = 16 / memory.Read8(eyes + 2);
        memory.Write16(eyes + 6, unchecked((ushort)(imageX + memory.Read16(eyes + 6) / divisor)));
        memory.Write16(eyes + 8, unchecked((ushort)(imageY + memory.Read16(eyes + 8))));
        memory.Write8(eyes + 3, unchecked((byte)(memory.Read8(eyes + 3) / divisor)));
        for (int frame = 0; frame < memory.Read8(eyes + 5); frame++)
        {
            uint position = checked(eyes + 12 + (uint)frame * 4);
            memory.Write16(position, unchecked((ushort)(imageX + memory.Read16(position) / divisor)));
            memory.Write16(position + 2, unchecked((ushort)(imageY + memory.Read16(position + 2))));
        }
    }
}
