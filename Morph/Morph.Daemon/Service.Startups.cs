using System.Collections.Generic;
using System.Net;
using Morph.Base;
using Morph.Internet;
using Morph.Params;

namespace Morph.Daemon
{
    /*
    public class RegisteredStartup : AwareObject<string>
    {
      internal RegisteredStartup(StartupImpl Owner, string ServiceName, string FileName, string Parameters, TimeSpan Timeout)
        : base(Owner, ServiceName)
      {
        _ServiceName = ServiceName;
        _FileName = FileName;
        _Parameters = Parameters;
        _Timeout = Timeout;
      }

      private string _ServiceName;
      public string ServiceName
      {
        get { return _ServiceName; }
      }

      private string _FileName;
      public string FileName
      {
        get { return _FileName; }
      }

      private string _Parameters;
      public string Parameters
      {
        get { return _Parameters; }
      }

      private TimeSpan _Timeout;
      public TimeSpan Timeout
      {
        get { return _Timeout; }
      }

      #region Internal

      private bool _IsRunning = false;
      private List<EventWaitHandle> _Waits = new List<EventWaitHandle>();
      private void ApplicationStart()
      { //  Copied from http://stackoverflow.com/questions/206323/how-to-execute-command-line-in-c-get-std-out-results
        //Create process
        Process pProcess = new Process();
        //path and file name of command to run
        pProcess.StartInfo.FileName = _FileName;
        //parameters to pass to program
        pProcess.StartInfo.Arguments = _Parameters;
        //pProcess.StartInfo.UseShellExecute = true;
        //Set output of program to be written to process output stream
        //pProcess.StartInfo.RedirectStandardOutput = false;
        //Optional
        //pProcess.StartInfo.WorkingDirectory = strWorkingDirectory;
        //Start the process
        pProcess.Start();
        //Get program output
        //string strOutput = pProcess.StandardOutput.ReadToEnd();
        //Wait for process to finish
        //pProcess.WaitForExit();
      }

      internal void ApplicationStarted(RegisteredService MorphService)
      {
        lock (_Waits)
          for (int i = 0; i < _Waits.Count; i++)
            _Waits[i].Set();
      }

      internal void ApplicationStopped()
      {
        _IsRunning = false;
      }

      #endregion

      //  This method has its design flaws, but it is thread safe enough for non-critical use.
      public RegisteredService Startup()
      {
        bool IsStartupThread = false;
        RegisteredService MorphService = null;
        EventWaitHandle Wait = new AutoResetEvent(false);
        lock (_Waits)
          _Waits.Add(Wait);
        try
        {
          //  Determine if this thread should start the service
          lock (this)
          {
            IsStartupThread = !_IsRunning;
            _IsRunning = true;
          }
          try
          {
            //  Start up
            if (IsStartupThread)
              ApplicationStart();
            //  Wait for it
            if (Wait.WaitOne(Timeout))
              //  MorphService started
              MorphService = ServicesImpl.Find(ServiceName);
            else
              //  Waited for too long
              MorphService = null;
          } //  Tidy up
          finally
          {
            if (IsStartupThread)
              lock (this)
                _IsRunning = MorphService != null;
          }
        }
        finally
        {
          lock (_Waits)
            _Waits.Remove(Wait);
        }
        //  Final result
        if (MorphService == null)
          throw new EMorphDaemon("Failed to obtain service \"" + ServiceName + '\"');
        return MorphService;
      }
    }
    */
    public class StartupImpl : IMorphParameters
    {
        #region Internal

        static internal ServiceCallbacks s_serviceCallbacks = new ServiceCallbacks();

        private void VerifyAccess(LinkMessage message)
        {
            if (message is LinkMessageFromIP)
            {
                LinkMessageFromIP messageIP = (LinkMessageFromIP)message;
                if (!Connections.IsEndPointOnThisDevice((IPEndPoint)messageIP.Connection.RemoteEndPoint))
                    throw new EMorphDaemon("Unauthorised access");
            }
        }

        #endregion

        public void Add(LinkMessage message, string serviceName, string fileName, string parameters, int timeout)
        {
            VerifyAccess(message);
            //  Add startup, then persist the daemon's startup list
            RegisteredServices.ObtainByName(serviceName).Startup = new RegisteredStartup(fileName, parameters, timeout);
            StartupStore.Save();
            //  Fire event
            StartupImpl.s_serviceCallbacks.DoCallbackAdded(serviceName);
        }

        public void Remove(LinkMessage message, string serviceName)
        {
            VerifyAccess(message);
            //  Remove startup, then persist the daemon's startup list
            RegisteredServices.ObtainByName(serviceName).Startup = null;
            StartupStore.Save();
            //  Fire event
            StartupImpl.s_serviceCallbacks.DoCallbackRemoved(serviceName);
        }

        public DaemonStartup[] ListServices(LinkMessage message)
        {
            //  List services
            RegisteredService[] allServices = RegisteredServices.ListAll();
            //  Filter in running services
            List<DaemonStartup> result = new List<DaemonStartup>();
            for (int i = 0; i < allServices.Length; i++)
            {
                RegisteredService service = allServices[i];
                lock (service)
                    if (service.Startup != null)
                    {
                        DaemonStartup runningService = new DaemonStartup();
                        runningService.serviceName = service.Name;
                        runningService.fileName = service.Startup.FileName;
                        runningService.parameters = service.Startup.Parameters ?? "";
                        runningService.timeout = (int)service.Startup.Timeout.TotalSeconds;
                        result.Add(runningService);
                    }
            }
            return result.ToArray();
        }

        public void Listen(LinkMessage message, ServiceCallback callback)
        {
            s_serviceCallbacks.Listen(callback);
        }

        public void Unlisten(LinkMessage message, ServiceCallback callback)
        {
            s_serviceCallbacks.Removed(callback);
        }
    }

    public struct DaemonStartup
    {
        public string serviceName;
        public string fileName;
        public string parameters;
        public int timeout;
    }
}