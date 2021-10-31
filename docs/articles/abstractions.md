# Abstractions Reference

This page lists the key abstractions used in the engine.

## RegularUpdater / CoroutineRegularUpdater

RegularUpdater is a MonoBehavior class that receives updates from the fixed ETime update loop rather than the Unity update loop. This class is fundamental to determinism and replay consistency. 

Note that the ETime update loop runs at 120 frames per second, regardless of refresh rate. This is the constant `ETime.ENGINEFPS = 120`. You can change this, but keep in mind that (a) all existing scripts that define operations in terms of frames (such as "fire a bullet every 5 frames") will change speed accordingly; and (b) if the engine FPS is not a multiple of all possible fixed refresh rates (defined as options in `XMLPauseMenu`), then users may see the number of engine updates between successive screen refreshes vary.

CoroutineRegularUpdater (CRU) is a subclass of RegularUpdater that has handling for coroutines (similar to Unity's StartCoroutine and the like). The two main methods it features are `RunRIEnumerator` and `RunDroppableRIEnumerator`. When a CRU is destroyed, it will try to close all its running coroutines by advancing them one frame and asserting that they have all finished. Any coroutine executed with `RunRIEnumerator` will log an error if it is not finished at this time.

 If you are using `ICancellee` or if your coroutines are awaited by other code, you should use `RunRIEnumerator` and `if (cancellee.Cancelled)` to ensure that the callback is performed. All code related to StateMachines and pattern execution does this. If your enumerator can safely be stopped at any time when the level ends, or if you don't have a cancellation pathway, then you can use `RunDroppableRIEnumerator`.

Basically any class you make should inherit from these two classes and use `RegularUpdate` instead of `Update`. 

## BehaviorEntity

BehaviorEntity (BEH) is the basic agentive game object in DMK.

- It can run StateMachines and fire bullets. 
- It can move around in general patterns by using the Velocity struct.
- It has handling for dealing damage to the player by collision (though I do not use this).
- It may also have an Enemy component (for damageable enemies only). 

The following objects in DMK levels are BehaviorEntities:

- the player (PlayerInput:BehaviorEntity)
  - Note that there is no existing usage for having the player run state machines or the Velocity struct.
- player shots (FireOption:BehaviorEntity)
  - All player shots are fired by one or more FireOptions, which have StateMachine scripts attached to them. They are not fired by the player itself.
  - The handling for player firing is basically the same as the handling for NPC firing, and uses all the same scripting interfaces. 
- bosses (BossBEH:BehaviorEntity)
  - BossBEH is a thin wrapper around BehaviorEntity that is used for bosses.
- enemies (BehaviorEntity + Enemy)
  - Enemies that *are damageable* have an Enemy component. Bosses also have an Enemy component.
- the stage itself (LevelController:BehaviorEntity)
- lasers/pathers (Laser/Pather:FrameAnimBullet:Bullet:BehaviorEntity)
- some graphical effects, such as Rect/CircleDrawers and Powerups.

## StateMachine

A StateMachine (SM) is basically "a function that does something on a BehaviorEntity". 

All boss scripts are StateMachines.

All stage scripts are StateMachines.

All player shot scripts are StateMachines.

Dialogue scripts used to be StateMachines, but as of v8, 

Boss and stage scripts use the PatternSM abstraction. A PatternSM is broken up into multiple phases that support arbitrary selection and switching. See the [tutorial on phases](t07.md) for details on PatternSM.

Most StateMachines are written in a text format, and then **reflected** into C# objects. This process of reflection occurs at runtime and allows us to recompile scripts while the game is running. When you are writing scripts, you should be writing them in text format. 

