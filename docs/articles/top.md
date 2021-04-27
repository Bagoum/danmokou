# What is Danmokou?

Welcome to the website for **Danmokou** (DMK), a free and open source 2D bullet hell engine built in Unity. See the [interactive demo](https://dmk.bagoum.com/demo)!

DMK allows you to efficiently create games that look something like this:

![image1](../images/Unity_MvNemqYyDO.jpg)

(Screenshot from [Spirits in Memetic Paradise](https://www.bulletforge.org/u/bagoum/p/dong-fang-yin-yang-zang-spirits-in-memetic-paradise))

![Unity_dTItUZvSOf](../images/Unity_dTItUZvSOf.jpg)

(Screenshot from [Fantastic Poetry Festival](https://bagoum.itch.io/fantastic-poetry-festival))

![Unity_cPqgj6VYwv](../images/Unity_cPqgj6VYwv.jpg)

(Screenshot from [Oops! All BoWaP](https://www.bulletforge.org/u/bagoum/p/oops-all-bowap))



Of the bullet hell engines you might find, Danmokou is one of the fastest, supporting over **100,000 projectiles at 4k 120 FPS**. Other engines will give you somewhere between 5,000 and 30,000 bullets at a lower resolution and framerate. It is also one of the most efficient for development with features such as instantaneous script recompilation. 

The entirety of DMK is written in C# with a little bit of F#, so you can change it freely. The scripting language is a thin wrapper around native C# that makes writing code even more efficient.

Here is a (not entirely complete) feature list for DMK:

- 100,000+ bullets at native 4k, 120 FPS
- Built-in architecture for stage-based games
- Built-in architecture for photo games like Shoot the Bullet
- Built-in architecture for challenge-based scene games
- Efficient compiled scripting language with the same interfaces as C#
- Instantaneous runtime script recompilation
- Support for practice modes, replays, spell history, high scores, statistics, and achievements
- Dialogue engine with Ace Attorney-style text unrolling
- Curved lasers, curvy lasers, and wavy lasers (yes, they're all different)
- Arbitrary functions for bullet movement  (No other engine supports this!)
- Optimized rendering pathway batching 500+ bullets at a time 
- Engine handling for complex cancellation pathways
- Zero-allocation bullets-- no lag and no garbage collector spikes!

If you're interested in the design of DMK and how it compares to other engines, you can read the [design philosophy introduction](t06.md).

If you want to try playing around with DMK, you can follow the instructions in the [setup doc](setup.md) to get started.

