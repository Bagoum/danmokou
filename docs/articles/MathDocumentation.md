# Pivoting

There are three main functions that "pivot" between target functions. They are `softmaxshift`, `logsumshift`, and `pivot`. 

There are two key differences:

1. `Pivot` does not care about direction (increasing/decreasing), after the pivot it always returns the second function. However, the other two are max/min functions, and so do not strictly pivot. If the second function decreases drastically after the pivot, then the first function may take over again.
2. The shape of the smoothed equation around the inflection point differs. For `pivot`, it is a hard shift. For `softmaxshift`, it first decreases and then jumps upwards to the second function. For `logsumshift`, it smoothly increases from the first to the second function. See https://www.desmos.com/calculator/3ipxfolpzf

# Easing

There are several ways to ease functions.

The standard method is via the `ease` and `eased` functions. These lerp **into** a target function and **always** use time as a controller. When using for movement, use `ease` for offset and `eased` for velocity.

Both of these make the movement start and end slowly. Note that the signature is slightly different: `ease` takes a lambda as argument, since it can be run with any lambda, and `eased` takes a string as argument, since it can only be run with expression functions in-engine that have well-defined derivatives. (In BDSL1, just the raw text `eiosine` is used in both cases.)

```
move 2 nroffset px ease($(eiosine), 2, sine 2 3 t)
move 2 nrvelocity px eased("eiosine", 2, cosine 2 3 t)
```

**Note that these two functions asymptote towards the target function**. They do **not** return a 0-1 value.

You can also invoke easing functions directly. For example, `eiosine(t)` is a distorted identity function with a slow entry and a slow exit. While `ease` and `eased` apply clamping to the time, direct invocations to easing functions do not apply clamping. You can get clamping by doing `eiosine(clamp01(t))`.

# Lasers and Extra Parametrization

Entities may want to parametrize their movement functions by information not contained in the BPI struct. For lasers, the "laser lifetime" is *not* stored in the BPI struct. (`t` is set to the time along the draw-path of the laser instead.)

**Aliasing** provides a solution. The idea is that we take the extra parameters, alias them to fixed shortcuts, and then execute a wrapped VTP. The VTP can access any of the extra parameters via the reference command. There are fixed functions in the engine that retrieve these extra parameters. 

Here is a table of automatic aliases:

| Entity      | Alias    | Value                     |
| ----------- | -------- | ------------------------- |
| Laser/LPath | LT()     | Laser lifetime            |
| Path        | CosRot() | Velocity cosine           |
| Path        | SinRot() | Velocity sine             |
| Path        | Angle()  | Velocity angle            |
| Path        | Root()   | Root location of velocity |

# Generation Context

When inside a repeater function (GTRepeat/GIRepeat/GCRepeat/GSRepeat), you have access to GenCtx, which is a running accumulator of values modified by the repeater functions.

GenCtx provides these automatic aliases in `GCXF<T>` functions. Note that the `&` prefix is only required for BDSL1.

| Alias         | Description                                                  |
| ------------- | ------------------------------------------------------------ |
| t (bpi.t)     | gcx.i -- iteration number                                    |
| p (bpi.index) | gcx.index -- firing index as determined by `p` modifiers     |
| loc (bpi.loc) | gcx.beh.globalLoc -- location of executor                    |
| &i            | gcx.i -- iteration number                                    |
| &pi           | gcx.pi -- parent interation number                           |
| &times        | number of times the repeater is running for                  |
| &rv2          | running RV2 value                                            |
| &brv2         | parent RV2 value                                             |
| &st           | gcx.summonTime                                               |
|               | **Automatic Bindings**                                       |
| &lr/&ud       | 1 when iteration number is even and -1 when odd (bindLR/bindUD) |
| &rl/&du       | -1 when iteration number is even and 1 when odd (bindLR/bindUD) |
| &axd          | Arrow formation X index (bindArrow)                          |
| &ayd          | Arrow formation Y index (bindArrow)                          |
| &aixd         | Inverted arrow formation X index (bindArrow)                 |
| &aiyd         | Inverted arrow formation Y index (bindArrow)                 |
| &angle        | rv2.a (bindAngle)                                            |

Variables inside the GenCtx can be accessed several ways:

- Within `GCXF<T>` (eg. repeater modifiers), use `VARNAME ` (BDSL2) or`&VARNAME` (BDSL1). This operates via lexical scoping on the environment frame within the GCX.
- Within `BPY/TP/Pred/VTP/etc`, use `VARNAME` (BDSL2) or `&VARNAME` (BDSL1). This operates via lexical scoping on the environment frame within the ParametricInfo struct.
- Within unscoped functions (eg. bullet controls), use `&VARNAME ` (BDSL2) or `&.VARNAME` (BDSL1). This operates via dynamic lookup on the environment frame. Variables cannot be exposed automatically since the functions are unscoped. 