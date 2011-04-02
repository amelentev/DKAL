﻿namespace Microsoft.Research.Dkal.SimpleParser.SimpleAst

  open System.Collections.Generic

  type SimpleFunction = string
  type SimpleVariable = string
  type SimpleType = string

  type SimpleArg = string * SimpleType

  type SimpleConstant = 
  | BoolSimpleConstant of bool
  | IntSimpleConstant of int
  | FloatSimpleConstant of float
  | StringSimpleConstant of string

  type SimpleMetaTerm = 
  | SimpleApp of SimpleFunction * SimpleMetaTerm list
  | SimpleConst of SimpleConstant
  | SimpleVar of SimpleVariable

  type SimpleTypeDeclaration = 
    { newTyp: SimpleType;
      targetTyp: SimpleType }

  type SimpleTableDeclaration = 
    { Name: string;
      Cols: SimpleArg list }

  type SimpleRelationDeclaration = 
    { Name: string;
      ArgsTyp: SimpleType list }

  type SimpleFunctionDeclaration = 
    { Name: string;
      RetTyp: SimpleType;
      Args: SimpleArg list; 
      Body: SimpleMetaTerm option }

  type SimpleAssertion = 
  | SimpleKnowledge of SimpleArg list * SimpleMetaTerm

  type SimplePolicy() =
    let typeDeclarations = new List<SimpleTypeDeclaration>()
    let tableDeclarations = new List<SimpleTableDeclaration>()
    let relationDeclarations = new List<SimpleRelationDeclaration>()
    let functionDeclarations = new List<SimpleFunctionDeclaration>()
    let assertions = new List<SimpleAssertion>()
    let infons = new List<SimpleMetaTerm>()
    
    member sp.TypeDeclarations = typeDeclarations
    member sp.TableDeclarations = tableDeclarations
    member sp.RelationDeclarations = relationDeclarations
    member sp.FunctionDeclarations = functionDeclarations
    member sp.Assertions = assertions
    member sp.Infons = infons
