using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SWManager.Model.ServiceControl;

namespace SWManager.Model
{
    public class WorkerReport
    {
        public string LogMessage;
        public string LogWarning;
        public string LogError;
        public ServiceState ServiceState;
        public bool SetServiceState;
    }
}
