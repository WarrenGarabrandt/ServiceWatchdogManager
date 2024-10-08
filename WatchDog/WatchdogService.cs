﻿using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.ComponentModel;
using System.Diagnostics;
using System.ServiceProcess;
using System.Net;
using static SWManager.Model.ServiceControl;
using SWManager.Model;
using SWManager.Database;


namespace SWManager.Watchdog
{
    public class WatchdogService : ServiceBase
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(System.IntPtr handle, ref ServiceStatus serviceStatus);

        [DllImport("kernel32.dll",
            EntryPoint = "AllocConsole",
            SetLastError = true,
            CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall)]
        private static extern int AllocConsole();

        private const UInt32 StdOutputHandle = 0xFFFFFFF5;
        private const int STD_OUTPUT_HANDLE = -11;
        private const int MY_CODE_PAGE = 437;

        [DllImport("kernel32.dll",
                    EntryPoint = "GetStdHandle",
                    SetLastError = true,
                    CharSet = CharSet.Auto,
                    CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr GetStdHandle(int nStdHandle);
        [DllImport("kernel32.dll")]
        private static extern void SetStdHandle(UInt32 nStdHandle, IntPtr handle);

        // main worker for our service
        BackgroundWorker worker;

        public bool RunningInteractively = false;

        public string DBPathOverride = null;

        public bool FailEventLog = false;

        public WatchdogService()
        {
            this.ServiceName = "Service Watchdog Service";
            this.EventLog.Log = "Application";

            if (!RunningInteractively)
            {
                try
                {
                    ((ISupportInitialize)(this.EventLog)).BeginInit();
                    if (!EventLog.SourceExists(this.EventLog.Source))
                    {
                        EventLog.CreateEventSource(this.EventLog.Source, this.EventLog.Log);
                    }
                    ((ISupportInitialize)(this.EventLog)).EndInit();
                }
                catch
                {
                    FailEventLog = true;
                }

            }
            this.CanHandlePowerEvent = true;
            this.CanHandleSessionChangeEvent = true;
            this.CanShutdown = true;
            this.CanStop = true;
        }
        /// <summary>
        /// The Main Thread: This is where your Service is Run.
        /// </summary>
        static void Main()
        {
            string dbPath = null;
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                dbPath = args[1];
            }
            if (Environment.UserInteractive)
            {
                AllocConsole();
                IntPtr stdHandle = GetStdHandle(STD_OUTPUT_HANDLE);
                SafeFileHandle safeFileHandle = new SafeFileHandle(stdHandle, true);
                FileStream fileStream = new FileStream(safeFileHandle, FileAccess.Write);
                Encoding encoding = System.Text.Encoding.GetEncoding(MY_CODE_PAGE);
                StreamWriter standardOutput = new StreamWriter(fileStream, encoding);
                standardOutput.AutoFlush = true;
                Console.SetOut(standardOutput);

                Console.WriteLine("Running interactively. To install and run as a service, use installutil.exe.");
                standardOutput.Flush();
                WatchdogService svc = new WatchdogService();
                svc.RunningInteractively = true;
                svc.DBPathOverride = dbPath;
                svc.OnStart(null);
                while (true)
                {
                    Thread.Sleep(10);
                }
            }
            else
            {
                WatchdogService svc = new WatchdogService();
                svc.DBPathOverride = dbPath;
                ServiceBase.Run(svc);
            }
        }

        /// <summary>
        /// Dispose of objects that need it here.
        /// </summary>
        /// <param name="disposing">Whether
        ///    or not disposing is going on.</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <summary>
        /// OnStart(): Put startup code here
        ///  - Start threads, get inital data, etc.
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            // Update the service state to Start Pending.
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            if (RunningInteractively)
            {
                Console.Write("Service Watchdog Starting.\r\n");
                if (FailEventLog)
                {
                    Console.WriteLine("Unable to set up Event Log Source. You'll need to run the program as an administrator to fix this.");
                }
            }
            else
            {
                if (!FailEventLog)
                {
                    this.EventLog.WriteEntry("Service Watchdog Starting.");
                }
            }
            worker = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            worker.RunWorkerAsync(DBPathOverride);
            base.OnStart(args);
        }

        /// <summary>
        /// OnStop(): Put your stop code here
        /// - Stop threads, set final data, etc.
        /// </summary>
        protected override void OnStop()
        {
            // Update the service state to Stop Pending.
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOP_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
            if (RunningInteractively)
            {
                Console.Write("Service Watchdog Stopping.\r\n");
            }
            else
            {
                if (!FailEventLog)
                {
                    this.EventLog.WriteEntry("Service Watchdog Stopping.");
                }
            }
            worker.CancelAsync();
        }

