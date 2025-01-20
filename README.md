# About

**Danmokou** is a danmaku (bullet hell) engine built in C# for Unity. It is free (as in free speech) software. The source code is on Github: https://github.com/Bagoum/danmokou

See [the website](https://dmk.bagoum.com/) for a friendly introduction to what DMK can do.

Setup and tutorial instructions can be found [here](https://dmk.bagoum.com/docs/articles/setup.html).

The changelog can be found [here](https://dmk.bagoum.com/docs/articles/changelog.html).

Documentation can be found [here](https://dmk.bagoum.com/docs).

There is a VSCode extension for DMK's scripting language, which can be found [here](https://marketplace.visualstudio.com/items?itemName=Bagoum.dmkscripting).

# Licensing

The source code is licensed under MIT. See the Danmokou.LICENSE file for details, as well as information on non-code assets. Note that submodule projects may have different licenses.

# FAQ

**Is there a tutorial?**

*Go to [https://dmk.bagoum.com/docs/articles/setup.html](https://dmk.bagoum.com/docs/articles/setup.html) for setup and tutorial instructions*

**Are there any examples?**

For real game code, see the MiniProjects directory. There are many small game scripts there, such as [the scripts](https://github.com/Bagoum/danmokou/tree/master/Assets/Danmokou/MiniProjects/Projects/THJam13) for this [Ikaruga-style jam game](https://bagoum.itch.io/kaimaroku) or [the scripts](https://github.com/Bagoum/danmokou/tree/master/Assets/Danmokou/MiniProjects/Projects/FlappyBird) for this [Flappy Bird-inspired jam game](https://bagoum.itch.io/super-flappy-bird). You can also check out the [scripts from Spirits in Memetic Paradise](https://github.com/Bagoum/danmokou-simp/tree/master/Scripts) if you have that installed as a submodule.

Under the `Patterns` folder, everything in `examples` and `feature testing` should work. The scripts there are fairly simple, but don't have many comments. The scripts in `Patterns/bdsl2` are instructive for learning about the scripting language's features.

**How difficult is it to port scripts from (DNH/LuaSTG/etc) to this engine?**

You'd have to rewrite any scripts from scratch. The scripting language is highly opiniated. For example, bullets are fired through repeater objects instead of for loops.

**Can I submit PRs?**

You may freely submit PRs for bugfixes or new features. **If you submit a PR, you must license that code under MIT or a MIT-compatible license.** If you would like to submit non-code resources, please contact me.

**How do I contact you?**

Email `reneedebatz@gmail.com` (keys on https://keys.openpgp.org/) or contact me on Discord (`Bagoum#4773`).

**Why Unity?**

Honestly, I'd rather not be using Unity, but I don't know of any engines which support all this project's requirements, and at this point there's too much investment in Unity-specific handling to have a simple port.

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

A significant amount of the engine's logic is in the [Suzunoya](https://github.com/Bagoum/suzunoya) repository, which is a pure C# project with a good amount of NUnit-based coverage.

