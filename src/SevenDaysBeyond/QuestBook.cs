using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace SevenDaysBeyond
{
    internal static class QuestBook
    {
        private static readonly object sync=new object();
        private static readonly Dictionary<string,QuestBookState> cache=new Dictionary<string,QuestBookState>();
        private static string directory;
        private static bool enabled;
        private static float nextTick;
        internal static void Configure() { GameManager.Instance.StartCoroutine(InstallWhenReady()); }
        private static IEnumerator InstallWhenReady()
        {
            while(!MultiWorldBridge.Ready || !SharedClock.Enabled) yield return null;
            yield return null;
            try
            {
                directory=Path.Combine(Directory.GetParent(Directory.GetParent(MultiWorldBridge.SavePath(MultiWorldBridge.Find("West"))).FullName).FullName,"SevenDaysBeyond","QuestBooks");
                NativeSelfTest();
                var h=new Harmony("org.blackmoor.sevendaysbeyond.quest-book");
                try
                {
                    h.Patch(MultiWorldBridge.Method("SaveSharedCharacter"),prefix:Hook("SavePage"));
                    h.Patch(MultiWorldBridge.Method("RestoreSharedCharacter"),postfix:Hook("RestorePage"));
                    h.Patch(MultiWorldBridge.Method("UpdateBothWorlds"),postfix:Hook("Tick"));
                    ModEvents.ChatMessage.RegisterHandler(OnChat);
                    enabled=true;
                    Log.Out("[SevenDaysBeyond] QUEST_BOOK_ENABLED character-owned map journals; all-map chat list; opt-in travel test.");
                }
                catch { h.UnpatchSelf(); throw; }
            }
            catch(Exception e) { Log.Error("[SevenDaysBeyond] QUEST_BOOK_INSTALL_FAILED: "+e); }
        }
        private static HarmonyMethod Hook(string name)
        { return new HarmonyMethod(typeof(QuestBook).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic)); }
        private static string FilePath(string id)
        {
            using(var hash=SHA256.Create()) return Path.Combine(directory,BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(id))).Replace("-","").ToLowerInvariant()+".xml");
        }
        private static QuestBookState Load(string id)
        {
            QuestBookState b;
            if(cache.TryGetValue(id,out b)) return b;
            string path=FilePath(id);
            b=File.Exists(path)?QuestBookState.Parse(File.ReadAllText(path),id):new QuestBookState(id);
            cache.Add(id,b); return b;
        }
        private static byte[] WriteJournal(QuestJournal journal)
        {
            using(var stream=new MemoryStream())
            using(var writer=new PooledBinaryWriter())
            { writer.SetBaseStream(stream); journal.Write(writer); writer.Flush(); if(stream.Length>2*1024*1024) throw new InvalidDataException("Native quest journal too large."); return stream.ToArray(); }
        }
        private static QuestJournal ReadJournal(byte[] bytes)
        {
            using(var stream=new MemoryStream(bytes))
            using(var reader=new PooledBinaryReader())
            { reader.SetBaseStream(stream); var journal=new QuestJournal(); journal.Read(reader); if(stream.Position!=stream.Length) throw new InvalidDataException("Quest journal had trailing bytes."); return journal; }
        }
        private static void Capture(PlayerDataFile data,string id,object context)
        {
            if(data?.questJournal==null || string.IsNullOrEmpty(id) || !MultiWorldBridge.Available(context)) return;
            if(data.questJournal.quests.Count>4096) throw new InvalidDataException("Too many native quests for this prototype.");
            string map=MultiWorldBridge.Name(context);
            var b=Load(id);
            b.Pages[map]=new QuestPage {Map=map,Journal=Convert.ToBase64String(WriteJournal(data.questJournal)),
                Summary=data.questJournal.quests.Select(q=>q.ID+" ["+q.CurrentState+"] "+string.Join(", ",q.Objectives.Select(o=>o.ID+"="+o.CurrentValue))
                    +"; map targets: "+string.Join(", ",q.PositionData.Select(p=>p.Key+"="+p.Value))).Select(s=>s.Length>2048?s.Substring(0,2048):s).ToArray()};
            b.Save(FilePath(id));
        }
        private static void SavePage(PlayerDataFile __0,string __1)
        {
            if(!enabled) return;
            try
            {
                lock(sync)
                {
                    ClientInfo client=SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.List.FirstOrDefault(c=>c.InternalId!=null && c.InternalId.CombinedString==__1);
                    object context=MultiWorldBridge.Active();
                    if(!MultiWorldBridge.Available(context) && client!=null) context=MultiWorldBridge.Resolve(client);
                    Capture(__0,__1,context);
                }
            }
            catch(Exception e) { Fail(e); }
        }
        private static void RestorePage(PlayerDataFile __0,string __1,object __2)
        {
            if(!enabled) return;
            try
            {
                lock(sync)
                {
                    QuestPage page;
                    var b=Load(__1);
                    if(b.Pages.TryGetValue(MultiWorldBridge.Name(__2),out page))
                    {
                        QuestJournal journal=ReadJournal(Convert.FromBase64String(page.Journal));
                        __0.questJournal=journal;
                        Log.Out("[SevenDaysBeyond] QUEST_BOOK_RESTORED map="+page.Map+" quests="+journal.quests.Count);
                    }
                    else Capture(__0,__1,__2); // Import this map's existing native journal once.
                }
            }
            catch(Exception e) { Fail(e); }
        }
        private static void Tick()
        {
            if(!enabled || Time.realtimeSinceStartup<nextTick) return;
            nextTick=Time.realtimeSinceStartup+1;
            try
            {
                lock(sync)
                foreach(ClientInfo c in SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.List)
                {
                    if(!c.loginDone || c.InternalId==null || MultiWorldBridge.Transfer(c)!=null) continue;
                    var b=Load(c.InternalId.CombinedString);
                    object context=MultiWorldBridge.Resolve(c);
                    if(!MultiWorldBridge.Available(context) || context==null || !c.bAttachedToEntity) continue;
                    if(b.Test!=null && b.Test.Advance(MultiWorldBridge.Name(context),SharedClock.Now))
                    { b.Save(FilePath(b.Player)); ReportTest(c,b); }
                }
            }
            catch(Exception e) { Fail(e); }
        }
        private static void Say(ClientInfo client,string text) { MultiWorldBridge.Reply(client,"[Seven Days Beyond] "+text); }
        private static void ReportTest(ClientInfo c,QuestBookState b)
        {
            if(b.Test==null) { Say(c,"No travel test. Use /sdb questtest start (or start 30 for an expiry test)."); return; }
            var t=b.Test;
            string goal=t.Stage==0?t.Target:t.Home;
            Say(c,"Travel test: "+t.Status+"; "+t.Home+" -> "+t.Target+" -> "+t.Home+"; legs="+t.Stage+"/2; next="+goal+"; calendar ticks remaining="+(SharedClock.Now<t.Deadline?t.Deadline-SharedClock.Now:0)+". No item or XP rewards.");
        }
        private static ModEvents.EModEventResult OnChat(ref ModEvents.SChatMessageData data)
        {
            string message=data.Message??"";
            if(message!="/sdb quests" && !message.StartsWith("/sdb questtest",StringComparison.Ordinal)) return (ModEvents.EModEventResult)0;
            ClientInfo c=data.ClientInfo;
            if(!enabled || c==null || !c.loginDone || c.InternalId==null) return (ModEvents.EModEventResult)2;
            try
            {
                lock(sync)
                {
                    if(MultiWorldBridge.Transfer(c)!=null) { Say(c,"Wait for the crossing to finish."); return (ModEvents.EModEventResult)2; }
                    object context=MultiWorldBridge.Resolve(c);
                    if(!MultiWorldBridge.Available(context)) return (ModEvents.EModEventResult)2;
                    string id=c.InternalId.CombinedString;
                    Capture(c.latestPlayerData,id,context);
                    var b=Load(id);
                    if(message=="/sdb quests")
                    {
                        foreach(var p in b.Pages.Values.OrderBy(p=>p.Map,StringComparer.Ordinal))
                        {
                            Say(c,p.Map+(p.Map==MultiWorldBridge.Name(context)?" (current map)":" (away; objectives inactive)")+": "+p.Summary.Length+" native quests.");
                            foreach(string line in p.Summary.Take(12)) Say(c,p.Map+": "+line);
                            if(p.Summary.Length>12) Say(c,"Further entries retained in the quest book; native journal shows this map's full list.");
                        }
                        ReportTest(c,b);
                    }
                    else
                    {
                        string[] parts=message.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
                        if(parts.Length>=3 && parts[2]=="start")
                        {
                            int seconds=300;
                            if(parts.Length>5 || (parts.Length>=4 && !int.TryParse(parts[3],out seconds)) || seconds<10 || seconds>1800) Say(c,"Use /sdb questtest start [10-1800 seconds] [target map].");
                            else if(b.Test!=null && b.Test.Status=="active") Say(c,"A test is active. Use /sdb questtest cancel first.");
                            else
                            {
                                string home=MultiWorldBridge.Name(context),target=parts.Length==5?parts[4]:BorderPolicy.Destination(home,BorderPolicy.Grid.FirstExit(home));
                                if(target==home || !BorderPolicy.Grid.Cells.Any(cell=>cell.Name==target))
                                { Say(c,"Choose a different configured map: "+string.Join(", ",BorderPolicy.Grid.Cells.Select(cell=>cell.Name))); return (ModEvents.EModEventResult)2; }
                                ulong budget=checked((ulong)seconds*(ulong)Math.Max(1,GameStats.GetInt(EnumGameStats.TimeOfDayIncPerSec)));
                                b.Test=new TravelQuest {Home=home,Target=target,Deadline=checked(SharedClock.Now+budget)};
                                b.Save(FilePath(id)); ReportTest(c,b);
                            }
                        }
                        else if(parts.Length==3 && parts[2]=="cancel")
                        { if(b.Test!=null && b.Test.Status=="active") { b.Test.Status="cancelled"; b.Save(FilePath(id)); } ReportTest(c,b); }
                        else { ReportTest(c,b); Say(c,"Native quest fixture: F1, givequest sdb_test_fieldwork. Kill 3 zombies on its owning map; progress is stored in that map's page."); }
                    }
                }
            }
            catch(Exception e) { Fail(e); Say(c,"Quest book unavailable; see server log. Native map saves remain in use."); }
            return (ModEvents.EModEventResult)2;
        }
        private static void Fail(Exception e)
        { enabled=false; Log.Error("[SevenDaysBeyond] QUEST_BOOK_DISABLED; native journals retained: "+e); }
        private static void NativeSelfTest()
        {
            Quest q=QuestClass.CreateQuest("sdb_test_fieldwork");
            if(q==null) throw new InvalidOperationException("Native test quest XML did not load.");
            var j=new QuestJournal(); q.OwnerJournal=j; j.quests.Add(q);
            // Accepted quests receive an identity from SetupQuestCode, which needs a
            // local player. Supply one only for this unsaved headless fixture.
            q.QuestUniqueId="sdb-self-test-"+Guid.NewGuid().ToString("N");
            q.QuestCode=q.QuestUniqueId.GetHashCode();
            q.Objectives[0].CurrentValue=1;
            byte[] bytes=WriteJournal(j); var copy=ReadJournal(bytes);
            if(copy.quests.Count!=1 || copy.quests[0].ID!=q.ID || copy.quests[0].Objectives[0].CurrentValue!=1) throw new InvalidDataException("Native quest progress roundtrip failed.");
            Log.Out("[SevenDaysBeyond] QUEST_BOOK_NATIVE_SELF_TEST_PASS actual game quest serialized/read with partial objective progress; no player changes.");
        }
    }
}

