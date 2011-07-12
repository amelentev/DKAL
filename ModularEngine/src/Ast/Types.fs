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

/// Defines the basic IType implementations for DKAL types
module Type = 

  type private BasicType(fullName: string, name: string) = 
    interface IType with
      member bt.FullName = fullName
      member bt.Name = name
    override bt.GetHashCode() = (bt :> IType).FullName.GetHashCode()
    override bt.Equals (o: obj) = 
      match o with
      | :? BasicType as bt' -> (bt :> IType).FullName.Equals((bt' :> IType).FullName)
      | _ -> false
    override bt.ToString() = (bt :> IType).FullName

  /// Type for infons
  let Infon = new BasicType("Dkal.Infon", "Infon") :> IType

  /// Type for principals
  let Principal = new BasicType("Dkal.Principal", "Principal") :> IType

  /// Type for substrate update terms
  let SubstrateUpdate = new BasicType("Dkal.SubstrateUpdate", "SubstrateUpdate") :> IType

  /// Type for substrate query terms
  let SubstrateQuery = new BasicType("Dkal.SubstrateQuery", "SubstrateQuery") :> IType

  /// Type for actions used in policy rules
  let Action = new BasicType("Dkal.Action", "Action") :> IType

  /// Type for conditions used in policy rules
  let Condition = new BasicType("Dkal.Condition", "Condition") :> IType

  /// Type for policy rules 
  let Rule = new BasicType("Dkal.Rule", "Rule") :> IType

  /// Type for evidence (a.k.a. justification, or proof)
  let Evidence = new BasicType("Dkal.Evidence", "Evidence") :> IType

  /// Defines an IType implementation to use .NET types as DKAL types
  type Substrate(typ: System.Type) = 
    interface IType
      with 
        member t.FullName = typ.FullName
        member t.Name = typ.Name
    /// The .NET type wrapped by this Substrate type
    member s.Type = typ
    override s.Equals t' = match t' with
                           | :? Substrate as t' -> typ.Equals(t'.Type)
                           | _ -> false
    override s.GetHashCode() = typ.GetHashCode()
    override s.ToString() = (s :> IType).FullName

  // type shortcuts
  let Boolean = Substrate(typeof<bool>) :> IType
  let Int32 = Substrate(typeof<int32>) :> IType
  let Double = Substrate(typeof<double>) :> IType
  let String = Substrate(typeof<string>) :> IType

  let FromFullName fn = 
    match fn with
    | "Dkal.Infon" -> Infon
    | "Dkal.Principal" -> Principal
    | "Dkal.SubstrateUpdate" -> SubstrateUpdate
    | "Dkal.SubstrateQuery" -> SubstrateQuery
    | "Dkal.Condition" -> Condition
    | "Dkal.Action" -> Action
    | "Dkal.Rule" -> Rule
    | "Dkal.Evidence" -> Evidence
    | fn -> 
      let t = System.Type.GetType(fn)
      if t <> null then
        Substrate(t) :> IType
      else
        failwithf "Unknown type: %O, check spelling and make sure to use fully qualified names (e.g., Dkal.Principal, System.Int32)" fn
