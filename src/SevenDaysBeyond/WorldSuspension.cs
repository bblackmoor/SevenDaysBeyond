using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SevenDaysBeyond
{
    internal static class WorldSuspension
    {
        internal sealed class State
        {
            internal object Context;
            internal bool Sleeping, PreviousWaterPause;
            internal float LastNeeded;
            internal long Frames, Physics, BlockedFrames, BlockedPhysics, BlockedCallbacks;
        }
        private static readonly Dictionary<World, State> states = new Dictionary<World, State>();
        private static volatile HashSet<World> sleeping = new HashSet<World>();
        private static bool enabled;

        private static MethodInfo findOwner;
        private static volatile Dictionary<int, World> sceneWorlds = new Dictionary<int, World>();
        private static Harmony harmony;
        internal static bool Enabled { get { return enabled; } }
        internal static long GlobalTaskPumps;
        private static float reportAt;
        private static MethodInfo active;
        internal static readonly HashSet<object> TestReservations = new HashSet<object>();
        internal static State Get(object c) { State s; return states.TryGetValue(MultiWorldBridge.World(c),out s) ? s : null; }
        private static object Active() { return active.Invoke(null,null); }
        internal static bool IsSleeping(World world) { return enabled && world != null && sleeping.Contains(world); }

        internal static void Install(Type upstream, bool selfTest)
        {
            if(!MultiWorldBridge.Ready)
            {
                GameManager.Instance.StartCoroutine(InstallWhenReady(upstream,selfTest));
                return;
            }
            active = MultiWorldBridge.Method("ActiveContext");
            findOwner = MultiWorldBridge.Method("FindServiceOwner");
            // Check all private access before changing live behavior.
            foreach (string n in new[] { "Contexts", "Transfers", "repeatedGlobalFrame" })
                if (upstream.GetField(n, BindingFlags.Static|BindingFlags.NonPublic) == null) throw new MissingFieldException(n);
            Type callback = upstream.GetNestedType("ScopeUnityCallbacks",BindingFlags.NonPublic);
            MethodInfo callbackTargets = callback.GetMethod("TargetMethods",BindingFlags.NonPublic|BindingFlags.Static);
            if (callbackTargets == null) throw new MissingMethodException("ScopeUnityCallbacks.TargetMethods");
            harmony = new Harmony("org.blackmoor.sevendaysbeyond.suspension");
            try
            {
                Log.Out("[SevenDaysBeyond] SUSPENSION_INSTALL_BEGIN after world startup");
                harmony.Patch(MultiWorldBridge.Method("UpdateBothWorlds"),prefix: HM("BeforeFrame"),postfix: HM("AfterFrame"));
                Log.Out("[SevenDaysBeyond] SUSPENSION_SCHEDULER_HOOK_INSTALLED");
                var transpiler = HM("GateWorldCore"); transpiler.priority = Priority.First;
                harmony.Patch(AccessTools.Method(typeof(GameManager),"gmUpdate"),transpiler: transpiler);
                harmony.Patch(MultiWorldBridge.Method("SimulatePhysics"),prefix: HM("BeforePhysics"));
                var componentGate=HM("BeforeUnityCallback"); componentGate.priority=Priority.Last;
                int callbackCount=0;
                foreach(MethodBase method in (IEnumerable<MethodBase>)callbackTargets.Invoke(null,null))
                { harmony.Patch(method,prefix:componentGate); callbackCount++; }
                Log.Out("[SevenDaysBeyond] SUSPENSION_COMPONENT_GATES="+callbackCount);
                enabled = true;
                if (selfTest) SuspensionSelfTest.Enable();
                Log.Out("[SevenDaysBeyond] EMPTY_WORLD_SUSPENSION_ENABLED grace=10s; world core, physics, water and owned Unity callbacks; global services retained.");
            }
            catch { harmony.UnpatchSelf(); throw; }
        }
        private static IEnumerator InstallWhenReady(Type upstream, bool selfTest)
        {
            while(!MultiWorldBridge.Ready) yield return null;
            // Resume from Unity's coroutine scheduler after the game update stack has returned.
            yield return null;
            try { Install(upstream,selfTest); }
            catch(Exception e) { Log.Error("[SevenDaysBeyond] EMPTY_WORLD_SUSPENSION_INSTALL_FAILED: "+e); }
        }
        private static HarmonyMethod HM(string name)
        { return new HarmonyMethod(typeof(WorldSuspension).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic)); }
        private static void BeforeFrame()
        {
            if (!enabled || !MultiWorldBridge.Ready) return;
            try
            {
                float now = Time.realtimeSinceStartup;
                var needed = new HashSet<object>(BorderTravel.ReservedContexts());
                foreach (object c in TestReservations) needed.Add(c);
                foreach (object r in MultiWorldBridge.Requests())
                { needed.Add(MultiWorldBridge.Source(r)); needed.Add(MultiWorldBridge.Destination(r)); }
                foreach (ClientInfo c in SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.List)
                    if (c.loginDone) needed.Add(MultiWorldBridge.Resolve(c)); // Includes login and loading, before entity spawn.
                var next = new HashSet<World>();
                var scenes = new Dictionary<int,World>();
                foreach (object c in MultiWorldBridge.AllContexts())
                {
                    if (!MultiWorldBridge.Available(c)) continue;
                    World w = MultiWorldBridge.World(c);
                    scenes[MultiWorldBridge.Scene(c).handle]=w;
                    State s;
                    if (!states.TryGetValue(w,out s)) { s = new State { Context=c, LastNeeded=now }; states.Add(w,s); }
                    bool needs = w.Players.Count > 0 || needed.Contains(c);
                    if (needs) s.LastNeeded = now;
                    bool run = SuspensionPolicy.MustRun(w.Players.Count,false,false,needed.Contains(c),now,s.LastNeeded);
                    if (!run) next.Add(w);
                    if (!run || s.Sleeping) MultiWorldBridge.Rebase(c);
                    if (s.Sleeping != !run)
                    {
                        WaterSimulationNative water = MultiWorldBridge.Water(c);
                        if (!run) { s.PreviousWaterPause = water.IsPaused; water.SetPaused(true); }
                        else { water.SetPaused(s.PreviousWaterPause); SharedClock.WorldResumed(c); }
                        s.Sleeping = !run;
                        Log.Out("[SevenDaysBeyond] WORLD_"+(run?"RESUMED ":"SUSPENDED ")+MultiWorldBridge.Name(c)+" ticks="+MultiWorldBridge.Timer(c).ticks+" worldTime="+w.worldTime);
                    }
                }
                sceneWorlds=scenes;
                sleeping = next; // Immutable publication for callbacks on other threads.
            }
            catch(Exception e) { FailOpen(e); }
        }
        // Insert after the normal global prologue, immediately before the first world-owned service.
        private static IEnumerable<CodeInstruction> GateWorldCore(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var code = instructions.Select(i=>new CodeInstruction(i)).ToList();
            MethodInfo marker = AccessTools.PropertyGetter(typeof(LockManager),"Instance");
            int[] indices = code.Select((i,n)=>new {i,n}).Where(v=>v.i.Calls(marker)).Select(v=>v.n).ToArray();
            if(indices.Length!=1) throw new InvalidOperationException("gmUpdate world-core anchor mismatch.");
            int at=indices[0];
            if(code[at].blocks.Count!=0) throw new InvalidOperationException("gmUpdate anchor exception boundary changed.");
            Label proceed=il.DefineLabel();
            var gate=new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(WorldSuspension),"RunWorldCore"));
            gate.labels.AddRange(code[at].labels); code[at].labels.Clear(); code[at].labels.Add(proceed);
            code.InsertRange(at,new[] {gate,new CodeInstruction(OpCodes.Brtrue,proceed),new CodeInstruction(OpCodes.Ret)});
            Log.Out("[SevenDaysBeyond] EMPTY_WORLD_CORE_GATE_INSTALLED anchors=1");
            return code;
        }
        private static bool RunWorldCore()
        {
            if (!enabled || !MultiWorldBridge.Ready) return true;
            object c=Active();
            if(c==null) return true;
            State s=Get(c);
            if(s==null) return true;
            if(!IsSleeping(MultiWorldBridge.World(c))) { s.Frames++; return true; }
            s.BlockedFrames++;
            // The normal first-world prologue already ran seven global services.
            // The eighth lives later in gmUpdate, so pump it before returning from a sleeping first world.
            if(!MultiWorldBridge.Repeated) { ThreadManager.UpdateMainThreadTasks(); GlobalTaskPumps++; }
            return false;
        }
        private static bool BeforePhysics(object __0)
        {
            if(!enabled || !MultiWorldBridge.Ready) return true;
            State s=Get(__0);
            if(s==null) return true;
            if(IsSleeping(MultiWorldBridge.World(__0))) { s.BlockedPhysics++; return false; }
            s.Physics++; return true;
        }
        private static bool BeforeUnityCallback(Component __instance)
        {
            if(!enabled || !MultiWorldBridge.Ready || __instance==null) return true;
            World world=null;
            var entity=__instance as Entity;
            if(entity!=null) world=entity.world;
            else
            {
                object context=findOwner.Invoke(null,new object[]{__instance,0});
                if(context!=null) world=MultiWorldBridge.World(context);
                else
                {
                    var environment=__instance as WorldEnvironment;
                    var weather=__instance as WeatherManager;
                    if(environment!=null) world=environment.world;
                    else if(weather!=null) world=weather.world;
                    else sceneWorlds.TryGetValue(__instance.gameObject.scene.handle,out world);
                }
            }
            if(!IsSleeping(world)) return true;
            State state;
            if(states.TryGetValue(world,out state)) System.Threading.Interlocked.Increment(ref state.BlockedCallbacks);
            return false;
        }
        private static void AfterFrame()
        {
            if(!enabled || !MultiWorldBridge.Ready) return;
            try
            {
                SuspensionSelfTest.Tick();
                if(Time.realtimeSinceStartup<reportAt) return;
                reportAt=Time.realtimeSinceStartup+15;
                foreach(State s in states.Values)
                    Log.Out("[SevenDaysBeyond] WORLD_ACTIVITY "+MultiWorldBridge.Name(s.Context)+" sleeping="+s.Sleeping
                        +" core="+s.Frames+" physics="+s.Physics+" blockedCore="+s.BlockedFrames+" blockedPhysics="+s.BlockedPhysics
                        +" blockedCallbacks="+s.BlockedCallbacks+" ticks="+MultiWorldBridge.Timer(s.Context).ticks
                        +" worldTime="+MultiWorldBridge.World(s.Context).worldTime+" globalPumps="+GlobalTaskPumps);
            }
            catch(Exception e) { FailOpen(e); }
        }
        internal static void Report(ClientInfo client)
        {
            if(!enabled) { PlayerMessages.Send(client,"sdb_suspension_disabled"); return; }
            foreach(State s in states.Values)
                PlayerMessages.Send(client,"sdb_suspension_report", MultiWorldBridge.Name(s.Context),
                    PlayerMessages.Key(s.Sleeping ? "sdb_world_suspended" : "sdb_world_awake"), MultiWorldBridge.World(s.Context).Players.Count, s.Frames);
        }
        private static void FailOpen(Exception error)
        {
            enabled=false; sleeping=new HashSet<World>();
            foreach(State s in states.Values)
                if(s.Sleeping)
                {
                    try
                    {
                        MultiWorldBridge.Water(s.Context).SetPaused(s.PreviousWaterPause);
                        MultiWorldBridge.Rebase(s.Context);
                    }
                    catch(Exception restore) { Log.Warning("[SevenDaysBeyond] Suspension restore: "+restore.Message); }
                    s.Sleeping=false;
                }
            SuspensionSelfTest.Cleanup();
            Log.Error("[SevenDaysBeyond] EMPTY_WORLD_SUSPENSION_DISABLED; upstream scheduling restored: "+error);
        }
    }
}
