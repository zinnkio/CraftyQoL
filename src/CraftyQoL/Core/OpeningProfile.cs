using System;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace CraftyQoL.Core
{
    internal static class OpeningProbeIdentity
    {
        internal static bool Matches(bool active,string owner,MethodInfo? method,string expectedOwner,MethodInfo? expectedMethod)
            => active && owner==expectedOwner && method!=null && expectedMethod!=null && method==expectedMethod;
    }

    internal enum OpeningPhase
    {
        Show, PanelIncludingHooks, Availability, ListIncludingHooks, NativeRowConstruction, ToolbarPresent,
        FilterTotal, MetadataEnsure, MetadataRecords, Query, Palette,
        RowsTotal, DecorationCleanup, RowOrderAndDecorations, SelectionRestore,
        ToolbarBind, ToolbarLayout, TrackerModel, CanvasRebuild, Count
    }

    // Bounded, Unity-independent capture. Timings are inclusive, never summed as exclusive costs.
    internal sealed class OpeningCapture
    {
        internal const int SampleLimit = 256;
        internal readonly long Started;
        private readonly long _frequency;
        private readonly bool _allocations;
        private readonly long[] _ticks = new long[(int)OpeningPhase.Count];
        private readonly long[] _bytes = new long[(int)OpeningPhase.Count];
        private readonly int[] _calls = new int[(int)OpeningPhase.Count];
        private readonly Sample[] _samples = new Sample[SampleLimit];
        private readonly Frame[] _frames = new Frame[16];
        private readonly int[] _events = new int[(int)Event.Count];
        private int _used, _frameCount, _dropped;
        internal readonly int Id, OpenFrame, Access, Recipes, Prefabs, Pins;
        internal readonly bool Enabled, CraftTab;
        internal enum Event { MetadataRebuild, MetadataInvalidated, RefreshRequested, ScheduledRefresh,
            AccessChanged, ListInput, ListOutput, AvailabilityOutput, MetadataMiss,
            RowWidgetCreated, RowWidgetReused, RowWidgetDiscarded, Count }
        private struct Sample { internal OpeningPhase Phase; internal int Frame, Input, Output; internal long Start, Ticks, Bytes; }
        private struct Frame { internal int Index; internal double Milliseconds; }

        internal OpeningCapture(int id, int frame, long started, long frequency, bool allocations,
            bool enabled, int access, int recipes, int prefabs, int pins, bool craftTab)
        {
            if (frequency <= 0) throw new ArgumentOutOfRangeException(nameof(frequency));
            Id=id; OpenFrame=frame; Started=started; _frequency=frequency; _allocations=allocations;
            Enabled=enabled; Access=access; Recipes=recipes; Prefabs=prefabs; Pins=pins; CraftTab=craftTab;
        }
        internal void Add(OpeningPhase phase, int frame, long start, long end, long bytes,int input=-1,int output=-1)
        {
            int i=(int)phase;
            if (i<0 || i>=_calls.Length || end<start) return;
            _calls[i]++; _ticks[i]+=end-start; if(_allocations)_bytes[i]+=Math.Max(0,bytes);
            if(phase==OpeningPhase.NativeRowConstruction)return; // Per-row aggregate without flooding the timeline.
            if(_used==SampleLimit){_dropped++;return;}
            _samples[_used++]=new Sample{Phase=phase,Frame=frame,Start=start,Ticks=end-start,Bytes=Math.Max(0,bytes),Input=input,Output=output};
        }
        internal void Note(Event kind,int amount=1)=>_events[(int)kind]+=amount;
        internal void FrameInterval(int precedingFrame,double ms)
        {
            if(precedingFrame<OpenFrame-2 || _frameCount==_frames.Length || double.IsNaN(ms) || double.IsInfinity(ms) || ms<0)return;
            _frames[_frameCount++]=new Frame{Index=precedingFrame,Milliseconds=ms};
        }
        internal int Calls(OpeningPhase phase)=>_calls[(int)phase];
        internal long Ticks(OpeningPhase phase)=>_ticks[(int)phase];
        internal int Dropped=>_dropped;
        internal bool CraftingIntegrationObserved=>Calls(OpeningPhase.ToolbarPresent)>0 &&
            Calls(OpeningPhase.FilterTotal)>0 && Calls(OpeningPhase.MetadataEnsure)>0 &&
            Calls(OpeningPhase.MetadataRecords)>0 && Calls(OpeningPhase.RowsTotal)>0;
        internal string? ComparisonIssue
        {
            get
            {
                if(!CraftTab)return "not-craft-tab-at-entry";
                if(Calls(OpeningPhase.Show)!=1 || Calls(OpeningPhase.PanelIncludingHooks)==0 || Calls(OpeningPhase.ListIncludingHooks)==0)
                    return "missing-native-opening-phases";
                if(Enabled && !CraftingIntegrationObserved)return "missing-enabled-crafting-integration";
                if(!Enabled && (Calls(OpeningPhase.ToolbarPresent)>0 || Calls(OpeningPhase.FilterTotal)>0 || Calls(OpeningPhase.RowsTotal)>0))
                    return "unexpected-crafting-integration-in-disabled-run";
                if(_dropped>0)return "truncated-timeline";
                return null;
            }
        }
        private double Ms(long ticks)=>ticks*1000.0/_frequency;
        private static string Number(double number)=>number.ToString("0.###",CultureInfo.InvariantCulture);
        internal string Json()
        {
            var s=new StringBuilder(4096);
            s.Append("{\"schema\":2,\"opening\":").Append(Id).Append(",\"frame\":").Append(OpenFrame)
                .Append(",\"featuresEnabled\":").Append(Enabled?"true":"false").Append(",\"craftTab\":").Append(CraftTab?"true":"false")
                .Append(",\"craftingIntegrationObserved\":").Append(CraftingIntegrationObserved?"true":"false")
                .Append(",\"comparisonValid\":").Append(ComparisonIssue==null?"true":"false")
                .Append(",\"invalidReason\":").Append(ComparisonIssue==null?"null":"\""+ComparisonIssue+"\"")
                .Append(",\"accessFlags\":").Append(Access).Append(",\"registeredRecipes\":").Append(Recipes)
                .Append(",\"registeredPrefabs\":").Append(Prefabs).Append(",\"pins\":").Append(Pins)
                .Append(",\"allocationBytesAvailable\":").Append(_allocations?"true":"false")
                .Append(",\"droppedSamples\":").Append(_dropped).Append(",\"events\":{");
            for(int i=0;i<_events.Length;i++)
            {if(i>0)s.Append(',');s.Append('"').Append((Event)i).Append("\":").Append(_events[i]);}
            s.Append("},\"phases\":[");bool first=true;
            for(int i=0;i<_calls.Length;i++)
            {
                if(_calls[i]==0)continue;
                if(!first)s.Append(',');first=false;
                s.Append("{\"name\":\"").Append((OpeningPhase)i).Append("\",\"calls\":").Append(_calls[i])
                    .Append(",\"inclusiveMs\":").Append(Number(Ms(_ticks[i]))).Append(",\"allocatedBytes\":")
                    .Append(_allocations?_bytes[i].ToString(CultureInfo.InvariantCulture):"null").Append('}');
            }
            s.Append("],\"samples\":[");
            for(int i=0;i<_used;i++)
            {
                if(i>0)s.Append(',');var v=_samples[i];
                s.Append("{\"phase\":\"").Append(v.Phase).Append("\",\"frame\":").Append(v.Frame)
                    .Append(",\"inputCount\":").Append(v.Input).Append(",\"outputCount\":").Append(v.Output)
                    .Append(",\"startMs\":").Append(Number(Ms(v.Start-Started))).Append(",\"ms\":").Append(Number(Ms(v.Ticks)))
                    .Append(",\"allocatedBytes\":").Append(_allocations?v.Bytes.ToString(CultureInfo.InvariantCulture):"null").Append('}');
            }
            s.Append("],\"frameStartIntervals\":[");
            for(int i=0;i<_frameCount;i++)
            {if(i>0)s.Append(',');s.Append("{\"precedingFrame\":").Append(_frames[i].Index).Append(",\"ms\":").Append(Number(_frames[i].Milliseconds)).Append('}');}
            return s.Append("]}").ToString();
        }
    }
}
