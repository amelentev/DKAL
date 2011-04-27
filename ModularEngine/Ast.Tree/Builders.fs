﻿[<AutoOpen>]
module Microsoft.Research.Dkal.Ast.Tree.Builders

  open Microsoft.Research.Dkal.Interfaces
  
  let App (f: Function, args: ITerm list) =
    if List.forall2 (fun (t: IType) (a: ITerm) -> t = a.Type) f.ArgsType args then
      { Function = f; Args = args } :> ITerm
    else
      failwith <| "Incorrect parameter types when building " + f.Name

  let Const (c: Constant) =
    c :> ITerm

  let Var (v: Variable) =
    v :> ITerm
