## Parsing

### Float values

Float values look like normal decimals, but have a few extra features:

- You can use two signs at the beginning of a float. `--5`, `-+5.1`, `++5.0h`, etc. Double negatives cancel and plus signs are ignored.
- You can use the letters `f,s,p,h,π` at the end of the string to multiply the float value by a constant:
  - `f` = 1/120 (Note: this is the time per frame of the engine)
  - `s` = 120 (Note: this is the framerate of the engine)
  - `p` = phi
  - `h` = 1/phi
  - `π` = pi

### V2RV2

Format: `<NX,NY:RX,RY:ANGLE>` or `<RX,RY:ANGLE>` or `<ANGLE>`

Note that in any of these variants, leaving a value blank (eg. `<3,:>`) results in that value being set to zero.

### Arrays

An array `T[]` is parsed as follows:

```
if NextChar is an open brace `{`:
	while NextChar is not a close brace `}`:
		Parse type T
else:
	Parse type T (only once)
```

For example, the array [1,2,3] would be written as follows (newlines optional):

```
{
	1
	2
	3
}
```

And singleton arrays don't require braces:

```
{
	1
}

OR

1
```

### State Machines

State machines have some unique parsing rules:

- `SMPhaseProperties` arguments are provided by collecting all lines starting with `<!>` since the previous phase. For each of these lines, the `<!>` is stripped and it is parsed as type `SMProperty`. For example:

```
<!> type non `This is a nonspell`
phase 40
	paction(0)
		noop

<!> type spell `This is a spell`
phase 40
	paction(0)
		noop
```

- If the first argument to a constructor is `List<StateMachine>`, then this is provided by parsing as many SMs as possible following parenting rules after all the rest of the arguments. For example:

```
public PhaseParallelActionSM(List<StateMachine> states, float wait);

# Note: indentation is purely visual
paction(0)
	noop	# PhaseParallelActionSM can parent NoOpLASM : LASM
	noop
	noop
paction(0)	# PhaseParallelActionSM cannot parent PhaseActionSM
```

- However, if it is `StateMachine[]`, then this is provided *normally*, that is to say, with array delimiters, and the children may be of any time.

```
gtrepeat {
	wait(40)
	times(10)
} {
	noop
	noop
}
```

### GCRule

GCRules are used to update GenCtx values in GCXProps like `Start, Preloop, End`. 

Each GCRule has a variable or variable member on the left side, an assignment operator and type in the middle, and a value on the right side. For example:

`r.nx +=f (3 * t)`

In this case, the left side is the variable member `r.nx` (the nonrotational X component of the RV2 named `r`), the operator is `+=`, the type is `float`, and the value is `3 * t`. 

The type names are: `f = float`, `v2 = vector2`, `v3 = vector3`, `rv2 = v2rv2`. 

It may appear that the type is redundant-- ideally, we should be able to determine the type of the right side by looking at the type of the variable or variable member on the left side. The problem is that GenCtx accumulators are, technically speaking, weakly typed, and the type of the left side is actually determined by looking through the bindings for a variable with the correct name. In fact, there is an ambiguity in GenCtx accumulators where you can (with some difficulty) assign the same name variable to different types, and then try to use the variable in a GCRule. It will not disambiguate via types correctly. While this is a terribly sad state of affairs, I don't know how to resolve it without turning the scripting language into a full-fledged language with ASTs and all the fuzz. (That in itself might be exceedingly difficult due to how the scripting language uses semantic knowledge instead of parentheses to disambiguate function application from argument lists.) 

To cut it down, this is a weak link in the engine, but requires way too much effort to improve.

