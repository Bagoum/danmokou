# About

**Danmokou** is a danmaku (bullet hell) engine built in C# for Unity. It is free (as in free speech) software. The source code is on Github: https://github.com/Bagoum/danmokou

You might know this project by the name NotDnh, ph4mmo, or Bagoum Danmaku Scripting Markup.

## Features

- Supports 100,000 bullets updating at native 4k 120 FPS (on my machine anyways)
- Extremely optimized mathematics logic that allow using arbitrary function for movement via expression trees
- Extremely optimized rendering pathway that batches 1000 bullets at a time
- Zero-allocation bullets
- Concise, opinionated, and always-correct model for summoning more than one bullet
- Efficient compiled scripting language with the same interfaces as native code
- Instantaneous runtime script recompilation
- Simple logic for unbounded difficulty controls
- Built-in dialogue engine with Ace Attorney-style text unrolling
- Built-in script analysis and practice architecture

# Building This Project

To build this project, follow the following steps:

- Run `git submodule update --init --recursive`. This imports code from SiMP which has some useful default values for getting started. This is not required but will help as a reference. (**Note: the SiMP repository is currently private, so this command probably won't work.**)
- Open Scenes/BasicSceneOPENME

- Import TextMeshPro essentials 

- You should see a prompt to do this as soon as you open the scene above
- You may need to run the project, stop the project, and run it again to fix lingering metadata bugs in UXML. 

- (Optional) Build the F# project in the `FS` folder targeted at 4.7.2. I have provided DLLs so you don't have to do this unless you make changes to the F# code.
  - Copy the output DLLs (Common, FParsec, FParsecCS, FSharp.Core, Parser, System.ValueTuple) to `Assets/Plugins/Self/Core`
- Then build it in Unity. The current version I am using is 2020.1.0b14. Any version above 2020 should work (there are some new mesh functions in 2020 that are not present in 2019). 

# Documentation

`https://dnh.bagoum.com/docs`

# Licensing

The source code is licensed under GPL v3 or later. Most other non-code resources are licensed under miscellaneous mostly-permissive licenses, preferably CC-BY. See the COPYING file for details.

The documentation site is almost entirely exported from the source code and is therefore also licensed under GPL, see the site for more information.

# Some Important Terminology

- "Curvy lasers" (as in Mysterious Snake Show) are in this engine called **pathers**.
- This engine splits lasers into several different functionalities. There are **straight lasers**, which are straight. Straight lasers may have a rotation function, which makes them **rotating lasers**. There are also **curved lasers**, which follow some arbitrary trajectory defined by a math function. A curved laser can be **static** or **dynamic**.
  - See `Patterns/examples/new lasers` to compare the laser variants.

# Feature Wishlist

- GFW-style gameplay
- Replays (this is probably possible)

# FAQ

**Are there any examples?**

Under the `Patterns` folder, everything in `examples` and `feature testing` should work. The scripts here are fairly simple.

The files under `demo` are from old scripts. They may not work, as the engine has gone through a lot of changes since then. 

The scripts under `Tests` are guaranteed to work, but they won't make any sense if you run them, so don't bother.

**How difficult is it to port scripts from (DNH/LuaSTG/etc) to this engine?**

You'd have to rewrite any scripts from scratch. The scripting language is highly opiniated. For example, there are no for loops (instead, there are repeater objects). 

**Can I submit PRs?**

You may freely submit PRs for bugfixes or new features. **If you submit a PR, you agree to license that code under the GNU GPL v3 or later.** If you would like to submit non-code resources, please contact me.

**How do I contact you?**

Email `reneedebatz@gmail.com` (keys on https://keys.openpgp.org/) or contact me on Discord (`Bagoum#4773`, [server](https://discord.gg/9GfkagF)).

**Why Unity?**

Honestly, I'd rather not be using Unity, but I don't know of any engines which support all this project's requirements. If you know of a good engine, or Godot has just received a major update fixing all the issues I list below, then contact me and I may be willing to do the port. 

- Supports C# and expression trees
- Supports indirect mesh instancing (see [my article](https://medium.com/@bagoum/devlog-002-graphics-drawmeshinstancedindirect-a4024e05737f), this is critical for this engine's efficiency)
- Supports property blocks on materials
- Supports efficient zero-allocation mesh reassignments

**What does Danmokou mean?**

See Chapter 18 of IoMIoE. 

**What does 陰陽葬 mean?**

This is the Japanese title for my Touhou fangame **Spirits in Memetic Paradise**. You can play the demo [here](https://www.bulletforge.org/u/bagoum/p/dong-fang-yin-yang-zang-spirits-in-memetic-paradise-demo).

# Testing

This repository has some test coverage.

In Unity, there are "edit-mode tests", which can be run statically, and "play-mode tests", which are run while the game is running. 

Definitions for these tests are under `Assets/Plugins/Self/Testing/Tests` and `Assets/Plugins/Self/Testing/PlayTests` respectively.

Most play-mode tests operate by running some state machine and checking the output game state. State machines used for testing are in the folder `Assets/Pattern/Tests`. 

Scenes used for play-mode tests are in the folder `Assets/Scenes/Testing`.

