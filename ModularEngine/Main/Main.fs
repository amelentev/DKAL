﻿namespace Microsoft.Research.Dkal.SimpleRouter

open System.IO

open Microsoft.Research.Dkal.Factories
open Microsoft.Research.Dkal.Ast
open Microsoft.Research.Dkal.Factories.Initializer

module Main =

  let args = System.Environment.GetCommandLineArgs() |> Seq.toList
  try  
    match args with
    | [_; routingFile; policyFile; kind] ->

      if not (File.Exists (routingFile)) then
        printfn "File not found: %O" routingFile
      elif not (File.Exists (policyFile)) then
        printfn "File not found: %O" policyFile
      else
        // Populate factories
        FactoriesInitializer.Init()

        let router = RouterFactory.Router kind routingFile
        let parser, printer = ParserFactory.InfonParser(kind, router.Me), PrettyPrinterFactory.InfonPrinter kind
        let engine = LogicEngineFactory.Engine kind
        let executor = ExecutorFactory.Executor kind router engine

        let assembly = parser.ParseAssembly (File.ReadAllText policyFile)
        printfn "Principal %O running..." router.Me
        printfn "------------------------------------------------------------------------"
        printfn "%O" <| printer.PrintPolicy assembly.Policy
        printfn "------------------------------------------------------------------------"
        for rule in assembly.Policy.Rules do
          executor.InstallRule rule
        executor.Start()

    | _ -> failwith "Wrong number of parameters"
  with e -> 
    printfn "%O" e





