module Common.Extensions
open System
let asList opt = match opt with
                 | Some x -> [x]
                 | None -> []
                 
let toString chars =
    chars |>
    Array.ofList |>
    String
let revToString chars =
    chars |>
    List.rev |>
    Array.ofList |>
    String
    
let revSnd(x, y) = (x, List.rev y)

type List =
    ///Zip two lists. If they have different lengths, only zip up to the shorter list.
    static member SoftZip<'T,'U> (x:List<'T>) (y:List<'U>) =
        let xl = x.Length
        let yl = y.Length
        if xl > yl
        then List.zip (List.take yl x) y
        else if xl < yl
        then List.zip x (List.take xl y)
        else List.zip x y
    
    static member FilterNone<'T> (arr: List<'T option>) =
        List.foldBack (fun x acc ->
            match x with
            | Some x -> x::acc
            | None -> acc) arr []
        
    static member SoftSkip<'T> (ct:int) (arr: List<'T option>) =
        match ct, arr with
        | 0, _ -> arr
        | _, [] -> arr
        | _, x::xs -> List.SoftSkip (ct - 1) xs

type Option =
    static member Bifurcate (opt: 'T option) ifSome ifNone =
        match opt with
        | Some _ -> Some ifSome
        | None -> ifNone