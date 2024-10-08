﻿using SWManager.Model.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWManager.Model.Query
{
    public class qryGetAllConfigValues : DatabaseQuery
    {
        public List<tblSystem> Results;
        public qryGetAllConfigValues()
        {
            Results = null;
            DoneSignal = new System.Threading.ManualResetEventSlim();
            Aborted = false;
        }

        public void SetResult(List<tblSystem> value)
        {
            Results = value;
            DoneSignal.Set();
        }

        public List<tblSystem> GetResult()
        {
            DoneSignal.Wait();
            DoneSignal.Dispose();
            if (Aborted)
            {
                throw new OperationCanceledException();
            }
            return Results;
        }

    }
}
