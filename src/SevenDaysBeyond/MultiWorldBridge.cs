using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace SevenDaysBeyond
{
    // All private upstream access lives here and is checked before installing patches.
    internal static class MultiWorldBridge
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        internal static Type Type, ContextType;
        private static MethodInfo resolve, command, reply, timeout, result;
        private static FieldInfo contexts, transfers, ready, world, name, closed, initialized, rollingBack, destination;
        private static ConstructorInfo lease;
        internal static void Bind(Type type)
        {
            Type = type;
            ContextType = type.GetNestedType("WorldContext", BindingFlags.NonPublic);
            Type requestType = type.GetNestedType("TransferRequest", BindingFlags.NonPublic);
            resolve = Method("ResolveClientWorld"); command = Method("OnWorldCommand");
            reply = Method("Reply"); timeout = Method("HandleTransferTimeout"); result = Method("SendTransferResult");
            contexts = Field(type, "Contexts", Static); transfers = Field(type, "Transfers", Static); ready = Field(type, "worldsReady", Static);
            world = Field(ContextType, "World", Instance); name = Field(ContextType, "Name", Instance);
            closed = Field(ContextType, "Closed", Instance); initialized = Field(ContextType, "Initialized", Instance);
            rollingBack = Field(requestType, "RollingBack", Instance); destination = Field(requestType, "Destination", Instance);
            lease = type.GetNestedType("WorldLease", BindingFlags.NonPublic).GetConstructor(Instance, null, new[] { ContextType }, null);
            if (lease == null) throw new MissingMethodException("WorldLease constructor");
        }
        internal static MethodInfo Method(string name)
        { return Type.GetMethods(Static).Single(m => m.Name == name); }
        private static FieldInfo Field(Type type, string name, BindingFlags flags)
        { return type.GetField(name, flags) ?? throw new MissingFieldException(type.FullName, name); }
        internal static bool Ready { get { return (bool)ready.GetValue(null); } }
        internal static object Resolve(ClientInfo c) { return resolve.Invoke(null, new object[] { c }); }
        internal static World World(object c) { return c == null ? null : (World)world.GetValue(c); }
        internal static string Name(object c) { return c == null ? "" : (string)name.GetValue(c); }
        internal static bool Available(object c) { return c != null && !(bool)closed.GetValue(c) && (bool)initialized.GetValue(c) && World(c) != null; }
        internal static object Find(string label)
        {
            // ConcurrentDictionary implements IEnumerable<KeyValuePair<World, Context>>.
            foreach (object pair in (IEnumerable)contexts.GetValue(null))
            {
                object c = pair.GetType().GetProperty("Value").GetValue(pair, null);
                if (Name(c) == label) return c;
            }
            return null;
        }
        internal static object Transfer(ClientInfo c) { return ((IDictionary)transfers.GetValue(null))[c]; }
        internal static bool RollingBack(object r) { return (bool)rollingBack.GetValue(r); }
        internal static object Destination(object r) { return destination.GetValue(r); }
        internal static IDisposable Scope(object c) { return (IDisposable)lease.Invoke(new[] { c }); }
        internal static void Reply(ClientInfo c, string text) { reply.Invoke(null, new object[] { c, text }); }
        internal static object Start(ClientInfo c, string target)
        {
            var chat = new ModEvents.SChatMessageData(c, (EChatType)0, c.entityId, "/world " + target, c.playerName, null);
            command.Invoke(null, new object[] { chat });
            return Transfer(c);
        }
        internal static void Recover(ClientInfo c, object request) { timeout.Invoke(null, new[] { (object)c, request }); }
        internal static void CancelBeforeSwitch(ClientInfo c, object request, string text)
        {
            ((IDictionary)transfers.GetValue(null)).Remove(c);
            result.Invoke(null, new[] { (object)c, request, false, text });
        }
    }
}
