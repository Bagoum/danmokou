## Cameras

The screen is 16x9 units. For 4k support, high-quality images should be in 240 PPU or 480 PPU.

The engine uses indirect mesh rendering for simple bullets. This disables some Unity convenience features like sorting layers, so we have to do some special things to get things sorted.

Here are the cameras, their culling layers, and the primary sorting layers (ordered) they use. Note that sorting layers are not enforced, you can arbitrarily change sorting layer on SpriteRenderer.

| Camera               | Culling Mask                         | Sorting Layers                   | Notes                                                        |
| -------------------- | ------------------------------------ | -------------------------------- | ------------------------------------------------------------ |
| Main Camera          |                                      |                                  | This is here to allow other cameras to screw around without stepping on Unity rules about Camera.main. |
| Wall Camera          | Wall                                 | Background, Walls, FX, UI        | Ground layer + Depth clear                                   |
| Low Direct-Render    | LowDirectRender                      |                                  | Player simple bullets                                        |
| Middle Camera        | Player, LowProjectile, LowEffects    | Player, PlayerHitbox, Projectile |                                                              |
| High Direct-Render   | HighDirectRender                     |                                  | Enemy simple bullets                                         |
| Top Camera           | TransparentFX, HighProjectile, Enemy | Enemy, Foreground, FX            |                                                              |
| 3D Camera            | 3DEffects                            | Any                              | 3D effects like boss cutins                                  |
| Shader Effect Camera |                                      |                                  | Renders postprocessing effects like Seija's screen flip.     |
| UI Camera            | UI                                   | UI                               | UI                                                           |

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

The engine runs at 120 fps and the screen runs at 60 fps (but may vary by computer). This means that we distinguish *render frames* as frames which precede the screen render (ie. the last frame invoked by the ETime updater loop).

For replays to be correct, render frames must have no mechanical difference from non-render frames.

On non-render frames, lasers only calculate their `centers` array, and do not update the mesh. The mesh is for graphical display only.

Pathers only update the trail renderer object on render frames. The trail renderer is for graphical display only. 