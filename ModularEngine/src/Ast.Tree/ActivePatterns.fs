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

/// Defines the public interface on how to pattern match AST elements defined
/// in the Ast.Tree module
[<AutoOpen>]
module Microsoft.Research.Dkal.Ast.Tree.ActivePatterns

  open Microsoft.Research.Dkal.Interfaces
  
  /// Active pattern to recognize function application AST elements
  let (|App|_|) (t: ITerm) =  match t with
                              | :? Application as a -> Some (a.Function, a.Args)
                              | _ -> None
