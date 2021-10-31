# Bullet Indirect

This is the shader used by all simple bullets (bullets controlled and rendered by BulletManager). It uses indirect draw mesh calls, which means that critical information must be passed through PropertyBlock properties instead of through Matrix4x4. 

Currently I have three forms of information being passed, but adding more is trivial. These are: position, rotation, and time. 

### Rotation

If a bullet is nonrotational, then it will always face "right" regardless of the direction vector. This is a shader feature `FT_ROTATIONAL`. **Note that nonrotational bullets will still rotate their colliders.** This means that you should restrict nonrotational bullets to circle colliders. 

### Uniform Frame Animation

Time also allows animating sprites within the shader (!). Here's the strategy for shader animation:

- On init:
  - Organize a spritesheet of identically-sized sprites horizontally (first sprite on the left).
  - Also create a sprite that is the size of one of the spritesheet's sprites. 
  - Use MeshGenerator with the one-sprite. 
  - Change the texture of the returned material to the spritesheet.
  - Set the inverse frame multiplier and time per frame on material.shader.
- CPU rendering:
  - Put the bullet `bpi.t` in the time buffer.
- GPU rendering (shader internal):
  - Scale down X-UV values to only get one sprite's width worth of X-UV.
  - Increase the X-UV values according to the current time to switch to the specified sprite.

It's a somewhat hacky method but it's super-effective, and allows adding frame animation generically to all bullets without significant overhead. 

It is optimized such that it only permits uniform frame-time animation. This is in order to avoid frame calculation on the CPU.

This has been implemented for the cwheel bullet.

This is a shader feature `FT_FRAME_ANIM`. 

# Basic Sprite

This shader is used for most standard objects, such as enemies, lasers, and pathers. It is a fairly normal 2D shader with no fancy instancing support. The supported features are largely similar to Bullet Indirect in order to try to keep parity between simple bullets and lasers/pathers.

**It does not support uniform frame animation**. If you're using this material, you should be using some kind of animation helper like FrameAnimBullet (Laser) or BehaviorEntity.Animate. 

It supports hue shifting, which is a pretty cool rendering feature. The current implementation shifts the hue by `X * T` where X is a float value, default zero, set in the bullet option `hueShift` when an applicable object is created, and T is the lifetime of the object. I have not been able to implement this for Bullet Indirect since it would require another array of variables...

## Bullet Indirect / Basic Sprite Feature Comparison

|                            | Instanced support?    | Basic Sprite support? | Interface                      |
| -------------------------- | --------------------- | --------------------- | ------------------------------ |
| FT_ROTATIONAL              | **YES**               | NO (use BEH rotation) | Initialize                     |
| FT_FRAME_ANIM              | **YES**               | NO (use BEH helpers)  | Initialize                     |
| FT_CYCLE                   | NO (trivial addition) | **YES**               | Initialize                     |
| FT_SLIDE_IN                | **YES**               | NO (trivial addition) | Initialize                     |
| FT_FADE_IN                 | **YES**               | **YES**               | Initialize                     |
| FT_DISPLACE*               | **YES**               | **YES**               | Initialize                     |
| FT_HUESHIFT                | NO                    | **YES**               | `hueShift` option              |
| Additive Blending          | **YES**               | **YES**               | _BlendOp, _BlendFrom, _BlendTo |
| Overall opacity multiplier | **YES**               | **YES**               | _SharedOpacityMul              |

# Shader Tips and Warnings

A shader variable needs to be provided in three places:

- Set in propertyblock
- Declared in Shader Properties
- Declared again in each Subshader Pass

If the value is not set, it will have a default value.

If the variable is not declared in Properties, **the shader will work but the value may get reset randomly**.

If the variable is not declared in Subshader Pass, the shader will not compile.