        ///// <summary>
        ///// OnPause: Put your pause code here
        ///// - Pause working threads, etc.
        ///// </summary>
        //protected override void OnPause()
        //{
        //    this.EventLog.WriteEntry("Service Watchdog Pausing.");
        //    base.OnPause();
        //}

        ///// <summary>
        ///// OnContinue(): Put your continue code here
        ///// - Un-pause working threads, etc.
        ///// </summary>
        //protected override void OnContinue()
        //{
        //    base.OnContinue();
        //}

        /// <summary>
        /// OnShutdown(): Called when the System is shutting down
        /// - Put code here when you need special handling
        ///   of code that deals with a system shutdown, such
        ///   as saving special data before shutdown.
        /// </summary>
        protected override void OnShutdown()
        {
            if (RunningInteractively)
            {
                Console.Write("Service Watchdog System Shutdown.\r\n");
            }
            else
            {
                if (!FailEventLog)
                {
                    this.EventLog.WriteEntry("Service Watchdog System Shutdown.");
                }
            }
            OnStop();
            base.OnShutdown();
        }

        /// <summary>
        /// OnCustomCommand(): If you need to send a command to your
        ///   service without the need for Remoting or Sockets, use
        ///   this method to do custom methods.
        /// </summary>
        /// <param name="command">Arbitrary Integer between 128 & 256</param>
        protected override void OnCustomCommand(int command)
        {
            //  A custom command can be sent to a service by using this method:
            //#  int command = 128; //Some Arbitrary number between 128 & 256
            //#  ServiceController sc = new ServiceController("NameOfService");
            //#  sc.ExecuteCommand(command);

            base.OnCustomCommand(command);
        }

