module FTests.LocalizationParser
open System
open StaticParser.LocalizationParser
open FTests.Utils
open FTests.Extensions

open NUnit.Framework

[<SetUp>]
let Setup () =
    ()
    

let objs str =
    (stringParser str).Try
    
let Eq(str, maxArg, units) =
    let (runits, rstate) = (stringParser str).Try
    Assert.AreEqual(maxArg, rstate.highestArg)
    Assert.ListEq(units, runits)
    
    
[<Test>]
let TestBasic() =
    Assert.IsFailed(stringParser "{")
    Eq("hello {0} world {1:F1}!", 1, [
        String "hello "
        StandardFormat "0"
        String " world "
        StandardFormat "1:F1"
        String "!"
    ])
    Eq("hello \{0\} world \{!", -1, [
        String "hello "
        String "{{"
        String "0"
        String "}}"
        String " world "
        String "{{"
        String "!"
    ])
    Eq("{0}{3:0000.00}", 3, [
        StandardFormat "0"
        StandardFormat "3:0000.00"
    ])
    
[<Test>]
let TestInvoke() =
    Eq("{0} picked up {1} gold {$PLURAL(2, coin, coins)}!", 2, [
        StandardFormat "0"
        String " picked up "
        StandardFormat "1"
        String " gold "
        ConjFormat {
            func = "PLURAL"
            args = [
                [Argument 2]
                [String "coin"]
                [String "coins"]
            ]
        }
        String "!"
    ])

[<Test>]
let TestQuote() =
    Eq(" \\\"Hello\\\" ", -1, [
        String " "
        String "\\\"" //escape for C# + one quotation
        String "Hello"
        String "\\\""
        String " "
    ])
    //Non-escaped quotes allowed at toplevel only
    Eq(" \"Hello\" ", -1, [
        String " \\\"Hello\\\" "
    ])

[<Test>]
let TestInvokeNested() =
    // Hello {$PLURAL(2, " "World" ", "gold {$PLURAL(4, coin, coins)}")}!
    //Non-escaped quotes allowed at toplevel only
    Assert.IsFailed(stringParser "Hello {$PLURAL(2, \" \"World\" \", \"gold {$PLURAL(4, coin, coins)}\")}!")
    
    // Hello {$PLURAL(2, " \"World\" ", "gold {$PLURAL(4, coin, coins)}")}!
    Eq("Hello {$PLURAL(2, \" \\\"World\\\" \", \"gold {$PLURAL(4, coin, coins)}\")}!", 4, [
        String "Hello "
        ConjFormat {
            func = "PLURAL"
            args = [
                [Argument 2]
                [
                    String " "
                    String "\\\"" //escape for C# + one quotation
                    String "World"
                    String "\\\""
                    String " "
                ]
                [
                    String "gold "
                    ConjFormat {
                        func = "PLURAL"
                        args = [
                            [Argument 4]
                            [String "coin"]
                            [String "coins"]
                        ]
                    }
                ]
            ]
        }
        String "!"
    ])