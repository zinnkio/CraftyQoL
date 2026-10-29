using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace CraftyQoL.Core
{
    internal static class PreferenceFiles
    {
        internal const int MaxBytes = 1024 * 1024;
        internal static T Read<T>(string path)
        {
            if (new FileInfo(path).Length > MaxBytes) throw new InvalidDataException("Preference file exceeds 1 MiB.");
            using (FileStream stream = File.OpenRead(path))
                return (T)(new DataContractJsonSerializer(typeof(T)).ReadObject(stream)
                           ?? throw new InvalidDataException("Empty preference document."));
        }
        internal static void Write<T>(string path, T document, bool preserveCorruptMain)
        {
            string directory = Path.GetDirectoryName(path) ?? throw new ArgumentException("Preference directory required.");
            Directory.CreateDirectory(directory);
            string temporary = path + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(T)).WriteObject(stream, document);
                    if (stream.Length > MaxBytes) throw new InvalidDataException("Preference file exceeds 1 MiB.");
                    stream.Flush(true);
                }
                if (File.Exists(path))
                {
                    if (preserveCorruptMain) File.Copy(path, path + ".corrupt." + DateTime.UtcNow.Ticks);
                    File.Replace(temporary, path, preserveCorruptMain ? null : path + ".bak");
                }
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    [DataContract]
    internal sealed class FavouriteDocument
    {
        [DataMember(Name = "version", IsRequired = true)] public int Version = 1;
        [DataMember(Name = "recipes", IsRequired = true)] public string[] Recipes = new string[0];
    }

    internal sealed class FavouriteStore
    {
        private readonly string _path;
        private readonly Action<string> _notice;
        private bool _readOnly, _recovered;
        private string? _lastNotice;
        internal readonly HashSet<string> Keys = new HashSet<string>(StringComparer.Ordinal);
        internal bool Dirty { get; private set; }
        internal bool SaveFailed { get; private set; }
        internal FavouriteStore(string path, Action<string> notice) { _path = Path.GetFullPath(path); _notice = notice; Load(); }
        private void Warn(string text)
        { SaveFailed = true; if (_lastNotice != text) { _lastNotice = text; _notice(text); } }
        private void Apply(FavouriteDocument document)
        {
            if (document.Recipes == null || document.Recipes.Length > 4096)
                throw new InvalidDataException("Invalid favourites count.");
            foreach (string id in document.Recipes)
                if (!string.IsNullOrWhiteSpace(id) && id.Length <= 1024) Keys.Add(id);
        }
        private void Load()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    if (File.Exists(_path + ".bak"))
                    {
                        FavouriteDocument backup = PreferenceFiles.Read<FavouriteDocument>(_path + ".bak");
                        if (backup.Version != 1) throw new InvalidDataException("Unsupported backup schema.");
                        Apply(backup); _recovered = true;
                    }
                    return;
                }
                FavouriteDocument main = PreferenceFiles.Read<FavouriteDocument>(_path);
                if (main.Version != 1)
                { _readOnly = true; Warn("Newer/unsupported favourites format; original file will not be overwritten."); return; }
                Apply(main);
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is SerializationException || error is ArgumentException)
            {
                Keys.Clear();
                try
                {
                    FavouriteDocument backup = PreferenceFiles.Read<FavouriteDocument>(_path + ".bak");
                    if (backup.Version != 1) throw new InvalidDataException("Unsupported backup schema.");
                    Apply(backup); _recovered = true;
                    _notice("Favourites recovered from backup; damaged main file will be preserved on next save.");
                }
                catch
                { Keys.Clear(); _readOnly = true; Warn("Favourites could not be read; changes remain in memory and original files are preserved."); }
            }
        }
        internal bool Migrate(IReadOnlyList<RecipeMove> approved)
        {
            if(_readOnly)return false;
            bool changed=false;
            foreach(var move in approved)if(Keys.Remove(move.OldKey)){Keys.Add(move.NewKey);changed=true;}
            if(changed)Dirty=true;
            return changed;
        }
        internal bool Toggle(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 1024 || (!Keys.Contains(id) && Keys.Count >= 4096))
            { Warn("Favourite identity/count exceeds supported bounds."); return false; }
            if (!Keys.Remove(id)) Keys.Add(id);
            Dirty = true;
            return true;
        }
        internal bool Save()
        {
            if (!Dirty) return !SaveFailed;
            Dirty = false; // Retry only after a new user change; never retry I/O every frame.
            if (_readOnly) { Warn("Favourites are memory-only until the preserved file is repaired or upgraded."); return false; }
            try
            {
                var ids = new string[Keys.Count]; Keys.CopyTo(ids); Array.Sort(ids, StringComparer.Ordinal);
                PreferenceFiles.Write(_path, new FavouriteDocument { Recipes = ids }, _recovered);
                _recovered = false; SaveFailed = false; _lastNotice = null;
                return true;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is SerializationException || error is ArgumentException || error is NotSupportedException)
            { Warn("Favourites could not be saved; changes remain in memory. " + error.Message); return false; }
        }
    }

    [DataContract]
    internal sealed class BiomeOverrideDocument
    {
        [DataMember(Name = "version", IsRequired = true)] public int Version = 1;
        [DataMember(Name = "recipes", IsRequired = true)] public BiomeOverrideEntry[] Recipes = new BiomeOverrideEntry[0];
    }
    [DataContract]
    internal sealed class BiomeOverrideEntry
    {
        [DataMember(Name = "key", IsRequired = true)] public string Key = "";
        [DataMember(Name = "biomes", IsRequired = true)] public string[] Biomes = new string[0];
    }
    internal static class BiomeOverrides
    {
        internal static Dictionary<string, BiomeAssociation> Load(string path, Action<string> notice)
        {
            var result = new Dictionary<string, BiomeAssociation>(StringComparer.Ordinal);
            if (!File.Exists(path)) return result;
            try
            {
                BiomeOverrideDocument document = PreferenceFiles.Read<BiomeOverrideDocument>(path);
                if (document.Version != 1 || document.Recipes == null || document.Recipes.Length > 4096)
                    throw new InvalidDataException("Unsupported biome override schema/count.");
                foreach (BiomeOverrideEntry entry in document.Recipes)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.Key.Length > 1024 || entry.Biomes == null || entry.Biomes.Length > 9)
                        throw new InvalidDataException("Invalid biome override entry.");
                    int mask = 0;
                    foreach (string name in entry.Biomes)
                    {
                        if (!Enum.TryParse(name, true, out BiomeChoice biome) || !Enum.IsDefined(typeof(BiomeChoice), biome) || biome == BiomeChoice.All)
                            throw new InvalidDataException("Unrecognised biome override value.");
                        mask |= BiomeAssociation.Bit(biome);
                    }
                    result[entry.Key] = new BiomeAssociation(mask, BiomeSource.Override);
                }
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is SerializationException || error is ArgumentException)
            { result.Clear(); notice("Biome overrides ignored; file was preserved. " + error.Message); }
            return result;
        }
    }
}
