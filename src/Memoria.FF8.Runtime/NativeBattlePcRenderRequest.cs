using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePcRenderViewport([property: JsonRequired] int X, [property: JsonRequired] int Y, [property: JsonRequired] int Width, [property: JsonRequired] int Height);

public sealed record NativeBattlePcRenderVertex(float X, float Y, float Z, float ReciprocalW, uint ColorArgb, float U, float V);

/// <summary>A56D1D0 call receipt. The framebuffer capture, texture allocation and final D3D material remain the port renderer's responsibility.</summary>
public sealed record NativeBattlePcRenderTailReceipt([property: JsonRequired] uint Requested, [property: JsonRequired] NativeBattlePcRenderViewport Viewport)
{
    [JsonIgnore] public uint DepthBits => 0x3AC49BA6;
    [JsonIgnore] public uint ColorArgb => 0x55FFFFFF;
    [JsonIgnore] public uint ExpandArgument => Requested == 2 ? 1u : 0u;

    public void Validate()
    {
        if (Requested == 0 || Viewport is null)
        {
            throw new InvalidDataException("Native PC render-tail receipt requires its actual nonzero request and viewport DWORD owners.");
        }
    }

    public ImmutableArray<NativeBattlePcRenderVertex> QuadVertices56D5F0()
    {
        Validate();
        int left = unchecked(Viewport.X - 1);
        int top = unchecked(Viewport.Y - 1);
        int right = unchecked(Viewport.X + Viewport.Width - 2);
        int bottom = unchecked(Viewport.Y + Viewport.Height - 2);
        if (ExpandArgument != 0)
        {
            left = unchecked(left - 16);
            top = unchecked(top - 16);
            right = unchecked(right + 16);
            bottom = unchecked(bottom + 16);
        }
        float z = BitConverter.Int32BitsToSingle(unchecked((int)DepthBits));
        return [new(left, top, z, 1, ColorArgb, 0, 0), new(right, top, z, 1, ColorArgb, 1, 0), new(left, bottom, z, 1, ColorArgb, 0, 1), new(right, bottom, z, 1, ColorArgb, 1, 1)];
    }
}

/// <summary>47CF50's process DWORD and the current47CF60 tail-call receipt, independent of COMMON packets.</summary>
public sealed record NativeBattlePcRenderRequest([property: JsonRequired] uint Requested, [property: JsonRequired] NativeBattlePcRenderTailReceipt? PublishedTail)
{
    public static NativeBattlePcRenderRequest FromColdProcess() => new(0, null);

    public void Validate()
    {
        PublishedTail?.Validate();
    }

    public NativeBattlePcRenderRequest Request47CF50(int argument0, int argument1, int argument2, int argument3, int argument4)
    {
        Validate();
        return this with { Requested = unchecked((uint)argument0 + 1u) };
    }

    /// <summary>Publish the deferred PC draw command beside the executed OT. The renderer performs capture after the scene and UI.</summary>
    public NativeBattlePcRenderRequest PublishTail47CF60(bool pauseDE9, NativeBattlePcRenderViewport? viewport)
    {
        Validate();
        if (pauseDE9 || Requested == 0)
        {
            return this with { PublishedTail = null };
        }
        if (viewport is null)
        {
            throw new NotSupportedException("Native PC render-tail requires its current viewport owner.");
        }
        return this with { Requested = 0, PublishedTail = new(Requested, viewport) };
    }

    public NativeBattlePcRenderRequest Tail47CF60(bool pauseDE9, NativeBattlePcRenderViewport? viewport, Action<NativeBattlePcRenderTailReceipt>? render56D1D0)
    {
        Validate();
        if (pauseDE9 || Requested == 0)
        {
            return this with { PublishedTail = null };
        }
        if (viewport is null || render56D1D0 is null)
        {
            throw new NotSupportedException("Native PC render-tail requires its actual viewport and framebuffer capture/material service.");
        }
        NativeBattlePcRenderTailReceipt receipt = new(Requested, viewport);
        render56D1D0(receipt);
        return this with { Requested = 0, PublishedTail = receipt };
    }
}
