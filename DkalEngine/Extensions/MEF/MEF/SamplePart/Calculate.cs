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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using DkalLib;

namespace SamplePart
{
    [Export(typeof(IDkalMessage))]
    [ExportMetadata("DkalType", "CalculateDisplay")]
    public class Calculate : IDkalMessage
    {
        #region IDkalMessage Members

        public void InvokeMessage()
        {
            Console.WriteLine("SamplePart - calculate implementation called");
        }

        #endregion
    }
}
