# Upgrading

To get the newest version from git, run:

`git fetch super` (replace `super` with whatever remote you assigned to the [DMK repo](https://github.com/Bagoum/danmokou))

`git pull --rebase super master` (again, replace `super`)

`git submodule update` (if you have made modifications to the submodules, you will need to `pull --rebase` them individually)

**DMK is only supported in version 2020.1.17**. There is currently a bug in 2020.2 and 2021.1b that cause any build containing both TextMeshPro and FSharp.dll to crash in all builds. 

# Future

- Player team switching

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
- Added the <xref:Danmaku.GenCtxProperty> "Sequential", which executes children sequentially (GIR/GTR only). This replaces the `seq` StateMachine, which has been removed. 
- Fixed a potentially crippling bug dealing with cancellation of dependent tasks between two bosses.