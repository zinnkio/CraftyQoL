using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace CraftyQoL.Core
{
    [DataContract] internal sealed class TrackerPin
    {
        [DataMember(Name = "key", IsRequired = true)] public string Key = "";
        [DataMember(Name = "signature", IsRequired = true)] public string Signature = "";
        [DataMember(Name = "station")] public string Station = "";
        [DataMember(Name = "alternative")] public string Alternative = "";
        [DataMember(Name = "quality")] public int Quality = 1;
        [DataMember(Name = "expanded")] public bool Expanded;
    }
    [DataContract] internal sealed class TrackerDocument
    {
        [DataMember(Name = "version", IsRequired = true)] public int Version = 1;
        [DataMember(Name = "pins", IsRequired = true)] public List<TrackerPin> Pins = new List<TrackerPin>();
        [DataMember(Name = "x")] public float X = 0.02f;
        [DataMember(Name = "y")] public float Y = 0.22f;
        [DataMember(Name = "minimised")] public bool Minimised;
        [DataMember(Name = "hideCompleted")] public bool HideCompleted;
        [DataMember(Name = "recipesTab")] public bool RecipesTab;
        [DataMember(Name = "keepVisible")] public bool KeepVisible;
    }
    internal sealed class TrackerNeed
    {
        internal readonly string Id, Name;
        internal readonly int Amount, Quality;
        internal TrackerNeed(string id, string name, int amount, int quality = -1)
        { Id = id; Name = name; Amount = amount; Quality = quality; }
    }
    internal sealed class TrackerPlan
    {
        internal readonly string Key, Name;
        internal readonly int OutputAmount;
        internal readonly TrackerNeed[] Needs;
        internal TrackerPlan(string key, string name, int outputAmount, TrackerNeed[] needs)
        { Key = key; Name = name; OutputAmount = outputAmount; Needs = needs; }
    }
    internal sealed class TrackerMaterial
    {
        internal readonly string Id, Name;
        internal readonly long Owned, Required, Deficit;
        internal bool Complete => Deficit == 0;
        internal TrackerMaterial(string id, string name, long owned, long required, long deficit)
        { Id = id; Name = name; Owned = owned; Required = required; Deficit = deficit; }
    }
    internal static class TrackerMath
    {
        internal const int MaxPins = 64, MaxNeeds = 128, MaxAmount = 1000000;
        internal static float Clamp(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Math.Max(0, Math.Min(1, value));
        internal static string Scope(long character, long world) => character.ToString("X16") + "-" + world.ToString("X16") + ".json";
        internal static string Signature(string structure)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(structure))).Replace("-", "");
        }
        internal static int Output(int amount, int quality, double multiplier, int extra)
        {
            double value = amount + Math.Ceiling((float)((quality - 1) * amount) * (float)multiplier) + extra;
            if (amount <= 0 || quality < 1 || double.IsNaN(value) || double.IsInfinity(value) || value <= 0 || value > MaxAmount)
                throw new InvalidDataException("Unsupported recipe yield.");
            return (int)value;
        }
        internal static List<TrackerMaterial> Aggregate(IReadOnlyList<TrackerPlan> plans, Func<string, int, long> carried)
        {
            var order = new List<string>(); var names = new Dictionary<string, string>(StringComparer.Ordinal);
            var demand = new Dictionary<string, Dictionary<int, long>>(StringComparer.Ordinal);
            foreach (TrackerPlan plan in plans)
                foreach (TrackerNeed need in plan.Needs)
                {
                    if (need.Amount <= 0 || need.Amount > MaxAmount || need.Quality < -1 || need.Quality == 0 || need.Quality > 1000) throw new InvalidDataException("Invalid tracker amount.");
                    if (!demand.TryGetValue(need.Id, out var quantities))
                    { demand.Add(need.Id, quantities = new Dictionary<int, long>()); names.Add(need.Id, need.Name); order.Add(need.Id); }
                    quantities.TryGetValue(need.Quality, out long before); quantities[need.Quality] = checked(before + need.Amount);
                }
            var result = new List<TrackerMaterial>(order.Count);
            foreach (string id in order)
            {
                long required = 0, allocated = 0, specificDeficit = 0, any = 0;
                long owned = Math.Max(0, carried(id, -1));
                foreach (var pair in demand[id])
                {
                    required = checked(required + pair.Value);
                    if (pair.Key < 0) any = pair.Value;
                    else
                    {
                        long used = Math.Min(pair.Value, Math.Max(0, carried(id, pair.Key)));
                        allocated += used; specificDeficit += pair.Value - used;
                    }
                }
                long deficit = specificDeficit + Math.Max(0, any - Math.Max(0, owned - allocated));
                result.Add(new TrackerMaterial(id, names[id], owned, required, deficit));
            }
            return result;
        }
    }
    internal readonly struct TrackerLayout
    {
        internal readonly float Width, Height, RangeX, RangeY;
        internal readonly bool CanFit;
        internal TrackerLayout(float parentWidth, float parentHeight, float desiredHeight=300)
        {
            bool finite = !float.IsNaN(parentWidth) && !float.IsInfinity(parentWidth) && !float.IsNaN(parentHeight) && !float.IsInfinity(parentHeight);
            if (!finite) parentWidth = parentHeight = 0;
            CanFit = finite && parentWidth >= 240 && parentHeight >= 180;
            Width = Math.Min(280, Math.Max(0,parentWidth - 16));
            if(float.IsNaN(desiredHeight) || float.IsInfinity(desiredHeight))desiredHeight=300;
            Height = Math.Min(Math.Min(300,Math.Max(26,desiredHeight)),Math.Max(0,parentHeight - 16));
            RangeX = Math.Max(0,parentWidth - Width - 16); RangeY = Math.Max(0,parentHeight - Height - 16);
        }
        internal float Left(float x) => 8 + TrackerMath.Clamp(x) * RangeX;
        internal float Top(float y) => 8 + TrackerMath.Clamp(y) * RangeY;
        internal float NormalX(float left) => TrackerMath.Clamp((left - 8) / Math.Max(1,RangeX));
        internal float NormalY(float top) => TrackerMath.Clamp((top - 8) / Math.Max(1,RangeY));
    }
    internal sealed class TrackerStore
    {
        private readonly string _path;
        private readonly Action<string> _notice;
        private bool _readOnly, _recovered;
        internal TrackerDocument State { get; private set; } = new TrackerDocument();
        internal bool Dirty { get; private set; }
        internal bool SaveFailed { get; private set; }
        internal TrackerStore(string path, Action<string> notice) { _path = path; _notice = notice; Load(); }
        private static TrackerDocument Validate(TrackerDocument doc)
        {
            if (doc.Pins == null || doc.Pins.Count > TrackerMath.MaxPins) throw new InvalidDataException("Tracker pin limit.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (TrackerPin pin in doc.Pins)
                if (pin == null || string.IsNullOrWhiteSpace(pin.Key) || pin.Key.Length > 1024 || pin.Signature == null || pin.Signature.Length != 64 ||
                    pin.Alternative == null || pin.Alternative.Length > 256 || pin.Quality < 1 || pin.Quality > 1000 || !seen.Add(pin.Key))
                    throw new InvalidDataException("Invalid/duplicate tracker pin.");
            doc.X = TrackerMath.Clamp(doc.X); doc.Y = TrackerMath.Clamp(doc.Y); return doc;
        }
        private void Load()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    if (File.Exists(_path + ".bak"))
                    { var backup = PreferenceFiles.Read<TrackerDocument>(_path + ".bak"); if (backup.Version != 1) throw new InvalidDataException(); State = Validate(backup); _recovered = true; }
                    return;
                }
                var doc = PreferenceFiles.Read<TrackerDocument>(_path);
                if (doc.Version != 1) { _readOnly = true; SaveFailed = true; _notice("Unsupported tracker format; file preserved."); return; }
                State = Validate(doc);
            }
            catch (Exception e) when (e is IOException || e is SerializationException || e is ArgumentException || e is UnauthorizedAccessException)
            {
                try
                {
                    var backup = PreferenceFiles.Read<TrackerDocument>(_path + ".bak");
                    if (backup.Version != 1) throw new InvalidDataException();
                    State = Validate(backup); _recovered = true; _notice("Tracker recovered from backup; damaged file preserved on next save.");
                }
                catch { State = new TrackerDocument(); _readOnly = true; SaveFailed = true; _notice("Tracker file unreadable; preferences remain in memory until repaired."); }
            }
        }
        internal bool Contains(string key) => State.Pins.Exists(p => p.Key == key);
        internal bool Toggle(TrackerPin pin)
        {
            int at = State.Pins.FindIndex(p => p.Key == pin.Key);
            if (at >= 0) State.Pins.RemoveAt(at);
            else { if (State.Pins.Count >= TrackerMath.MaxPins) return false; State.Pins.Add(pin); }
            Touch(); return true;
        }
        internal void Remove(string key) { if (State.Pins.RemoveAll(p => p.Key == key) > 0) Touch(); }
        internal bool Migrate(IReadOnlyList<RecipeMove> approved)
        {
            if(_readOnly || !RecipeMoves.MigratePins(State.Pins,approved))return false;
            Touch();return true;
        }
        internal void Touch() => Dirty = true;
        internal bool Save()
        {
            if (!Dirty) return !SaveFailed;
            Dirty = false;
            if (_readOnly) { SaveFailed = true; return false; }
            try { Validate(State); PreferenceFiles.Write(_path, State, _recovered); _recovered = false; SaveFailed = false; return true; }
            catch (Exception e) when (e is IOException || e is SerializationException || e is ArgumentException || e is UnauthorizedAccessException || e is NotSupportedException)
            { SaveFailed = true; _notice("Tracker save failed; current changes remain in memory. " + e.Message); return false; }
        }
    }
}
