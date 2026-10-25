# Controller prompt vectors

`sony.svg` and `xbox.svg` contain original geometric artwork created for this product and distributed under the product license. They contain no extracted game images, controller photographs, embedded fonts or external resources.

Each atlas contains sixteen 64 × 64 groups in a four by four grid. The group IDs identify physical controls:

| ID | Control |
| --- | --- |
| 128, 129 | Left / right trigger |
| 130, 131 | Left / right shoulder |
| 132, 133, 134, 135 | North / east / south / west face button |
| 136 | Back / share / view |
| 137, 138 | Left / right stick press |
| 139 | Start / options / menu |
| 140, 141, 142, 143 | D-pad up / down / left / right |

The SVG files are the scalable sources embedded in the application. `ControllerVectorArtwork` draws their circles, rectangles, polygons and polylines at the requested prompt size. The live and preview canvases use this same renderer; letters are outlined geometry and require no installed font. Sony face symbols retain the traditional green triangle, red circle, blue cross and pink square. Xbox face letters retain the traditional green A, red B, blue X and yellow Y.

Prompt metadata carries both physical control and selected presentation. Generic and Nintendo prompts retain their descriptive labels. Plain text output preserves names without private-use presentation markers.

For a rendered contact sheet and mixed keyboard/controller example from the actual UI path, set `FF8_CONTROLLER_PREVIEW_DIRECTORY` to an output directory and run the `ControllerArtworkPromptTests` tests. The export test writes `sony-controller-atlas.png` and `xbox-controller-atlas.png` there.
