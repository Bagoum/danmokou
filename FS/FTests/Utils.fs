module FTests.Utils

open FCommon.Types
open NUnit.Framework


module Assert =
    /// Verifies that a delegate raises the specific exception when called.
    let Raises<'TExn when 'TExn :> exn> (f:unit->unit) = 
        Assert.Throws<'TExn>(TestDelegate(f)) |> ignore
    let IsSuccess = function
                | OK x -> ()
                | Failed errs -> Assert.Fail(String.concat "\n" errs)
    let IsFailed = function
                | OK x -> Assert.Fail("Expected failed computation")
                | Failed errs -> ()
    let IsTooFew = function
                | TooFew -> ()
                | _ -> Assert.Fail("Expected underflow computation")
    let IsTooMany = function
                | TooMany -> ()
                | _ -> Assert.Fail("Expected underflow computation")
                