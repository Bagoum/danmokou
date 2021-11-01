# Upgrading

To get the newest version from git, run:

`git fetch super` (replace `super` with whatever remote you assigned to the [DMK repo](https://github.com/Bagoum/danmokou))

`git pull --rebase super master` (again, replace `super`)

`git submodule update` (if you have made modifications to the submodules, you will need to `pull --rebase` them individually)

# [Unreleased] v9.0.0

The following features are planned by v9, which will probably be completed sometime early next year.

- Default handling for graze flake items and bullet cancel flake items (may be in a minor release of v8)
- Implementation of a TH18-like card engine
- Procedural generation of stages and bullet patterns
- Full-featured implementation of Suzunoya
  - **The classes for DialogueProfile will be deleted in v9, so make sure you port any custom dialogue profiles to Suzunoya entities by then.**

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