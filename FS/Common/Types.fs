module Common.Types

type UOErrorable<'t> =
    | TooFew
    | TooMany
    | Enough of 't
    with
    member this.fmap f =
        match this with
        | Enough x -> f x |> Enough
        | TooFew -> TooFew
        | TooMany -> TooMany
    member this.bind f =
        match this with
        | Enough x -> f x
        | TooFew -> TooFew
        | TooMany -> TooMany
    static member Fmap f (x:UOErrorable<'t>) = x.fmap(f)
    static member Bind f (x:UOErrorable<'t>) = x.bind(f)

type Errorable<'t> =
    | Failed of string list
    | OK of 't
    with
    member this.asErrs =
        match this with
        | Failed errs -> errs
        | OK _ -> []
    member this.fmap f =
        match this with
        | OK x -> f x |> OK
        | Failed errs -> Failed errs
    member this.bind f =
        match this with
        | OK x -> f x
        | Failed errs -> Failed errs
    static member Fmap f (x:Errorable<'t>) = x.fmap(f)
    static member Bind f (x:Errorable<'t>) = x.bind(f)
    static member Acc errbs =
        //ugly solution to avoid a type depth annoyance in il2cpp
        if List.forall (function | OK _ -> true | _ -> false) errbs
        then errbs |> List.map (function | OK x -> x) |> OK
        else errbs |> List.map (fun x -> x.asErrs) |> List.concat |> Failed
        
        //List.foldBack (fun x acc ->
        //    match (x, acc) with
        //    | OK x, OK acc -> OK (x::acc)
        //    | _, _ -> Failed <| List.append x.asErrs acc.asErrs
        //) errbs (OK [])
    static member AccFmap f errbs = Errorable<_>.Acc errbs |> Errorable.Fmap f
    static member AccConcat errbs = Errorable<_>.AccFmap List.concat errbs
    
    static member DeTuple f t =
        let err, y = t
        match err with
        | OK x -> OK (x, y)
        | Failed errs -> Failed <| (f y)::errs
    member this.Try =
        match this with
        | OK x -> x
        | Failed errs -> failwith <| String.concat "\n" errs
        
    static member FromOption err opt =
        match opt with
        | Some x -> OK x
        | None -> Failed [ err ]