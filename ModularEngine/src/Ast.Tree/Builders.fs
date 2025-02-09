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

/// Defines the public interface on how to construct AST elements defined
/// in the Ast.Tree module
[<AutoOpen>]
module Microsoft.Research.Dkal.Ast.Tree.Builders

  open Microsoft.Research.Dkal.Interfaces
  
  /// Build a function application AST node. It will check that the amount of 
  /// parameters and their types are correct before building the term
  let App (f: Function, args: ITerm list) =
    if f.ArgsType.Length = args.Length && List.forall2 (fun (t: IType) (a: ITerm) -> a.Type.IsSubtypeOf t) f.ArgsType args then
      { Function = f; Args = args } :> ITerm
    else
      failwithf "Incorrect parameter types when building %O. Expecting %O but found %O (types %O)" 
          f.Name [for t in f.ArgsType -> t.Name] args [for a in args -> a.Type.Name]


