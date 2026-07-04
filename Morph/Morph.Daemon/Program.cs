using System;
using System.ServiceProcess;
using Bat.Library.Logging;

namespace Morph.Daemon
{
    class Program
    {
        static void Main(string[] args)
        {
            Log.Default.Types.Add(new LogTypeException());
            Log.Default.Add("");
            Log.Default.Add("Starting instance: " + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString());
            try
            {
                //  Run from a console for development;  run under the SCM as a Windows service
                if (Environment.UserInteractive)
                    new MorphDaemonService().RunConsole(args);
                else
                    ServiceBase.Run(new MorphDaemonService());
            }
            finally
            {
                Log.Default.Add("Stopping instance: " + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString());
            }
        }
    }
}