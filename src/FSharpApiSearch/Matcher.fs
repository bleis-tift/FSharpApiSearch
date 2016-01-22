﻿module FSharpApiSearch.Matcher

open FSharpApiSearch.Types

type Equations = {
  Equalities: (Type * Type) list
  Inequalities: (Type * Type) list
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Equations =
  let sortTerm x y = if x <= y then (x, y) else (y, x)

  let containsEquality left right eqs =
    let x = sortTerm left right
    eqs.Equalities |> List.contains x
  let findEqualities left eqs = eqs.Equalities |> List.filter (fst >> ((=)left))

  let testInequality left right eqs =
    eqs.Inequalities
    |> List.choose (fun (x, y) -> if x = left then Some y elif y = left then Some x else None)
    |> List.forall (fun inequalityTerm ->
      let xy = sortTerm right inequalityTerm
      eqs.Equalities |> List.exists ((=)xy) |> not
    )

  let tryAddEquality left right eqs =
    let left, right = sortTerm left right
    if testInequality left right eqs then
      Some { eqs with Equalities = (left, right) :: eqs.Equalities }
    else
      None

  let empty = { Equalities = []; Inequalities = [] }

  let strict { Query = t } =
    let nonEqualities =
      [
        let variables = Type.collectVariables t
        for x in variables do
          for y in variables do
            if x < y then yield (x, y)
      ]
    { Equalities = []; Inequalities = nonEqualities }

type MatchResult =
  | Success of Equations
  | Failure

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MatchResult =
  let ofOption = function
    | Some newEqs -> Success newEqs
    | None -> Failure

  let inline bind f = function Success x -> f x | Failure -> Failure

let rec run (left: Type) (right: Type) (eqs: Equations): MatchResult =
  match left, right with
  | TypeIdentity (Type leftName), TypeIdentity (Type rightName) ->
    if leftName = rightName then
      Success eqs
    else
      Failure
  | Arrow leftTypes, Arrow rightTypes ->
    runGeneric leftTypes rightTypes eqs
  | Generic (leftId, leftParams), Generic (rightId, rightParams) ->
    runGeneric (TypeIdentity leftId :: leftParams) (TypeIdentity rightId :: rightParams) eqs
  | Tuple leftTypes, Tuple rightTypes ->
    runGeneric leftTypes rightTypes eqs
  | TypeIdentity (Variable _), TypeIdentity (Variable _) ->
    let left, right = Equations.sortTerm left right
    if Equations.containsEquality left right eqs then
      Success eqs
    else
      attemptToAddEquality left right eqs
  | (TypeIdentity (Variable _) as left), right
  | right, (TypeIdentity (Variable _) as left) ->
    attemptToAddEquality left right eqs
  | _ ->
    Failure
and runGeneric (leftTypes: Type list) (rightTypes: Type list) (eqs: Equations): MatchResult =
  if leftTypes.Length <> rightTypes.Length then
    Failure
  else
    List.zip leftTypes rightTypes
    |> List.fold (fun result (left, right) -> MatchResult.bind (run left right) result) (Success eqs)
and attemptToAddEquality left right eqs =
  eqs
  |> Equations.findEqualities left
  |> List.fold (fun result (_, x) -> MatchResult.bind (run right x) result) (Success eqs)
  |> MatchResult.bind (Equations.tryAddEquality left right >> MatchResult.ofOption)

let matches { Query = query } target initialEquations =
  match run query target initialEquations with
  | Success _ -> true
  | Failure -> false