using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using UnityEngine;

namespace SevenDaysBeyond
{
    internal static class BorderTravel
    {
        private sealed class State
        {
            internal object Context;
            internal Vector3 Safe;
            internal bool HasSafe;
            internal float WarnAt, Cooldown;
            internal int WarningLevel;
            internal Job Pending;
        }
        private sealed class Job
        {
            internal ClientInfo Client;
            internal object Source, Destination, Request;
            internal MapBounds Bounds;
            internal Vector3 Centre, Hold, Point;
            internal ChunkManager.ChunkObserver Observer;
            internal float Deadline, Started, CheckAt;
            internal bool Approach;
        }
        private static readonly Dictionary<ClientInfo, State> states = new Dictionary<ClientInfo, State>();
        private static readonly Dictionary<World, MapBounds> bounds = new Dictionary<World, MapBounds>();
        private sealed class Probe
        {
            internal object Context;
            internal MapBounds Bounds;
            internal Vector3 Centre;
            internal ChunkManager.ChunkObserver Observer;
            internal float Deadline, InspectAt;
        }
        private static readonly List<Probe> probes = new List<Probe>();
        private static bool probesStarted;
        private static Harmony harmony;
        private static bool enabled, allowApproach, failed;
        private static float nextTick;
        private const string HarmonyId = "org.blackmoor.sevendaysbeyond.borders";

