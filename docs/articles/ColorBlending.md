# Color Blending

Unity has a few problems with how it blends colors. These problems only show up when you start rendering on a transparent background, which is not the "default Unity scene" use case, as a default setup will have a camera that clears to black (<0, 0, 0, 1>) or a similar color, and then cameras that render to screen on top of it. However, if you work with render textures and custom shaders, you might just end up banging your head against this part of Unity. This doc explains the general problem of color blending, how to think about color blending, and how to solve color blending issues in Unity.

## Generalized Problem: Overlapping Colors

Let's start by introducing a simple problem. There exists a blue circle with opacity 0.4 (<0, 0, 1, 0.4>). On top of the blue circle, there exists a red circle with opacity 0.7 (<1, 0, 0, 0.7>). What is the color of the area where they overlap?

We can solve this problem in reverse by rendering this in Photoshop and then using the specialized eyedropper info tool to get the information.

<img src="..\images\red_blue.jpg" alt="red_blue" style="zoom:50%;" />

The RGB values are displayed as 0-255. To convert it back to 0-1, the color of the overlap area is <0.855, 0, 0.104, 0.82>.

This is a very strange result, and it's not at all clear how we got from clean numbers to long fractions. Also, if we use the eyedropper on the red circle, it returns <1, 0, 0, 0.7>. It's strange that the red color has gone down even though the red circle is on top.

However, it turns out that this all becomes a lot simpler when we use *premultiplied alpha* colors (hereafter "pcolors").

## Premultiplied Alpha Colors (PColors)

For a color C = <R, G, B, A>, we can compute the equivalent pcolor PC = [R, G, B, A] as:

`PC = [C.R*C.A, C.G*C.A, C.B*C.A, C.A]`

In other words, we multiply the RGB components by the alpha component. This is why it is called *premultiplied alpha*. 

Let's compute the pcolors in the problem above. The red circle has pcolor [0.7, 0, 0, 0.7]. The blue circle has pcolor [0, 0, 0.4, 0.4]. The overlap has pcolor [0.701, 0, .119, 0.82].

Except for a small rounding error due to the 0-255 reading from Photoshop, we can see that the overlap pcolor actually has the same red value as the red circle pcolor, which we should expect since the overlap color is on top.

It turns out that the equation for combining two pcolors is very simple. Let `X -> Y` be the operation where we put the object X on top of the object Y. If `P(X)` and `P(Y)` are the pcolors of X and Y, then we have the following equation:

`P(X -> B) = [P(X).R + (1 - P(X).A) * P(Y).R, P(X).G + (1 - P(X).A) * P(Y).G, P(X).B + (1 - P(X).A) * P(Y).B, P(Y).A + (1 - P(Y).A) * P(X).A]`

Thus, if we have a shader where the source is P(X) and the dest is P(Y), we can combine them using the operation `One OneMinusSrcAlpha, OneMinusDstAlpha One`, and the output will have `P(X -> Y)`.

- Note that the alpha blend mode `OneMinusDstAlpha One` is mathematically equivalent to `One OneMinusSrcAlpha` (`A1*(1-A2)+A2 = A1+(1-A1)*A2`). This means that we can also use use `One OneMinusSrcAlpha` without declaring a separate alpha blend.

Let `C(X)` be the color of X. Recall that based on the definition of pcolors, we have `P(X).R = C(X).R * C(X).A` and `P(X).A = C(X).A`. This means that we can also write the combination expression as follows:

`P(X -> B) = [C(X).R * C(X).A + (1 - C(X).A) * P(Y).R, C(X).G * C(X).A + (1 - C(X).A) * P(Y).G, C(X).B * C(X).A + (1 - C(X).A) * P(Y).B, P(Y).A + (1 - P(Y).A) * C(X).A]`

Thus, if we have a shader where the source is C(X) and the dest is P(Y), we can combine them using the operation `SrcAlpha OneMinusSrcAlpha, OneMinusDstAlpha One`, and the output will have `P(X -> Y)`. 

For rendering purposes, this means that as long as we keep our destination render (ie. the "accumulation" of all rendered objects)  in terms of pcolors, it's actually quite straightforward to deal with rendering. If the destination render is not in terms of pcolors, there's simply no way to encode "normal blending" with basic shader operations. 

## Generalized Solution For Unity Usage

There are three things we need to be careful of here: 

1. Standard objects have a source of colors. Therefore, they need to use `SrcAlpha OneMinusSrcAlpha, OneMinusDstAlpha One`. (or the equivalent `SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha`).
   - Alternatively, they can include the line `c.rgb *= c.a;` at the end of their fragment shader (which converts a color into a pcolor) and then use `One OneMinusSrcAlpha`. eg. TextMeshPro (TMP_SDF.shader) and many inbuilt Unity shaders do this.

2. If we capture camera output into a render texture, that render texture has a source of pcolors. If we then render the render texture, it needs to use `One OneMinusSrcAlpha`. 

3. When we render to final output that is not the screen (such as an image saved to disks), we need to convert from pcolors back to colors, since Unity expects pcolors for the screen, but other final output generally expects colors. This means including the two lines `if (c.a > 0) {c.rgb /= c.a;}` at the end of the fragment shader.

For standard objects, most (maybe all) Unity stuff uses `c.rgb *= c.a` and `One OneMinusSrcAlpha`. DMK shaders usually use `SrcAlpha OneMinusSrcAlpha, OneMinusDstAlpha One` for historical reasons. 

For rendering render texture output, the following DMK shaders use such functionality:

- FinalScreenRender (outputs the accumulated global render texture to screen)
- ViewfinderRender (outputs the accumulated global render texture for export as an image file in screenshot functionality)
  - This also has code to handle the case in 3.
- BackgroundCombiner (outputs the combined output of up to two background render textures)
- SeijaCamera (applies mutations to the accumulated global render texture)
- RenderGroupDisplay (displays the content of render groups, which are groupings of visual novel objects in Suzunoya-Unity)

Note that some of these shaders use Blend Off instead, because they are the sole rendering in the destination texture.

Note that to use additive instead of normal blending, we simply use `One` instead of `OneMinusSrcAlpha` for the destination color blender. Taking a standard object as an example, assuming we included the line `c.rgb *= c.a` as per an inbuilt Unity shader, we would use the blender `One One, One OneMinusSrcAlpha`. 

## Other People Who Have Had This Issue

See posts:

- https://www.reddit.com/r/Unity3D/comments/5s4u8e/how_to_get_the_correct_composite_alpha_channel/

-  https://www.tangledrealitystudios.com/code-examples/problem-with-transparency-in-rendertexture-screencapture-or-readpixels-unity/