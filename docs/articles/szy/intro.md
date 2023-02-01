# Introduction to Suzunoya

[Suzunoya](https://github.com/Bagoum/suzunoya) is a free and open-source data-first visual novel library. It is *data-first* in that the Suzunoya library contains no GameObjects or MonoBehaviors or any other code dependent on concepts from game engines. The Suzunoya library only contains pure C# classes and interfaces.

When we actually want to incorporate a visual novel into a game, we of course need to instantiate GameObjects and the like that appear on screen or are subject to player interaction. In Suzunoya, we handle this via *mimicry* architecture. The idea behind mimicry is that all the heavy lifting is done in the pure C# classes handled by the Suzunoya library, and on top of that we have a small layer that translates the pure C# classes into GameObjects by mimicking their values. 

For example, let's say we have a pure C# object that has a field called "Location" of type `Subject<Vector3>`. (`Subject` is the common description for a value that can change and which sends notifications to subscribers when it changes. In Suzunoya, it is called `Evented`.) On its own, this variable does not allow us to make a GameObject appear on screen, as we would want for actual game usage. To make a GameObject appear on screen with the correct location, we can write a very simple GameObject class that executes code similar to the following:

```
pureObject.Location.Subscribe(loc => gameObject.transform.position = loc);
```

Essentially, all this GameObject class does is copy the values of the pure C# object into engine-specific handling, such as transforms or renderers. This GameObject is called a *mimic*.

The mimicry architecture makes the core Suzunoya library more testable, simple, and engine-independent. It also allows handling game logic at a much higher level than you would otherwise get by working directly over GameObjects.

## Suzunoya's Scope

Suzunoya is built for use in C#, and does not have a simplified scripting frontend like Danmokou, or like other other visual novel engines such as RenPy. If you are not comfortable with C#, it may be difficult to use Suzunoya. On the flip side, this also means that you can drag-and-drop Suzunoya into almost *any* project, as long as it uses C#. You can simply use it for dialogue handling in an RPG, or you can use it to make a full-on visual novel, or you can use it to make an ADV (point-and-click style) game.

In order to ensure maximum modularity, Suzunoya's scope is very limited. It is capable of much fewer things than other VN engines. For example, Suzunoya does not contain any support for playing sound effects. Instead, you can simply call into your own project's sound effect services when running Suzunoya code. It also does not have any generalized UI support beyond the basic default dialogue box provided in Suzunoya-Unity. Case in point: while Suzunoya supports saving and loading, you will have to write your own UI handling for the menus that allow the player to save and load. If you need an out-of-the-box environment in which all these features are provided, you can use Danmokou, which has all these auxiliary systems.

## A Simplest Use Case (in Unity)

Because Suzunoya has no knowledge of engine-specific concepts like GameObjects, it can be used with any game engine, as long as you create the relevant mimic GameObjects. The [Suzunoya-Unity](https://github.com/Bagoum/suzunoya-unity) library contains mimic implementations specific to Unity, which we will examine here.

In Suzunoya-Unity, the scene Plugins/SuzunoyaUnity/Examples/ExampleScene contains the basic setup for running a visual novel using Suzunoya. This example can be run in any Unity project, regardless of whether or not you are using Danmokou.

Before running this scene, make sure you have the five user layers `RenderGroup0, RenderGroup1, RenderGroup2, RenderGroup3, RenderGroup4` defined in Project Settings > Tags and Layers. This is used for rendering handling (more details in the [rendering doc](rendering.md)). The order does not matter.

There are three objects in the scene. 

- The first is the main camera, which has no special scripts. Its culling mask is set to UI only, which is the layer to which all visual output from Suzunoya-Unity is exported in its rendering handling. 
- The second object is VNWrapper, which contains two important scripts: `VNWrapper`, which is responsible for creating mimics, and `ExampleVNUpdater`, which simply calls `DoUpdate` on VNWrapper every frame with Unity's inbuilt `Update` function.
- The last object is ThisIsAVNScript, which contains the `ExampleVNScript` file. Let's take a closer look at this file ([Github link](https://github.com/Bagoum/suzunoya-unity/blob/main/Plugins/SuzunoyaUnity/Examples/ExampleVNScript.cs)).

The `ExampleVNScript` file shows the most basic case for running visual novel code with Suzunoya. First, we set up the VNState object on which we want to run the code:

```
var lifetimeToken = new Cancellable();
tokens.Add(lifetimeToken);
var globalData = new GlobalData();
var instanceData = loadFrom == null ?
	new InstanceData(globalData) :
	InstanceData.Deserialize<InstanceData>(loadFrom.text, globalData);
vn = new UnityVNState(lifetimeToken, instanceData);
```

- `lifetimeToken` is a cancellation token that, when cancelled, will destroy everything within the scope of the VNState. 
- `GlobalData` (interface `IGlobalData`) is a class that contains settings and other data that are shared between all possible VNStates. You should serialize and save this separately, depending on your game setup.
- `InstanceData` (interface `IInstanceData`) is a class that contains data specific to one VNState, such as the current location and the results of user choices or other interactions. We can trivially serialize and deserialize it, which allows straightforward handling of saving and loading. See the [save/load documentation](saveload.md) for more details.
- `UnityVNState` is a thin wrapper around `VNState` (interface `IVNState`) that handles Unity-specific rendering and interaction. VNState is a class that contains references to constructed objects and executing code.

UnityVNState/VNState are pure C# classes, which means that they won't do anything on their own. We need something else to create the mimics and also send updates and user input to VNState.  This is the purpose of VNWrapper. Thus, the next step is to register the constructed VNState with VNWrapper, so it receives updates and inputs from Unity:

```
ServiceLocator.Find<IVNWrapper>().TrackVN(vn);
```

This completes setup. Afterwards, we can start running code on the VNState. Let's take a look at the `RunSomeCode` function. It returns a `BoundedContext`, which is the type for a task that runs on a VNState and which may save results into the instance data. The constructor for BoundedContext takes three arguments, the VNState, a string identifier for the results saved, and a constructor for an asynchronous task (`Func<Task>`). This task is where the actual code is run.

The first line of the task code is:

```
using var alice = vn.Add(new ExampleCharacter());
```

`ExampleCharacter` is a pure C# object. If you look at the class file, you can see that it's `Name` property is set to "Alice". You'll also see the corresponding mimic class, `ExampleCharacterMimic`, defined in the same file. `ExampleCharacterMimic` is a MonoBehavior, and its `CoreTypes` property links it to `ExampleCharacter`. 

When we call `new ExampleCharacter()`, we create an instance of the pure C# object, and nothing else happens. When we call `vn.Add`, the object is added to the VN context, so it receives updates when the VN receives updates. Furthermore, VNWrapper will create an instance of `ExampleCharacterMimic` at this point. The value returned by `vn.Add` is the pure C# object, and in some cases we can access the mimic by accessing the `Mimic` property on it.

The pure C# object implements IDisposable. When it is disposed, it is destroyed, and the mimic also copies its destruction. `using` makes it so that the the object is disposed when the task ends. If we don't want "Alice" to get destroyed at the end of the task, we can remove the `using`. 

The next two lines are:

```
alice.Location.Value = new(-2, 0, 0);
alice.Tint.Value = new(1f, 0.8f, 0.8f);
```

Location and Tint are pure C# event types. We set new values on them, and the mimic will indirectly copy the values to gameObject.transform.position and spriteRenderer.color by listening to the events.

Note that the types for Location.Value and Tint.Value are not UnityEngine.Vector3 and UnityEngine.Color, but rather System.Numerics.Vector3 (inbuilt C# type that's about the same as UnityEngine.Vector3) and FColor (a custom type for 0-1 floats defined in Suzunoya's underlying libraries).

The most important part of a visual novel is, of course, dialogue:

```
using var dialogueBox = vn.Add(new ADVDialogueBox());
await alice.Say("Hello world").C;
```

To make dialogue, we first need to make a dialogue box. Then, our characters can speak into the dialogue box. We can provide the dialogue box as an argument to the `Say` function, but if we don't, then the VNState will default to the first created dialogue box. 

`alice.Say("Hello world")` is an asynchronous operation (VNOperation) for gradually printing the text "Hello world" into the dialogue box. We could just await this operation, but in general we want to wait for a user confirm input, such as a mouse click, before going to the next dialogue line. By attaching `.C`, the VNOperation is transformed into a VNConfirmTask, which will wait for user confirmation before proceeding to the next line.

```
await alice.EmoteSay("happy", "Foo bar").C;
```

`EmoteSay` is a convenience function that combines two functions: `SetEmote`, which changes the emote on the character, and `Say` as shown above. We could also write this as `await alice.SetEmote("happy"); await alice.Say("Foo bar").C;`. Changing the emote may cause the sprite and icon of the game object to change, depending on how the mimic is defined. In this case, the sprite changes from a blurry circle to a non-blurry circle, and the dialogue box icon changes from a black square to a white square.

```
await alice.MoveBy(new(5, 0, 0), 2f, Easers.EOutSine);
```

Suzunoya also contains many utilities for manipulating objects. `MoveBy` will shift the position of the object by a delta (`new Vector3(5, 0, 0)`) over a given time period (`2` seconds) with a provided easing function (`EOutSine`, defaults to InOutSine). (See [this website](https://easings.net/) for a reference on common easing functions. There are many defined in the helper class `Easers`.) 

```
using var bob = vn.Add(new ExampleCharacter2());
bob.Location.Value = new(-2, 0, 0);
bob.Tint.Value = new(0.8f, 0.8f, 1f, 0f);
```

This creates a different class `ExampleCharacter2`, which has the field `Name` set to `Bob`. As before, we set the location and tint, though this time the tint has an alpha of zero, which means that the sprite will not initially be visible.

```
await bob.FadeTo(1f, 1f).And(bob.Say("Lorem ipsum dolor sit amet")).C;
```

The `FadeTo` function creates an asynchronous operation that will update the alpha value of Bob's sprite to 1 over 1 second. We've seen the `Say` function before, and these two operations are joined via `And`, which combines two VNOperations so they run at the same time. Alternatively, you could use `Then`, which runs them in sequence. When both operations are complete, then `.C` will wait for player confirmation.

If you run the scene, this code will be executed. While the code is executing, you can see all the mimics generated as children of VNWrapper: `BasicDialogueBox(Clone)`, which has the mimic for `new ADVDialogueBox`, `ExampleCharacter(Clone)` for `new ExampleCharacter`, and `RGMimic(Clone)`, which is an implicitly-constructed mimic that handles rendering in the Unity context. See the [rendering](rendering.md) documentation for more details on RGMimic. When Bob appears, the mimic `ExampleCharacter2(Clone)` is also constructed.



This concludes the introduction to Suzunoya. 



