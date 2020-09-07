# Pitfalls and Warnings

This is a collection of miscellaneous small warnings you may need to heed when using this engine.

## Backgrounds

- If you are making complex backgrounds, do not use functions in the class `RNG`, and do not inherit from `BehaviorEntity` or use `ParametricInfo.WithRandomID` in the objects under the background. The reason for this is that backgrounds can be turned off, so these random functions can cause replay desyncing. Alternatively, you can disable the ability to turn off backgrounds. 

## Compilation Targets

- IL2CPP (including WebGL) builds do not currently work for two reasons:
  - IL2CPP cannot handle expressions compiled to byref functions. [Related Github issue](https://github.com/dotnet/runtime/issues/31075). The root cause seems to be a dotnet policy, but this is solvable with a few minor engine deoptimizations.
  - IL2CPP cannot handle expressions compiled to functions using value types. [Related forum thread](https://forum.unity.com/threads/are-c-expression-trees-or-ilgenerator-allowed-on-ios.489498/). As far as I can tell this is not solvable except by boxing, which would be far too heavy on the GC, but Unity should eventually get around to fixing the root cause. When that happens, I will standardize IL2CPP support. 
- However, it is possible to build to IL2CPP if you avoid the use of functions written as expressions. I am working on a small amount of support for this under the compilation keyword `NO_EXPR`. 