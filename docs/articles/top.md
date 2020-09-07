# What is Danmokou?

Welcome to the website for **Danmokou** (DMK), a free and open source bullet hell engine built in Unity. 

DMK allows you to efficiently create games that look something like this:

![image1](../images/Danmokou_Gbn8TIOqvs.jpg)

(Screenshot from [Spirits in Memetic Paradise](https://www.bulletforge.org/u/bagoum/p/dong-fang-yin-yang-zang-spirits-in-memetic-paradise))

![Unity_cPqgj6VYwv](../images/Unity_cPqgj6VYwv.jpg)

(Screenshot from [Oops! All BoWaP](https://www.bulletforge.org/u/bagoum/p/oops-all-bowap))

![Danmokou_VJafl6MVNA](../images/Danmokou_VJafl6MVNA.jpg)

(Screenshot from [Fantastic Poetry Festival](https://bagoum.itch.io/fantastic-poetry-festival))

Of the bullet hell engines you might find, Danmokou is one of the fastest, supporting over **100,000 projectiles at 4k 120 FPS**. (For comparison, Danmakufu runs about 10,000 projectiles at 720p 60 FPS, and LuaSTG can get about 30,000 projectiles at 1080p 60 FPS). It is also one of the most efficient for development with features such as instantaneous script recompilation. 

The entirety of DMK is written in C# with a little bit of F#, so you can change it freely. The scripting language is a thin wrapper around native C# that makes writing code even more efficient.

Here is a (not entirely complete) feature list for DMK:

- 100,000+ bullets at native 4k, 120 FPS
- Curved lasers, curvy lasers, and wavy lasers (yes, they're all different)
- Built-in support architectures for stage-based games, scene-based games, and challenges
- Efficient compiled scripting language with the same interfaces as C#
- Instantaneous runtime script recompilation
- Replay support
- Dialogue engine with Ace Attorney-style text unrolling
- Script analysis and practice architecture
- Optimized mathematics logic that allow using arbitrary functions for bullet movement  (No other engine supports this!)
- Optimized rendering pathway 
- Engine handling for complex cancellation pathways
- Zero-allocation bullets



If you are interested in DMK, you can follow the instructions in the [setup doc](setup.md) to start playing around with it.

