## Cameras

The screen is 16x9 units. For 4k support, high-quality images should be in 240 PPU or 480 PPU.

The engine uses indirect mesh rendering for simple bullets. This disables some Unity convenience features like sorting layers, so we have to do some special things to get things sorted.

Here are the cameras ordered by depth, their culling layers, and the primary sorting layers (ordered) they use. Note that sorting layers are not enforced, you can arbitrarily change sorting layer on SpriteRenderer.

| Camera               | Culling Mask                         | Sorting Layers                   | Notes                                                        |
| -------------------- | ------------------------------------ | -------------------------------- | ------------------------------------------------------------ |
| Wall Camera          | Wall                                 | Background, Walls, FX, UI        | Depth clear + backgrounds.<br />Renders to the composite texture. |
| Low Direct-Render    | LowDirectRender                      |                                  | Player simple bullets.<br />Renders to the composite texture. |
| Middle Camera        | Player, LowProjectile, LowEffects    | Player, PlayerHitbox, Projectile | Renders to the composite texture.                            |
| High Direct-Render   | HighDirectRender                     |                                  | Enemy simple bullets.<br />Renders to the composite texture. |
| Top Camera           | TransparentFX, HighProjectile, Enemy | Enemy, Foreground, FX            | Renders to the composite texture.                            |
| 3D Camera            | 3DEffects                            | Any                              | 3D effects like boss cutins. <br />Renders to the composite texture. |
| Shader Effect Camera |                                      |                                  | Postprocessing effects like Seija's screen flip.<br />Renders to the composite texture. |
| Main Camera          |                                      |                                  | Renders the composite texture to screen.                     |
| UI Camera            | UI                                   | UI                               | Renders directly to screen.                                  |

Don't try to order Mesh/Sprites and direct-render stuff on the same camera-- it doesn't seem to work. Direct-render doesn't offer control over the sorting layer. 

## Rendering Order

Call order only affects rendering order for the **same** material. 

If you want to render certain projectiles high/lower, you need to change the "render queue" value. The default is 3000, lower means it will render *under*. I have automated this, so you can simply add a relative priority on the `SimpleBulletEmptyScript` container. 

Note, though, that this only affects things that are rendered the same way. As in, projectiles rendered via `DrawMeshInstanced` will be ordered according to this, but projectiles rendered via `MeshRenderer` cannot be reordered relative to the `DrawMeshInstanced` projectiles with this method. This is why we have separate cameras for `LowDirectRender` and `HighDirectRender`. 

# RenderTextures

You can render to a RenderTexture simply by setting `cam.targetTexture`. 

Do not set this on MainCamera, as Unity has some weird internal rules about MainCamera.

If you are doing screen-grabbing, then you usually want to render the RT back to screen to avoid skipping frames. In this case, you should do

```
cam.targetTexture = null;
Graphics.Blit(captured, null as RenderTexture);
```

in OnPostRender somewhere. (The target texture set is, for some reason, necessary.)

Note that if you are screwing with `cam.targetTexture`, you should set it **every frame** in OnPreRender; I don't know why but Unity will give you strange issues if you don't, like the main camera rendering to the target texture as well...

`RenderTextureFormat.ARGB32` (R8G8B8A8_SNORM) is the non-HDR format and `ARGBHalf` is the HDR format. This is used in `MainCamera.DefaultTempRT`. I do not use HDR.

# Frame/Render Frame

The engine runs at 120 fps (fixed) and the screen runs at 60 fps (may vary by computer). Unity's update call occurs according to the screen framerate, so within one Unity update call, the engine may run multiple engine frames. Of these multiple engine frames, the last of them is called the *render frame*, because it directly precedes Unity rendering.

For replays to be correct, render frames must have no mechanical difference from non-render frames.

On non-render frames, lasers only calculate their `centers` array, and do not update the mesh. The mesh is for graphical display only.

Pathers only update the trail renderer object on render frames. The trail renderer is for graphical display only. 