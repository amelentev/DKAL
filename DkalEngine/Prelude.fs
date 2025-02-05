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

namespace Microsoft.Research.DkalEngine


(*
Syntax
~~~~~~

"function" [priority] ...rule... "returns" {type}
   rule is sequence of verbatims or types enclosed in {...}
   the type does not affect parsing, only type checking
   lower priority rules are run first
   odd priority means scanning for rule matches right to left, even: left to right

"attribute" [priority] ...rule...
  same as "function [priority] ...rule... returns {infon}"

"type" name
  declare type

*)

namespace Microsoft.Research.DkalEngine

module Prelude =
  let text = @"
type principal
type int
type bool
type float
type text
type infon
type assertion

// table column access
function [2] {anytype} . {anytype} returns {anytype}
function [3] {int} ^ {int} returns {int}
function [4] {int} * {int} returns {int}
function [4] {int} / {int} returns {int}
function [4] {int} % {int} returns {int}
function [6] {int} + {int} returns {int}
function [6] {int} - {int} returns {int}
function [8] {int} <= {int} returns {bool}
function [8] {int} >= {int} returns {bool}
function [8] {int} < {int} returns {bool}
function [8] {int} > {int} returns {bool}
function [8] {anytype} == {anytype} returns {bool}
function [8] {anytype} != {anytype} returns {bool}
function [10] {bool} && {bool} returns {bool}
function [10] {bool} || {bool} returns {bool}
function [9] not ( {bool} ) returns {bool}
attribute [2] asInfon ( {bool} )
function [2] true returns {bool}
function [2] false returns {bool}
function [2] int_null returns {int}
function [2] string_null returns {text}
function [2] float_null returns {float}
function [2] bool_null returns {bool}

function [2] __tuple ({int}) returns {int}


attribute [21] {principal} tdon {infon}
attribute [21] {principal} said {infon}

function [knows 32] {principal} knows {block assertion} returns {assertion}
function [send 30] if {principal} knows {block assertion} then they send {block assertion} returns {assertion}
function [send_target 30] to {principal} {block assertion} returns {assertion}
function [send_target_cert 30] to {principal} with certificate {block assertion} returns {assertion}
function [==> 30] if {block assertion} then {block assertion} returns {assertion}

function [send 30] if {principal} knows {block assertion} then {block assertion} returns {assertion}
function [send_target_cert 28] send with justification to {principal} {block assertion} returns {assertion}
function [say_target_cert 28] say with justification to {principal} {block assertion} returns {assertion}

attribute [10] {infon} --> {infon}

"
