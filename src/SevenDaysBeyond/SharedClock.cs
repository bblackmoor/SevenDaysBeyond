using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SevenDaysBeyond
{
    internal static class SharedClock
    {
        private static CalendarState clock;
        private static string path;
        private static bool enabled, running;
        private static Harmony harmony;
        private static float saveAt, reportAt;
        private static float clientSyncAt;
        private static long wirePackets;
        private static readonly Dictionary<MethodBase,int> expectedReads=new Dictionary<MethodBase,int>();
        private static readonly Dictionary<World,object> contexts=new Dictionary<World,object>();
        internal static bool Enabled { get { return enabled; } }
        internal static ulong Now { get { return clock==null?0:clock.Ticks; } }
        internal static bool TestRunning;
        internal static bool Expired(ulong deadline) { return clock!=null && clock.Expired(deadline); }
        internal static void Configure(bool selfTest)
        { GameManager.Instance.StartCoroutine(InstallWhenReady(selfTest)); }
        private static IEnumerator InstallWhenReady(bool selfTest)
        {
            while(!MultiWorldBridge.Ready) yield return null;
            yield return null;
            try { Install(selfTest); }
            catch(Exception e) { Log.Error("[SevenDaysBeyond] SHARED_CLOCK_INSTALL_FAILED: "+e); }
        }
        private static void Add(string type, string method, int reads)
        {
            Type t=AccessTools.TypeByName(type);
            MethodInfo target=t==null?null:AccessTools.Method(t,method);
            if(target==null) throw new MissingMethodException(type+"::"+method);
            expectedReads.Add(target,reads);
        }
        private static void Install(bool selfTest)
        {
            if(!WorldSuspension.Enabled) throw new InvalidOperationException("Shared calendar requires active simulation suspension.");
            object[] all=MultiWorldBridge.AllContexts().Where(MultiWorldBridge.Available).ToArray();
            foreach(object c in all) contexts.Add(MultiWorldBridge.World(c),c);
            string saves=Directory.GetParent(Directory.GetParent(MultiWorldBridge.SavePath(MultiWorldBridge.Find("West"))).FullName).FullName;
            path=Path.Combine(saves,"SevenDaysBeyond","SharedCalendar.xml");
            ulong latest=all.Max(c=>MultiWorldBridge.World(c).worldTime);
            if(File.Exists(path))
            {
                clock=CalendarState.Parse(File.ReadAllText(path));
                if(clock.Ticks<latest) clock=new CalendarState(latest);
            }
            else clock=new CalendarState(latest);
            // Only audited calendar consumers change. WorldTime, GetWorldTime, block/loot/entity
            // timestamps, saves, wandering hordes and native GameTimer retain simulation time.
            Add("World","IsDark",1); Add("World","WorldEventUpdateTime",3);
            Add("WorldEnvironment","WorldTimeChanged",2);
            Add("ConsoleCmdGetTime","Execute",1); Add("GameServerInfo","SetLocalServerWorldInfo",1);
            Add("TraderInfo","get_IsOpen",3); Add("TraderInfo","get_IsTraderActivitiesOpen",1);
            Add("TraderInfo","get_ShouldPlayOpenSound",1); Add("TraderInfo","get_ShouldPlayCloseSound",2); Add("TraderInfo","get_IsWarningTime",3);
            Add("PowerTimerRelay","CheckForActiveChange",1); Add("TimeOfDay","IsValid",1); Add("IsDayNumber","IsValid",1);
            Add("AIDirectorBloodMoonComponent","Tick",1); Add("AIDirectorBloodMoonComponent","SetForToday",1);
            Add("AIDirectorBloodMoonComponent","TimeChanged",3); Add("AIDirectorBloodMoonComponent","StartBloodMoon",1);
            Add("AIDirectorBloodMoonComponent","EndBloodMoon",1); Add("AIDirectorBloodMoonComponent","CalcNextDay",1);
            Add("QuestJournal","CompleteQuest",1); Add("QuestJournal","FailedQuest",1);
            Add("QuestTraderData","AddPOI",1); Add("QuestTraderData","CheckReset",1);
            Add("QuestEventManager","GetQuestList",1); Add("NPCQuestData+PlayerQuestData","set_QuestList",1);
            Add("QuestLockInstance","SetUnlocked",1); Add("QuestLockInstance","CheckQuestLock",1);
            Add("DialogRequirementQuestStatus","CheckRequirement",2);
            harmony=new Harmony("org.blackmoor.sevendaysbeyond.calendar");
            try
            {
                foreach(MethodBase target in expectedReads.Keys) harmony.Patch(target,transpiler:HM("CalendarReads"));
                harmony.Patch(AccessTools.Method(typeof(NetPackageWorldTime),"Setup"),prefix:HM("PacketTime"));
                harmony.Patch(AccessTools.Method(typeof(NetPackageWorldTime),"write"),transpiler:HM("WireClock"));
                var browser=HM("PacketTime"); browser.priority=Priority.Last;
                harmony.Patch(AccessTools.Method(typeof(GameServerInfo),"UpdateGameTimePlayers"),prefix:browser);
                harmony.Patch(MultiWorldBridge.Method("SendTransferResult"),postfix:HM("AfterTransfer"));
                harmony.Patch(AccessTools.Method(typeof(GameManager),"PlayerSpawnedInWorld"),postfix:HM("AfterPlayerSpawned"));
                // Update before occupancy evaluation and native per-world ticking.
                var before=HM("BeforeFrame"); before.priority=Priority.First;
                harmony.Patch(MultiWorldBridge.Method("UpdateBothWorlds"),prefix:before,postfix:HM("AfterFrame"));
                ModEvents.GameShutdown.RegisterHandler(OnShutdown);
                enabled=true;
                foreach(object c in all) WorldResumed(c);
                Publish(); clock.Save(path); saveAt=Time.realtimeSinceStartup+10;
                if(selfTest) SharedClockSelfTest.Enable();
                Log.Out("[SevenDaysBeyond] SHARED_CLOCK_ENABLED time="+Now+" calendarMethods="+expectedReads.Count+" storage="+path+"; native simulation clocks preserved; wire clock enforced; arrival and 1s client synchronization; pauses when all clients leave.");
            }
            catch { enabled=false; harmony.UnpatchSelf(); throw; }
        }
        private static HarmonyMethod HM(string name)
        { return new HarmonyMethod(typeof(SharedClock).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)); }
        private static IEnumerable<CodeInstruction> CalendarReads(IEnumerable<CodeInstruction> input, MethodBase __originalMethod)
        {
            FieldInfo field=AccessTools.Field(typeof(World),"worldTime");
            MethodInfo read=AccessTools.Method(typeof(SharedClock),"Read");
            MethodInfo get=AccessTools.Method(typeof(WorldBase),"GetWorldTime");
            MethodInfo readBase=AccessTools.Method(typeof(SharedClock),"ReadBase");
            int count=0;
            var code=input.Select(i=>new CodeInstruction(i)).ToArray();
            foreach(var instruction in code)
                if(instruction.opcode==OpCodes.Ldfld && Equals(instruction.operand,field))
                { instruction.opcode=OpCodes.Call; instruction.operand=read; count++; }
                else if(instruction.Calls(get)) { instruction.opcode=OpCodes.Call; instruction.operand=readBase; count++; }
            if(count!=expectedReads[__originalMethod]) throw new InvalidOperationException("Calendar read contract changed: "+__originalMethod+" reads="+count);
            return code;
        }
        private static ulong Read(World world)
        { return enabled && world!=null && contexts.ContainsKey(world)?Now:world.worldTime; }
        private static ulong ReadBase(WorldBase world)
        { return enabled && world is World && contexts.ContainsKey((World)world)?Now:world.GetWorldTime(); }
        private static void PacketTime(ref ulong __0) { if(enabled) __0=Now; }
        // Enforce the calendar where bytes are written, including previously compiled/inlined Setup callers.
        private static System.Collections.Generic.IEnumerable<CodeInstruction> WireClock(System.Collections.Generic.IEnumerable<CodeInstruction> input)
        {
            FieldInfo field=AccessTools.Field(typeof(NetPackageWorldTime),"worldTime");
            int count=0;
            foreach(CodeInstruction instruction in input)
            {
                yield return instruction;
                if(instruction.opcode==OpCodes.Ldfld && Equals(instruction.operand,field))
                { count++; yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(SharedClock),"WireTime")); }
            }
            if(count!=1) throw new InvalidOperationException("World-time serialization contract changed.");
        }
        private static ulong WireTime(ulong native)
        {
            if(!enabled) return native;
            System.Threading.Interlocked.Increment(ref wirePackets);
            return Now;
        }
        private static void SynchronizeClient(ClientInfo client)
        {
            if(!enabled || client==null || !client.loginDone || !client.bAttachedToEntity || client.entityId<0) return;
            object context=MultiWorldBridge.Resolve(client);
            if(!MultiWorldBridge.Available(context) || MultiWorldBridge.World(context).GetEntity(client.entityId)==null) return;
            client.SendPackage(NetPackageManager.GetPackage<NetPackageWorldTime>().Setup(Now));
        }
        private static void AfterTransfer(ClientInfo __0, bool __2)
        {
            if(!enabled || !__2) return;
            try { SynchronizeClient(__0); }
            catch(Exception e) { Disable(e); }
        }
        private static void AfterPlayerSpawned(ClientInfo __0)
        {
            if(!enabled) return;
            try { SynchronizeClient(__0); }
            catch(Exception e) { Disable(e); }
        }
        private static void BeforeFrame()
        {
            if(!enabled || !MultiWorldBridge.Ready) return;
            try
            {
            // Once per server frame, never once per world. Transfer/loading sessions count as present.
            running=TestRunning || SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.List.Any(c=>c.loginDone);
            clock.Advance(Time.deltaTime,Math.Max(0,GameStats.GetInt(EnumGameStats.TimeOfDayIncPerSec)),running);
            Publish();
            }
            catch(Exception e) { Disable(e); }
        }
        private static void Publish()
        {
            foreach(object c in contexts.Values)
                if(MultiWorldBridge.Available(c))
                    using(MultiWorldBridge.Scope(c)) SkyManager.SetGameTime(Now);
        }
        private static void AfterFrame()
        {
            if(!enabled || !MultiWorldBridge.Ready) return;
            try
            {
            // Native SetTime runs during active simulation; restore the shared sky value afterward.
            Publish();
            SharedClockSelfTest.Tick();
            float now=Time.realtimeSinceStartup;
            if(now>=clientSyncAt)
            {
                clientSyncAt=now+1;
                foreach(ClientInfo client in SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.List)
                    if(MultiWorldBridge.Transfer(client)==null) SynchronizeClient(client);
            }
            if(now>=saveAt) { clock.Save(path); saveAt=now+10; }
            if(now<reportAt) return;
            reportAt=now+15;
            Log.Out("[SevenDaysBeyond] SHARED_CLOCK time="+Now+" running="+running+" wirePackets="+System.Threading.Interlocked.Read(ref wirePackets)+"; "+string.Join("; ",contexts.Values.Select(c=>MultiWorldBridge.Name(c)+" simulation="+MultiWorldBridge.World(c).worldTime)));
            }
            catch(Exception e) { Disable(e); }
        }
        private static void Disable(Exception error)
        { enabled=false; TestRunning=false; Log.Error("[SevenDaysBeyond] SHARED_CLOCK_DISABLED; native map clocks remain available: "+error); }
        internal static void WorldResumed(object context)
        {
            if(!enabled) return;
            try
            {
                using(MultiWorldBridge.Scope(context))
                {
                    var moon=MultiWorldBridge.World(context).aiDirector.BloodMoonComponent;
                    AccessTools.Method(typeof(AIDirectorBloodMoonComponent),"TimeChanged").Invoke(moon,new object[]{false});
                }
            }
            catch(Exception e) { Disable(e); }
        }
        internal static void SaveAndVerify()
        { clock.Save(path); if(CalendarState.Parse(File.ReadAllText(path)).Ticks!=Now) throw new InvalidDataException("Shared calendar persistence mismatch."); }
        private static void OnShutdown(ref ModEvents.SGameShutdownData data)
        {
            if(!enabled) return;
            try { clock.Save(path); Log.Out("[SevenDaysBeyond] SHARED_CLOCK_SAVED time="+Now); }
            catch(Exception e) { Log.Error("[SevenDaysBeyond] SHARED_CLOCK_SAVE_FAILED: "+e); }
        }
        internal static void Report(ClientInfo client)
        {
            if(!enabled) { PlayerMessages.Send(client,"sdb_clock_disabled"); return; }
            var time=GameUtils.WorldTimeToElements(Now);
            PlayerMessages.Send(client,"sdb_clock_report", time.Item1, time.Item2.ToString("00",CultureInfo.InvariantCulture),
                time.Item3.ToString("00",CultureInfo.InvariantCulture), PlayerMessages.Key(running ? "sdb_clock_running" : "sdb_clock_paused"));
        }
    }
}
