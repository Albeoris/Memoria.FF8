# Movie subtitles

Put one UTF-8 JSON file per movie in the `movies` directory of a native Memoria package. Use `movies/discDD_II.subtitles.json`, with the disc padded to two digits and the clip padded to at least two digits. The movie address is the same for installed and replacement video. For example, `movies/disc00_30.subtitles.json` accompanies `movies/disc00_30h.avi`, and `movies/disc04_00.subtitles.json` accompanies the startup clip.

The enabled package with the highest effective priority for an address owns its whole track. Different movies can use tracks from different packages. An empty `cues` array supplies a blank track. A malformed selected track reports its owning package and document and keeps the last accepted mod publication. A playing clip keeps its selected track until playback ends. Movie timing and skip controls are owned by the existing player.

You can supply a replacement in the same package as `movies/discDD_IIh.avi` or `movies/discDD_IIh.mp4`. Native Memoria movie replacements take priority over Junction videos for the same address. A same-package `movies/discDD_II.cam` can accompany the winning video. In its absence, the installed camera timeline remains active. If one package supplies both video extensions at the same address, AVI wins. See [movie lifetime and timing](file-replacements.md#movie-lifetime-and-timing) for the replacement contract.

## Cue timing and style

```json
{
  "version": 1,
  "cues": [
    {
      "text": "First line\nSecond line",
      "startSeconds": 2.15,
      "endSeconds": 4.6,
      "layer": 1,
      "style": {
        "fontFamily": "Arial",
        "fontSize": 0.055,
        "x": 0.5,
        "y": 0.83,
        "color": "#FDF9EE",
        "bold": true
      },
      "keyframes": [
        { "seconds": 0, "opacity": 0, "reveal": 0 },
        { "seconds": 0.16, "opacity": 1, "reveal": 1 },
        { "seconds": 2.05, "opacity": 1 },
        { "seconds": 2.45, "opacity": 0, "blur": 0.6 }
      ]
    }
  ]
}
```

`text`, `startSeconds` and `endSeconds` are required. Decimal times are seconds measured from the start of the full clip, including for startup segments. A cue is drawn at its start time and removed at its end time. Different cues may overlap or start or end in quick succession. Higher `layer` values draw later, and ties keep the order written in the file. The default layer is `0`.

| Style property | Default | Meaning |
| --- | --- | --- |
| `fontFamily` | `Arial` | System font family. The system can supply a fallback when the family or a glyph is unavailable. |
| `fontSize` | `0.05` | Font em as a fraction of movie height, from `0.005` through `0.25`. |
| `x`, `y` | `0.5`, `0.9` | Center of the visible lettering as fractions of movie width and height, from `0` through `1`. The origin is top left. |
| `color` | `#FFFFFF` | Hexadecimal RGB or RGBA, such as `#FFFFFFCC`. |
| `bold` | `false` | Uses the bold font face when available. |

Write explicit line breaks as `\n`. Lines are centered on the same anchor and clipped at the movie edges. The coordinates follow the visible movie rectangle at different window sizes.

## Animating a cue

Every keyframe has a `seconds` value relative to the cue's start. Keyframe times increase strictly from zero through the cue duration. Each property interpolates linearly between its own keys. An unwritten property uses its default at time zero, and keeps its last written value after its final key. To hold a property before changing it, repeat its value at the end of the hold. A cue without keyframes uses the defaults for its entire duration.

| Keyframe property | Default | Range | Effect |
| --- | --- | --- | --- |
| `opacity` | `1` | `0`–`1` | Multiplies the text's alpha. |
| `reveal` | `1` | `0`–`1` | Reveals the text from left to right. |
| `offsetX`, `offsetY` | `0` | `-2`–`2` | Moves the text in fractions of movie width and height. |
| `scaleX`, `scaleY` | `1` | `0`–`8` | Sets independent horizontal and vertical scale. Animate both for zoom or reduce `scaleY` to zero for a vertical collapse. |
| `blur` | `0` | `0`–`2` | Sets the Gaussian blur standard deviation as a multiple of the font em. |
| `scatter` | `0` | `0`–`8` | Disperses text fragments up to the given multiple of the font em along each axis. |
| `ripple` | `0` | `0`–`8` | Adds a moving vertical wave with the given amplitude in font ems. |
| `noise` | `0` | `0`–`1` | Varies fragment opacity, brightness and position in a pattern tied to playback time. |
| `brightness` | `1` | `0`–`4` | Multiplies the color channels before saturation and intensifies the halo of blurred text above `1`. |
| `splitX`, `splitY` | `0` | `-2`–`2` | Separates two complete copies by moving them in opposite directions, in fractions of the movie width and height. |

Each document can contain up to 4,096 cues, with up to 16 active at once. Each cue can have up to 128 keyframes and 1,024 UTF-16 characters across 16 lines. The file limit is 2 MiB. Field names are case-sensitive, and unknown or repeated keys fail validation.
