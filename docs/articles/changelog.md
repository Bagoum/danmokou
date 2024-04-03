# Upgrading

To get the newest version from git, run:

`git fetch super` (replace `super` with whatever remote you assigned to the [DMK repo](https://github.com/Bagoum/danmokou))

`git pull --rebase super master` (again, replace `super`)

`git submodule update` (if you have made modifications to the submodules, you will need to `pull --rebase` them individually)

# Unreleased

The following features are planned for future releases. 

- [Backlog] Safeguards around control rebinding
- [Backlog] UI improvements: custom cursors
- [Backlog] Implementation of a TH18-like card engine
- [Backlog] Procedural generation of stages and bullet patterns



# v11.1.0

In this version, I've removed the `.csproj` and `.sln` files from the repository. These are required for normal C# projects, but Unity autogenerates them. When upgrading via `git pull --rebase`, this may cause merges error like the following:

```
CONFLICT (modify/delete): SuzunoyaUnity.csproj deleted in HEAD and modified in d801b396...
```

 If you get this error, then manually delete the `*.csproj` and `*.sln` files in the base directory, then run `git add . ` and `git rebase --continue`. The files will be regenerated when you reopen Unity.

### UI Changes

This version introduces a significant overhaul to the internal handling for Danmokou's UI utilities (built on top of UIToolkit). In previous versions, any change to the UI (such as the cursor moving from one node to another) resulted in the entire visible UI being redrawn. In order to reduce this overhead, dynamic UI rendering is now handled in a [MVVM pattern](https://en.wikipedia.org/wiki/Model%E2%80%93view%E2%80%93viewmodel) where Views write to the UI whenever they detect changes in the View-Model. As an example, consider a case where we have a string that might change, and we want to render this string to screen. We can implement a basic view model as follows (abbreviated for simplicity):

```C#
public class LabelViewModel : IUIViewModel {
	private Func<string> _value;
	public string Label => _value();
	
	public LabelViewModel(Func<string> value) {
		this._value = value;
	}
	
	public long GetViewHashCode() => Label.GetHashCode();
}
```

The view model contains data that we want to render (the `Label` string), as well as some mechanism for determining whether the data has changed (in this case, we compute the hash-code of the Label string to determine changes, but we could instead explicitly track changes via versioning, as in `VersionedUIViewModel`). Then, we can create a view that actually performs rendering as follows:

```c#
public class LabelView : UIView<LabelViewModel> {
    public LabelView(LabelViewModel model) : base(model) { }
    
    protected override BindingResult Update(in BindingContext context) {
        Node.HTML.Q<Label>().text = ViewModel.Label;
        return base.Update(in context);
    }
}
```

(Note: UIView is a helper class in DMK, but it derives from CustomBinding, which is a Unity UIToolkit class that is required for this functionality.)

Finally, we can bind this view model and view to a node by calling `node.Bind(new LabelView(new LabelViewModel(stringFunctionHere)))`. Now, `LabelView.Update` will be called automatically whenever `LabelViewModel` changes.

(In this basic example, the Model part of MVVM is whatever provided us the `stringFunctionHere` data.) 

The benefit of this approach is that a view makes changes to the UI HTML— which are extremely expensive!— *only* when `GetViewHashCode` changes on its view model. This minimizes the amount of changes made to the UI HTML at any point. Furthermore, one node can have multiple independent view models handling different parts of the UI interaction, making it easy to compose behavior and minimize dependencies between different areas of the UI HTML. In fact, the basic behavior of highlighting and animating nodes when the mouse cursor is moved over them is handled by `RootNodeView`, which is bound to every node. 

Along with this, the handling for OptionNodeLR has been updated to simplify creation and fix a lot of lingering issues. Instead of taking a getter and setter as arguments, OptionNodeLR now takes an `ITwoWayBinder`, which allows source data to be modified by the OptionNodeLR View or by backend services, and for those modifications to be visible in both directions.

### Features

- When holding the L/R/U/D directions on a menu, the input will begin repeating after a short delay, mirroring standard input handling in most programs. This is configurable as `InputTriggerMethod.OnceRefire`.
- The color theming for DMK's UITK support has been standardized, and supports overriding theming via classes. For example, dropdown menus use the CSS class `.theme-blue`, which gives them a blue background instead of a purple one. See the CSS configuration in `UINode.uss`. 
- In SuzunoyaUnity, text now scales in (in addition to fading in) in the text box. This can be configured as "Char Scale In Time" and "Char Scale In From" on ADV Dialogue Box Mimic.
- UI nodes can now have context menus (viewable by pressing C while selecting a node) that show up to the lower-right of the node. You can create such a context menu by binding a view model to the node that implements `IUIViewModel.OnContextMenu` (eg. `ContextMenuViewModel`). See Assets/Danmokou/Plugins/Danmokou/Utility/LocalXMLUIFreeformExample for an example. (Note that context menus are interactable, as opposed to tooltips, which are not.)
  - Similarly, in order to implement a tooltip, you can binding a view model that implements `IUIViewModel.Tooltip` (eg. `TooltipViewModel`).

### Breaking Changes

- `UINode.NodeHTML` has been removed. (It was the same as `UINode.HTML`,  so please use that instead.)

### Fixes

- Fixed an issue where controller menu navigation with the joystick/DPad could occasionally result in double movement. 
- Fixed an old issue where menu navigation would not be correct when moving from the Records screen to the Replay screen to watching a replay, then back to the Records screen.
- Fixed an issue where the default dialogue box could allow clicking buttons that were not visible.
- Fixed issues that could arise when the Backgrounds setting was turned on and off repeatedly. Also adjusted internal logic so menu backgrounds always appear regardless of the Backgrounds setting.

# v11.0.0 (2024/02/17)

**The engine has changed significantly in v11**- make sure to read the Language Changes section before upgrading.

I've upgraded the project's Unity version to 2023.2.9f1. Unity 2023 has a critical change where the TextMeshPro package has now been merged into Unity internals, so it is not clear that the project will still run on Unity 2022. As such, please upgrade to Unity 2023 along with updating DMK. Also make sure to update the [VSCode extension](https://marketplace.visualstudio.com/items?itemName=Bagoum.dmkscripting).

#### Language Changes

This build introduces a new scripting language, BDSL2, which is **now the default scripting language for all script files**. Any script files that start with `<#>` (the marker for a ParsingProperty, such as `<#> warnprefix` or `<#> strict(comma)`) will be parsed according to BDSL1 rules. You can add the line `<#> bdsl1` to the top of any of your existing scripts. If a script does not start with `<#>`, then it will be parsed according to BDSL2 rules. 

Likewise, the backend handling for `string.Into<T>`, which previously used BDSL1 reflection, now uses BDSL2 reflection instead. This may cause some minor errors— for example, if you had a field reflected into `FXY` that was previously filled as `^ x 0.8`, you will need to rewrite it as `x ^ 0.8`. You can use `string.IntoBDSL1<T>` if you absolutely need BDSL1 reflection in code (`<#>` only works for script files).

To verify the correctness of your scripts after upgrading, you can run any scene, click the GameManagement object, right click the `Game Management (Script)` MonoBehavior, and select "Verify Expressions" from the context menu. This *will* freeze Unity for several seconds, and then should raise exceptions in the Console for any scripts or fields that could not be reflected. (All .txt and .bdsl files in the directory Assets/Danmokou/Patterns, as well as any directories configured in GameUniqueReferences/ScriptFolders, will be reflected.)

The tutorials have been updated to use BDSL2.

Existing BDSL1 scripts will function more-or-less as-is, though there have been some internal data model changes to support BDSL2. Critically, dynamic type construction (introduced in v9.2.0) and the GCXU type abstraction have been removed and replaced with a more "standard" data model based on [environment frames](https://www.composingprograms.com/pages/16-higher-order-functions.html). For the most part, this shouldn't affect scripts, except for one major change to language functionality: variables are now **shared** by all consumers. Consider the following BDSL1 code:

```
gtr {
	start { speed =f 1 }
} {
	sync fireball-red/w <-90>  s rvelocity(px(&speed))
	_ 2 debugf(set { f speed 4 } &speed)
}
```

This code launches one bullet with a velocity of `&speed`, then sets `&speed` to 4 after 2 seconds. In previous versions of the engine, the bullet would always move slowly, even after the speed variable was updated. Now, the bullet will start moving faster after two seconds.

The rules for variable sharing are the same as in a `for` loop in a standard language: every loop iteration has its own variables. For example, if we repeated the GTR multiple times:

```
gtr {
	start { speed =f 1 }
	wait 1s
	rv2incr <30>
	times _
} {
	sync fireball-red/w <-90>  s rvelocity(px(&speed))
	_ 2 debugf(set { f speed 4 } &speed)
}
```

Each bullet would have its own `&speed` since each bullet occurs in a separate loop iteration of GTR, where `&speed` is declared. On the other hand, if instead of firing one bullet we fired multiple (eg. `async fireball-red/w <-90> gcr2 10 10 <2> { } s rvelocity(px(&speed))`), then each group of 10 bullets would share the same `&speed`, since they occur in the same loop iteration of GTR.

Also, cases where expressions were provided in an array to a function (such as the expression function `Func<TExArgCtx,TEx<T>> Select<T>(Func<TExArgCtx,TEx<float>> index, Func<TExArgCtx,TEx<T>>[] points)`, which used `index` to get a value from `points`) now use the `UncompiledCode` abstraction. In these cases, the signature becomes `Func<TExArgCtx,TEx<T>> Select<T>(Func<TExArgCtx,TEx<float>> index, UncompiledCode<T>[] points)`. In many cases, you will need to wrap expressions in the `points` array with the `code` function. For example:

```C#
//Select a value based on the difficulty counter dc
//Before
select(dc, {
	(0 + t)
	(2 + t)
	(4 + t)
})

//After
select(dc, {
	code(0 + t)
	code(2 + t)
	code(4 + t)
})
```

There are also a few less impactful changes:

- The automatically-bound variable `&bulletTime` has been removed (it was unused in the engine as provided).
- The AsyncPattern `idelay` was removed (you can use `gir { delay FRAMES }` instead).
- The SyncPatterns `target`, `targetx`, `targety` have been removed (you can use `gsr { target a/rx/ry TARGET }` instead).
- `EventLASM.Listen` has been modified so it now takes a function as an argument. It is generally not feasible to use this method in BDSL1. See `Patterns/FeatureTesting/event based firing.bdsl` for examples on how to use this in BDSL2. Note that this method will likely be changed in the near future.
- The BulletControls `Event` and `Event0` have been renamed to `ProcEvent` and `ProcEvent0`.
- SyncPatterns `oArrowI`, `FArrow`, and `TreeArrow` have been removed. I plan to replace them in the next version (at least for BDSL2).
- `FinishPSM` (the `finish` StateMachine) has been removed. It was mostly unused in the engine as provided and it was also buggy. Note that using `EndPSM` (the `end` StateMachine under a phase) does basically the same thing: it runs code when the phase times out or the executing entity runs out of HP (but not if the executing entity is autoculled or cancelled).
- `Compilers.CompileDelegateFromString` (BDSL1) has been removed. Please use `Reflection2.Helpers.ParseAndCompileDelegate` (BDSL2) instead.
- The alias "r" for the `rank` function was removed. You can re-add it if you need it.

#### Features

- Significantly improved the speed of the engine's handling of movement functions in many common use cases by removing `new Vector2()` calls in generated code where possible.
- Enemies can now use nonpiercing lasers. As with players shots, simply add the `nonpiercing()` option to the laser options. Note that nonpiercing only works with `dynamic` lasers.
- The engine now supports restarting from checkpoints in stage or boss scripts. Add a `<!> checkpoint` flag to any stage phase and/or boss phase where you would like to set up a checkpoint. (If using it on a boss phase, make sure the stage phase that creates the boss also has a checkpoint flag.) The player can then select "Restart from Checkpoint" from the pause menu or the death menu. This will result in their score and other features being reset, as if they had used a Continue. It is supported for the checkpoint to be on a previous stage (ie. if the last checkpoint was on stage 1 and the player dies on stage 2, they will get sent back to stage 1). Since Continues are generally stronger than checkpoints, it may be best (but it is not required) to disable Continues in your GameDef if using this feature.
  <img src="..\images\rider64_WjEUkgqqUZ.jpg" alt="rider64_WjEUkgqqUZ" style="zoom:33%;" />
- The Newtonsoft.JSON package has been changed from jillieJr's package to the new inbuilt one, which resolves some issues with AoT code stripping.

#### Breaking Changes

- The method `GameManagement.Restart` has been moved to `GameManagement.Instance.Restart`.
- The GenCtx property `Expose` has been removed. It no longer did anything with the implementation of envframes.
- Instead of being derived automatically, the bounds on player movement are now determined via the `m_playerMovementBounds` fields on GameDef. By default, this is set to the values that would be derived automatically.
  <img src="..\images\Unity_xVjFiR1eGx.jpg" alt="Unity_xVjFiR1eGx" style="zoom: 33%;" />
- Replay data storage can now be configured per GameDef via overriding `RecordReplayFrame` and `CreateReplayInputSource`. By default, danmaku games use StandardDanmakuInputExtractor (which supports horizontal/vertical movement and the controls fire, focus, bomb, meter, swap, dialogue confirm, dialogue skip), and non-danmaku games do not support replays.
- Player configurations now require a "movement handler" configuration.  For standard bullet hell ships, you can use the "PlayerStandardMovement" configuration, which has been set on all the existing players. For now, this will default to "PlayerStandardMovement" if not provided, but it may be required in future versions.
  <img src="..\images\move_cfg.jpg" alt="move_cfg" style="zoom:33%;" />
- The repeater modifier `alternate(GCXF<float>)` has been removed. It has been replaced with the function `SyncPattern Alternate(GCXF<float>, SyncPattern[])` (with similar signatures for AsyncPattern and StateMachine), which do the same thing.

#### Changes

- The field `CampaignConfig.practiceBosses` has been renamed to `bosses` and now should include *all* bosses used in the campaign. Nonpracticeable bosses should set the field `BossConfig.practiceable` to false.
- Rearchitectured bullet collision handling so it now uses service location instead of hardcoded targets. Now, any object that implements `IEnemySimpleBulletCollisionReceiver` and calls `RegisterService<IEnemySimpleBulletCollisionReceiver>(this)` will receive collision checks from enemy-fired simple bullets. Likewise for `IPlayerSimpleBulletCollisionReceiver`, `IEnemyPatherCollisionReceiver`, `IPlayerPatherCollisionReceiver`, `IEnemyLaserCollisionReceiver`, `IPlayerLaserCollisionReceiver`, `IEnemyBulletCollisionReceiver`, and `IPlayerBulletCollisionReceiver`. This makes it possible to introduce collisions with other objects such as terrain.
  - The `BulletBlocker` class/prefab contains a basic implementation of terrain that blocks enemy simple bullets and lasers.
  - If nonpiercing lasers collide with terrain, they will stop (just as if a player nonpiercing laser collides with an enemy, or just as if an enemy nonpiercing laser collides with the player).
- UI popups can now choose between having a row of centered action buttons or a row of left-flush and right-flush action buttons, based on the `PopupButtonOpts` argument passed to `PopupUIGroup.CreatePopup` (renamed from `PopupUIGroup.LRB2`).

#### Fixes

- Fixed an issue where the scrolling on the control bindings option screen was too slow (it was correct on all other menus after Unity-side fixes in 2022.2.13).
- Fixed an issue where trailing options wouldn't have a correct position immediately after a traditional respawn. Also fixed an issue where items could be collected while in the process of respawning.
- Fixed an issue where the fairy generated on shot demos would sometimes not be deleted when leaving the shot demo screen.
- Fixed an issue where the mini-tutorial could prevent running the main campaign.
- Fixed an issue where the default dialogue box could allow clicking buttons that were disabled.
- Fixed an issue where replays could be recorded even after they were cancelled.
- Fixed an issue where simple and rotating lasers would have incorrect bounding box calculations.
- Fixed an issue where replays could be saved but not viewed on Mac builds.

# v10.1.0 (2023/04/02)

This release includes code for [Reimu the Boomer](https://bagoum.itch.io/boomer), an Ace Attorney-style point-and-click game, in the MiniProjects folder. There is also some work-in-progress code in `Danmokou/Danmaku/Reflection/NewReflector` for BDSL2, a more generalized scripting language.

Unity version 2022.2.13 fixes some pending issues with UITK. As such, it is recommended to upgrade to 2022.2.13.

#### Pending Issues

- When spawning danmaku in WebGL, there is a chance to get very significant lag spikes a small number of times. This issue is due to some deep problems with WebGL ([see this thread](https://forum.unity.com/threads/webgl-performance-and-getprogramparameter.991766/) as well as [this bug](https://issuetracker.unity3d.com/issues/cpu-spike-in-batchrenderer-dot-flush-when-using-graphics-dot-drawmeshinstanced-on-webgl)) and seems unlikely to be fixed in the near future. There are some potential mitigations involving preloading bullet textures.
- The UITK issue where some elements take extra newlines for no reason has been **resolved** as of 2022.2.13. [See ticket](https://issuetracker.unity3d.com/issues/ui-toolkit-labels-width-is-not-extended-causing-additional-empty-lines-when-using-specific-resolutions)
- The UITK issue where mouse scrolling is slow has been **resolved** as of 2022.2.13, using the property `mouseWheelScrollSize` on the `ScrollView` class. In DMK, the function `AddScrollColumn` sets `mouseWheelScrollSize` to 1000 by default. [See ticket](https://issuetracker.unity3d.com/issues/slow-scroll-view-scrolling-when-entering-play-mode-and-in-builds)

#### Features

- Added scoring items that are generated when bullets are cleared (by default, via photo shot or end-of-phase, configurable as SoftcullProperties.UseFlakeItems); homes after the player and grants 42 points on contact (configurable in IScoreFeature.AddBulletFlakeItem).
- Added safety guarantees around UI navigation so it is not possible to enqueue two navigation events at the same time.
- Enhanced logging to reduce allocations/string formatting in cases where logging calls would be ignored.
- Added support for easy show-on-hover tooltips on UI nodes, by calling `myUINode.MakeTooltip(myUIScreen, myTooltipText)`.
- Added support for targeting evidence (in Ace Attorney-style ADV games) at objects or entities in the environment, via `InteractableEvidenceTargetA<E, T>` and `EvidenceTargetProxy<E, T>`.
- Generalized AudioTrackService.InvokeBGM so that BGM tracks are effectively stored in a stack where only the topmost one is executing. This allows temporarily overriding the BGM.
- In Suzunoya: Added the boolean `Trivial` on `BoundedContext<T>`. This can be set to true if a bounded context does not modify any save-data anywhere within it, and the effect is to speed up the startup time for ADV top-level VN execution.
- Changed some handling around reflection so that reflected scripts can be preserved between Unity scenes.
- Added an attract/demo mode that loops through a sequence of replays if the main menu is left unattended for some time. This can be configured via the "Attract Mode" child object on the "GameManagement" persistent object. See `SiMP.MainMenu` for an example.
- Added a "quickfade" scene change functionality (ie. changing the scene without a separate loading screen image). Use the function `AsQuickFade` on `ICameraTransitionConfig` to skip the loading screen image. This is currently used for switching between replays in attract mode.

#### Changes

- Reflected functions using `BEHPointer` now instead use `TEx<BehaviorEntity>`. This is backwards-compatible, so you can still do things like `hpratio(yukari)`, but now you can also use `mine` to get the BehaviorEntity associated with the function caller. For example, `hpratio(mine)` will get the HP ratio of the BehaviorEntity calling the function, or if it is called by a bullet, it will get the HP ratio of the BehaviorEntity that fired the bullet.
- Removed the `EEx` helper class. Replaced all usages with `TEx`. `EEx.Resolve` is now `TEx.Resolve`.

#### Fixes

- In Suzunoya:
  - Tint modifiers, such as `FadeTo`, now work on render groups, so you can write code like `await vn.DefaultRenderGroup.FadeTo(0f, 1f)`. In previous versions, this would not actually cause the tint to change.
  - Fixed some bugs regarding calculation of location for entities with parents.
- Fixed a regression with the `Darkness` command that would cause the shadow to appear incorrectly.
- Fixed some cases where errors in GTRepeat would not be logged.
- Fixed a bug where replays could start one frame early if restarted.
- Fixed a bug where certain achievements could not be gotten due to erroneous configuration of the shot demo screen.
- Improved WebGL handling. It is now possible to build an ADV/VN game for WebGL without going through [AoT handling](AoTSupport.md). 
- Improved code generation from Localization-Utils to cost less memory.
- Simplified AoTGenerator (a helper for precompiled expressions) and fixed some edge-cases.

# v10.0.0 (2022/12/03)

This release includes a large number of miscellaneous features for danmaku and visual novel functionality. There is also a new version of the [VSCode extension](https://marketplace.visualstudio.com/items?itemName=Bagoum.dmkscripting). 

#### Features

- The `oncollide` bullet control can now be run on simple bullets as well as complex bullets (such as pathers or lasers). See `examples/on collide control.bdsl` for some examples.

- Added three new simple bullet pool controls: `destructible`, which sets whether or not bullets will be destroyed when they collide with the player, `damage`, which sets the amount of damage bullets do to the player (limited to 0 or 1), and `nograze`, which disallows grazing on the bullet.

  - `damage` and `nograze` have also been added to BehaviorEntity and Laser options, so they work on pathers and lasers as well, though you provide them in the options array instead of as pool controls. See `examples/on collide control.bdsl` for some examples.
  
- The `lasercolliding` function has been removed and replaced with a general `colliding` function that works on all complex bullets, including lasers and pathers. See `examples/on collide control.bdsl` for examples on how to use this function more generally with some of its helper functions.

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

- Added the `SetRenderQueue` simple bullet pool control, which allows changing the rendering order of specific simple bullet pools. See [the bullets documentation](bullets.md) for details on render queue numbers.

- Added initial support for bucketing bullets. "Bucketing" groups bullets based on their screen location, which makes collision detection far more efficient. Since there is an overhead to the bucketing process, it is not particularly useful for computing many-against-one collisions (such as enemy bullets against the player). It is used for player bullet on enemy collisions and  bullet on bullet collisions. You can call `RequestBucketing` on a simple bullet collection to make sure it is bucketed. To handle collisions, you can either implement `ISimpleBulletCollisionReceiver` and call `SimpleBulletCollection.CheckCollisions` (example in `PlayerController.cs`), or you can call `SimpleBulletCollection.GetCollisionFormat` and do custom handling (example in `BxBCollision.cs`).

  - Added initial support for bullet-on-bullet collision, currently only between simple bullets. See `examples/bullet on bullet collision.bdsl`.

- Optimized handling of player bullets by adding bucketing for simple bullets, as well as AABB pruning for lasers and pathers. This fixes some cases where large amounts of enemies (100+) could cause lag during player bullet collision checking.

- It is now possible to use generic complex bullets in player shots. (The only generic complex bullet currently present in the engine is `moon`, eg. `moon-blue/w`.) To use complex bullets, use the `complex` bullet firing function (as opposed to `s/laser/pather`).

- There is now generalized support in Suzunoya's VN library for presenting "evidence" (think Ace Attorney) during dialogue, as demonstrated in [this](https://bagoum.itch.io/ghost-of-tranquil-vows) proof-of-concept game (relevant code is in `GhostOfThePastGameDef.cs`). To do this, first create a field `evidenceRequester = new EvidenceRequest<E>()` once during game setup, where `E` is a parent type for the evidence that a player can present. Then, there are two ways that you can use to request evidence from the player:

  - `using (var _ = evidenceRequester.Request(CONTINUTATION)) { ... }`. In this case, the player can optionally present evidence while the code inside the brackets is being executed, and if they do, the CONTINUATION function, which must be of type `Func<E, BoundedContext<InterruptionStatus>>`, will be run on the provided evidence. After running some code, it should return either `InterruptionStatus.Continue` (the nesting code should continue running) or `InterruptionStatus.Abort` (the nesting code should stop running). Note that you cannot save or load within the CONTINUATION function, but if you make the CONTINUATION function a `StrongBoundedContext<InterruptionStatus>` with `LoadSafe = false`, then saving/loading within the CONTINUATION function will send the player to the point right before they started the CONTINUATION function.
  - `var ev = await evidenceRequester.WaitForEvidence(KEY)`. In this case, the player *must* present evidence to continue the game execution. Save/load can still be used with this method, and KEY will be used to preserve the value of the evidence provided when saving. (Note that your evidence type E must be serializable!)

- In order to increase the modularity of supported game mechanics, mechanics are now handled by an abstraction `IInstanceFeature` that can be slotted into `InstanceData`. `IInstanceFeature` has methods that are called upon certain game events; for example, the method `OnGraze` is called when the player grazes, and the class `MeterFeature`, which implements a mechanic for special meter abilities, implements this method by adding to the meter. Furthermore, there are interfaces for specific mechanics, such as `IPowerFeature` for the power mechanic. The strength of this architecture is that implementations can easily be switched out; you can use the class `PowerFeature`, which has traditional 1-4 Touhou-style power handling, or `PowerFeature.Disabled`, which disables power items and sets the player power to always 4.

- In previous versions of the engine, it was not straightforward to add game-specific code handling. In v10, this is now handled by the `GameDef` scriptable object, which is a generic container for game-specific code. There are several abstract subclasses of `GameDef` according to the type— for example, `ADVGameDef` for ADV-style games and `CampaignDanmakuGameDef` for multi-stage danmaku games. To make game-specific code, create a subclass such as `SiMPGameDef : CampaignDanmakuGameDef` and implement the abstract methods.

  - One of the abstract methods on `CampaignDanmakuGameDef` is `MakeFeatures`, which returns the set of game-specific `IInstanceFeature`s.

- Refactored code related to danmaku game execution. It is now possible to add custom handling for the execution of multi-stage games, whether this be with regards to alternate path handling (such as in Imperishable Night) or with regards to endings or with regards to anything else. You can do this by creating a scriptable object subclassing `BaseCampaignConfig`, and then override `RunEntireCampaign`. Reference `CampaignConfig.RunEntireCampaign` for the default handling.

- `FreeformUIGroup`, which is used to make keyboard-friendly interactable UI from arbitrarily-positioned nodes, can now have UIGroups within it. Use `AddGroupDynamic` to add UIGroups.

- Expression compilation with GCXU is now faster due to some extra indirection provided by `ReadyToCompileExpr`.

- Improved memory allocation handling for UI handling, player bullet firing, the GTRepeat StateMachine, DMCompactingArray, and string allocation.

- Added support for on-screen option selection in VN/ADV contexts. You can set up default handling by (once at the beginning of setup) creating a `SelectionRequest` object from one of the `SetupSelector` methods on `DMKExecutingADV` (in the ADV context) or `VNUtils` (in the VN-only context), and then (every time you want the player to make a selection) awaiting the task `selector.WaitForSelection(...)`. Note that the default handling is DMK-specific, but you can wire up your own UI listeners elsewise.

#### Breaking Changes

- As part of the introduction of GameDef, achievement handling has been moved from AchievementsProviderSO to GameDef.
- Deprecated SOPlayerHitbox in favor of using ServiceLocator to get the PlayerController object where necessary.
- Replaced the laser-specific function `lasercolliding` with the bullet function `colliding`.
- Much of the Danmokou ADV library has been moved to Suzunoya. You may need to update imports if you have been using it.
- Parallelization support has been removed from simple bullet collision-checking in order to support bucketing.

#### Changes

- Right-clicking while playing dialogue will now bring up the pause menu instead of moving to the next line.
- Collision handling has been generalized across the engine. Now, there are three phases to RegularUpdate, which are: RegularUpdate, RegularUpdateCollision, RegularUpdateFinalize. In RegularUpdateCollision, entities should find any targets whose hurtboxes overlap the entity's hitbox, and call collision functions on those targets. In RegularUpdateFinalize, entities should perform calculations based on the sum of collisions they received. Rendering handling is also best placed in RegularUpdateFinalize.
- The core UI library (built on top of Unity's UIToolkit) has been moved to the Danmokou.Core assembly (Assets/Danmokou/Plugin/Danmokou/Core/UI). This does not affect most usage, but it does mean that it's easier to pull it out and use it outside of DMK.

#### Fixes

- Fixed a "bug" where, during VN execution, pressing Z to select an option from the menu at the right side of the dialogue box would also cause the dialogue to advance.
- Fixed an issue where the UI would flash off for one frame when changing the resolution.
- Fixed a bug where the background would stop rendering until the game was unpaused when changing the resolution.

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