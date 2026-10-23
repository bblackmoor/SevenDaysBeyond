using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SevenDaysBeyond
{
    internal static class SharedClockSelfTest
    {
        private static bool enabled;
        private static int stage;
        private static float deadline;
        private static object[] contexts;
        private static ulong start;
        private static long[] frames, physics;
        private static ulong[] simulation, ticks;
        internal static void Enable() { enabled=true; }
        internal static void Tick()
        {
            if(!enabled) return;
            if(SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.List.Any(c=>c.loginDone))
            { enabled=false; SharedClock.TestRunning=false; Log.Out("[SevenDaysBeyond] SHARED_CLOCK_SELF_TEST_CANCELLED client joined."); return; }
            if(stage==0)
            {
                if(SuspensionSelfTest.Running) return;
                contexts=new[]{MultiWorldBridge.Find("West"),MultiWorldBridge.Find("East")};
                if(contexts.Any(c=>WorldSuspension.Get(c)==null || !WorldSuspension.Get(c).Sleeping)) return;
                frames=contexts.Select(c=>WorldSuspension.Get(c).Frames).ToArray();
                physics=contexts.Select(c=>WorldSuspension.Get(c).Physics).ToArray();
                simulation=contexts.Select(c=>MultiWorldBridge.World(c).worldTime).ToArray();
                ticks=contexts.Select(c=>MultiWorldBridge.Timer(c).ticks).ToArray();
                start=SharedClock.Now;
                SharedClock.TestRunning=true; deadline=Time.realtimeSinceStartup+2; stage=1;
                Log.Out("[SevenDaysBeyond] SHARED_CLOCK_SELF_TEST_BEGIN two-second calendar-only test; worlds remain suspended.");
                return;
            }
            if(Time.realtimeSinceStartup<deadline) return;
            AssertFrozen();
            if(stage==1)
            {
                SharedClock.TestRunning=false;
                if(SharedClock.Now<=start || !SharedClock.Expired(start+1)) throw new InvalidOperationException("Shared calendar failed to advance independently.");
                CheckPackets(); SharedClock.SaveAndVerify();
                Log.Out("[SevenDaysBeyond] SHARED_CLOCK_ADVANCE_PASS calendar="+start+" -> "+SharedClock.Now+"; both simulation clocks and physics remained frozen; matching packet time; persistence roundtrip.");
                start=SharedClock.Now; deadline=Time.realtimeSinceStartup+2; stage=2;
            }
            else
            {
                if(SharedClock.Now!=start) throw new InvalidOperationException("Empty-server calendar did not pause.");
                CheckPackets(); SharedClock.SaveAndVerify();
                enabled=false;
                Log.Out("[SevenDaysBeyond] SHARED_CLOCK_SELF_TEST_PASS common calendar, frozen worlds, equal packet time, empty-server pause, persisted reload.");
            }
        }
        private static void AssertFrozen()
        {
            for(int i=0;i<contexts.Length;i++)
            {
                var s=WorldSuspension.Get(contexts[i]);
                if(!s.Sleeping || s.Frames!=frames[i] || s.Physics!=physics[i]
                    || MultiWorldBridge.World(contexts[i]).worldTime!=simulation[i] || MultiWorldBridge.Timer(contexts[i]).ticks!=ticks[i])
                    throw new InvalidOperationException("Calendar advanced simulation in "+MultiWorldBridge.Name(contexts[i]));
            }
        }
        private static void CheckPackets()
        {
            FieldInfo time=typeof(NetPackageWorldTime).GetField("worldTime",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            foreach(object c in contexts)
                using(MultiWorldBridge.Scope(c))
                {
                    var packet=new NetPackageWorldTime().Setup(MultiWorldBridge.World(c).worldTime);
                    if((ulong)time.GetValue(packet)!=SharedClock.Now) throw new InvalidOperationException("Client packet time differed between maps.");
                }
        }
    }
}
