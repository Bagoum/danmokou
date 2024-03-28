# BDSL2 Language Guide - Introduction

BDSL2 is the updated scripting language used in DMK v11 (released February 2024). It is an imperative programming language (like C#, Javascript, Python, etc) that can be compiled into C# expression trees at runtime. It has four major features that make it more convenient to use than direct C# for scripting:

- It can be recompiled *at runtime* within the context of a C# program.
- It supports dynamic scoping (though, like all modern languages, it defaults to lexical scoping).
- It allows simplified syntax for many types of expression critical to describing danmaku movement. For example, the lambda expression `(SimpleBullet sb) => new Vector2(sb.pi.t, 0)` can be written as just `pxy(t, 0)` in many contexts.
- It uses type unification to auto-determine types, including for lambdas and function definitions.

This document (and the other documents in this folder) go through the details of the language.

## How to Start

BDSL2 is only supported within the context of DMK. As such, you should first [setup DMK](../setup.md). Then, you can compile a BDSL2 script by using the function `Danmokou.Reflection2.ParseAndCompileDelegate`. For convenience, there is a MonoBehavior `BDSL2LanguageHelper` which will parse a script from a text file. If you open `Assets/Danmokou/Scenes/BasicSceneOPENME`, there is an object called `LanguageHelper` which has this script attached. It will try to parse the script on startup, and also whenever you right-click the MonoBehavior and select `Parse script` from the context menu. By default, it will assume that the script should return a `float`, but you can change this by changing the `Target Type` attribute on the MonoBehavior. (If you set it to `void`, then the return value will be ignored.)

You can then open up the linked `Example BDSL2 Script` using the [VSCode extension](https://marketplace.visualstudio.com/items?itemName=Bagoum.dmkscripting) to edit it.

In addition to the examples provided in this file, there are also example scripts in `Assets/Danmokou/Patterns/bdsl2` that show most of the features of BDSL2.

## What's in a Script?

A BDSL2 script is a **block**. A block is a **list of statements**. These concepts are shared widely across programming languages. In BDSL2 specifically, a statement can be one of a few things:

- A value expression (such as `myVector.X + 5.1`),
- A variable declaration (such as `var myVector = pxy(2, 0)`),
- A function declaration (to be discussed below),
- An if/else block,
- A for/while loop,
- A return/continue/break statement.

Unlike in most programming languages, BDSL2 blocks are themselves value expressions. Specifically, the value of a block is the value of its last statement. See the section below on Block Expressions for more details.

## Basic Expressions

The following code suffices to create a script (returning type `float`):

```C#
1.0;
```

This script is treated as a block with one statement, which is just the value expression `1.0f (float)`. The script will return `1.0f`.

Numbers are parsed as floats if they have any content after a decimal point. If they have a decimal point but nothing after (eg. `12.`), then they will be parsed as ints. Natural numbers (eg. `7`) can be parsed as ints or floats depending on context.



When there is more that one statement, they are executed in order, and the value of the last statement is the return value of the script.

```C#
var a = 1;
a + 2.3;
```

Because `a` is added to a float, it is automatically assumed to be a float itself. This script first assigns `1.0f` to `a`, and then adds `2.3f`. The script will return `3.3f`.



We can call methods on values.

```C#
var a = -1.0
var b = abs(a)
b + 2.3
```

*Note: If successive statements have the same indentation level, then you do not need to use semicolons.*

The `abs` (absolute value) function here refers to an expression function defined in the DMK backend. The documentation for it is provided [here](https://dmk.bagoum.com/docs/api/Danmokou.DMath.Functions.ExM.html#Danmokou_DMath_Functions_ExM_Abs_tfloat_), and you can find the code defined in `Assets/Danmokou/Plugins/Danmokou/Danmaku/Math/MathRepos/GenericMath/ExMBasic.cs`. 

This method is also defined in the default C# class `System.Math`. We could invoke that method instead:

```C#
var a = -1.0
var b = Math.Abs(a)
b + 2.3
```

If there is a function defined in the DMK backend, you should generally prefer it over any default C# functions, as it will usually be faster.



The above examples are static methods. We can also use C# instance methods.

```C#
var hello = "world"
hello.IndexOf("r")
```

This script returns the value `2.0f`. Even though `IndexOf` returns an `int`, the script will implicitly cast this int to a float, since the conversion is well-defined.

## Block Expressions

The entirety of a script is a block. We can also create smaller blocks at will by using `b{ }`.

```C#
var a = 5.0
4 * b{
    a += 3
    a * 2.0
}
```

In this script, `a` is first set to `5.0f`. Then, within the block, it is updated to be `8.0f` (after adding 3). The last statement of the block is `a * 2.0`, which returns `16.0f`. This is then multiplied by 4, so the script returns `64.0f`.



If we declare a variable inside a block, it is only visible inside that block.

```C#
//This code does not compile.
var a = 5.0
4 * b{
    var c = 3.0
    a += c
    a * 2.0
} + c
```

However, if we use `hvar` instead of `var`, then the declaration of `b` is hoisted one scope up, and we can access it.

```C#
var a = 5.0
4 * b{
    hvar c = 3.0
    a += c
    a * 2.0
} + c
```

This script returns `67.0f`.

Note that `hvar` only moves the declaration **one** scope up. In the below example, `c` is hoisted into the scope of the outer block, but it still not visible in the top-level scope, so it cannot be accessed in the expression `4 * b{ ... } + c`.

```C#
//This code does not compile.
var a = 5.0
4 * b{
    b{
        hvar c = 3.0
        a += c
    } * 2.0
} + c
```

## Declaring Variables

The basic form of declaring a variable is `var VARNAME = VALUE`. 

If necessary, we can provide a type annotation:

```C#
var a::int = 5
a
```

The name of a variable can shadow static functions, and the language can still access the static function in most cases. For example, we introduced the `abs` function above. We could write:

```C#
var abs = "hello"
abs(-3) + abs.Length
```

This script returns `8.0f`.



As mentioned above, we can use `hvar` to move the declaration of a variable up by one scope. The main usage of this is in scoping declarations in StateMachine repeaters, but it can be applied generally.

```C#
for (hvar a = 0; a < 10; ++a) {
    // do nothing
}
a
```

This script returns `10.0f`.  If we did not use `hvar`, then `a` would not be visible outside the for loop.



Variables can be marked as constant. Constant variables cannot be reassigned, but are significantly faster to access in lexical and dynamic contexts, and can be accessed by constant script functions. Also, the initialization of a constant variable cannot reference non-constant variables or non-constant script functions.

```C#
const var a = 5.0;
var b = 5.0;
const var c = abs(a - 20);

//Does not compile
//const var c = abs(b - 20);
```



BDSL2 supports dynamic variable lookup in a limited set of circumstances. This is detailed in [Part 2](./guide2.md) of the language guide.

## Declaring Script Functions

We can declare functions using the `function` keyword. As with variables, the type is usually inferred. Generic functions are currently not supported. 

```C#
function myFn(x) {
    return x + 1;
}
myFn(10.0)
```

This script returns `11.0f`.

If necessary, we can annotate any of the parameter or return types of the function.

```C#
function myFn(x::int):: float {
    return x + 1;
}
myFn(10)
```

From DMK v11.1.0 onwards, functions can have a void return type. This must be annotated.

```c#
function myFn(x::int):: void {
	Logs.Log(x.ToString(), null, LogLevel.INFO);
	return; //return statement is optional
}
myFn(1000)
```



Functions can have default arguments. If you need to provide default arguments out-of-order, you can use the `default` keyword.

```c#
function add(a = 100, b = 9000)::int {
    return a + b;
}
add(); //returns 9100
add(1); //returns 9001
add(default, 2); //returns 102
```



Functions can access variables outside of the function definition if they are lexically visible.

```C#
//This script returns 14.0f.
var a = 4.0;
function myFn(x) {
    return x + a;
}
myFn(10)

//This script does not compile because ii is not lexically visible within the function.
function myFn(x) {
    return x + ii;
}
var total = 0.0;
for (var ii = 0.; ii < 5; ++ii) {
    total += myFn(1);
}
total;
```



As with variables, we can hoist the declaration of a function up by one scope by using `hfunction` instead of `function`. However, there aren't many good usages for this. 



Functions can be defined within other code blocks.

```C#
var total = 0.0;
for (var ii = 0.; ii < 10; ++ii) {
    function getSquare() {
        return ii * ii;
    }
    total += getSquare();
}
total
```

This script returns `285.0f`.



In the current version of BDSL2, functions do not have strong compile-time guarantees about the correctness of `return` statements for non-void functions. For example, consider the script below.

```C#
function myFn(x::float) {
    if (x > 2) {
        return x + 4;
    }
}
myFn(3)
myFn(1)
```

This script will compile successfully, though it would be considered incorrect in most languages. The `myFn(3)` call will successfully get the value `7`. However, the `myFn(1)` call will cause a runtime exception. In a future version, this will ideally return a compile-time exception instead.

- Note that functions with a void return type do not require `return` statements.



Functions can be marked as constant. Constant functions can be efficiently accessed from dynamic scopes. (Dynamic scopes are explained further in [Part 2](./guide2.md) of the language guide.) If a constant function accesses variables outside the function, those variables must be constant. If a constant function calls other functions, those functions must be constant.

```C#
const var hello = "hello";
const function join(a, b) {
    return a + " " + b;
}
const function sayHello(to) {
    return join(hello, to);
}
sayHello("world").Length
```

This script returns `11.0f` (or you can set the script's target type to string and remove `.Length`, in which case it returns `hello world`.) Since `sayHello` accesses `hello` and `join`, both `hello` and `join` must be constant for the script to compile.

## Declaring Script Macros

Macros replace text in the script before the script is compiled. They are declared similarly to functions, but do not involve function calls. They basically run a find&replace on the provided code.

The below macro usage:

```C#
macro add(a, b) {
    a + b
}
add(5.0, 4.0)
```

is EXACTLY THE SAME as the following script:

```
5.0 + 4.0
```

In most cases, it's better to use functions instead of macros, because functions offer better typechecking and code inspection without significant overhead. Also, recursive functions are supported in the engine, but recursive macros might blow up your computer. That said, there are some cases where it makes sense to use macros for certain types of StateMachine repeaters. See the `bdsl2 macro vs function.bdsl` script for am example of this.

## Comments

A line comment starts with `//` and proceeds until the end of the line.

```C#
//this is a comment
5.0 //this is also a comment
```

A block comment starts with `///` and ends with `///`.

```C#
///this is a comment
1.0 this line is also a comment
///
5.0
```



The VSCode extension has basic support for doc comments. If you place a comment right above a variable declaration or function, then that comment will appear whenever that variable or function is provided as a completion or hovered over.

![Code_qeHM1u50kd](../../images/Code_qeHM1u50kd.jpg)

## Imports

One script can import another script. To do this, the script to be imported must first be provided in the `Importable Scripts` field of the `GameReferences` object linked on `GameManagement`. For `BasicSceneOPENME`, the GameReferences object linked is "Default Game References", which by default has one importable script provided as "eximport".

![Unity_k3p10beFIx](../../images/Unity_k3p10beFIx.jpg)

Then, the script can be imported as follows:

```C#
import eximport at "./Example BDSL2 Import.bdsl" as imp

var x = 5.0
imp.aFn(x) + imp.aConstFn(imp.ms.Length);
```

Import statements must occur at the top of a script. In the import statement `import A as B as C`, `A` is the name of the import set in GameReferences (used by Unity to locate the import), `at B` is the relative filepath of the import from the current script (used by VSCode to locate the import), and `as C` is an optional alias for the import. `at B` is technically also optional, but the VSCode extension will not work without it.

When importing a script, all variables and functions (including constant variables and functions) declared in the top-level scope are visible. Non-constant variables can be modified (eg. we could write `imp.mn = 0` above, since `mn` is a non-constant float variable declared in the top-level scope of "Example BDSL2 Import.bdsl"). In the current implementation, these modifications are shared across all importers.

## Other Value Expressions

So far, we have only shown a few types of basic value expressions, such as addition and function calls. This section lists all the types of value expressions. Most of these value expressions are common across languages.

### Atomic Expressions

Atomic expressions are the simplest type of expression. The types of atomic expressions are as follows:

- Identifiers (variable, function, method, enum, or type names)

- Numbers (floats or ints)

- Value keywords `true`, `false`, and `null`

  - Note that `null` returns the "default value" for a type. For example, `null::float` is 0.0f. (You can specify the type of `null` the same way you specify the type of variables.)

- Strings (as in C#, written `"like this"`)

- Chars (as in C#, must have only one character: `'a'`)

- Localized strings, which are pulled from spreadsheets and can be translated at runtime

  - As an example, the below script returns `38.0f`, because the LocalizedString `boss.elly.dark` is configured to point to a much longer string.

    ```C#
    var w = :boss.elly.dark;
    w.Value.Length
    ```

- V2RV2s (see [the first bullet tutorial](../t01.md) for more details)

- Tuples (as in C#, in the format `(a, b, c)`)

- Block expressions (as discussed above, in the format `b{ ... }`)

- Arrays

  - Arrays are written in the format `{ a, b, c }`. If the elements are on separate lines with the same indentation, then the commas can be removed. The below examples are all valid.

    ```C#
    var arr0 = { 
        100.0
        200.0
        300.0
    }
    var arr1 = { 
        100.0, 200.0
        300.0
    }
    var arr2 = { 100.0, 200.0, 300.0 }
    ```

- Parenthesized expressions (ie. a tuple of one element)

### Term Expressions

A term expression is a simple expression which composes some atomic expressions. The types of term expressions are as follows:

- Constructors from default C# (eg. `new List<int>(4)`)

- Array/dictionary indexer (eg. `arr[4]` or `dict["key"]`)

- Property/field/method calls from default C#

  - Instance calls, eg. `myV2.x` or `myV2.Set(4, 6)` (where myV2 is a variable)
  - Static calls, eg. `Vector2.left` or `Vector2.SqrMagnitude(new Vector2(3, 4))` (Vector2 is a type)

- DMK static method calls

  - These functions do not need to be called with their declaring type, eg. `abs(-4)`. The relevant functions can be browsed in the [API documentation](https://dmk.bagoum.com/docs/api/) or looked up in the engine code itself.
  - These functions are usually faster than using the equivalent calls from default C#. 

- Partial function calls

  - See the next section.


### Partial Function Calls

DMK supports a limited amount of handling for partially-applied functions and lambdas. Currently, they are only supported for DMK static method calls, script functions, and `Func/Action` objects. The format to construct a lambda is `$(funcName, ...partialArgs)`. For example, using the DMK static method `Vector2 Rotate(float, Vector2)`, which rotates a Vector2 by some degrees:

```C#
var myRotator::Func<Vector2,Vector2> = $(rotate, 45);
myRotator(px(2)).x //=1.414...
var lazyV2::Func<Vector2> = $(myRotator, px(2))
lazyV2().x //=1.414...
```

There are some methods in the engine which require lambdas as arguments. For example, the `lerpsmooth` function takes an easing function as an argument. For example, we can provide [eoutbounce](https://easings.net/#easeOutBounce), which has type `Func<float, float>`, or we could provide [ceoutback](https://easings.net/#easeOutBack), which has type `Func<float, float, float>` and takes an extra argument that controls how far the movement moves past the end target.

```C#
sync "arrow-red/w" <> gsr2c 40 {
} s roffset px(lerpsmooth($(elinear), 0, 2, t, 0.4, 2))

sync "arrow-red/w" <> gsr2c 40 {
} s roffset px(lerpsmooth($(eoutbounce), 0, 2, t, 0.4, 2))
    
sync "arrow-red/w" <> gsr2c 40 {
} s roffset px(lerpsmooth($(ceoutback, 10), 0, 2, t, 0.4, 2))
```



You can also use partial methods where you would use lambdas in normal C# code. The following script code logs the current graze count to the console whenever a graze occurs ([instance.Graze](https://github.com/Bagoum/danmokou/blob/master/Assets/Danmokou/Plugins/Danmokou/Danmaku/GameInstance/InstanceData.cs#L129) is an event defined on the game instance data that is fired whenever a graze occurs). The Subscribe method takes an argument of type `Action<long>`, and since `printGraze` has a return type of void, it becomes an `Action` when partially applied.

```c#
function printGraze(graze::long)::void {
	Logs.Log(graze.ToString(), null, LogLevel.INFO);
}
(instance.Graze as IObservable<long>).Subscribe($(printGraze))
```

### Non-Parenthesized Methods and Operators

DMK static methods can be called without parentheses/commas. This can be nested arbitrarily. For example, consider the `float Min(float, float)` function. We could write:

```C#
min min min 2 3 min 5 min min 3 6 1 7
```

and this would be automatically parsed as:

```C#
min(min(min(2, 3), min(5, min(min(3, 6)), 1), 7)
```

Note that typechecking errors will become less clear if they occur within non-parenthesized method calls. 



Operators, such as `x++` or `x + y`, are grouped into "tight operators" and "loose operators". Tight operators have precedence over non-parenthesized method calls. For example, `x--` is a tight operator, so the following code:

```C#
var a = 3.2
min 3 a--
```

is parsed as `min(3, a--)`, returns 3.0f and sets `a` to 2.2f. (Note that `min(3, a)--` would not compile, since `--` can only be applied to writeable expressions.)

On the other hand, `x + y` is a loose operator, so the following code:

```C#
min 5 1 + 10
```

is parsed as `min(5, 1) + 10` and returns 11.0f.



The list of tight operators are as follows, in order of descending priority:

- Increment/decrement: `x++` / `x--` / `++x` / `--x` 
- Positive number: `+x`
  - This operator doesn't actually do anything.
- Negation: `-x`
- Boolean negation: `!x`

Except for `!x`, tight operators must have no whitespace between the operator and the value.



The list of loose operators are as follows, in order of descending priority:

- Power `x ^ y` 
- Multiplication `x * y`, Division `x / y`, Modulo `x % y` 
- Addition `x + y`, Subtraction `x - y`
- Comparators `<`, `>`, `<=`, `>=`
- Equality `==`, `!=`
- Boolean And `x && y`/ `x & y`, Boolean Or `x || y`/`x | y`
  - In BDSL2, `&` is the same as `&& ` and `|` is the same as `||`.
- Type conversion `x as Type`
- Assignment `=`, `+=`, `-=`, `*=`, `/=`, `%=`, `&=`, `|=`
- Conditional `cond ? ifTrue : ifFalse`

Loose operators can be written with or without whitespace, but there should not be a newline between the left-hand side and the operator (except for the conditional operator).

## Statements

Unlike value expressions, statements generally cannot be nested within each other. They are only allowed within blocks.

The valid types of statements are as follows:

- A value expression,
- A variable declaration,
- A function declaration,
- A return statement (within a function declaration),
- An if/else block,
- A for/while loop,
- A continue/break statement (within a loop).

If/else blocks and for/while loops are handled the same as in C#. Likewise, continue and break statements within loops are handled the same as in C#. The other types of statements are discussed in their own sections above.

