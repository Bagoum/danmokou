# Adding Stuff

This is a reference page for all questions in the line of "How can I add/change X feature?"

## Plugins/ Unity Packages

You can add Unity packages through the Package Manager. As far as I know Unity does not have a NuGet-compatible solution, so general C# packages need to be manually imported as DLLs.

Note that in order to reference namespaces defined in Unity packages, you have to add the package's asmdef file as a reference in the asmdef of the script doing the reference. For example, most of the engine's code is under the scope of `Plugins/Danmokou/Danmaku/Danmokou.Danmaku.asmdef`, so in order to reference Unity's TextMeshPro functions from code within this folder, the `Unity.TextMeshPro.asmdef` assembly definition is linked in `Danmokou.Danmaku.asmdef`. 

## Bullets

Bullets are defined as prefabs. The engine bullets are in `Prefab/Bullets`. 

Simple bullets use the `SimpleBulletEmptyScript` components. See the existing bullets for reference.

Lasers and pathers use the `Laser` and `Pather` components respectively. See the existing bullets in `frame` for reference.

You may create the prefab anywhere. To make it accessible, link it in `SO/References/Bullet Styles`. The name you assign it here will be the name that you can access it by.

## Bullet Colors

Global colors: Add a `Palette` object to the GameManagement object under `BulletManager/Basic Gradient Palettes`. Make sure to apply this change to the prefab.

Bullet-specific colors: Add a `ColorMap` object to the `Gradients` list on the bullet definition, and provide a custom color name. If the colorization should have a unique sprite, add to `SpriteSpecificGradients` instead, and provide the sprite. (You can provide a null ColorMap for SpriteSpecificGradients.)

## On-hit Effects, Background Transitions

Add an `EffectStrategy` or `SOBGTransition` object on the GameManagement object under `Resource Manager/Effects` or `Resource Manager/ Bg Transitions`.

## Backgrounds

Backgrounds are prefabs that contain a `Background Controller` or `Background Controller 2D` component and render everything to a single layer, ARBITRARY_CAPTURE_1 or ARBITRARY_CAPTURE_2. 

Once you have created a background, you add it as follows:

- Boss nonspell/spell backgrounds: Add to boss metadata.
- Stage backgrounds: Set the `Default BGC Prefab` variable on the `NonUICameras/Wall Camera` object in the scene.
- Other cases, eg. backgrounds called arbitrarily in scripts: Add to `Danmokou/SO/References/Backgrounds`. 

## Screen Bounds, Bosses, Dialogue, Music, Items, Summonables

Most objects that are game-specific are stored on the `GameUniqueReferences` class. Each game should have its own instance of this class, and it should be linked on the GameManagement object under the "References" field in the first scene the game opens. 

Open up `MiniProjects/Scenes/MiniprojectLevel`  and take a look at the GameManagement object. Under References, you should see a "MiniProjects Game Data (TH)" object. Click on this.

Take a look at the following fields:

- Bounds describes the bounds of the playable field. The Touhou standard is `LR=±3.6` and `center=(-1.9, 0)`. You can build a widescreen game with `LR=±5` and `center=(0, 0)`. 
- Dialogue links all the dialogue storage objects used by the game. Normally you will only have one dialogue storage object. A dialogue storage object is a list of keys, and for each key a list of translated dialogue files. 
- Any bosses must have their boss metadata object added to Boss Metadata (see the [boss tutorial](tbosses.md)).
- Any characters that appear in dialogue must have their dialogue profile added to Dialogue Profiles. There is currently no dialogue tutorial.
- Tracks contains all the metadata for music played in the game.
- Items links to an ItemReferences object, which links to prefabs for each of the item types that exist by default in the game. To visually change items, simply change the prefabs linked here. To add new item types, you should subclass Danmokou.Behavior.Items.Item (see the folder Danmaku/Behavior/Items), add an entry to the Danmokou.Core.ItemType enum, and add handling for that entry in ItemPooler.RequestItem.
- Summonables links all the summonable storage objects used by the game. Normally you will have the Default Summonable Styles object, which links summonables such as enemies and screen effects, as well as a game-specific summonables object for game-specific enemies or game-specific screen effects. 

## Code

### Changing Function Names

The easiest way to change a function name is to add an alias. Take a look at the `GSRepeat` function in `Patterning/SyncPatterns.cs`. It has a tag `[Alias("GSR")]`, which means that you can call the function by the name `gsr` in scripting code. You can add as many aliases as you like. Alternatively, you can refactor the function, but that might break existing scripts.

### Adding Functions

To add a function, simply write an implementation for it in one of the classes monitored by the reflector (ie. any class in `DMK.Core` or `DMK.Danmaku` with the `Reflect` attribute). Ideally, you should also add it in the class that it is semantically close to. For example, if you want to add a SyncPattern, then add it in `Patterning/SyncPatterns.cs` or `Patterning/ExtraSyncPatterns.cs`. If you want to add a simple bullet option, then add it in `BulletManagement/SimpleBulletOption.cs`.

Note that mathematics functions are written as expressions and are therefore not as easy to add as most other functions.