using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SevenDaysBeyond
{
    // Temporary, unsaved rigid bodies exercise each world's real isolated physics scene.
    // Never synthesize a player, edit a save, advance a clock, or leave fixtures behind.
    internal static class SuspensionSelfTest
    {
        private static bool enabled, started;
        private static int stage;
        private static float deadline;
        private static object[] contexts;
        private static Rigidbody[] bodies = new Rigidbody[0];
        private static Vector3[] positions;
        private static ulong[] clocks, ticks;
        private static long[] frames, physics;
        private static long pumps;
        internal static bool Running { get { return enabled; } }
        internal static void Enable() { enabled=true; }
        internal static void Tick()
        {
            if(!enabled) return;
            if(SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.List.Any(c=>c.loginDone))
            { Log.Out("[SevenDaysBeyond] SUSPENSION_SELF_TEST_CANCELLED client joined; fixtures removed."); Cleanup(); return; }
            if(!started)
            {
                contexts=new[]{MultiWorldBridge.Find("West")}.Concat(MultiWorldBridge.AllContexts().Where(c=>MultiWorldBridge.Name(c)!="West" && MultiWorldBridge.Available(c))).ToArray();
                if(contexts.Any(c=>c==null || WorldSuspension.Get(c)==null || !WorldSuspension.Get(c).Sleeping)) return;
                started=true;
                bodies=new Rigidbody[contexts.Length];
                for(int i=0;i<contexts.Length;i++)
                {
                    var go=new GameObject("SDB suspension test "+i);
                    SceneManager.MoveGameObjectToScene(go,MultiWorldBridge.Scene(contexts[i]));
                    go.transform.position=new Vector3(0,200,0);
                    bodies[i]=go.AddComponent<Rigidbody>();
                    bodies[i].useGravity=true;
                }
                Capture(); deadline=Time.realtimeSinceStartup+2; stage=1;
                Log.Out("[SevenDaysBeyond] SUSPENSION_SELF_TEST_BEGIN worlds="+contexts.Length+"; isolated unsaved rigid bodies.");
                return;
            }
            if(Time.realtimeSinceStartup<deadline) return;
            if(stage==1)
            {
                for(int i=0;i<contexts.Length;i++) AssertFrozen(i);
                if(WorldSuspension.GlobalTaskPumps<=pumps) throw new InvalidOperationException("Global task pump stopped while all worlds slept.");
                Log.Out("[SevenDaysBeyond] SUSPENSION_ALL_EMPTY_PASS stable clocks, core/physics counts and rigid bodies; global tasks continued.");
                WorldSuspension.TestReservations.Add(contexts[0]);
                Capture(); deadline=Time.realtimeSinceStartup+2; stage=2;
            }
            else if(stage==2)
            {
                var s=WorldSuspension.Get(contexts[0]);
                if(s.Sleeping || s.Frames<=frames[0] || s.Physics<=physics[0] || bodies[0].position.y>=positions[0].y-0.5f)
                    throw new InvalidOperationException("Reserved destination failed to resume core and physics.");
                for(int i=1;i<contexts.Length;i++) AssertFrozen(i);
                Log.Out("[SevenDaysBeyond] SUSPENSION_RESERVATION_WAKE_PASS West core/physics resumed; all other worlds remained frozen.");
                WorldSuspension.TestReservations.Remove(contexts[0]);
                deadline=Time.realtimeSinceStartup+SuspensionPolicy.GraceSeconds+1; stage=3;
            }
            else if(stage==3)
            {
                if(!WorldSuspension.Get(contexts[0]).Sleeping) throw new InvalidOperationException("Released destination did not suspend after grace.");
                Capture(); deadline=Time.realtimeSinceStartup+2; stage=4;
            }
            else
            {
                for(int i=0;i<contexts.Length;i++) AssertFrozen(i);
                Log.Out("[SevenDaysBeyond] SUSPENSION_SELF_TEST_PASS all-empty freeze, live global tasks, independent reservation wake, and release/refreeze; fixtures removed.");
                Cleanup();
            }
        }
        private static void Capture()
        {
            positions=bodies.Select(b=>b.position).ToArray();
            clocks=contexts.Select(c=>MultiWorldBridge.World(c).worldTime).ToArray();
            ticks=contexts.Select(c=>MultiWorldBridge.Timer(c).ticks).ToArray();
            frames=contexts.Select(c=>WorldSuspension.Get(c).Frames).ToArray();
            physics=contexts.Select(c=>WorldSuspension.Get(c).Physics).ToArray();
            pumps=WorldSuspension.GlobalTaskPumps;
        }
        private static void AssertFrozen(int i)
        {
            var s=WorldSuspension.Get(contexts[i]);
            if(!s.Sleeping || s.Frames!=frames[i] || s.Physics!=physics[i] || Vector3.Distance(bodies[i].position,positions[i])>0.001f
                || MultiWorldBridge.World(contexts[i]).worldTime!=clocks[i] || MultiWorldBridge.Timer(contexts[i]).ticks!=ticks[i]
                || !MultiWorldBridge.Water(contexts[i]).IsPaused)
                throw new InvalidOperationException("Suspended world advanced: "+MultiWorldBridge.Name(contexts[i]));
        }
        internal static void Cleanup()
        {
            enabled=false;
            if(contexts!=null) foreach(object c in contexts) WorldSuspension.TestReservations.Remove(c);
            foreach(Rigidbody body in bodies) if(body!=null) UnityEngine.Object.Destroy(body.gameObject);
        }
    }
}
