using ElmerBot.Models;
using ElmerBot.Repositories;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ElmerBot.Classes
{
    internal class StickyVault(string jsonFolderLocation, ILogging_Repository logger)
    {
        public delegate void Modify_GluedMessage(ref GluedMessage value);

        ConcurrentDictionary<string, GluedMessage> Stickys => _stickys ??= Load();
        ConcurrentDictionary<string, GluedMessage>? _stickys;
        System.Timers.Timer? saveTimer;

        bool isSaving = false,
            initialLoad = true,
            saveNeeded = false;
        DateTime? lastUpdate = null;

        internal ICollection<string> Keys => Stickys.Keys;

        public async Task<(bool success, GluedMessage? sticky)> TryGetValue(string key)
        {
            await WaitForUnlocked();
            bool success = Stickys.TryGetValue(key, out var sticky);
            return (success, sticky);
        }

        public async Task<bool> TryAdd(string key, GluedMessage message)
        {
            await WaitForUnlocked();
            return saveNeeded = Stickys.TryAdd(key, message);
        }

        public async Task<bool> TryUpdate(string key, Modify_GluedMessage Modify)
        {
            await WaitForUnlocked();
            if (await TryGetValue(key) is (true, GluedMessage sticky))
            {
                var updated = new GluedMessage(sticky);
                Modify(ref updated);
                return saveNeeded = Stickys.TryUpdate(key, updated, sticky);
            }

            return false;
        }

        public async Task<(bool success, GluedMessage? sticky)> Remove(string key)
        {
            await WaitForUnlocked();
            saveNeeded = Stickys.Remove(key, out var sticky);
            return(saveNeeded, sticky);
        }

        async Task WaitForUnlocked()
        {
            while (isSaving) await Task.Delay(100);
        }

        ConcurrentDictionary<string, GluedMessage> Load()
        {
            if (saveTimer is null)
            {
                saveTimer = new System.Timers.Timer(5000)
                {
                    AutoReset = true
                };
                saveTimer.Elapsed += async (s, e) =>
                {
                    if (saveNeeded)
                        await Save();
                };
                saveTimer.Start();
            }

            if (Directory.Exists(jsonFolderLocation))
                if (File.Exists(jsonFolderLocation + "list.json"))
                    if (JsonSerializer.Deserialize<List<GluedMessage>>(File.ReadAllText(jsonFolderLocation + "list.json")) is List<GluedMessage> m)
                    {
                        if (initialLoad)
                            m.ForEach(m => m.isWatching = false);

                        initialLoad = false;

                        return new ConcurrentDictionary<string, GluedMessage>(m.ToDictionary(k => $"{k.Server_ID}_{k.Channel_ID}", v => v));
                    }
            return [];
        }

        public async Task Save()
        {
            isSaving = true;

            try
            {
                if (!Directory.Exists(jsonFolderLocation))
                    Directory.CreateDirectory(jsonFolderLocation);
                await File.WriteAllTextAsync(jsonFolderLocation + "list.json", JsonSerializer.Serialize(Stickys.Select(m => m.Value).ToList()));

                if ((DateTime.Now - (lastUpdate ?? DateTime.Now)).TotalMinutes > 2)
                {
                    _stickys = null!;
                    lastUpdate = null;
                    await logger.LogBasic("Memory Release", "Released stickys from memory, as it has been over 2 minutes since the last save.");
                }
                else
                {
                    lastUpdate = DateTime.Now;
                }
            }
            catch (IOException) { }
            catch (Exception ex)
            {
                await logger.LogError($"Error saving stickys", Exception: ex);
            }

            isSaving = false;
        }
    }
}
