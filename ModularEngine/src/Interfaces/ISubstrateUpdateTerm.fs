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

namespace Microsoft.Research.Dkal.Interfaces

/// ISubstrateUpdateTerm implementations are substrate terms that perform
/// updates (addition, modification, removal, changing parameters etc.) 
/// on a substrate. They are used in "apply" acctions
type ISubstrateUpdateTerm =
  inherit ISubstrateTerm
  