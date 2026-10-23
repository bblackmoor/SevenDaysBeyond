using System;

namespace SevenDaysBeyond
{
    // Compiled into both the metadata checker and the eventual game assembly.
    internal sealed class HookSpec
    {
        public readonly string Name;
        public readonly string ReturnType;
        public readonly string[] Parameters;

        public HookSpec(string name, string returnType, params string[] parameters)
        {
            Name = name;
            ReturnType = returnType;
            Parameters = parameters;
        }
    }

    internal static class MultiWorldContract
    {
        public const string AssemblyName = "LITWMultiWorldProbe";
        public const string TypeName = "MultiWorldProbe";
        public const string Sha256 = "946e95cc22a2e27a1d7e8e2114554966f97970d42042088cee3577dbb6ce5bd6";
        private const string Context = "MultiWorldProbe+WorldContext";
        private const string Request = "MultiWorldProbe+TransferRequest";

        public static readonly HookSpec[] Hooks =
        {
            new HookSpec("OnWorldCommand", "ModEvents+EModEventResult", "ModEvents+SChatMessageData&"),
            new HookSpec("ResolveClientWorld", Context, "ClientInfo"),
            new HookSpec("AcceptTransferCheckpoint", "System.Void", "ClientInfo", "System.String"),
            new HookSpec("BeginSessionWorldChange", "System.Void", "ClientInfo", Request, Context),
            new HookSpec("RestoreSharedCharacter", "System.Void", "PlayerDataFile", "System.String", Context),
            new HookSpec("SaveSharedCharacter", "System.Void", "PlayerDataFile", "System.String"),
            new HookSpec("HandleTransferTimeout", "System.Void", "ClientInfo", Request),
            new HookSpec("CompleteTransferAfterDisconnect", "System.Void", "ClientInfo"),
            new HookSpec("UpdateBothWorlds", "System.Void"),
            new HookSpec("SimulatePhysics", "System.Void", Context),
            new HookSpec("ShouldAdvanceWorldTime", "System.Boolean", "World"),
            new HookSpec("PrepareClientTransfer", "System.Void", "System.String", "System.String")
        };
    }
}
