using System;
using System.Reflection;
using HarmonyLib;

namespace SevenDaysBeyond
{
    internal static class PlayerMessages
    {
        internal static void Install(Type upstream)
        {
            // MultiWorld owns cancellation cleanup. Translate only Beyond's keyed
            // cancellation text while leaving the upstream protocol and messages intact.
            var harmony = new Harmony("org.blackmoor.sevendaysbeyond.localization");
            try
            {
                harmony.Patch(AccessTools.Method(upstream, "Reply"), prefix: Hook("BeforeReply"));
                harmony.Patch(AccessTools.Method(upstream, "ClientWorldResult"), prefix: Hook("BeforeResult"));
            }
            catch { harmony.UnpatchSelf(); throw; }
        }
        private static HarmonyMethod Hook(string name)
        { return new HarmonyMethod(typeof(PlayerMessages).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)); }
        private static bool BeforeReply(ClientInfo __0, string __1)
        {
            if (__1 != "sdb_crossing_ground_changed") return true;
            Send(__0, __1); return false;
        }
        private static void BeforeResult(ref string __1)
        {
            if (!GameManager.IsDedicatedServer && __1 == "sdb_crossing_ground_changed")
                __1 = Render(LocalizedText.Key(__1));
        }
        internal static LocalizedText Key(string key) { return LocalizedText.Key(key); }
        internal static LocalizedText Direction(BorderEdge edge)
        { return Key("sdb_direction_" + edge.ToString().ToLowerInvariant()); }
        internal static void Send(ClientInfo client, string key, params object[] arguments)
        { Send(client, LocalizedText.Message(key, arguments)); }
        internal static void Send(ClientInfo client, LocalizedText message)
        {
            if (client == null) return;
            // A display/connection failure must not disable quest saving or border safety.
            try { client.SendPackage(NetPackageManager.GetPackage<NetPackageSdbLocalizedMessage>().Setup(message)); }
            catch (Exception e) { Log.Warning("[SevenDaysBeyond] LOCALIZED_MESSAGE_SEND_FAILED: " + e.Message); }
        }
        private static string Render(LocalizedText message)
        { return message.Render(k => Localization.Get(k), k => Localization.Get(k, false, "english")); }
        internal static void Display(LocalizedText message, GameManager manager)
        {
            // Dedicated servers never resolve player-facing text into their own language.
            if (GameManager.IsDedicatedServer || manager == null) return;
            var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (connection == null || connection.IsServer || !connection.IsConnected) return;
            try
            {
                string text = Render(message);
                manager.ChatMessageClient(EChatType.Whisper, -1, "[Seven Days Beyond] " + text,
                    null, EMessageSender.Server, GeneratedTextManager.BbCodeSupportMode.NotSupported);
            }
            catch (Exception e) { Log.Warning("[SevenDaysBeyond] LOCALIZED_MESSAGE_FAILED: " + e.Message); }
        }
    }

    // Discovered and mapped by the game's standard NetPackageManager on both sides.
    public sealed class NetPackageSdbLocalizedMessage : NetPackage
    {
        private LocalizedText message;
        public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }
        internal NetPackageSdbLocalizedMessage Setup(LocalizedText value) { message = value; return this; }
        public override void write(PooledBinaryWriter writer) { base.write(writer); message.Write(writer); }
        public override void read(PooledBinaryReader reader) { message = null; message = LocalizedText.Read(reader); }
        public override void ProcessPackage(World world, GameManager manager)
        {
            if (message != null) PlayerMessages.Display(message, manager);
        }
    }
}
