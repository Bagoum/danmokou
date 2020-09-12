# Pitfalls and Warnings

This is a collection of miscellaneous small warnings you may need to heed when using this engine.

## Reflection

A lot of this engine's power is in its ability to turn simple text into complex C# objects via reflection and expression trees. Keep these things in mind when using reflection:

- Interpolating floats into strings and then reflecting them is **incorrect**. Depending on locale, a float may be interpolated as `1.2` or `1,2`, but the engine only reads `1.2` as valid. The correct way to do this is with FormattableString.Invariant: 

  ```C#
  // FireCutin.cs
  fireScaler = 
      FormattableString.Invariant($"lerpsmooth bounce2 {timeToFirstHit} {timeToSecondHitPost} t {fireMultiplier.x} {fireMultiplier.y}").Into<BPY>();
  ```

- Functions in the math libraries may construct caches of data (see `stopsampling`). Various forms of data caching are linked in DataHoisting, PrivateDataHoisting, and PublicDataHoisting. When the boss clears a phase, these data caches are emptied to prevent cross-contamination between bullets (`DataHoisting.ClearValues`). When switching between scenes, the data caches are destroyed and unlinked from the data hoisting classes. The reason for this is to prevent out-of-scope reflected content, such as complex boss StateMachines, from remaining in memory when they are no longer in use, and to preserve complete logical independence between scenes. As a result, as a general rule, any constructed objects that depends on functions that use data caches must be destroyed when the scene ends. If you are using static objects or objects on the GameManagement GameObject (which is persistent across scenes) that depend on these libraries, you should wrap it in `ReflWrap<T>`, which will lazily load the object the first time it's used in a scene. For an example, see `PlayerBombs.cs:TB1_1`.

## Backgrounds

- If you are making complex backgrounds, do not use functions in the class `RNG`, and do not inherit from `BehaviorEntity` or use `ParametricInfo.WithRandomID` in the objects under the background. The reason for this is that backgrounds can be turned off, so these random functions can cause replay desyncing. Alternatively, you can disable the ability to turn off backgrounds. 

## Compilation Targets

- IL2CPP (including WebGL) builds do not currently work for two reasons:
  - IL2CPP cannot handle expressions compiled to byref functions. [Related Github issue](https://github.com/dotnet/runtime/issues/31075). The root cause seems to be a dotnet policy, but this is solvable with a few minor engine deoptimizations.
  - IL2CPP cannot handle expressions compiled to functions using value types. [Related forum thread](https://forum.unity.com/threads/are-c-expression-trees-or-ilgenerator-allowed-on-ios.489498/). As far as I can tell this is not solvable except by boxing, which would be far too heavy on the GC, but Unity should eventually get around to fixing the root cause. When that happens, I will standardize IL2CPP support. 
- However, it is possible to build to IL2CPP if you avoid the use of functions written as expressions. I am working on a small amount of support for this under the compilation keyword `NO_EXPR`. 