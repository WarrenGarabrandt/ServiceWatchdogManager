﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWManager.Model.Query
{
    public class qrySetConfigValue : DatabaseQuery
    {
        public string Category { get; private set; }
        public string Setting { get; private set; }
        public string Value { get; private set; }

        public bool SuccessResult { get; private set; }

        public qrySetConfigValue(string category, string setting, string value)
        {
            Category = category;
            Setting = setting;
            Value = value;
            DoneSignal = new System.Threading.ManualResetEventSlim();
            Aborted = false;
        }

        public void SetResult(bool result)
        {
            SuccessResult = result;
            DoneSignal.Set();
        }

        public bool GetResult()
        {
            DoneSignal.Wait();
            DoneSignal.Dispose();
            if (Aborted)
            {
                throw new OperationCanceledException();
            }
            return SuccessResult;
        }

    }
}
