# Ahead-of-Time Compilation

DMK makes extensive use of expression trees and runtime reflection to handle script code and make script math more efficient. However, on IL2CPP / Ahead-of-Time Compilation platforms (eg. iOS, WebGL, PS4, XBox, Switch), both of these language features are severely limited.

As of v7.0.0, DMK supports using an *expression precompilation* model for IL2CPP. Before exporting your project, you can use the precompilation pipeline to convert all of your expressions into source code, which can then be compiled into the final export and used as a replacement for those same expressions.

## Precompilation Limitations

- Any usage of expression compilation with variable values at runtime cannot be encoded. For example, consider `FXY CreateAdder(float addTo) => "+ {addTo} x".Into<FXY>()`. Since the compiled expression cannot be determined at precompile time, there is no way to precompile this function. You should be able to get around this by using generic compilation (see the CrosshairOpacity function) or writing a lambda yourself.
- Expressions that are created using nontrivial runtime data structures require special hoisting treatment. For example, let's say we have an expression function `TEx<Vector2> ConstantValue(Vector2 x) => Expression.Constant(x)`. Vector2 is a "trivial" data structure, in that we can recreate any Vector2 by simply creating another one, and this is easily handled via `BakeCodeGenerator.DMKObjectPrinter`. On the other hand, there is an expression function `SBCFp Force(ExPred cond, VTP path)`, that then constructs a `Movement` struct using the VTP. The `Movement` struct is a "nontrivial" data structure, so in the precompile handling, it needs to be hoisted above the method and replaced with a variable. This hoisting handling is doable, but it is not trivial.
- Runtime compilation for purposes such as procedural content generation or user-input scripts is not handled by precompilation, and is still not feasible on IL2CPP platforms.
- If any change is made to the script files, even a change to `shift-phase-to` in phase 0, precompilation must be redone.

## Instructions for IL2CPP Usage

First, ensure that you are using the ReflectInto attribute on all reflected object fields/properties. The expression precompilation procedure will search through all GameObjects and ScriptableObjects in your project and precompile fields/properties marked with this attribute. Usage is as follows:

- For a string field/property that may be reflected into type T at runtime, use `[ReflectInto(typeof(T))]`. It is OK if the string might be empty. 
  - For an RString field/property or a string array field/property, do the same.
- For a property of type T that creates an object of type T when it is evaluated, use `[ReflectInto]`.

Examples:

```C#
//An inspector field "rotator" is converted into type TP3 at runtime.
[ReflectInto(typeof(TP3))]
public string rotator;
private TP3 rotate;

protected override void Awake() {
    base.Awake();
    rotate = rotator.Into<TP3>();
}

//FireOption option locations is dependent on the player shot power,
// so it is of type string[]. Attribute usage is the same
[ReflectInto(typeof(TP3))]
public string[] powerOffsetFree;

//BossConfig rotators are properties that return type TP3 directly. 
[ReflectInto]
public TP3 SpellRotator =>
    ReflWrap<TP3>.Wrap(string.IsNullOrWhiteSpace(spellRotator) ? defaultSpellRotator : spellRotator);
private const string defaultSpellRotator = "pxyz(0,0,lerpback(3, 4, 7, 8, mod(8, t), -220, -160))";
```

Second, make sure that all pattern/dialogue scripts have the extension `.txt` (or change the filter in BakeCodeGenerator:BakeExpressions).

Third, put all pattern/dialogue scripts in either the `Assets/Danmokou/Patterns` folder, or a folder that has been added to the `Script Folders` field for the Game Unique References object for your game. Below is an image of how the `Script Folders` property is filled out for Spirits in Memetic Paradise.

<img src="..\images\Unity_u3hLarftEl.jpg" style="zoom:33%;" />

Finally, do the following immediately before exporting a build:.

- Set TeleportAtPhaseStart and any other editor-only features off.
- In Project Settings/Player, add the compile flag `EXBAKE_SAVE` to the current platform.
- In the editor, run any scene with the GameManagement object active, with the References field set to the Game Unique References for the game you will export.
- Right-click the GameManagement object and select "Bake Expressions" in the context menu. This may freeze Unity for several seconds. When it is finished, Playmode will stop, and you should see entries in the folder `Assets/Danmokou/Plugins/Danmokou/Danmaku/Expressions/Generated/`, as well as a file `Assets/Danmokou/Plugins/Danmokou/Danmaku/AoTHelper_CG.cs`. 
- Remove the compile flag `EXBAKE_SAVE`. Add the compile flag `EXBAKE_LOAD`. You should now be able to run through the game in Editor mode, and it will only use the precompiled expressions. When this compile flag is present, an exception will be thrown when a requested precompiled expression does not exist.
  - **If there are errors in the generated code, or there are missing expressions during the game, please file a ticket or otherwise contact me.**
- You can now build/export the game with the compile flag `EXBAKE_LOAD` on the IL2CPP backend.

