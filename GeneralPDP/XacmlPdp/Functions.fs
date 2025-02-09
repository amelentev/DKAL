﻿namespace Microsoft.Research.GeneralPDP.XACML.PDP

open Microsoft.Research.GeneralPDP.XACML.Ast
open Basics

open System.Collections.Generic

module Functions =

  /// An function environment which stores XACML function definitions used in expressions
  let funcEnv = new Dictionary<string, FunctionF>()

  /// Add some flexibility to functions and let them accept single-valued bags as well
  let unbag v = match v with
                | BagValue [v] -> v
                | v -> v

  // ------------ Equality ------------
  let valueEq vs =
    match vs with
      | [a; b] -> 
        match unbag a, unbag b with
        | IndeterminateValue, _ -> IndeterminateValue
        | _, IndeterminateValue -> IndeterminateValue
        | a, b -> BoolAtomValue (a = b)
      | _ -> IndeterminateValue

  // ------------ Bag functions ------------
  let bagOneAndOnly vs =
    match vs with
      | [BagValue [v]] -> v
      | _ -> IndeterminateValue

  // ------------ Boolean functions ------------
  let logicalOr = 
    let f r b = 
      let r = unbag r
      let b = unbag b
      match r, b with
        | BoolAtomValue true, _ -> BoolAtomValue true
        | BoolAtomValue a, BoolAtomValue b -> BoolAtomValue (a || b)
        | _, _ -> IndeterminateValue
    List.fold f (BoolAtomValue false)

  let logicalAnd = 
    let f r b = 
      let r = unbag r
      let b = unbag b
      match r, b with
        | BoolAtomValue false, _ -> BoolAtomValue false
        | BoolAtomValue a, BoolAtomValue b -> BoolAtomValue (a && b)
        | _, _ -> IndeterminateValue
    List.fold f (BoolAtomValue true)

  let logicalNot vs = 
    match vs with 
    | [v] -> 
      match unbag v with 
      | BoolAtomValue b -> BoolAtomValue (not b)
      | _ -> IndeterminateValue
    | _ -> IndeterminateValue

  // ------------ Integer functions ------------
  let integerGreaterThanOrEqual vs = 
    match vs with 
      | [v1; v2] -> 
        match unbag v1, unbag v2 with
        | IntAtomValue i1, IntAtomValue i2 -> BoolAtomValue (i1 >= i2)
        | _ -> IndeterminateValue
      | _ -> IndeterminateValue

  let integerGreaterThan vs = 
    match vs with 
      | [v1; v2] -> 
        match unbag v1, unbag v2 with
        | IntAtomValue i1, IntAtomValue i2 -> BoolAtomValue (i1 > i2)
        | _ -> IndeterminateValue
      | _ -> IndeterminateValue

  let integerLessThanOrEqual vs = 
    match vs with 
      | [v1; v2] -> 
        match unbag v1, unbag v2 with
        | IntAtomValue i1, IntAtomValue i2 -> BoolAtomValue (i1 <= i2)
        | _ -> IndeterminateValue
      | _ -> IndeterminateValue

  let integerLessThan vs = 
    match vs with 
      | [v1; v2] -> 
        match unbag v1, unbag v2 with
        | IntAtomValue i1, IntAtomValue i2 -> BoolAtomValue (i1 < i2)
        | _ -> IndeterminateValue
      | _ -> IndeterminateValue

  let integerAdd vs = 
    match vs with 
      | [v1; v2] -> 
        match unbag v1, unbag v2 with
        | IntAtomValue i1, IntAtomValue i2 -> IntAtomValue (i1 + i2)
        | _ -> IndeterminateValue
      | _ -> IndeterminateValue

  let integerSubtract vs = 
    match vs with 
      | [v1; v2] -> 
        match unbag v1, unbag v2 with
        | IntAtomValue i1, IntAtomValue i2 -> IntAtomValue (i1 - i2)
        | _ -> IndeterminateValue
      | _ -> IndeterminateValue

  // ------------ Double functions ------------
  let doubleGreaterThanOrEqual vs = 
    match vs with 
      | [v1; v2] -> 
        match unbag v1, unbag v2 with
        | DoubleAtomValue i1, DoubleAtomValue i2 -> BoolAtomValue (i1 >= i2)
        | _ -> IndeterminateValue
      | _ -> IndeterminateValue

  let doubleGreaterThan vs = 
    match vs with 
      | [v1; v2] -> 
        match unbag v1, unbag v2 with
        | DoubleAtomValue i1, DoubleAtomValue i2 -> BoolAtomValue (i1 > i2)
        | _ -> IndeterminateValue
      | _ -> IndeterminateValue

  let doubleLessThanOrEqual vs = 
    match vs with 
      | [v1; v2] -> 
        match unbag v1, unbag v2 with
        | DoubleAtomValue i1, DoubleAtomValue i2 -> BoolAtomValue (i1 <= i2)
        | _ -> IndeterminateValue
      | _ -> IndeterminateValue

  let doubleLessThan vs = 
    match vs with 
      | [v1; v2] -> 
        match unbag v1, unbag v2 with
        | DoubleAtomValue i1, DoubleAtomValue i2 -> BoolAtomValue (i1 < i2)
        | _ -> IndeterminateValue
      | _ -> IndeterminateValue

  let doubleAdd vs = 
    match vs with 
      | [v1; v2] -> 
        match unbag v1, unbag v2 with
        | DoubleAtomValue i1, DoubleAtomValue i2 -> DoubleAtomValue (i1 + i2)
        | _ -> IndeterminateValue
      | _ -> IndeterminateValue

  let doubleSubtract vs = 
    match vs with 
      | [v1; v2] -> 
        match unbag v1, unbag v2 with
        | DoubleAtomValue i1, DoubleAtomValue i2 -> DoubleAtomValue (i1 - i2)
        | _ -> IndeterminateValue
      | _ -> IndeterminateValue


  // ------------ Do the function environment filling ------------
  funcEnv.Add("anyURI-equal", valueEq)
  funcEnv.Add("string-equal", valueEq)
  funcEnv.Add("boolean-equal", valueEq)
  funcEnv.Add("integer-equal", valueEq)
  funcEnv.Add("double-equal", valueEq)

  funcEnv.Add("or", logicalOr)
  funcEnv.Add("and", logicalAnd)
  funcEnv.Add("not", logicalNot)

  funcEnv.Add("integer-greater-than-or-equal", integerGreaterThanOrEqual)
  funcEnv.Add("integer-greater-than", integerGreaterThan)
  funcEnv.Add("integer-less-than-or-equal", integerLessThanOrEqual)
  funcEnv.Add("integer-less-than", integerLessThan)
  
  funcEnv.Add("double-greater-than-or-equal", doubleGreaterThanOrEqual)
  funcEnv.Add("double-greater-than", doubleGreaterThan)
  funcEnv.Add("double-less-than-or-equal", doubleLessThanOrEqual)
  funcEnv.Add("double-less-than", doubleLessThan)

  funcEnv.Add("integer-add", integerAdd)
  funcEnv.Add("integer-subtract", integerSubtract)

  funcEnv.Add("double-add", doubleAdd)
  funcEnv.Add("double-subtract", doubleSubtract)

  funcEnv.Add("integer-one-and-only", bagOneAndOnly)
  funcEnv.Add("string-one-and-only", bagOneAndOnly)
  funcEnv.Add("double-one-and-only", bagOneAndOnly)

