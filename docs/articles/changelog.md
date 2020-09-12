# Version 2.1.0 (2020/09/11)

- Parsing rules are now more rigorous about parentheses and commas. When using parentheses in function invocations, commas are now also required. For example, `mod(5 2)` is no longer valid.

- Added support for infix operators (`+`,`-`,`*`,`/`,`//`,`&`,`|`). They are resolved in standard PEMDAS priority (though exponentiation is not supported as an infix).

  - Valid contexts:
    - When within a parenthesized argument list to a function, eg. `mod(5 + 2, 3)`
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