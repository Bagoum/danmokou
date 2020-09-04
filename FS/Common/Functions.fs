module Common.Functions
open Common.Types

let firstSome x i =
    if i = 0 then []
    else (Some x)::[for _ in 2 .. i -> None]
    
let partialZip ls =
    let ls = List.map (fun (i, j) -> (i, firstSome j (List.length i))) ls
    ls |> List.map fst |> List.concat, ls |> List.map snd |> List.concat
    
let partialZip2 ls =
    let is, js = List.foldBack (fun (is, j) (accis, accj)
                                    -> (is::accis, (firstSome j (List.length is))::accj))
                                        ls ([], [])
    (is |> List.concat, js |> List.concat)


let maxConsecutive x =
    List.fold (fun (max, curr) y ->
            if x = y then
                let curr = curr + 1
                if curr > max then (curr, curr) else (max, curr)
            else (max, 0)
        ) (0, 0) >> fst

let intertwine xs ys =
    List.foldBack2 (fun x y acc -> x::y::acc) xs ys []
let intertwine1 xs y = List.replicate (List.length xs) y |> intertwine xs

let replaceEntries allowFew dst source filter =
    List.fold (fun (acc: UOErrorable<_>) x
                -> acc.bind (fun (dst2r, remsrc) ->
                    if filter x
                    then match remsrc with
                            | rh::rt -> Enough (rh::dst2r, rt)
                            | [] -> if allowFew
                                    then Enough (x::dst2r, remsrc)
                                    else TooFew
                    else Enough (x::dst2r, remsrc))
    ) (Enough ([], source)) dst |> UOErrorable<_>.Bind (fun (dst2r, remsrc) ->
        if List.isEmpty remsrc
        then Enough <| List.rev dst2r
        else TooMany)
    
let countFilter f l =
    List.fold (fun i x ->
        if f x then i + 1 else i) 0 l