        /// <summary>
        /// OnPowerEvent(): Useful for detecting power status changes,
        ///   such as going into Suspend mode or Low Battery for laptops.
        /// </summary>
        /// <param name="powerStatus">The Power Broadcast Status
        /// (BatteryLow, Suspend, etc.)</param>
        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            return base.OnPowerEvent(powerStatus);
        }

        /// <summary>
        /// OnSessionChange(): To handle a change event
        ///   from a Terminal Server session.
        ///   Useful if you need to determine
        ///   when a user logs in remotely or logs off,
        ///   or when someone logs into the console.
        /// </summary>
        /// <param name="changeDescription">The Session Change
        /// Event that occured.</param>
        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            base.OnSessionChange(changeDescription);
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            string dbPath = null;
            if (e.Argument is string)
            {
                dbPath = e.Argument as string;
            }
            try
            {
                // initialize the database
                try
                {
                    WorkerReport InitReport = SQLiteDB.InitDatabase("ServiceWatchdog", dbPath);
                    if (InitReport != null)
                    {
                        worker.ReportProgress(0, InitReport);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    worker.ReportProgress(0, new WorkerReport()
                    {
                        LogError = string.Format("Failed to start. {0}", ex.Message)
                    });
                    return;
                }

                //// get the list of EndPoints that we will be listening on.
                //List<tblIPEndpoint> endpoints = SQLiteDB.IPEndpoint_GetAll();
                //List<SMTPListener> smtpListeneners = new List<SMTPListener>();
                //// Start SMTP listeners
                //foreach (var ep in endpoints)
                //{
                //    if (ep.Protocol != tblIPEndpoint.IPEndpointProtocols.None && ep.IPEndPoint != null)
                //    {
                //        smtpListeneners.Add(new SMTPListener(ep));
                //    }
                //}
                //// Start SMTP queue
                //SMTPSendQueue smtpQueue = new SMTPSendQueue();

                System.Threading.Thread.Sleep(1000);

                // Test email sending
                //EmailSendBenchmark();

                //while (!worker.CancellationPending)
                //{
                //    WorkerReport status;
                //    foreach (var smtpListener in smtpListeneners)
                //    {
                //        if (smtpListener.WorkerReports.TryDequeue(out status))
                //        {
                //            worker.ReportProgress(0, status);
                //        }
                //    }
                //    if (smtpQueue.WorkerReports.TryDequeue(out status))
                //    {
                //        worker.ReportProgress(0, status);
                //    }
                //    Thread.Sleep(10);
                //}

                //if (worker.CancellationPending)
                //{
                //    worker.ReportProgress(0, new WorkerReport()
                //    {
                //        LogMessage = "Shutting down Listeners and Senders."
                //    });
                //    foreach (var smtpListener in smtpListeneners)
                //    {
                //        smtpListener.Cancel();
                //    }
                //    smtpQueue.Cancel();
                //    bool stillRunning = true;
                //    while (stillRunning || smtpQueue.Running)
                //    {
                //        stillRunning = false;
                //        foreach (var smtpListener in smtpListeneners)
                //        {
                //            stillRunning |= smtpListener.Running;
                //        }
                //        Thread.Sleep(100);
                //    }
                //}
            }
            catch (Exception ex)
            {
                worker.ReportProgress(0, new WorkerReport()
                {
                    LogError = string.Format("Exception: {0}", ex.Message)
                });
            }
            finally
            {
                worker.ReportProgress(0, new WorkerReport()
                {
                    LogMessage = "Cleanup complete."
                });
            }
        }

        private void EmailSendBenchmark()
        {
            //System.Diagnostics.Stopwatch sw = new Stopwatch();
            //string testUserEmail = string.Format("testuser{0}@{1}.test.local", SQLiteDB.GenerateNonce(24), SQLiteDB.GenerateNonce(16));
            //tblUser testUser = new tblUser("Test User", testUserEmail, null, null, true, false, false, null);
            //string testuserPass = SQLiteDB.GenerateNonce(24);
            //SQLiteDB.GeneratePasswordHash(testUser, testuserPass);
            //SQLiteDB.User_AddUpdate(testUser);
            //sw.Start();
            //// send a test email.
            //int SendQuantity = 1;
            //Parallel.For(0, SendQuantity, index =>
            //{
            //    SendTestEmail(testUserEmail, testuserPass);
            //});
            //sw.Stop();
            //System.Diagnostics.Debug.WriteLine(string.Format("Benchmark email sending took {0} seconds.", sw.Elapsed.TotalSeconds));
        }

        private void SendTestEmail(string senderEmail, string senderPassword)
        {
            string Addr_to = "testrecipient@domain.com";
            string Addr_from = "testsender@domain.com";
            System.Net.Mail.MailMessage msg = new System.Net.Mail.MailMessage(Addr_from, Addr_to);
            msg.Subject = "Test Email";

            msg.Body = "Hello world!";
            string server = "127.0.0.1";
            using (System.Net.Mail.SmtpClient client = new System.Net.Mail.SmtpClient(server, 1025))
            {
                client.EnableSsl = false; // true;
                client.UseDefaultCredentials = false;    // false;
                client.Credentials = new NetworkCredential(senderEmail, senderPassword);
                client.Send(msg);
            }
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (RunningInteractively)
            {
                Console.Write("Service Watchdog Stopped.\r\n");
            }
            else
            {
                this.EventLog.WriteEntry("Service Watchdog Stopped.");
            }
            // Update the service state to Stopped.
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            base.OnStop();
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            WorkerReport rep = e.UserState as WorkerReport;
            if (rep != null)
            {
                if (rep.LogMessage != null)
                {
                    if (RunningInteractively)
                    {
                        Console.Write(string.Format("Service Watchdog {0}\r\n", rep.LogMessage));
                    }
                    else
                    {
                        this.EventLog.WriteEntry(string.Format("Service Watchdog {0}", rep.LogMessage), EventLogEntryType.Information);
                    }
                }
                if (rep.LogWarning != null)
                {
                    if (RunningInteractively)
                    {
                        Console.Write(string.Format("Service Watchdog {0}\r\n", rep.LogWarning));
                    }
                    else
                    {
                        this.EventLog.WriteEntry(string.Format("Service Watchdog {0}", rep.LogWarning), EventLogEntryType.Warning);
                    }
                }
                if (rep.LogError != null)
                {
                    if (RunningInteractively)
                    {
                        Console.Write(string.Format("Service Watchdog {0}\r\n", rep.LogError));
                    }
                    else
                    {
                        this.EventLog.WriteEntry(string.Format("Service Watchdog {0}", rep.LogError), EventLogEntryType.Error);
                    }
                }
                if (rep.SetServiceState)
                {
                    // Update the service state.
                    ServiceStatus serviceStatus = new ServiceStatus();
                    serviceStatus.dwCurrentState = rep.ServiceState;
                    serviceStatus.dwWaitHint = 100000;
                    SetServiceStatus(this.ServiceHandle, ref serviceStatus);
                }
            }
        }

    }
}
