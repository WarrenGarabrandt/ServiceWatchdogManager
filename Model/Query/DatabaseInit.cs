using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWManager.Model.Query
{
    public class DatabaseInit : DatabaseQuery
    {
        public WorkerReport ValueResult { get; set; }

        public DatabaseInit(string programName, string dbPath = null)
        {
            ProgramName = programName;
            DBPath = dbPath;
            DoneSignal = new System.Threading.ManualResetEventSlim();
            Aborted = false;
        }

        public void SetResult(WorkerReport value)
        {
            ValueResult = value;
            DoneSignal.Set();
        }

        public WorkerReport GetResult()
        {
            DoneSignal.Wait();
            DoneSignal.Dispose();
            if (Aborted)
            {
                throw new OperationCanceledException();
            }
            return ValueResult;
        }
        public string ProgramName { get; set; }

        public string DBPath { get; set; }


    }
}
