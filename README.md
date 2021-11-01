# About

**Danmokou** is a danmaku (bullet hell) engine built in C# for Unity. It is free (as in free speech) software. The source code is on Github: https://github.com/Bagoum/danmokou

The engine name may be abbreviated as **dmk**.

See [the website](https://dmk.bagoum.com/) for a friendly introduction to what DMK can do.

Setup and tutorial instructions can be found here: [https://dmk.bagoum.com/docs/articles/setup.html](https://dmk.bagoum.com/docs/articles/setup.html)

The changelog can be found here: [https://dmk.bagoum.com/docs/articles/changelog.html](https://dmk.bagoum.com/docs/articles/changelog.html)

Documentation can be found here: [https://dmk.bagoum.com/docs](https://dmk.bagoum.com/docs)

# Licensing

The source code is licensed under MIT. See the Danmokou.LICENSE file for details, as well as information on non-code assets. Note that submodule projects may have different licenses.

# FAQ

**Is there a tutorial?**

*Go to [https://dmk.bagoum.com/docs/articles/setup.html](https://dmk.bagoum.com/docs/articles/setup.html) for setup and tutorial instructions*

**Are there any examples?**

For real game code, look in the directory [MiniProjects/Scripts](https://github.com/Bagoum/danmokou/tree/master/Assets/Danmokou/MiniProjects/Scripts), which contains boss scripts for recent short games I've made. You can also check out the [scripts from Spirits in Memetic Paradise](https://github.com/Bagoum/danmokou-simp/tree/master/Scripts) if you have that installed as a submodule.

Under the `Patterns` folder, everything in `examples` and `feature testing` should work. The scripts there are fairly simple, but don't have many comments.

The files under `demo` are from old scripts. They may not work, as the engine has gone through a lot of changes since then. 

The scripts under `Tests` are guaranteed to work, but they won't make any sense if you run them, so don't bother.

**How difficult is it to port scripts from (DNH/LuaSTG/etc) to this engine?**

You'd have to rewrite any scripts from scratch. The scripting language is highly opiniated. For example, there are no for loops (instead, there are repeater objects). 

**Can I submit PRs?**

You may freely submit PRs for bugfixes or new features. **If you submit a PR, you must license that code under MIT or a MIT-compatible license.** If you would like to submit non-code resources, please contact me.

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

This is the Japanese title for my Touhou fangame **Spirits in Memetic Paradise**. You can play it [here](https://bagoum.itch.io/spirits-in-memetic-paradise).

# Testing

This repository has some test coverage.

In Unity, there are "edit-mode tests", which can be run statically, and "play-mode tests", which are run while the game is running. 

Definitions for these tests are under `Assets/Plugins/Danmokou/Testing/Tests` and `Assets/Plugins/Self/Testing/PlayTests` respectively.

Most play-mode tests operate by running some state machine and checking the output game state. State machines used for testing are in the folder `Assets/Pattern/Tests`. 

Scenes used for play-mode tests are in the folder `Assets/Scenes/Testing`.

