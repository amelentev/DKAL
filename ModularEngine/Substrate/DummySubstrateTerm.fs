﻿namespace Microsoft.Research.Dkal.Substrate

open Microsoft.Research.Dkal.Interfaces
open Microsoft.Research.Dkal.Ast

type DummySubstrateTerm(query : ITerm, ns : string) =
  member this.Query = query
  interface ISubstrateTerm with
    member this.Namespace = ns
  interface ITerm with
    member this.Type = query.Type
    member this.Vars = query.Vars
    member this.Apply s = DummySubstrateTerm(query.Apply s, ns) :> ITerm
    member this.Normalize() = DummySubstrateTerm(query.Normalize(), ns) :> ITerm
    member this.UnifyFrom s t = query.UnifyFrom s t
    member this.Unify t = query.Unify t
  override this.ToString() = 
    "{| \"" + ns + "\" | " + query.ToString() + " |}"
  override this.Equals (o: obj) =
    match o with
    | :? DummySubstrateTerm as d' ->
      this.Query = d'.Query && (this :> ISubstrateTerm).Namespace = (d' :> ISubstrateTerm).Namespace
    | _ -> false
  override this.GetHashCode() =
    (this.Query, (this :> ISubstrateTerm).Namespace).GetHashCode()

