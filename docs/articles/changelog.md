# Upgrading

To get the newest version from git, run:

`git fetch super` (replace `super` with whatever remote you assigned to the [DMK repo](https://github.com/Bagoum/danmokou))

`git pull --rebase super master` (again, replace `super`)

`git submodule update` (if you have made modifications to the submodules, you will need to `pull --rebase` them individually)

# Unreleased

The following features are planned for future releases. 

- [9.3.0] LockedBoundedContext handling in Suzunoya
- [9.3.0] Safeguards around control rebinding
- [10.0.0] ADV-style gameplay, state management, and generalized UI support
  - Basic implementation of ADV gameplay and state management implemented in 9.0.0 (see Purple Heart).
- [10.0.0] UI improvements, including custom cursor handling, controls tooltips, and smarter navigation on menus
  - Control tooltips and some smarter menu navigation implemented in 9.1.0
- [Backlog] Default handling for graze flake items and bullet cancel flake items
- [Backlog] Implementation of a TH18-like card engine
- [Backlog] Procedural generation of stages and bullet patterns



# v10.0.0 

#### Features

- Added a scripting function `set` which allows setting values on bullet data when it is executed. Consider the following case for homing lasers:

  ```
  async gdlaser-*/b <> gcr {
  	root zero
  	colorf({ red black }, p // 2)
  	start targetLocation =v2 cy(50)
  } laser(nroffset(OptionLocation(mine)),
  	0, _, {
  		beforeDraw(set {
  			v2 targetLocation lerp01(0.02, &targetLocation, LNearestEnemy)
  		} 0)
  		dynamic(nrvelocity(laserrotatelerp(lerpt(3, 8, 0.7, 0),
  			rotate(OptionAngle(mine), cy 1),
  			&targetLocation - loc
  		)))
  		... 
  })
  ```

  In this example, `set` is used with `beforeDraw` (a laser option that runs a BPY right before it draws the laser) to update the laser's target location based on the closest enemy. This is more visually pleasing and also more optimized than directly using `LNearestEnemy` inside `dynamic`, since the contents of `dynamic` are evaluated at every point along the laser, and `LNearestEnemy` is an expensive function.

- Added the `SetRenderQueue` pool control, which allows changing the rendering order of specific simple bullet pools. See [the bullets documentation](bullets.md) for details.

- Added initial support for bucketing bullets. "Bucketing" groups bullets based on their screen location, which makes collision detection far more efficient. Since there is an overhead to the bucketing process, it is not particularly useful for computing many-against-one collisions (such as enemy bullets against the player). It is used for player bullet on enemy collisions and  bullet on bullet collisions. You can call `RequestBucketing` on a simple bullet collection to make sure it is bucketed. To handle collisions, you can either implement `ISimpleBulletCollisionReceiver` and call `SimpleBulletCollection.CheckCollisions` (example in `PlayerController.cs`), or you can call `SimpleBulletCollection.GetCollisionFormat` and do custom handling (example in `BxBCollision.cs`).

- Added initial support for bullet-on-bullet collision. See `examples/bullet on bullet collision.bdsl`.

- Optimized handling of player bullets by adding bucketing for simple bullets, as well as AABB pruning for lasers and pathers.

- It is now possible to use generic complex bullets in player shots. (The only generic complex bullet currently present in the engine is `moon`, eg. `moon-blue/w`.) To use complex bullets, use the `complex` bullet firing function (as opposed to `s/laser/pather`).

- There is now generalized support in Danmokou's VN library for presenting "evidence" (think Ace Attorney) during dialogue, as demonstrated in [this](https://bagoum.itch.io/ghost-of-tranquil-vows) proof-of-concept game (relevant code is in `GhostOfThePastGameDef.cs`). To do this, first create a field `evidenceRequester = new EvidenceRequest<E>()` in the `IExecutingADV` process, where `E` is a parent type for the evidence that a player can present. Then, there are two ways that you can use to request evidence from the player:

  - `using (var _ = evidenceRequester.Request(CONTINUTATION)) { ... }`. In this case, the player can optionally present evidence while the code inside the brackets is being executed, and if they do, the CONTINUATION function, which must be of type `Func<E, BoundedContext<InterruptionStatus>>`, will be run on the provided evidence. It should return either `InterruptionStatus.Continue` (the code should continue running) or `InterruptionStatus.Abort` (the code should stop running). Note that you cannot save or load within such dialogue when using this method.
  - `var ev = await evidenceRequester.WaitForEvidence(KEY)`. In this case, the player *must* present evidence to continue the game execution. Save/load can still be used with this method, and KEY will be used to preserve the value of the evidence provided when saving. (Note that your evidence type E must be serializable!)

- In order to increase the modularity of supported game mechanics, mechanics are now handled by an abstraction `IInstanceFeature` that can be slotted into `InstanceData`. `IInstanceFeature` has methods that are called upon certain game events; for example, the method `OnGraze` is called when the player grazes, and the class `MeterFeature`, which implements a mechanic for special meter abilities, implements this method by adding to the meter. Furthermore, there are interfaces for specific mechanics, such as `IPowerFeature` for the power mechanic. The strength of this architecture is that implementations can easily be switched out; you can use the class `PowerFeature`, which has traditional 1-4 Touhou-style power handling, or `PowerFeature.Disabled`, which disables power items and sets the player power to always 4.

- In previous versions of the engine, it was not straightforward to add game-specific code handling. In v10, this is now handled by the `GameDef` scriptable object, which is a generic container for game-specific code. There are several abstract subclasses of `GameDef` according to the type— for example, `ADVGameDef` for ADV-style games and `CampaignDanmakuGameDef` for multi-stage danmaku games. To make game-specific code, create a subclass such as `SimpGameDef : CampaignDanmakuGameDef` and implement the abstract methods.

  - One of the abstract methods on `CampaignDanmakuGameDef` is `MakeFeatures`, which returns the set of game-specific `IInstanceFeature`s.

- Refactored code related to danmaku game execution. It is now possible to add custom handling for the execution of multi-stage games, whether this be with regards to alternate path handling (such as in Imperishable Night) or with regards to endings or with regards to anything else. You can do this by creating a scriptable object subclassing `BaseCampaignConfig`, and then override `RunEntireCampaign`. Reference `CampaignConfig.RunEntireCampaign` for the default handling.

- `FreeformUIGroup`, which is used to make keyboard-friendly interactable UI from arbitrarily-positioned nodes, can now have UIGroups within it. Use `AddGroupDynamic` to add UIGroups.

- Expression compilation with GCXU is now faster due to some extra indirection provided by `ReadyToCompileExpr`.

#### Breaking Changes

- As part of the introduction of GameDef, achievement handling has been moved from AchievementsProviderSO to GameDef.

#### Changes

- Right-clicking while playing dialogue will now bring up the pause menu instead of moving to the next line.
- Collision handling has been generalized across the engine. Now, there are three phases to RegularUpdate, which are: RegularUpdate, RegularUpdateCollision, RegularUpdateFinalize. In RegularUpdateCollision, entities should find any targets whose hurtboxes overlap the entity's hitbox, and call collision functions on those targets. In RegularUpdateFinalize, entities should perform calculations based on the sum of collisions they received. Rendering handling is also best placed in RegularUpdateFinalize.
- Parallelization support has been removed from simple bullet collision-checking in order to support bucketing.

#### Fixes

- Fixed a "bug" where, during VN execution, pressing Z to select an option from the menu at the right side of the dialogue box would also cause the dialogue to advance.

# v9.2.0 (2022/09/10)

#### Features

- There is now a Visual Studio Code language extension for the DMK scripting language, titled "Danmokou Scripting". You can find it [here](https://marketplace.visualstudio.com/items?itemName=Bagoum.dmkscripting) (or search up "Danmokou" in the extensions tab of VSCode). The extension operates over files that end in `.bdsl`.
  - Most DMK scripts have been renamed to end in `.bdsl` instead of `.txt`. They are still text files, so feel free to open them in Notepad or whatever text editor you use.
  -  **You may need to reassign any variable fields in your custom code that held references to inbuilt DMK scripts.** The code still accepts any TextAsset files as scripts. BDSL files are converted to TextAssets via `Assets/Danmokou/Editor/BDSLExtensionImporter.cs`.
    - If you have many variable fields dependent on inbuilt DMK scripts, then: make a copy of your repository, upgrade to v9.2.0 in one repository, and use "Tools > Missing References > Search in Assets" to show all broken references. You can identify what those references ought to be by looking at your un-upgraded copy.
  - Note: I recommend setting "colorized bracket pairs" off in VSCode.
- Bullet variable access (variables set within GXR repeaters and then read using `&` in bullet functions) now uses dynamic type construction instead of dictionary variable storage. This makes `&` about 5 times faster overall. This feature is controlled via the flag `PICustomDataBuilder.DISABLE_TYPE_BUILDING`. For AOT build targets that require expression baking, dynamic type construction is also not possible, so this feature is not enabled when using expression baking.
- Simple bullet updates can now be parallelized when there are more than 16384 bullets in a single pool. This limit is configurable as `SimpleBulletCollection.PARALLEL_CUTOFF` and can be disabled via `SimpleBulletCollection.PARALLELISM_ENABLED`. It is disabled by default as parallelization has significant garbage collection overhead.
- There is a patch for SRP/URP support in DMK, on [this branch](https://github.com/Bagoum/danmokou/tree/urp). I do not recommend using SRP/URP, but if you insist, please use the 2022.2 prerelease version of Unity and add [this commit](https://github.com/Bagoum/danmokou/commit/bfe0918de2c17b19ce72ffc9ec09e47fde53e3ef) to your repo.

#### Breaking Changes

- The function `powerup2` has been removed. It can be replaced with `poweraura`. For example:

  ```
  ## Old code
  sync powerup1 <> powerup2
  	x-powerup-1 x-powerdown-1
  	witha 0.9 lerp 0 1 t purple pink
  	witha 0.9 yellow
  	1.5 2 0 0.5
  
  ## New code
  sync powerup1 <> poweraura {
      sfx(x-powerup-1)
      color(witha 0.9 lerp 0 1 t purple pink)
      time(1.5)
      iterations(2)
      next({
          sfx(x-powerdown-1)
          color(witha 0.9 yellow)
          time(0.5)
          iterations(-1)
      })
  }
  ```

# v9.1.0 (2022/07/17)

This release includes various minor improvements on top of v9.0.0.

#### Features

- Bullets now support a limited amount of 3D handling. By using the `Velocity3D` or `Offset3D` VTP functions, you can modify the Z-position of the bullet. You can then use the `SortZ` pool control to make the bullets order themselves by their Z-position (lowest Z is on top, per standard Unity handling). The Z-position will be ignored for rendering and collision. Bullet rotation handling is unchanged; they will only rotate in the XY plane. See `Patterns/feature testing/z position support.txt` for an example. In SiMP, the "God's Throwing Dice" spell on stage 5 has been updated to use this mechanism.
  - Note that SortZ will slow down bullet processing on the affected pools, because sorting is expensive and must be done every frame. The amount of slowdown depends on how often the ordering of bullets changes. The sorting algorithm is a [bottom-up implementation of merge sort](https://github.com/Bagoum/suzunoya/blob/master/BagoumLib/Sorting/CombMergeSort.cs) that is extremely fast when the array is approximately sorted. You can find the sorting overhead in the Unity profiler as PlayerLoop > Update.ScriptRunBehaviorUpdate > BehaviorUpdate > ETime.Update > NPC-fired simple bullet collision checking > Z-sort.
  - Note that this does not allow you to change the sorting order between different pools.
  - Even if you do not use `SortZ`, you can still use the z-position as a variable in pool controls or the like. The functions to get x,y, and z-position are `x`, `y`, `z` respectively. For example, you may want to scale bullets based on z-position, in which you could use something like `scale(1 - 0.2 * z)`.
  - V2RV2 was not changed to support 3D handling, but may be changed in the future.
- Errors in script parsing are now more detailed and contain links to relevant files.
- Improved handling of text input. Replay names and other usages of TextInputNode can now contain capitalized letters (only via Shift, not CapsLock) and non-alphanumeric characters. This does not use `Input.inputString` due to its limitations around holding keys.
- Added smarter navigation for complex nested UI groups. When the UIGroup does not define navigation, the UI will look for the nearest HTML object in the inputted direction.
- Added full support for keyboard/mouse and gamepad control rebinding. There is a new tab on the Options menu that allows the player to rebind controls. Note that mouse-left-click is reserved and cannot be rebound. Any control that is stored in the InputConfig class (Plugins/Danmokou/Core/Core/Input/InputConfig.cs) can be rebound by adding it to the Bindings property in that class.
  - This also includes support for multiple connected controllers (XBox/PS4) and changing button prompts based on the most recently used input method. You can look this up as `InputManager.PlayerInput.MainSource.Current.focusHold.Description`-- replace `focusHold` with the relevant input button. See UIScreen.SetControlText as an example.
- Removed the garbage allocation in the frame timer used when VSync is disabled.

#### Changes

- Suzunoya now distinguishes between StrongBoundedContext (which provides guarantees that allows it to be skipped while loading) and BoundedContext (which does not provide such guarantees). It also improves the internal handling around BoundedContexts. The main consequence of this is that if you define a very long VN sequence-- such as an entire game for a traditional VN-- you may want to make some of the nested tasks StrongBoundedContexts to avoid loading lag. Also, any calls to external tasks *must* be wrapped in StrongBoundedContexts so that they can be skipped, since the internals of external tasks are not governed by VNState's loading process. Handling of external tasks will be improved with the introduction of LockedBoundedContext in a later version.

#### Fixes

- Fixed an issue where recolorizable pools would have inconsistent colors while culling.
- Fixed an issue where bullets would not use scale-in when being created or destroyed while using the legacy renderer.

# v9.0.0 (2022/05/16)

This release includes code for [Blessed Rain](https://bagoum.itch.io/blessed-rain), a short visual novel, and [The Purple Heart Paradox](https://bagoum.itch.io/purple-heart), a short ADV-format visual novel, in the MiniProjects folder. As of this release, Suzunoya functionality is mostly complete, but I haven't thoroughly tested functionality related to choices and external tasks, and there are some theoretical issues in save/load handling with regards to nondeterminism as well as certain configurations of global switches that open or close branches between executions. This release also includes some work-in-progress code for some adventure-game style usage of Suzunoya (think Ace Attorney), with some basic exploratory work in the Purple Heart project.

#### Breaking changes

- Please upgrade Unity to 2021.2.1 or 2021.3.3+ or 2022.2.a11+ before opening this project. Note that if you get errors at runtime about missing settings, you may need to run Window > UIToolkit > Package Asset Converter > "I want my assets to function without the UIToolkit package installed". This upgrade makes UIToolkit much more resilient, allows some new CSS features that are used in the updated UI, and also enables some C#8 and C#9 features that are now used in the codebase.
- The architecture for UIToolkit-based UI has been overhauled. As a part of this, all the UIScreen uxml files were replaced with a single universal file, the UIScreen and UINode classes were completely rewritten, and the process of constructing UIs in code was changed in a backwards-incompatible way. See the [UI design document](uidesign.md) for details on the new UI architecture, which I plan to keep stable.
- Dialogue profiles, which were made obsolete in v8.0.0 with Suzunoya integration, have been removed from the codebase.

#### Pending issues

- If you jump to the Replays screen from the Records screen, then view a replay, then return from the replay, then return to the records screen, the cursor will be invisible but navigation will still be possible. This is due to limitations in the menu position regeneration process.
- There is a UITK issue where disabling and re-enabling visual elements causes old pointer events to be resent in 2021.2.1. Fixed in 2021.3 and 2022.2.
- There is a pending UITK issue where some elements take extra newlines for no reason that occurs in basically every version except 2021.2.1. [See ticket](https://issuetracker.unity3d.com/issues/ui-toolkit-labels-width-is-not-extended-causing-additional-empty-lines-when-using-specific-resolutions)
- There is a long-pending UITK issue where mouse scrolling is slow. [See ticket](https://issuetracker.unity3d.com/issues/slow-scroll-view-scrolling-when-entering-play-mode-and-in-builds)
- There is a UITK issue where closing a menu while using the mouse to drag a scrollbar on it results in the mouse not applying input to any other menus.

#### Features

- Replaced the default UI handling. It should be a lot prettier now. If you had any code modifying or dependent on the default UI handling, you will need to change it.
- Added save/load handling for visual novels in DMK.
- Added an in-game license viewer. It uses a general Markdown parser from Mizuhashi, so it can support other usages as well.
- The game screen now pillarboxes or letterboxes when the resolution of the window is not 16x9, and the windowed mode can be freely rescaled in any direction. (Note that rescaling the windowed mode by dragging the corners of the window will not increase the resolution, in the sense that if you set the windowed resolution to 800x450 in the options, and then expand the window to the size of your monitor, it will still be low-quality 800x450.)
- Revamped input handling to separate types of user input (currently, KBM and controller are supported), to expose a uniform API for code-triggered pseudo-input (see InCodeInputSource), and to incorporate replays as another type of input (see ReplayPlayerInputSource).
- DMK can now be run on Android. However, I have not written any input handling for mobile yet, so only the touch-to-navigate-menus handling works.
- Added support for multiple vectors of localization (eg. having separate text and voice localization). 
- Added support for skipping only read text in visual novel segments (default setting).
- Added support for freeform UI menus (ie. where objects are assigned fixed positions) **and arrow-key traversal of such menus**.

#### Changes

- Most additive bullets now use soft additive blending instead, which should provide more visual consistency and prevent blowout.
- Moved bomb handling out of PlayerBombs into per-bomb classes. This architecture allows per-project extension.
- Removed the UISkip button (previously assigned to X). Instead, the Confirm button (assigned to Z/Enter, clicking and scrolling down also work in non-recorded contexts) now doubles as a skip button.

#### Fixes

- Fixed a bug where rotated straight lasers might incorrectly calculate rotation on their first frame.
- Fixed undesired functionality where scripts continue executing during end-of-screen cleanup if an awaited task was cancelled before the script executor received a Destroy call.
- Fixed a UI bug where shot descriptors on the Statistics page and elsewhere don't update with language settings.
- Fixed handling for 240fps monitors, and removed the "Refresh Rate" option from the game. The game should now run smoothly on any computer, with or without Vsync (though on strange framerates like 50 or 144, there may be some visual tearing, since the base framerate is 120).
- Fixed a bug where backlogging in a visual novel context might not destroy looping sound effects.
- Fixed an issue in dialogue where string characters would not be affected by surrounding color tags while fading in. This was caused by a more general design problem with TextMeshPro where alpha tags *modify* the enclosing color tag instead of creating a new enclosure.
- Fixed an issue where the meter bar would not hide itself when the player approached it.
- Fixed some lingering issues with color blending. See [the color blending doc](ColorBlending.md).

# v8.0.0 (2021/10/31)

- **Breaking changes:**
  - The functions `bullet-control-sm`, `beh-control-sm`, and `laser-control-sm` have been removed. Instead, use `bullet-control`/`beh-control`/`laser-control` with the SM command.
- **Code formatting notices:**
  - I have re-added csproj/sln files to the repo. Be warned of possible merge conflicts.
  - License files now consist of a single Markdown file for each repository/submodule.
  - Much code in the Core assembly has been moved to the BagoumLib subproject in the [Suzunoya repo](https://github.com/Bagoum/suzunoya). It is now provided as a DLL.
  - FastExpressionCompiler has been replaced with a custom expression printing solution in BagoumLib.
  - The FS project has been deleted and its code has been moved out, some of it to [localization-utils](https://github.com/Bagoum/localization-utils), some of it to [Suzunoya/Mizuhashi](https://github.com/Bagoum/suzunoya), and some of it to Plugins/Danmokou. 
- **Pending issues:**
  - If you see a compiler error about missing PanelSettings, you can fix it by triggering a recompile (eg. by adding code or comments to any C# file in the solution). This is probably due to some oddities in UIToolkit.
  - The UIToolkit bug with CJK characters (see Pending issues in the v7.0.0 changelog) has been **resolved** in the current version of UIToolkit.
- Replaced legacy dialogue system with Suzunoya-based visual novel system. Old-style dialogue scripts will still work, but they are superseded by in-code visual novel scripts. See `MiniProjects/Scripts/CV_VNStage.txt` (wrapper stage code to execute a visual novel script) and `MiniProjects/Plugins/Danmokou/VNCrimsonVermilion.cs` (a fairly long visual novel script for the proof-of-concept [Crimson and Vermilion](https://bagoum.itch.io/crimson-and-vermilion)).
  - Note that dialogue profiles must be ported to Suzunoya entities.
  - Suzunoya supports much more thorough dialogue localization via the same string localization strategy as the rest of the engine. You can get it working with spreadsheets using https://github.com/Bagoum/localization-utils .
- Improvements to service-based architecture (I hate singletons now)
- Complex bomb implementations for Reimu, Mokou, and Mima (who has a nice black hole effect)
- New bullet types: GDCircle (a circle, size between "circle" and "lcircle", with heavy displacement effects), StellDecagon (a once-stellated decagon, or alternatively two pentagons on top of each other), GDLaser2c (a variant of gdlaser which has two colors-- format `gdlaser2c-red;blue/w`).
  - Bullets may now enable multi-channel automatic colorization (only current example is gdlaser2c) by setting "Multi Channel Recolor" to "RB" and using red and blue channels in the sprite. Three-channel recoloring is not enabled but is trivial to add to the existing code. Be warned that a typical (one-channel) bullet has about 30 recolors, a RB multi-channel bullet has about 300 recolors, and a RGB multi-channel bullet has about 3000 recolors.
- When simple bullets are softculled, they fade out in addition to (now optionally) spawning a softcull effect like cwheel. The fade out process can be configured as fadeOut on SimpleBulletEmptyScript (it will use the fadeIn config if none is provided).
- Simple bullets now softcull over time at end-of-phase. By default, they only fade out and do not have a softcull effect.
- Complex bullets, including complex player bullets, may now have nontrivial colliders by attaching a GenericColliderInfo script to the same object as the Bullet script.
- Player bullets may now have noncircular hitboxes (effectiveRadius has been removed).
- Player bullets may now use empty guiding.
- Implementation of complex player teams and runtime ship switching
- Significant improvements to the architecture around engine state management, especially pausing, loading, and freezeframes
- Improved functionality and architecture of events in script code. See EventLASM
- Spell and card circles now trail the boss while they are moving.
- Multiple small improvements around boss UI graphics (among which: fixed an issue where the UI HP bar would flash or temporarily display the incorrect color, fixed an issue where `set-ui-from` would not work with boss life stars, fixed issues with the UI timer not deactivating)
- Proof-of-concept implementation of a music room
- Support for dynamic difficulty (rank)
- Internal handling of end-of-phase is now entirely handled via cancellation tokens and disposables, which (besides being much better for the architecture) theoretically allows for boss scripts to independently run alongside other content.
- Refactored base namespace to "Danmokou" (formerly "DMK")
- Documentation and code improvements for reflection handling

# v7.0.0 (2021/03/23)

- **Breaking changes:**
  - Please upgrade your Unity to **2020.2**. 2021 currently has a bug with UIToolkit disallowing switching between debug and release modes. 2019 is no longer supported (it does not support some C#8 changes).
  - `SS`, `SSD`, `SDD`, `SSDD` and related simple-bullet firing functions have been removed. Use `simple` instead with `scale`/`dir`/`dir2` options (<xref:Danmokou.Danmaku.Options.SBOption>)
  - Previous versions of DMK allowed implicit casts from TP to TP3 and from TP3 to TP in script code. This kind of circular cast is architecturally problematic, and support has been removed in v7.0.0. Now, TP may be implicitly casted to TP3, but the downcast is not allowed. Instead, use the `TP` function, as follows: `nroffset(tp(qrotate(...)))`. The reason that the TP->TP3 cast is prioritized is to protect the rule that *implicit casts should not result in information loss*.
  - The signatures of functions operating over firing options, lasers, and player data have changed (see the note below about FiringCtx). The names have also been standardized. See `Assets/Danmokou/Plugins/Danmokou/Danmaku/Math/MathRepos/GenericMath/ExM.cs`.
- **Pending issues:**
  - There is a bug in the current version of UIToolkit that makes Chinese/Japanese/Korean text break when used on a label with text-wrap. To avoid this, the text wrapping on the UINode and UINodeLRSwitch UXML files has been disabled for now. When this bug is fixed, this will be reverted.
- Fixed a very long-standing issue with mysterious transparent pixels in render textures. See [this doc](ColorBlending.md) for a primer on what was going on.
- In-game achievements and achievements screen
- Implementation of localization utilities in FS/StaticParser, and basic end-to-end localization of the engine complete for Japanese
  - Dialogue localization is not yet handled in a fully satisfactory way, but it works.
- Parser code ported to C#
- Shot demo on shot selection screen
- Statistics display screen
- Nonpiercing lasers (see the Reimu Lazors shot)
- Added a feature for clearing bullets on the screen when the player dies
- Expression functions for lasers, options, and player fire now operate via an extra field in the BPI struct called "FiringCtx". The object is bound to the FiringCtx and is used by the expression function. For example, a laser fired by an option can use `OptionLocation(mine)` to get its location. An option can use `PlayerMarisaAPos(mine, 0.5)` to get the recorded position of the player 0.5 seconds ago (computed according to MoF MarisaA rules). A player laser can use `LaserIsColliding(mine)` to get a bool for whether or not it is currently colliding with an enemy. This fixes a lot of the very strange static and case-specific handling for these usages.
  - Private data hoisting now also operates via FiringCtx.
  - Method data hoisting (within eg. StopSampling) now also operates via FiringCtx.
  - (There are no plans to change the functionality of public data hoisting (eg. guideEmpty).)
- Support for generalized function reflection. This is primarily for use within C# code and for architectural cleanliness. For example, below is the code that creates the opacity function for the inner crosshair circle. This functionality allows using string reflection in many more cases than previously possible.

```c#
CompileDelegate<Func<float, float, float, ParametricInfo, float>, float>(@"
if (> t &fadein,
    if(> t &homesec,
        c(einsine((t - &homesec) / &sticksec)),
        1),
    eoutsine(t / &fadein))",
    new DelegateArg<float>("fadein"),
    new DelegateArg<float>("homesec"),
    new DelegateArg<float>("sticksec"),
    new DelegateArg<ParametricInfo>("bpi", priority: true)
)
```

- Support for precompiling expressions (IL2CPP/AoT platforms are now functional, including WebGL and (untested) Switch). NO_EXPR support removed.
- `[CanBeNull]` annotations replaced with C#8 nullable reference annotations
- Gradient remapping code now uses custom `DGradient` class instead of Unity gradients, making runtime texture coloring take about 75% less time.
- Fixed a float rounding error that would cause frame-animated bullets to disappear for one frame when looping around on some computers.
- Support for a play-mode menu and a "commentator" on menus (see SiMP v4)
- Time-variant autoculling at end of boss phase (bullets farther away from the boss appear to cull later)
- Powerup effect refactored into PowerAura with options architecture (renamed to avoid the name overlap with the Powerup item)
- Phase performance display at end of boss phase
- Fixed a bug where bosses could be destroyed during the two-frame window between phases

# v6.0.0 (2021/01/23)

- **Breaking changes:**
  - The `action` SM has been removed. Instead, use `paction`, which is always blocking and takes only one argument, which is the delay. `action block X` = `paction X`. 
  - The special SM `wait`, which would be used under summons when they had no code to run but still needed to be auto-cleaned at the end of the phase, has been replaced by `stall`. This is due to the naming contradiction with the SM command `wait(time)`. 
- **Pending issues:**
  - There is a bug in the current version of UIToolkit that makes Chinese/Japanese/Korean text break when used on a label with text-wrap. To avoid this, the text wrapping on the UINode UXML file has been disabled for now. When this bug is fixed, this will be reverted.
- [Fantastic Poetry Festival v2](https://bagoum.itch.io/fantastic-poetry-festival) and [Spirits in Memetic Paradise v3.2.0](https://bagoum.itch.io/spirits-in-memetic-paradise) released!
- Replay size compression
- Minor bugfixes around BehaviorEntity event subscription
- Lorelei-style visual impairment effect (see SyncPattern `Darkness`)
- Bullet tinting functionality for lasers/pathers (via `tint` option) and simple bullets (via `tint` pool control)
- Fixed bugs in various edge cases around GXRepeat usage and non-blocking SMs
- Rearchitectured ease functions to no longer be stringly typed via `[LookupMethod]` attribute support in reflection libraries
- Significant improvements to namespacing and script file organization
- Fixed argument ordering oddities for StateMachine reflection (no changes to script code required)
- Consistent naming scheme for "Engine" (the underlying libraries of DMK), "Game" (a full product such as SiMP or FPF), and "Instance" (the act of playing or replaying the game in any of its modes)
- New default card/spell circles
- Improved localization support
  - Localization is doable but engine support is not feature-complete. Let me know about your feature requirements if you're planning to localize your game.

# v5.2.0 (2020/12/06)

- Improvements to Seija-style screen flipping/rotation
- Support for arbitrary "complex" bullets (gameobjects subclassing Bullet). See the Moon bullet type, which features a particle trail

# v5.1.0 (2020/12/03)

- Improvements to code coupling via DI and events

# v5.0.0 (2020/11/24)

- Spirits in Memetic Paradise v3 released!
- All submodules rewritten with TH viewport; wide viewport deprecated
- Variant sidebars for Touhou-size viewport
- Granular custom difficulty modifiers with menu support
- UI nodes now use pointers for selection instead of underlining
- Fancy difficulty select menu
- Improvements to player select menu
- UI screens now slide when enabled/disabled/switched
- High score display window
- Spell history
- Separated BehaviorEntity display handling into DisplayController class; added y-transform breathing modification
- Player bullet controls (experimental)
- Cancellation-bounded bullet controls
- Partial game scores and continued replays
- Bugfixes for nested summons and controls over summons

# v4.2.0 (2020/10/18)

- Piecewise dialogue sprites
- Touhou-like viewport structure (see `Miniprojects/Scenes/Working Scene (TH)`)
- Persistent backgrounds
- Traditional Touhou delay effects
- Miscellaneous bugfixes for replays and photo games
- Improved architecture for recording scene-game completion

# v4.1.0 (2020/10/16)

- Fixed critical bug in coroutine code for gcrepeat with wait 0
- Fixed bug in continuation challenges
- Bullet style updates
- Replay optimizations
- Architecture upgrades for collision
- Minor updates to WebGL demo
- Basic implementation of player teams and player switching (to complete in 4.4.0)
- Basic implementation of extra difficulty modifiers, eg. bullet speed multiplier (to complete in 4.3.0)

# v4.0.0 (2020/10/04)

- [WebGL demo](https://dmk.bagoum.com/demo) (note: WebGL is not generally supported, see [the warnings page](warnings.md) for details).
- Ending architecture.
- Generalized challenge architecture to enable BPoHC-style random events (yet unimplemented).
- Arbitrary bullet recoloring via `recolor` BEHOption/ LaserOption/ SimpleBullet pool control.
  - Note: a simple bullet pool control is used instead of an SBOption in order to minimize the struct size of SimpleBullet. This feature only works with the `recolor` palette, so adding 16 bytes to the struct for a small fraction of bullet counts would be inefficient. This does make the usage with simple bullets slightly more contorted and less flexible when there are multiple recolor patterns executing simultaneously.
- Unified difficulty/player selection screen (and it's fancy!).
- Ability to run different script code based on the selected player via `PlayerVariant`.
- Powerbomb support. See `PlayerBombs.cs:PowerRequired`.
- Multishot support (that is: a group of shots that can be switched between during the game by grabbing a special powerup object).
- Support for "respawning" the player on death. Set PlayerHP.RespawnOnHit to true.
- Espgaluda meter mechanic.
- New items types: gems (meter refill), full power, 1up, multishot switches.
- Support for difficulty sliders in stage-based games.
- UI improvements.
- Improved handling for global slowdown effects.
- Improved handling for Seija-style camera flipping/rotation.
  - Camera flipping has the following limitations:
    - Boss distortion effects are disabled when a flip is active. This is because the existing distortion effects end up flipping twice, which makes the effect appear incorrect.
    - Photos are not taken correctly when the screen is flipped, since the code uses the true position of the viewfinder to determine what to capture. The resulting photo object that appears on screen will also appear at the true position of the viewfinder, since the photo objects display on the UI camera, which does not flip.
- Improved handling for player-fired lasers/pathers.
- Improved handling for looping audio effects.

# v3.0.0 (2020/09/16)

- Restructured repository files by bringing engine code under Assets/Danmokou. This will eventually be separated as a submodule. 

# v2.2.0 (2020/09/15)

- Photo game architecture. See the `LuA` (L'unique Appareil-photo) [submodule](https://github.com/Bagoum/danmokou-lua) for a basic demo of how it works, as well as the "Aya Camera" shot type, which can be used anywhere.
- Made parsing better (hopefully).
- Added support for MoF MarisaA-style tracking options. See the "MariA Lazors" shot type for an example.
- Removed a lot of extraneous files from this repository.

# v2.1.0 (2020/09/11)

- Parsing rules are now more rigorous about parentheses and commas. When using parentheses in function invocations, commas are now also required. For example, `mod(5 2)` is no longer valid.

- Added support for infix operators (`+`,`-`,`*`,`/`,`//`,`&`,`|`). They are resolved in standard PEMDAS priority (though exponentiation is not supported as an infix).

  - Valid contexts:
    - When within a parenthesized argument list to a function, eg. `mod(5 + 2, 3)`.
    - When within parentheses, eg. `(5 + 4)`. 

  - One limitation of infix operators is that the first argument must be of the target type. You can write `pxy(0, 0) * 3` if a vector2 is required, but you cannot write `3 * pxy(0, 0)`. 
  - Note that you must parenthesize the right-hand side of a GCRule to use infix operators. Eg:

```python
			preloop {
				rv2.rx =f ([&brv2].rx + -0.1 * &aixd)
				rv2.ry =f ([&brv2].ry + 0.1 * &aiyd)
			}
```

- You can now use the parsing property `<#> warnprefix` to tell the parser to throw warnings if you use infix operators as prefix operators. Recommended for all new scripts. Stick the line at the top of your script to get the warnings. In the future, this property may be enabled by default.
- Added a power mechanic.
- Added a bomb mechanic. See `Danmaku/StateMachines/PlayerBombs.cs`. Bombs are tied to shot configurations.
- Added player-fireable pathers and lasers. The edges on this architecture are still rough.
  - Note: the `Home + Laser Shot (Reimu)` shows how all three above features can be used.
- Replaced shot selection with player/shot selection. Player/shot now shows up on replay descriptions. Players have a list of usable shots, but the architecture permits matching arbitrary shots. 
- Added the <xref:Danmokou.Danmaku.Options.GenCtxProperty> "Sequential", which executes children sequentially (GIR/GTR only). This replaces the `seq` StateMachine, which has been removed. 
- Fixed a potentially crippling bug dealing with cancellation of dependent tasks between two bosses.