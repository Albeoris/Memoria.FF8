# Silver title artwork

`SilverCrest.contours.json` is vector geometry traced from the user-supplied clean silver crest (reference image 3, 1254 × 1254 RGBA). It contains contour point lists, holes, and silver tone values. It contains no encoded pixels, textures, or bitmap data. Its source SHA-256 is recorded in the JSON.

The converter removes brushed microtexture with a bilateral filter and a small normalized Gaussian filter. It traces the alpha silhouette separately, traces 40 cumulative grayscale tone regions, and simplifies contour segments with a maximum 0.72 source-pixel Douglas–Peucker tolerance. This retains the supplied lion profile, cutout jaws, swept mane, metallic ridges, ornamented guard, and blade instead of substituting a generic symbol.

The result has 497 silhouette points and 70,022 tone-contour points across 2,021 contours. Runtime initialization tessellates these closed paths, including their holes, into approximately 864,132 triangle-list vertices (13.8 MB before the smaller wordmark and highlight buffers). Geometry is generated once. Drawing does not read the source image or rebuild paths. The travelling highlight uses precomputed contour intersections.

The wordmark uses individual Times New Roman glyph outlines at their natural proportions, with tracked spacing and larger word spaces. Its outlines also become vector triangles, without font textures.

Crest/wordmark exposure and edge glints follow `TitleSceneAnimation.LightIntensity`, shared with the background's 8-second pulse. The crest's narrow reflection travels over the precomputed highlight slices every 16 seconds. No geometry is generated or uploaded during animation.

To regenerate the vector source, supply the original design-reference image to the offline tool:

```powershell
& 'python' Artwork/Tools/trace_silver_crest.py <reference-image.png> Artwork/SilverCrest.contours.json
```

Run this from the title scene project folder. The converter requires NumPy and OpenCV, available in the indicated local research runtime; the shipped application requires neither. The bitmap is a design reference only and is not part of the title renderer's embedded resources.
