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

namespace Microsoft.Research.Dkal.Substrate

open Microsoft.FSharp.Reflection
open System.Collections.Generic

open Microsoft.Research.Dkal.Ast
open Microsoft.Research.Dkal.Ast.Infon
open Microsoft.Research.Dkal.Interfaces

/// A substrate the wraps a mapping from function names to (curried) F# functions.
/// This substrate is not intended to be used directly. The purpose of this
/// substrate is to serve as a basis for the implementation of specialized
/// substrates via initialization of an instance of an FSharpSubstrate with a
/// concrete mapping from function names to corresponding F# implementations.
/// Example for usage: Substrate.Crypto
// TODO: support uncurried functions and do typechecking
module FSharp =

  /// Extracts the argument types of a curried fsharp function
  let internal getFuncTypes f = 
    let rec r x =
      match FSharpType.GetFunctionElements(x) with
      | (a,b) when FSharpType.IsFunction(b) -> a :: r b
      | (a,b) -> [a;b]
    if FSharpType.IsFunction(f.GetType()) then
      r (f.GetType())
    else
      failwith <| sprintf "%s is not a function" (f.GetType().Name)

  let internal getFuncArgTypes f = 
    getFuncTypes f |> List.rev |> List.tail |> List.rev

  let internal getFuncResultType f =
    getFuncTypes f |> List.rev |> List.head

  let internal getFirstArg f = getFuncTypes f |> List.head
  let internal getMethodInfo f = f.GetType().GetMethod("Invoke",[|getFirstArg f|])
  let internal invoke (mi:System.Reflection.MethodInfo) f o =  mi.Invoke(f,[|o|])
  let internal apply f o = invoke (getMethodInfo f) f o

  /// A function application AST element
  type Function = { Name:string; ReturnType:System.Type; Args:list<FunctionTerm> }
  /// A type to keep AST elements of the F# Substrate
  and FunctionTerm = 
  /// Function application AST element for the F# Substrate (uses Function type)
  | Function of Function
  /// Variable AST element for the F# Substrate
  | Var of IVar
  /// Constant AST element for the F# Substrate
  | Const of Constant
    with
    member this.Type =
      match this with
      | Function { ReturnType = t } -> Type.Substrate(t) :> IType
      | Var v -> v.Type
      | Const c -> (c :> ITerm).Type

    member this.Vars = 
      match this with
      | Function { Args = args } -> List.collect (fun (x:FunctionTerm) -> x.Vars) args
      | Const _ -> []
      | Var v -> [v]

    member this.Apply s = 
      match this with
      | Function ({ Args = args } as func) -> Function { func with Args = List.map (fun (x:FunctionTerm) -> ((x :> ITerm).Apply s) :?> FunctionTerm) args } :> ITerm
      | Var v ->  
        match s.Apply v with
        | :? FunctionTerm as x -> x :> ITerm
        | :? IVar as x -> Var x :> ITerm
        | :? Constant as x -> Const x :> ITerm
        | _ -> failwith "Illegal substitution"
      | Const c as x -> x :> ITerm

    member this.Normalize() =
      match this with
      | Function ({Args = args } as func) -> Function { func with Args = List.map (fun (x:FunctionTerm) -> (x :> ITerm).Normalize() :?> FunctionTerm) args } :> ITerm
      | x -> x  :> ITerm

    member this.isGround =
      match this with
      | Function { Args = args } -> List.forall (fun (x:FunctionTerm) -> x.isGround) args
      | Var _ -> false
      | Const _ -> true
  
    member this.isGroundOrVar = 
      match this with
      | Var _ -> true
      | x -> x.isGround

    member this.UnifyFrom s t =
      match (t:ITerm) with 
      | :? FunctionTerm as x -> 
        match (this,x) with
        // XXX: when unifying from a substitution s, it needs to be applied to both operands.
        // XXX: this must be done with care so that not to enter an infinite loop 
        // XXX: check Ast.Tree and Ast to see how unifyFrom is solved there
        | Function f, Function g -> Some s
        | Var v, Var w -> Some s
        | Const c, Const d -> Some s
        | _ -> None
      | _ -> None

    interface ITerm with
      member this.Type = this.Type
      member this.Vars = this.Vars
      member this.BoundVars = []
      member this.Apply s = this.Apply s
      member this.Normalize() = this.Normalize()
      member this.UnifyFrom s t = this.UnifyFrom s t
      member this.Unify t = this.UnifyFrom Substitution.Id t

  let createFunction (name:string) (retType:System.Type) (args:FunctionTerm list) = 
    Function { Name = name; ReturnType = retType; Args = args }

  /// The Result is either a _ground_ term, a constant or a variable.
  /// When solving the term the values of Body and Result are unified.
  type FunctionQueryTerm(ns:string, body:FunctionTerm, result:FunctionTerm) =
    do 
      if not result.isGroundOrVar then failwith "Result term must be ground or a variable"
      if result.Type <> body.Type then failwith "Result type must match type of body"
    member this.Body = body
    member this.Result = result
    member this.Namespace = ns
    member this.Type = Type.SubstrateQuery
    member this.Vars = this.Body.Vars @ this.Result.Vars
    member this.Apply s = FunctionQueryTerm(ns, this.Body.Apply s :?> FunctionTerm, this.Result.Apply s :?> FunctionTerm) :> ITerm
    member this.Normalize() = FunctionQueryTerm(ns, this.Body.Normalize() :?> FunctionTerm, this.Result.Normalize() :?> FunctionTerm) :> ITerm
    member this.UnifyFrom s t =
      match (t:ITerm) with 
      | :? FunctionQueryTerm as x when x.Namespace = this.Namespace -> this.Body.UnifyFrom s (x.Body) 
                                                                       |> Option.bind (fun s -> this.Result.UnifyFrom s (x.Result))
      | _ -> None
    interface ISubstrateQueryTerm with
      member this.Namespace = ns
    interface ITerm with
      member this.Type = this.Type
      member this.Vars = this.Vars
      member this.BoundVars = []
      member this.Apply s = this.Apply s
      member this.Normalize() = this.Normalize()
      member this.UnifyFrom s t = this.UnifyFrom s t
      member this.Unify t = this.UnifyFrom Substitution.Id t
    override this.Equals (o: obj) =
      match o with
      | :? FunctionQueryTerm as o ->
        this.Body = o.Body && this.Result = o.Result && this.Namespace = o.Namespace
      | _ -> false
    override this.GetHashCode() =
      (this.Body, this.Result, this.Namespace).GetHashCode()
 
  /// Convenient Functions for in memory construction and inspection of FunctionTerms and FunctionQueryTerms 
  let var<'t> (n:string) : FunctionTerm = Var {Name=n; Type=Type.Substrate(typeof<'t>); }
  let con c : FunctionTerm = Const <| Constant (box c)
  
  let toVar = function
  | Var x -> x
  |_ -> failwith "toVar: Term is not a Variable" 
  
  let toConstElem = function
  | Const (SubstrateConstant x) -> x
  | _ -> failwith "toConstElem: Term is not a Constant" 


  /// FSharpSubstrate that takes FunctionQueryTerms and solves them by means
  /// of applying the functions to their arguments
  type FSharpSubstrate(ns:string list) =

    let functions = new Dictionary<string,obj>()
    let namespaces = new HashSet<string>()

    do namespaces.UnionWith(ns) |> ignore

    member this.createFunctionTerm name (args:FunctionTerm list) =
      let f = unbox <| functions.[name]
      let argTypes = getFuncArgTypes f |> List.map (fun x -> Type.Substrate(x) :> IType)
      if Seq.zip args argTypes |> Seq.forall (fun (a,b) -> a.Type = b)
        then
         createFunction name (getFuncResultType f) args
        else
          failwith "The types of the variables do not match the types of the function"

    member this.createQueryTerm body result = FunctionQueryTerm(Seq.head namespaces, body, result)

    member x.Add n f = functions.Add(n,box f)

    member internal x.simpleSolve (query:FunctionQueryTerm) (subst:ISubstitution) =
      if not <| namespaces.Contains(query.Namespace) then 
        failwith "The namespace of the query is not within scope of the substrate"
      let a = query.Apply subst :?> FunctionQueryTerm
      if not a.Body.isGround then failwith "simpleSolve: Insufficient instantiated function call. Substitution did not result in ground function body"

      // FIXME do typechecking
      let rec compute (t:FunctionTerm) =
        match t with
        | Const (SubstrateConstant c) -> c
        | Function { Name = name; ReturnType = returnType; Args = args } -> 
          let f = functions.[name]
          let computedArgs = List.map compute args
          printf "%s, %s" ((unbox f).ToString()) ((unbox f).GetType().ToString())
          //List.fold (fun f x -> box ((unbox f) ((unbox x):int))) f computedArgs
          List.fold (fun f x -> apply f x) f computedArgs
        | _ -> failwith "simpleSolve.compute: Insufficient istantiated function call. Substitution did not result in ground function body"

      let res = compute a.Body
          
      match a.Result with
      | Var v -> [subst.Extend(v,Const (Constant res))]
      | x -> let ret = compute x
             if unbox ret = unbox res then [subst]
                                      else []

    member this.Solve (queries:seq<ISubstrateQueryTerm>) substs =
      try
        let cq:seq<FunctionQueryTerm> = Seq.cast queries
        let runQuery q s =
          match this.simpleSolve q s with
          | [] -> None
          | [s'] -> Some s'
          | _ -> failwith "Must not happen"
        substs |> Seq.choose (fun sub -> Seq.fold (fun s q -> Option.bind (runQuery q) s) (Some sub) cq)
      with
      | _ -> failwith <| sprintf "Solve: This substrate does not handle queries of this kind. %s" ((Seq.head queries).ToString())

    interface ISubstrate with
      member this.Solve queries substs = this.Solve queries substs
      member this.Update _ = failwith "F# substrate does not support updates (for now?)"
      member this.AreConsistentUpdates _ = failwith "F# substrate does not support updates (for now?)"
      member x.Namespaces = namespaces
      member x.RequiredVars mt =
        match mt with
        | :? FunctionQueryTerm as q -> q.Body.Vars
        | _ -> failwith <| sprintf "RequiredVars: This substrate does not handle queries of this kind. %s" (mt.ToString())
      