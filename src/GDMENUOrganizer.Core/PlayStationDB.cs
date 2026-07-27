using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GDMENUOrganizer.Core
{
    //uses data from DuckStation
    //https://github.com/stenzek/duckstation/blob/master/data/resources/gamedb.json

    public static class PlayStationDB
    {
        private static readonly List<PSDBEntry> _list = new List<PSDBEntry>();
        private static readonly object _loadLock = new object();
        private static bool _loaded;

        /// <summary>
        /// Loads gamedb.json on first use (or when called explicitly for warm-up).
        /// Safe to call from a background thread.
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_loaded)
                return;

            lock (_loadLock)
            {
                if (_loaded)
                    return;

                var path = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    Constants.PS1GameDBFile
                );
                LoadFrom(path);
                _loaded = true;
            }
        }

        public static void LoadFrom(string file)
        {
            if (!File.Exists(file))
                return;

            try
            {
                _list.Clear();
                using (var stream = File.OpenRead(file))
                {
                    var deserialized = JsonSerializer.Deserialize<IEnumerable<PSDBEntry>>(stream);
                    if (deserialized != null)
                        _list.AddRange(deserialized);
                }
            }
            catch
            {
            }
        }

        public static void SaveTo(string file)
        {
            EnsureLoaded();
            var opt = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

            using (var stream = File.Create(file))
            {
                JsonSerializer.Serialize(stream, _list, opt);
            }
        }

        public static PSDBEntry FindBySerial(string serial)
        {
            EnsureLoaded();
            return _list.FirstOrDefault(x =>
                x.serial.Equals(serial, StringComparison.InvariantCultureIgnoreCase)
            );
        }
    }

    public class PSDBEntry
    {
        public string serial { get; set; }
        public string name { get; set; }
        //public List<string> codes { get; set; }
        public string releaseDate { get; set; }
    }
}
