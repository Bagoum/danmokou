# About

**Danmokou** is a danmaku (bullet hell) engine built in C# for Unity. It is free (as in free speech) software. The source code is on Github: https://github.com/Bagoum/danmokou

The official abbreviation for this engine is **dmk**.

You might know this project by the name NotDnh, ph4mmo, DanMaKufu, or Bagoum Danmaku Scripting Markup.

See [the website](https://dmk.bagoum.com/) for a friendly introduction to what DMK can do.

# Setup/Tutorials

## Go to [https://dmk.bagoum.com/docs/articles/setup.html](https://dmk.bagoum.com/docs/articles/setup.html) for setup and tutorial instructions

# Documentation

`https://dmk.bagoum.com/docs`

# Licensing

The source code is licensed under MIT. See the COPYING file for details, as well as information on non-code assets.

# Feature Wishlist

- GFW-style gameplay
- WebGL (see the Pitfalls section)

# FAQ

**Is there a tutorial?**

*Go to [https://dmk.bagoum.com/docs/articles/setup.html](https://dmk.bagoum.com/docs/articles/setup.html) for setup and tutorial instructions*

**Are there any examples?**

Under the `Patterns` folder, everything in `examples` and `feature testing` should work. The scripts here are fairly simple. Unlike the tutorial, these are not commented. 

The files under `demo` are from old scripts. They may not work, as the engine has gone through a lot of changes since then. 

The scripts under `Tests` are guaranteed to work, but they won't make any sense if you run them, so don't bother.

**How difficult is it to port scripts from (DNH/LuaSTG/etc) to this engine?**

You'd have to rewrite any scripts from scratch. The scripting language is highly opiniated. For example, there are no for loops (instead, there are repeater objects). 

**Can I submit PRs?**

You may freely submit PRs for bugfixes or new features. **If you submit a PR, you agree to license that code under MIT or a weaker license.** If you would like to submit non-code resources, please contact me.

**How do I contact you?**

Email `reneedebatz@gmail.com` (keys on https://keys.openpgp.org/) or contact me on Discord (`Bagoum#4773`).

**Why Unity?**

Honestly, I'd rather not be using Unity, but I don't know of any engines which support all this project's requirements. If you know of a good engine, or Godot has just received a major update fixing all the issues I list below, then contact me and I may be willing to do the port. 

- Supports C# and expression trees
- Supports indirect mesh instancing (see [my article](https://medium.com/@bagoum/devlog-002-graphics-drawmeshinstancedindirect-a4024e05737f), this is critical for this engine's efficiency)
- Supports property blocks on materials
- Supports efficient zero-allocation mesh reassignments

**What does Danmokou mean?**

See Chapter 18 of IoMIoE. 

**What does 陰陽葬 mean?**

This is the Japanese title for my Touhou fangame **Spirits in Memetic Paradise**. You can play it [here](https://www.bulletforge.org/u/bagoum/p/dong-fang-yin-yang-zang-spirits-in-memetic-paradise).

# Testing

This repository has some test coverage.

In Unity, there are "edit-mode tests", which can be run statically, and "play-mode tests", which are run while the game is running. 

Definitions for these tests are under `Assets/Plugins/Self/Testing/Tests` and `Assets/Plugins/Self/Testing/PlayTests` respectively.

Most play-mode tests operate by running some state machine and checking the output game state. State machines used for testing are in the folder `Assets/Pattern/Tests`. 

Scenes used for play-mode tests are in the folder `Assets/Scenes/Testing`.

# Pitfalls

This is a collection of miscellaneous small warnings you may need to heed when using this engine.

- If you are making complex backgrounds, do not use functions in the class `RNG`, and do not inherit from `BehaviorEntity` or use `ParametricInfo.WithRandomID` in the objects under the background. The reason for this is that backgrounds can be turned off, so these random functions can cause replay desyncing. Alternatively, you can disable the ability to turn off backgrounds. 
- IL2CPP (including WebGL) builds do not currently work for two reasons:
  - IL2CPP cannot handle expressions compiled to byref functions. [Related Github issue](https://github.com/dotnet/runtime/issues/31075). The root cause seems to be a dotnet policy, but this is solvable with a few minor engine deoptimizations.
  - IL2CPP cannot handle expressions compiled to functions using value types. [Related forum thread](https://forum.unity.com/threads/are-c-expression-trees-or-ilgenerator-allowed-on-ios.489498/). As far as I can tell this is not solvable except by boxing, which would be far too heavy on the GC, but Unity should eventually get around to fixing the root cause. When that happens, I will standardize IL2CPP support. 
  - 