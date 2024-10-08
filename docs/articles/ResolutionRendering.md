## Cameras

The screen is 16x9 units. For 4k support, high-quality images should be in 240 PPU or 480 PPU.

The engine uses indirect mesh rendering for simple bullets. Unity does not support sorting layers with indirect mesh rendering (it is always rendered to the "Default" sorting layer).

Here are the cameras ordered by depth, their culling layers, and the primary sorting layers (ordered) they use. Note that sorting layers are not enforced; you can arbitrarily change sorting layer on SpriteRenderer.

| Camera      | Culling Mask                                           | Sorting Layers                   | Notes                                                        |
| ----------- | ------------------------------------------------------ | -------------------------------- | ------------------------------------------------------------ |
| Wall Camera | Wall, Obstacle                                         | Background, Walls, FX, UI        | Clears to black.<br />Captures backgrounds through BackgroundOrchestrator.<br />Renders to the composite texture. |
| Low Camera  | LowDirectRender, Player, LowProjectile, LowEffects     | Player, PlayerHitbox, Projectile | Uses indirect mesh rendering (default sorting layer with order 0) for player simple bullets and some screen effects.**<br />Renders to the composite texture. |
| High Camera | HighDirectRender, TransparentFX, HighProjectile, Enemy | Enemy, Foreground, FX            | Uses indirect mesh rendering (default sorting layer with order 0) for enemy simple bullets.*<br />Also applies postprocessing effects like Seija's screen flip via PlayScreenPostprocessing.cs.<br />Renders to the composite texture. |
| UI Camera   | UI                                                     | UI                               | Renders to the composite texture.                            |
| Main Camera |                                                        |                                  | Doesn't capture any content. Renders the composite texture to screen, adding black bars if the monitor is not 16x9. |

Indirect mesh rendering uses `Graphics.DrawMeshInstanced` or `Graphics.DrawMeshInstancedProcedural`, which are Unity functions that optimize the process of drawing the same sprite multiple times. They do not offer control over sorting layers, and are effectively rendered with Sorting Layer = Default and Sorting Order = 0. 

## Rendering Order

Call order only affects rendering order for the **same** material. 

If you want to render certain projectiles high/lower, you need to change the "render queue" value. The default is 3000, lower means it will render *under*. I have automated this, so you can simply add a relative priority on the `SimpleBulletEmptyScript` container. 

Note, though, that this only affects things that are rendered the same way. As in, projectiles rendered via `DrawMeshInstanced` will be ordered according to this, but projectiles rendered via `MeshRenderer` cannot be reordered relative to the `DrawMeshInstanced` projectiles with this method (as mentioned above). This is why we have separate cameras for `LowDirectRender` and `HighDirectRender`. 

# RenderTextures

You can render to a RenderTexture simply by setting `cam.targetTexture`. 

Do not set this on MainCamera, as Unity has some weird internal rules about MainCamera.

If you are doing screen-grabbing, then you usually want to render the RT back to screen to avoid skipping frames. In this case, you should do

```
cam.targetTexture = null;
Graphics.Blit(captured, null as RenderTexture);
```

in OnPostRender somewhere. (The target texture set is, for some reason, necessary.)

Note that if you are modifying `cam.targetTexture`, you may need to set it **every frame** in OnPreRender; the last time I dealt with such issues, various bugs popped up if I didn't set it every frame.

`RenderTextureFormat.ARGB32` (R8G8B8A8_SNORM) is the non-HDR format and `ARGBHalf` is the HDR format. This is used in `MainCamera.DefaultTempRT`. I do not use HDR.

# Camera Clearing

WallCamera (the lowest rendering camera) clears to opaque black (<0,0,0,1>), which is the "base color" of the game. All other cameras listed in the table above do not clear, since they need to render on top of WallCamera in the same RenderTexture. 

ArbitraryCapturer, a camera prefab used to separately capture only objects on certain Unity layers, clears to transparent black (<0,0,0,0>). The reason for this is that the output of an ArbitraryCapturer screen capture may itself have some transparency on it, and when that output is somehow reflected in a sprite and rendered to one of the cameras in the table above, the transparency needs to be preserved throughout.

Note: The Camera definition on WallCamera clears to transparent black (<0,0,0,0>). Whatever rendered content captured by this camera is then merged with backgrounds rendered in BackgroundOrchestrator.OnRenderImage, and the shader used for the merge operation (BackgroundOrchestrator.shader) effectively clears to opaque black.

See [Color Blending](ColorBlending.md) for a discussion of some of the issues around blending colors and preserving transparency.

# Frame/Render Frame

The engine runs at 120 fps (fixed) and the screen usually runs at 60 fps (may vary by computer). Unity's update call occurs according to the screen framerate, so within one Unity update call, the engine may run multiple engine frames. Of these multiple engine frames, the last of them is called the *render frame*, because it directly precedes Unity rendering.

For replays to be correct, render frames must have no mechanical difference from non-render frames. It is also possible for zero or two render frames to run in one Unity update call if the slowdown is modified.

A render frame is marked in-engine as `ETime.LastUpdateForScreen == true`. 

On non-render frames, lasers only calculate their `centers` array, and do not update the mesh. The mesh is for graphical display only.

Pathers only update the trail renderer object on render frames. The trail renderer is for graphical display only. 

There is also a similar functionality, `ETime.FirstUpdateForScreen`, which is used for input management.