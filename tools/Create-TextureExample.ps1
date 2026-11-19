[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$exampleRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $exampleRoot)
{
    if (Get-ChildItem -LiteralPath $exampleRoot -Force | Select-Object -First 1)
    {
        throw 'Choose a new or empty output directory for the synthetic fixture.'
    }
}

if (-not ('MemoriaSyntheticTexturePng' -as [type]))
{
    Add-Type -TypeDefinition @"
using System;
using System.IO;
using System.IO.Compression;
using System.Buffers.Binary;
using System.Text;

public static class MemoriaSyntheticTexturePng
{
    public static byte[] Create()
    {
        using MemoryStream png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), 8);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), 8);
        header[8] = 8;
        header[9] = 6;
        Chunk(png, "IHDR", header);
        using MemoryStream pixels = new MemoryStream();
        using (ZLibStream compressed = new ZLibStream(pixels, CompressionLevel.Optimal, true))
        {
            for (int y = 0; y < 8; y++)
            {
                compressed.WriteByte(0);
                for (int x = 0; x < 8; x++)
                {
                    bool light = (x / 2 + y / 2) % 2 == 0;
                    compressed.Write(new byte[] { light ? (byte)235 : (byte)35, light ? (byte)170 : (byte)65, light ? (byte)55 : (byte)140, 255 });
                }
            }
        }
        Chunk(png, "IDAT", pixels.ToArray());
        Chunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void Chunk(Stream output, string name, byte[] data)
    {
        byte[] type = Encoding.ASCII.GetBytes(name);
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        output.Write(number);
        output.Write(type);
        output.Write(data);
        uint crc = uint.MaxValue;
        foreach (byte value in type)
        {
            crc = Update(crc, value);
        }
        foreach (byte value in data)
        {
            crc = Update(crc, value);
        }
        BinaryPrimitives.WriteUInt32BigEndian(number, ~crc);
        output.Write(number);
    }

    private static uint Update(uint crc, byte value)
    {
        crc ^= value;
        for (int bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return crc;
    }
}
"@
}

$relativeImage = 'textures/ff8/rsynthetic/rcheckerboard%2Etex/members/rimage/palette-0/r.png'
$imagePath = Join-Path $exampleRoot $relativeImage
New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($imagePath)) -Force | Out-Null
[IO.File]::WriteAllBytes($imagePath, [MemoriaSyntheticTexturePng]::Create())
$descriptor = [ordered]@{
    id = 'synthetic-texture-example'
    name = 'Synthetic texture example'
    package = 'Examples'
    description = @{ en = 'Procedurally generated checkerboard fixture. Its synthetic target does not exist in the game.' }
}
[IO.File]::WriteAllText((Join-Path $exampleRoot 'mod.json'), ($descriptor | ConvertTo-Json -Depth 4))
[IO.File]::WriteAllText((Join-Path $exampleRoot 'README.txt'), "This is a redistributable synthetic fixture, not a game replacement.`nTarget: ff8 / synthetic/checkerboard.tex / image / palette 0 / empty variant.`nImage: 8 by 8 RGBA pixels, created procedurally; no game content is included.`n")
Write-Output $exampleRoot
