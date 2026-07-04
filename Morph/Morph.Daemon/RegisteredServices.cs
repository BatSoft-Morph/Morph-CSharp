using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using Morph.Base;

namespace Morph.Daemon
{
    public abstract class RegisteredRunning
    {
        public RegisteredRunning(RegisteredService RegisteredService)
        {
            this.RegisteredService = RegisteredService;
        }

        public RegisteredService RegisteredService;

        public bool AccessLocal = true;
        public bool AccessRemote = false;

        public abstract void HandleMessage(LinkMessage message);
    }

    public class RegisteredStartup
    {
        public RegisteredStartup(string fileName, string parameters, int timeout)
        {
            FileName = fileName;
            Parameters = parameters;
            Timeout = new TimeSpan(0, 0, 0, timeout);
        }

        public string FileName;
        public string Parameters;
        public TimeSpan Timeout;

        #region Internal

        internal ManualResetEvent _startupGate = new ManualResetEvent(false);

        internal void Run()
        {
            //  Copied from http://stackoverflow.com/questions/206323/how-to-execute-command-line-in-c-get-std-out-results
            //Create process
            Process pProcess = new Process();
            //path and file name of command to run
            pProcess.StartInfo.FileName = FileName;
            //parameters to pass to program
            pProcess.StartInfo.Arguments = Parameters;
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
            //  Wait for start up to complete
            _startupGate.WaitOne(Timeout);
        }

        #endregion
    }

    public class RegisteredService : IDisposable
    {
        internal RegisteredService(string ServiceName)
        {
            _name = ServiceName;
        }

        private readonly string _name;
        public string Name
        {
            get => _name;
        }

        public bool IsRunning
        {
            get => _running != null;
        }

        private RegisteredRunning _running = null;
        public RegisteredRunning Running
        {
            get
            {
                RegisteredStartup Starter = null;
                //  Either return the service handler, or...
                lock (this)
                    if (_running == null)
                        Starter = _startup;
                    else
                        return _running;
                //  ...try to start the service
                if (Starter == null)
                    throw new EMorphDaemon("Service " + Name + " is not available.");
                Starter.Run();
                lock (this)
                    if (_running == null)
                        throw new EMorphDaemon("Service " + Name + " failed to start.");
                    else
                        return _running;
            }

            set
            {
                lock (this)
                {
                    //  Prepare for change
                    if (value == null)
                    { //  - Need to tidy up "dying" service
                        if ((_running != null) && (_running is IDisposable))
                            ((IDisposable)_running).Dispose();
                    }
                    else
                    { //  - Not allowed to replace an existing service
                        if (_running != null)
                            throw new EMorphDaemon("Service " + Name + " is already running.");
                    }
                    //  Callback: Notify listeners that the service is no longer running
                    if (_running != null)
                        ServicesImpl._ServiceCallbacks.DoCallbackRemoved(Name);
                    //  Apply the change
                    _running = value;
                    if (_running == null)
                    {
                        //  Will need to wait for startup to complete
                        if (_startup != null)
                            _startup._startupGate.Reset();
                        //  Tidy up self
                        if (_startup == null)
                            RegisteredServices.ReleaseByName(Name);
                    }
                    else
                    {
                        //  Release any threads that are waiting for startup
                        if (_startup != null)
                            _startup._startupGate.Set();
                        //  Callback: Notify listeners about the newly running service
                        ServicesImpl._ServiceCallbacks.DoCallbackAdded(Name);
                    }
                }
            }
        }

        internal RegisteredStartup _startup = null;
        public RegisteredStartup Startup
        {
            get => _startup;
            set
            {
                //  Startups are persisted by the Morph Manager (Morph.Manager.json);  the daemon only holds them in memory
                lock (this)
                {
                    _startup = value;
                    if (_startup != null)
                    {
                        //  Correct the start gate
                        if (_running == null)
                            _startup._startupGate.Reset();  //  Will need to wait for startup to complete
                        else
                            _startup._startupGate.Set();  //  Release any threads that are waiting for startup
                    }
                }
            }
        }

        #region IDisposable

        public void Dispose()
        {
            Running = null;
        }

        #endregion
    }

    static public class RegisteredServices
    {
        #region Internal

        static readonly Hashtable Services = new Hashtable();
        static readonly Hashtable Connections = new Hashtable();

        #endregion

        static public RegisteredService FindByName(string serviceName)
        {
            lock (Services)
                return (RegisteredService)Services[serviceName];
        }

        static public RegisteredService ObtainByName(string serviceName)
        {
            lock (Services)
            {
                RegisteredService service = (RegisteredService)Services[serviceName];
                if (service == null)
                {
                    service = new RegisteredService(serviceName);
                    Services.Add(serviceName, service);
                }
                return service;
            }
        }

        static public void ReleaseByName(string serviceName)
        {
            lock (Services)
                Services.Remove(serviceName);
        }

        static public RegisteredService[] ListAll()
        {
            DictionaryEntry[] entries;
            lock (Services)
            {
                entries = new DictionaryEntry[Services.Count];
                Services.CopyTo(entries, 0);
            }
            RegisteredService[] result = new RegisteredService[entries.Length];
            for (int i = 0; i < entries.Length; i++)
                result[i] = (RegisteredService)entries[i].Value;
            return result;
        }
    }
}