        internal static void Install(Type upstream)
        {
            string path = Path.Combine(Path.GetDirectoryName(typeof(BorderTravel).Assembly.Location), "BoundaryTest.xml");
            if (!File.Exists(path)) { Log.Out("[SevenDaysBeyond] Border travel disabled: BoundaryTest.xml absent."); return; }
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            var config = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(path, settings)) config.Load(reader);
            if (config.DocumentElement.Name != "SevenDaysBeyond" || config.DocumentElement.GetAttribute("phase2") != "true") return;
            allowApproach = config.DocumentElement.GetAttribute("allowApproachCommand") == "true";
            MultiWorldBridge.Bind(upstream);
            var cells=config.DocumentElement.SelectNodes("Grid/Cell");
            if(cells.Count>0)
                BorderPolicy.Grid=new GridLayout(cells.Cast<XmlElement>().Select(c=>new GridCell(c.GetAttribute("name"),int.Parse(c.GetAttribute("x"),CultureInfo.InvariantCulture),int.Parse(c.GetAttribute("z"),CultureInfo.InvariantCulture))));
            harmony = new Harmony(HarmonyId);
            try
            {
                Patch("UpdateBothWorlds", "AfterWorldTick", false);
                Patch("RestoreSharedCharacter", "PlaceArrival", false);
                Patch("BeginSessionWorldChange", "BeforeSwitch", true);
                ModEvents.ChatMessage.RegisterHandler(OnChat);
                if (config.DocumentElement.GetAttribute("freezeEmptyWorlds") == "true")
                    WorldSuspension.Install(upstream, config.DocumentElement.GetAttribute("suspensionSelfTest") == "true");
                if (config.DocumentElement.GetAttribute("sharedClock") == "true")
                    SharedClock.Configure(config.DocumentElement.GetAttribute("clockSelfTest") == "true");
                if (config.DocumentElement.GetAttribute("questBook") == "true") QuestBook.Configure();
                enabled = true;
                Log.Out("[SevenDaysBeyond] BORDER_TRAVEL_ENABLED gridCells="+BorderPolicy.Grid.Cells.Length+"; warnings=50m/10m before travel line; trigger=500m; arrival=600m; on-foot only; approachCommand=" + allowApproach);
            }
            catch { harmony.UnpatchSelf(); throw; }
        }
        internal static IEnumerable<object> ReservedContexts()
        {
            foreach (Probe p in probes) yield return p.Context;
            foreach (State s in states.Values)
                if (s.Pending != null) { yield return s.Pending.Source; yield return s.Pending.Destination; }
        }
        private static void Patch(string upstream, string ours, bool prefix)
        {
            var patch = new HarmonyMethod(typeof(BorderTravel).GetMethod(ours, BindingFlags.Static | BindingFlags.NonPublic));
            harmony.Patch(MultiWorldBridge.Method(upstream), prefix: prefix ? patch : null, postfix: prefix ? null : patch);
        }
        private static MapBounds Bounds(object context)
        {
            World w = MultiWorldBridge.World(context);
            MapBounds value;
            if (!bounds.TryGetValue(w, out value))
            {
                Vector3i min, max;
                using (MultiWorldBridge.Scope(context))
                    if (!w.GetWorldExtent(out min, out max)) throw new InvalidOperationException("Map has no finite bounds.");
                value = new MapBounds(min.x, max.x, min.z, max.z);
                if (!value.Valid) throw new InvalidOperationException("Map too small for configured safety margins.");
                bounds.Add(w, value);
                Log.Out("[SevenDaysBeyond] BORDER_BOUNDS " + MultiWorldBridge.Name(context) + ": " + min + " -> " + max);
            }
            return value;
        }
        private static EntityPlayer Player(ClientInfo c, object context)
        { return MultiWorldBridge.World(context).GetEntity(c.entityId) as EntityPlayer; }
        private static void Say(ClientInfo c, string key, params object[] args) { PlayerMessages.Send(c, key, args); }
        private static void AfterWorldTick()
        {
            if (!enabled || failed || !GameManager.IsDedicatedServer || !MultiWorldBridge.Ready || Time.realtimeSinceStartup < nextTick) return;
            nextTick = Time.realtimeSinceStartup + 0.25f;
            try
            {
                // Validate both routes before monitoring any player.
                if (BorderPolicy.Grid.Cells.Any(cell=>!MultiWorldBridge.Available(MultiWorldBridge.Find(cell.Name)))) return;
                foreach(GridCell cell in BorderPolicy.Grid.Cells) Bounds(MultiWorldBridge.Find(cell.Name));
                ProbeLanding();
                ClientInfo[] clients = SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.List.ToArray();
                foreach (ClientInfo gone in states.Keys.Where(c => !clients.Contains(c)).ToArray())
                { Cleanup(states[gone]); states.Remove(gone); }
                foreach (ClientInfo c in clients)
                {
                    if (!c.loginDone || c.entityId == -1) continue;
                    State state;
                    if (!states.TryGetValue(c, out state)) { state = new State(); states.Add(c, state); }
                    if (state.Pending != null) { TickJob(state); continue; }
                    if (MultiWorldBridge.Transfer(c) != null) continue;
                    object context = MultiWorldBridge.Resolve(c);
                    if (!MultiWorldBridge.Available(context) || !BorderPolicy.Grid.Cells.Any(cell=>cell.Name==MultiWorldBridge.Name(context))) continue;
                    if (state.Context != context) { state.Context = context; state.HasSafe = false; state.WarnAt = 0; state.WarningLevel = 0; }
                    var w = MultiWorldBridge.World(context);
                    using (MultiWorldBridge.Scope(context))
                    {
                        EntityPlayer p = Player(c, context);
                        if (p == null || p.IsDead()) continue;
                        MapBounds b = Bounds(context);
                        float distance;
                        BorderEdge edge = BorderPolicy.Nearest(b, p.position.x, p.position.z, out distance);
                        Vector3 safe;
                        if (distance >= BorderPolicy.Trigger + 40 && p.AttachedToEntity == null
                            && SafeLanding.TryAt(w, b, (int)Math.Floor(p.position.x), (int)Math.Floor(p.position.z), out safe)
                            && Math.Abs(p.position.y - safe.y) < 2)
                        { state.Safe = safe; state.HasSafe = true; }
                        int warningLevel = BorderPolicy.WarningLevel(distance);
                        if (warningLevel == 0) { state.WarningLevel = 0; continue; }
                        string dest = BorderPolicy.Destination(MultiWorldBridge.Name(context), edge);
                        if (warningLevel > state.WarningLevel || Time.realtimeSinceStartup >= state.WarnAt)
                        {
                            string remaining = Math.Max(0, distance - BorderPolicy.Trigger).ToString("0", CultureInfo.InvariantCulture);
                            if (dest == null) Say(c, "sdb_border_closed", remaining);
                            else Say(c, "sdb_border_open", remaining, dest);
                            state.WarnAt = Time.realtimeSinceStartup + 15;
                        }
                        state.WarningLevel = warningLevel;
                        if (distance > BorderPolicy.Trigger) continue;
                        if (p.AttachedToEntity != null)
                        {
                            if (Time.realtimeSinceStartup >= state.Cooldown)
                            { Say(c, "sdb_vehicle_crossing"); state.Cooldown = Time.realtimeSinceStartup + 10; }
                            continue;
                        }
                        // Retreat before preflight, also protecting closed edges and cooldowns.
                        if (!Retreat(c, p, state, b)) continue;
                        if (dest == null || Time.realtimeSinceStartup < state.Cooldown) continue;
                        object target = MultiWorldBridge.Find(dest);
                        if (!MultiWorldBridge.Available(target)) { Say(c, "sdb_destination_unavailable"); state.Cooldown = Time.realtimeSinceStartup + 10; continue; }
                        MapBounds tb = Bounds(target);
                        float tx, tz;
                        BorderPolicy.ArrivalPoint(b, tb, edge, p.position.x, p.position.z, out tx, out tz);
                        Begin(c, state, context, target, tb, new Vector3(tx, 80, tz), p.position, false);
                    }
                }
            }
            catch (Exception error) { Disable(error); }
        }
        private static void ProbeLanding()
        {
            if (!probesStarted)
            {
                probesStarted = true;
                foreach (string name in BorderPolicy.Grid.Cells.Select(c=>c.Name))
                {
                    object context = MultiWorldBridge.Find(name);
                    MapBounds b = Bounds(context);
                    var centre = new Vector3(name == "West" ? b.MaxX - BorderPolicy.Arrival : b.MinX + BorderPolicy.Arrival, 80, 0);
                    using (MultiWorldBridge.Scope(context))
                    {
                        Vector3 rejected;
                        if (SafeLanding.TryAt(MultiWorldBridge.World(context), b, (int)b.MaxX, 0, out rejected))
                            throw new InvalidOperationException("Safety check accepted the outer edge.");
                        probes.Add(new Probe { Context = context, Bounds = b, Centre = centre,
                            Deadline = Time.realtimeSinceStartup + 45, InspectAt = Time.realtimeSinceStartup + 10,
                            Observer = MultiWorldBridge.World(context).m_ChunkManager.AddChunkObserver(centre, false, 8, -1) });
                    }
                }
            }
            foreach (Probe probe in probes.ToArray())
                using (MultiWorldBridge.Scope(probe.Context))
                {
                    Vector3 point;
                    bool safe = SafeLanding.Find(MultiWorldBridge.World(probe.Context), probe.Bounds, probe.Centre, out point);
                    if (!safe && Time.realtimeSinceStartup >= probe.InspectAt)
                    {
                        Log.Out("[SevenDaysBeyond] BORDER_GROUND_DIAGNOSTIC " + MultiWorldBridge.Name(probe.Context) + " " + SafeLanding.Explain(MultiWorldBridge.World(probe.Context), probe.Centre));
                        probe.InspectAt = float.MaxValue;
                    }
                    if (!safe && Time.realtimeSinceStartup < probe.Deadline) continue;
                    MultiWorldBridge.World(probe.Context).m_ChunkManager.RemoveChunkObserver(probe.Observer);
                    probes.Remove(probe);
                    Log.Out("[SevenDaysBeyond] BORDER_SAFETY_PROBE " + MultiWorldBridge.Name(probe.Context) + " safe=" + safe + " point=" + point);
                }
        }
        private static bool Retreat(ClientInfo c, EntityPlayer p, State state, MapBounds b)
        {
            float x, z;
            BorderPolicy.Inland(b, p.position.x, p.position.z, BorderPolicy.Trigger + 40, out x, out z);
            Vector3 safe;
            if (!SafeLanding.Find(p.world, b, new Vector3(x, 80, z), out safe))
            {
                if (!state.HasSafe || !SafeLanding.TryAt(p.world, b, (int)Math.Floor(state.Safe.x), (int)Math.Floor(state.Safe.z), out safe))
                { if (Time.realtimeSinceStartup >= state.Cooldown) Say(c, "sdb_retreat_unavailable"); state.Cooldown = Time.realtimeSinceStartup + 5; return false; }
            }
            Teleport(c, p, safe);
            return true;
        }
        private static void Teleport(ClientInfo c, EntityPlayer p, Vector3 point)
        {
            p.SetPosition(point, true);
            if (p.ChunkObserver != null) p.ChunkObserver.SetPosition(point);
            c.SendPackage(NetPackageManager.GetPackage<NetPackageTeleportPlayer>().Setup(point, null, false));
        }
        private static void Begin(ClientInfo c, State state, object source, object target, MapBounds b, Vector3 centre, Vector3 hold, bool approach)
        {
            var job = new Job { Client = c, Source = source, Destination = target, Bounds = b, Centre = centre, Hold = hold,
                Deadline = Time.realtimeSinceStartup + 30, Started = Time.realtimeSinceStartup, Approach = approach };
            using (MultiWorldBridge.Scope(target)) job.Observer = MultiWorldBridge.World(target).m_ChunkManager.AddChunkObserver(centre, false, 8, -1);
            state.Pending = job;
            if (approach) Say(c, "sdb_approach_preparing");
            else Say(c, "sdb_landing_preparing", MultiWorldBridge.Name(target));
        }
        private static void TickJob(State state)
        {
            Job job = state.Pending;
            object transfer = MultiWorldBridge.Transfer(job.Client);
            if (job.Request != null)
            {
                if (transfer != job.Request || MultiWorldBridge.RollingBack(job.Request))
                {
                    Log.Out("[SevenDaysBeyond] BORDER_TRANSFER_FINISHED current=" + MultiWorldBridge.Name(MultiWorldBridge.Resolve(job.Client)));
                    state.Cooldown = Time.realtimeSinceStartup + 20;
                    Cleanup(state);
                }
                return;
            }
            EntityPlayer p = Player(job.Client, job.Source);
            if (p == null || p.IsDead() || p.AttachedToEntity != null || transfer != null || MultiWorldBridge.Resolve(job.Client) != job.Source
                || (Time.realtimeSinceStartup - job.Started > 2 && Vector3.Distance(p.position, job.Hold) > 16))
            { Abort(state, "sdb_crossing_cancelled"); return; }
            if (Time.realtimeSinceStartup >= job.Deadline)
            { Abort(state, "sdb_landing_timeout"); return; }
            if (Time.realtimeSinceStartup < job.CheckAt) return;
            job.CheckAt = Time.realtimeSinceStartup + 1;
            using (MultiWorldBridge.Scope(job.Destination))
            {
                if (!SafeLanding.Find(MultiWorldBridge.World(job.Destination), job.Bounds, job.Centre, out job.Point)) return;
            }
            if (job.Approach)
            {
                using (MultiWorldBridge.Scope(job.Source)) Teleport(job.Client, p, job.Point);
                float remaining;
                BorderEdge approachEdge=BorderPolicy.Nearest(job.Bounds,job.Point.x,job.Point.z,out remaining);
                Say(job.Client, "sdb_approach_ready", PlayerMessages.Direction(approachEdge));
                Log.Out("[SevenDaysBeyond] BORDER_TEST_APPROACH " + MultiWorldBridge.Name(job.Source) + " point=" + job.Point);
                state.Cooldown = Time.realtimeSinceStartup + 3;
                Cleanup(state); return;
            }
            using (MultiWorldBridge.Scope(job.Source)) job.Request = MultiWorldBridge.Start(job.Client, MultiWorldBridge.Name(job.Destination));
            if (job.Request == null) { Abort(state, "sdb_crossing_failed"); return; }
            Log.Out("[SevenDaysBeyond] BORDER_TRANSFER_REQUEST " + MultiWorldBridge.Name(job.Source) + " -> " + MultiWorldBridge.Name(job.Destination) + " landing=" + job.Point);
        }
        private static void Abort(State state, string text)
        { Say(state.Pending.Client, text); state.Cooldown = Time.realtimeSinceStartup + 10; Cleanup(state); }
        private static void Cleanup(State state)
        {
            Job job = state.Pending;
            state.Pending = null;
            if (job != null && job.Observer != null)
                using (MultiWorldBridge.Scope(job.Destination)) MultiWorldBridge.World(job.Destination).m_ChunkManager.RemoveChunkObserver(job.Observer);
        }
        private static void Disable(Exception error)
        {
            failed = true;
            Log.Error("[SevenDaysBeyond] BORDER_TRAVEL_DISABLED: " + error);
            foreach (Probe probe in probes.ToArray())
            {
                using (MultiWorldBridge.Scope(probe.Context)) MultiWorldBridge.World(probe.Context).m_ChunkManager.RemoveChunkObserver(probe.Observer);
                probes.Remove(probe);
            }
            foreach (State state in states.Values)
            {
                // Keep already-started upstream transfers and their validated landing available.
                if (state.Pending == null || state.Pending.Request != null) continue;
                try { Cleanup(state); } catch (Exception cleanup) { Log.Warning("[SevenDaysBeyond] Observer cleanup: " + cleanup.Message); }
            }
        }
        // Called after upstream has combined shared inventory/progression with map-local data.
        private static void PlaceArrival(PlayerDataFile __0, string __1, object __2)
        {
            foreach (State state in states.Values)
            {
                Job j = state.Pending;
                if (j == null || j.Approach || j.Request == null || j.Destination != __2
                    || j.Client.InternalId.CombinedString != __1 || MultiWorldBridge.Transfer(j.Client) != j.Request
                    || MultiWorldBridge.RollingBack(j.Request)) continue;
                __0.ecd.pos = j.Point;
                __0.lastSpawnPosition = new SpawnPosition(j.Point, __0.ecd.rot.y);
                Log.Out("[SevenDaysBeyond] BORDER_ARRIVAL_PLACED " + MultiWorldBridge.Name(__2) + " point=" + j.Point);
            }
        }
        private static bool BeforeSwitch(ClientInfo __0, object __1, object __2)
        {
            State state;
            if (!states.TryGetValue(__0, out state) || state.Pending == null || state.Pending.Request != __1) return true;
            Job j = state.Pending;
            if (MultiWorldBridge.RollingBack(__1) || __2 != j.Destination) { Cleanup(state); return true; }
            Vector3 verified;
            bool safe;
            using (MultiWorldBridge.Scope(j.Destination)) safe = SafeLanding.TryAt(MultiWorldBridge.World(j.Destination), j.Bounds,
                (int)Math.Floor(j.Point.x), (int)Math.Floor(j.Point.z), out verified) && Vector3.Distance(verified, j.Point) < 0.1f;
            if (safe) return true;
            MultiWorldBridge.CancelBeforeSwitch(__0, __1, "sdb_crossing_ground_changed");
            state.Cooldown = Time.realtimeSinceStartup + 10; Cleanup(state); return false;
        }
        private static ModEvents.EModEventResult OnChat(ref ModEvents.SChatMessageData data)
        {
            string message = (data.Message ?? "").Trim().ToLowerInvariant();
            if (message != "/sdb status" && message != "/sdb approach" && !message.StartsWith("/sdb approach ",StringComparison.Ordinal) && message != "/sdb time" && message != "/sdb map") return (ModEvents.EModEventResult)0;
            ClientInfo c = data.ClientInfo;
            if (!enabled || failed || !MultiWorldBridge.Ready || c == null || !c.loginDone) return (ModEvents.EModEventResult)2;
            try
            {
                object context = MultiWorldBridge.Resolve(c);
                if (!MultiWorldBridge.Available(context)) return (ModEvents.EModEventResult)2;
                using (MultiWorldBridge.Scope(context))
                {
                    EntityPlayer p = Player(c, context);
                    if (p == null || p.IsDead()) return (ModEvents.EModEventResult)2;
                    MapBounds b = Bounds(context);
                    if (message == "/sdb time") SharedClock.Report(c);
                    else if(message == "/sdb map")
                    {
                        foreach(GridCell cell in BorderPolicy.Grid.Cells.OrderByDescending(v=>v.Z).ThenBy(v=>v.X))
                            foreach (BorderEdge exit in Enum.GetValues(typeof(BorderEdge)))
                                if (BorderPolicy.Destination(cell.Name, exit) != null)
                                    Say(c, "sdb_grid_route", cell.Name, cell.X, cell.Z, PlayerMessages.Direction(exit), BorderPolicy.Destination(cell.Name, exit));
                    }
                    else if (message == "/sdb status")
                    {
                        float distance;
                        BorderEdge edge = BorderPolicy.Nearest(b, p.position.x, p.position.z, out distance);
                        Say(c, "sdb_border_status", MultiWorldBridge.Name(context), PlayerMessages.Direction(edge),
                            distance.ToString("0", CultureInfo.InvariantCulture), BorderPolicy.Trigger);
                        WorldSuspension.Report(c);
                        SharedClock.Report(c);
                    }
                    else
                    {
                        State state;
                        if (!states.TryGetValue(c, out state)) { state = new State { Context = context }; states.Add(c, state); }
                        if (!allowApproach) Say(c, "sdb_approach_disabled");
                        else if (state.Pending != null || MultiWorldBridge.Transfer(c) != null) Say(c, "sdb_crossing_in_progress");
                        else if (p.AttachedToEntity != null) Say(c, "sdb_approach_dismount");
                        else if (Time.realtimeSinceStartup < state.Cooldown) Say(c, "sdb_approach_wait");
                        else
                        {
                            BorderEdge edge=BorderPolicy.Grid.FirstExit(MultiWorldBridge.Name(context));
                            if(message.StartsWith("/sdb approach ",StringComparison.Ordinal))
                            {
                                string direction=message.Substring("/sdb approach ".Length);
                                if(!new[]{"east","west","north","south"}.Contains(direction.ToLowerInvariant()) || !Enum.TryParse(direction,true,out edge))
                                { Say(c,"sdb_approach_usage"); return (ModEvents.EModEventResult)2; }
                            }
                            float x = BorderPolicy.Clamp(p.position.x,b.MinX+BorderPolicy.Arrival,b.MaxX-BorderPolicy.Arrival);
                            float z = BorderPolicy.Clamp(p.position.z, b.MinZ + BorderPolicy.Arrival, b.MaxZ - BorderPolicy.Arrival);
                            if(edge==BorderEdge.East) x=b.MaxX-560;
                            else if(edge==BorderEdge.West) x=b.MinX+560;
                            else if(edge==BorderEdge.North) z=b.MaxZ-560;
                            else z=b.MinZ+560;
                            Begin(c, state, context, context, b, new Vector3(x, 80, z), p.position, true);
                        }
                    }
                }
            }
            catch (Exception error) { Disable(error); }
            return (ModEvents.EModEventResult)2;
        }
    }
}
