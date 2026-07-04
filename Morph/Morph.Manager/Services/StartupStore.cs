using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Morph.Manager.Services
{
    public class StartupEntry
    {
        public string ServiceName { get; set; }
        public string FileName { get; set; }
        public string Parameters { get; set; }
        public int Timeout { get; set; }
    }

    /// <summary>
    /// The Morph Manager owns the persistent list of startup services,
    /// stored as "Morph.Manager.json" in the same directory as the Morph Manager exe.
    /// The daemon is synchronised to this list, best effort.
    /// </summary>
    public class StartupStore
    {
        private static readonly string s_filePath =
            Path.Combine(AppContext.BaseDirectory, "Morph.Manager.json");

        private static readonly JsonSerializerOptions s_jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };

        private class StoreDocument
        {
            public List<StartupEntry> Startups { get; set; }
        }

        public List<StartupEntry> Load()
        {
            try
            {
                if (File.Exists(s_filePath))
                {
                    StoreDocument document = JsonSerializer.Deserialize<StoreDocument>(File.ReadAllText(s_filePath), s_jsonOptions);
                    if (document?.Startups != null)
                        return document.Startups;
                }
            }
            catch (JsonException)
            {
                //  A corrupt store must not stop the manager from starting;  it is rewritten on the next save
            }
            return new List<StartupEntry>();
        }

        public void Save(List<StartupEntry> startups)
        {
            StoreDocument document = new StoreDocument { Startups = startups };
            File.WriteAllText(s_filePath, JsonSerializer.Serialize(document, s_jsonOptions));
        }
    }
}
