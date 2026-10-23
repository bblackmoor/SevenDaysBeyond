using System;
using System.Reflection;
using HarmonyLib;

namespace SevenDaysBeyond
{
    // Session-only state: never grant modes on a normal login or serialize them into map saves.
    internal static class ClientDebugState
    {
        private sealed class Snapshot
        {
            internal string Token;
            internal object Connection;
            internal EntityPlayerLocal Source;
            internal bool God, Fly, NoCollision, DebugMenu;
            internal float Speed;
        }
        private static bool installed;
        private static FieldInfo transferToken, sessionEpoch;
        private static Snapshot pending;

        internal static void Install(Type upstream)
        {
            if(installed) return;
            transferToken=AccessTools.Field(upstream,"clientTransferToken");
            sessionEpoch=AccessTools.Field(upstream,"clientSessionEpoch");
            if(transferToken==null || transferToken.FieldType!=typeof(string)
                || sessionEpoch==null || sessionEpoch.FieldType!=typeof(int))
                throw new MissingFieldException("MultiWorld client transfer identity changed.");
            MethodInfo change=AccessTools.Method(upstream,"SwitchClientWorld",new[]{typeof(string),typeof(string),typeof(int)});
            MethodInfo result=AccessTools.Method(upstream,"ClientWorldResult",new[]{typeof(string),typeof(string),typeof(bool)});
            if(change==null || result==null) throw new MissingMethodException("MultiWorld client transfer hooks changed.");
            var harmony=new Harmony("org.blackmoor.sevendaysbeyond.client-debug-state");
            try
            {
                harmony.Patch(change,prefix:Hook("Capture"));
                harmony.Patch(AccessTools.Method(typeof(GameManager),"setLocalPlayerEntity",new[]{typeof(EntityPlayerLocal)}),postfix:Hook("LocalPlayerCreated"));
                harmony.Patch(AccessTools.Method(typeof(EntityPlayerLocal),"onSpawnStateChanged"),postfix:Hook("PlayerSpawned"));
                harmony.Patch(result,prefix:Hook("BeforeResult"),postfix:Hook("AfterResult"));
                harmony.Patch(AccessTools.Method(typeof(ConnectionManager),"Connect"),prefix:Hook("NewConnection"));
                installed=true;
                Log.Out("[SevenDaysBeyond] CLIENT_DEBUG_STATE_ENABLED god/fly/noclip/speed/debug-menu preserved within authenticated map transfers.");
            }
            catch { harmony.UnpatchSelf(); throw; }
        }
        private static HarmonyMethod Hook(string name)
        { return new HarmonyMethod(typeof(ClientDebugState).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)); }
        private static string Token { get { return (string)transferToken.GetValue(null); } }
        private static object Connection()
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(manager==null || manager.IsServer || !manager.IsConnected) return null;
            var connections=manager.GetConnectionToServer();
            return connections==null || connections.Length==0?null:connections[0];
        }
        private static void NewConnection() { pending=null; }
        private static void Capture(string __0, int __2)
        {
            try
            {
                object connection=Connection();
                if(connection==null || __0==null || __0!=Token || __2<=(int)sessionEpoch.GetValue(null)) return;
                // Recovery uses the same token with a newer epoch. Retain the original snapshot.
                if(pending!=null && pending.Token==__0 && ReferenceEquals(pending.Connection,connection)) return;
                EntityPlayerLocal player=GameManager.Instance.World?.GetPrimaryPlayer();
                pending=player==null?null:new Snapshot { Token=__0,Connection=connection,Source=player,
                    God=player.IsGodMode.Value,Fly=player.IsFlyMode.Value,NoCollision=player.IsNoCollisionMode.Value,
                    Speed=player.GodModeSpeedModifier,DebugMenu=GamePrefs.GetBool(EnumGamePrefs.DebugMenuEnabled) };
            }
            catch(Exception e) { pending=null; Log.Warning("[SevenDaysBeyond] CLIENT_DEBUG_CAPTURE_FAILED: "+e.Message); }
        }
        private static void LocalPlayerCreated(EntityPlayerLocal __0) { Restore(__0); }
        private static void PlayerSpawned(EntityPlayerLocal __instance) { Restore(__instance); }
        private static void Restore(EntityPlayerLocal player)
        {
            try
            {
                if(pending==null || player==null || ReferenceEquals(player,pending.Source)
                    || pending.Token!=Token || !ReferenceEquals(pending.Connection,Connection())) return;
                GamePrefs.Set(EnumGamePrefs.DebugMenuEnabled,pending.DebugMenu);
                // Assign independent values: God Mode with flight off must stay that way.
                player.IsGodMode.Value=pending.God;
                player.IsFlyMode.Value=pending.Fly;
                player.IsNoCollisionMode.Value=pending.NoCollision;
                player.GodModeSpeedModifier=pending.Speed;
            }
            catch(Exception e) { pending=null; Log.Warning("[SevenDaysBeyond] CLIENT_DEBUG_RESTORE_FAILED: "+e.Message); }
        }
        private static void BeforeResult(string __0, bool __2, out bool __state)
        {
            __state=pending!=null && __0==pending.Token && __0==Token && __2;
            if(__state) Restore(GameManager.Instance.World?.GetPrimaryPlayer());
        }
        private static void AfterResult(bool __state)
        {
            if(!__state || pending==null || Token!=null) return;
            Log.Out("[SevenDaysBeyond] CLIENT_DEBUG_STATE_RESTORED god="+pending.God+" fly="+pending.Fly
                +" noclip="+pending.NoCollision+" speed="+pending.Speed);
            pending=null;
        }
    }
}
