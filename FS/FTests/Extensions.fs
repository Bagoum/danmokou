module FTests.Extensions

open FCommon.Extensions

open NUnit.Framework

type Assert with
    static member Raises<'TExn when 'TExn :> exn> (f:unit->unit) = 
        Assert.Throws<'TExn>(TestDelegate(f)) |> ignore
    static member ListEq(xs, ys) =
        let lenErr =
            if List.length xs = List.length ys
            then None
            else sprintf "First list has length %d, second has length %d" (List.length xs) (List.length ys) |> Some
        let compErr = List.SoftZip xs ys
                      |> List.mapi (fun i (x,y) -> (i,x,y))
                      |> List.fold (fun err (i,x,y) ->
                            match err with
                            | None -> if x = y then None
                                      else sprintf "Difference at index %d: \n%O\n|\n%O" i x y |> Some
                            | x -> x) None
        match lenErr, compErr with
        | Some le, Some ce -> Assert.Fail(sprintf "%s\n%s" le ce)
        | Some le, _ -> Assert.Fail(le)
        | _, Some ce -> Assert.Fail(ce)
        | _, _ -> ()
    
    static member HasValue<'T,'U when 'T : comparison>(map:Map<'T,'U>, key:'T) =
        match map.TryFind key with
        | Some x -> x
        | None -> Assert.Fail() |> failwith("Key does not exist")