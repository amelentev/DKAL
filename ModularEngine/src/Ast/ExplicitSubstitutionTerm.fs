﻿// *********************************************************
//
//    Copyright (c) Microsoft. All rights reserved.
//    This code is licensed under the Apache License, Version 2.0.
//    THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
//    ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
//    IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
//    PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
// *********************************************************

namespace Microsoft.Research.Dkal.Ast

open Microsoft.Research.Dkal.Interfaces

open System.Collections.Generic

/// Represents an AST term that carries an explicit substitution. Whenever a
/// substitution is applied on an ExplicitSubstitutionTerm, instead of modifying
/// the contained term, the given substitution is composed with the explictly
/// carried substitution. This is particularly useful to prevent fragile terms
/// from being corrupted when substitutions are applied on them (e.g., digital
/// signatures on evidence terms)
type ExplicitSubstitutionTerm(t: ITerm, subst: ISubstitution) =
  let s = subst.RestrictTo t.BoundVars

  new(t: ITerm) = new ExplicitSubstitutionTerm(t, Substitution.Id)

  /// The contained term on which substituions are not going to be applied 
  /// directly
  member et.Term = t

  /// The susbstitution that's being explicitly carried
  member et.Substitution = s

  override et.Equals(et': obj) = 
    match et' with
    | :? ExplicitSubstitutionTerm as et' -> 
      et.Term.Equals(et'.Term) && et.Substitution.Equals(et'.Substitution)
    | _ -> false

  override et.GetHashCode() =
    (et.Term, et.Substitution).GetHashCode()
  
  override et.ToString() =
    "(" + et.Term.ToString() + " " + et.Substitution.ToString() + ")"

  interface ITerm with
    
    member et.Type = Type.Evidence

    member et.BoundVars = 
      let ret = new HashSet<_>(t.BoundVars)
      for v in s.Domain do  
        if ret.Contains(v) then
          ret.Remove v |> ignore
          ret.UnionWith <| (s.Apply v).BoundVars
      ret |> Seq.toList

    member et.Vars = 
      let ret = new HashSet<_>(t.Vars)
      for v in s.Domain do  
        if ret.Contains(v) then
          ret.Remove v |> ignore
          ret.UnionWith <| (s.Apply v).Vars
      ret |> Seq.toList

    member et.Apply (s': ISubstitution) = new ExplicitSubstitutionTerm(et.Term, s'.ComposeWith et.Substitution) :> ITerm

    member et.Normalize () = new ExplicitSubstitutionTerm(et.Term.Normalize(), et.Substitution) :> ITerm

    member et.UnifyFrom (s': ISubstitution) (t': ITerm) = 
      match t' with
      | :? ExplicitSubstitutionTerm as et' -> 
        et.Term.Apply(et.Substitution).UnifyFrom s' (et'.Term.Apply(et'.Substitution))
      | _ -> t'.UnifyFrom s' et

    member et.Unify (t': ITerm) = (et :> ITerm).UnifyFrom (Substitution.Id) t'


