# RichTextKit Unicode bidi source subset

Source: [Topten Software RichTextKit](https://github.com/toptensoftware/RichTextKit), commit `4345e967be84d4667275db9af8f2b40c7aab0038` (0.5.169), licensed under Apache-2.0. The original copyright and license headers are retained; see LICENSE.txt. The embedded bidi class trie comes from that same revision. Unicode data is covered by UNICODE-LICENSE.txt.

This directory contains only the UAX #9 embedding-level resolver, its array helpers and the Unicode trie reader. It has no SkiaSharp, native or UI dependency. Memoria uses its embedding levels to divide HarfBuzz shaping runs by direction and script and to apply visual run ordering. HarfBuzz still supplies glyph shaping and font scripts.

Local changes: UnicodeClasses loads only the bidi trie; UnicodeTrieConstants retains the original runtime constants while excluding the offline builder; BidiLayout supplies a small array-based public facade. Nullable analysis is disabled for this unannotated upstream subset. Source formatting changes add braces without changing the algorithm.

The rest of RichTextKit is intentionally excluded because its published package versions target different SkiaSharp major versions. No older SkiaSharp implementation is used.
