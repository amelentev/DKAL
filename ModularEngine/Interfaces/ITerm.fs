﻿namespace Microsoft.Research.Dkal.Interfaces

type IType = 
  abstract member Name: string
  abstract member FullName: string

type ITerm =
  abstract member Type: IType
  abstract member Vars: IVar list
  abstract member Apply: ISubstitution -> ITerm
  abstract member Normalize: unit -> ITerm
  abstract member UnifyFrom: ISubstitution -> ITerm -> ISubstitution option
  abstract member Unify: ITerm -> ISubstitution option

and IVar =
  inherit ITerm
  abstract member Name: string

and ISubstitution =
  abstract member Apply: IVar -> ITerm
  abstract member Extend: v: IVar * t: ITerm -> ISubstitution
  abstract member ComposeWith: ISubstitution -> ISubstitution
  abstract member Contains: IVar -> bool
