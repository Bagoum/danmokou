# Color Blending

When a non-opaque object is rendered, it needs to blend its colors with all the colors beneath it.

Consider the case where we have an existing color `(1,0,0,1)` (full-opacity red), and we are trying to render `(0,0,1,0.5)` (half-opacity blue) on top of it.

If we use the Unity blend mode `SrcAlpha OneMinusSrcAlpha`, we get:

`(0,0,1,0.5) * 0.5 + (1,0,0,1) * (1-0.5) = (0.5,0,0.5,0.75)` (partially-transparent purple)

The blend mode `One OneMinusSrcAlpha` returns:

`(0,0,1,0.5) * 1 + (1,0,0,1) * (1-0.5) = (0.5,0,1,1)` (full-opacity indigo)

Practically speaking, **both of these results are wrong**. The correct result is `(0.5, 0, 0.5, 1)` (full-opacity purple).

For whatever reason, using either of these methods in a default Unity scene with a default camera will work without issue (sometimes they don't, but usually they do). However, they will always not work if you render to a RenderTexture (you can test this by creating a RenderTexture and setting the default camera's Target Texture to it. Put a translucent color on top of a solid color and observe that the alpha channel of the RenderTexture is less than 1, or that the color is too bright.)

To blend correctly, we need a different blend for the alpha channel. One functional blend is `OneMinusDstAlpha One`. In this case, the Unity blend mode `SrcAlpha OneMinusSrcAlpha, OneMinusDstAlpha One` gives us:

`(0,0,1,0.5) * (0.5,0.5,0.5,0) + (1,0,0,1) * (0.5,0.5,0.5,1) = (0.5,0,0.5,1)`

DMK uses render textures to render backgrounds, and the current implementation of basic screen rendering is also based on render textures. As such, if you have partially-transparent sprites, you need to use this alpha blend mode or something similar (regardless of what color blend mode you use) to make rendering work correctly. If you don't, you will get some very strange errors, like darkened objects, upside-down reflections of the screen rendering in your backgrounds, mysterious apparitions in photos, and so on. 

All the default DMK shaders use `OneMinusDstAlpha One` for alpha blending.



See posts:

- https://www.reddit.com/r/Unity3D/comments/5s4u8e/how_to_get_the_correct_composite_alpha_channel/

-  https://www.tangledrealitystudios.com/code-examples/problem-with-transparency-in-rendertexture-screencapture-or-readpixels-unity/