using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Bat.Library.Logging;

namespace Morph.Daemon
{
    /// <summary>
    /// The daemon owns and persists the list of startup services.  In a Debug build the file sits
    /// next to the daemon exe (convenient during development);  in a Release build it lives under
    /// %ProgramData%\Morph\ - a machine-wide location an installed Windows service can always write.
    /// </summary>
    static public class StartupStore
    {
        #region Persisted shape

        private class Entry
        {
            public string ServiceName { get; set; }
            public string FileName { get; set; }
            public string Parameters { get; set; }
            public int Timeout { get; set; }
        }

        private class Document
        {
            public List<Entry> Startups { get; set; }
        }

        #endregion

        #region File location

        public const string FileName = "Morph.Daemon.json";
        static private readonly string s_filePath = BuildFilePath();

        static private string BuildFilePath()
        {
#if DEBUG
            //  Development:  keep the file beside the daemon exe.
            string directory = AppContext.BaseDirectory;
#else
            //  Deployed:  a machine-wide location that an installed service can always write to.
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Morph");
#endif
            return Path.Combine(directory, FileName);
        }

        static private readonly JsonSerializerOptions s_jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };

        #endregion

        #region Load and save

        /// <summary>Registers every persisted startup with the daemon, so the services launch on demand again.</summary>
        static public void LoadInto()
        {
            foreach (Entry entry in Load())
                RegisteredServices.ObtainByName(entry.ServiceName).Startup =
                    new RegisteredStartup(entry.FileName, entry.Parameters, entry.Timeout);
        }

        /// <summary>Rewrites the store from the daemon's current set of registered startups.</summary>
        static public void Save()
        {
            List<Entry> entries = new List<Entry>();
            foreach (RegisteredService service in RegisteredServices.ListAll())
                lock (service)
                    if (service.Startup != null)
                        entries.Add(new Entry
                        {
                            ServiceName = service.Name,
                            FileName = service.Startup.FileName,
                            Parameters = service.Startup.Parameters,
                            Timeout = (int)service.Startup.Timeout.TotalSeconds,
                        });
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(s_filePath));
                File.WriteAllText(s_filePath, JsonSerializer.Serialize(new Document { Startups = entries }, s_jsonOptions));
            }
            catch (Exception x)
            {
                //  Persistence is best-effort:  a write failure must not break a live Add/Remove.
                Log.Default.Add(x);
            }
        }

        static private List<Entry> Load()
        {
            try
            {
                if (File.Exists(s_filePath))
                {
                    Document document = JsonSerializer.Deserialize<Document>(File.ReadAllText(s_filePath), s_jsonOptions);
                    if (document?.Startups != null)
                        return document.Startups;
                }
            }
            catch (Exception x)
            {
                //  A missing or corrupt store must not stop the daemon;  it is rewritten on the next change.
                Log.Default.Add(x);
            }
            return new List<Entry>();
        }

        #endregion
    }
}
