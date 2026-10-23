using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace SevenDaysBeyond
{
    // Validate the pinned upstream contract before enabling optional travel patches.
    public sealed class ModApi : IModApi
    {
        public void InitMod(Mod mod)
        {
            ModEvents.GameStartDone.RegisterHandler(OnGameStartDone);
            Log.Out("[SevenDaysBeyond] Compatibility checks registered; border travel requires opt-in configuration.");
        }

        private static void OnGameStartDone(ref ModEvents.SGameStartDoneData data)
        {
            try
            {
                Assembly upstream = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(
                    a => a.GetName().Name == MultiWorldContract.AssemblyName);
                if (upstream == null)
                    throw new InvalidOperationException("LITW MultiWorld is not loaded.");

                string hash;
                using (var stream = File.OpenRead(upstream.Location))
                using (var sha = SHA256.Create())
                    hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (hash != MultiWorldContract.Sha256)
                    throw new InvalidOperationException("Loaded MultiWorld DLL differs from the inspected 0.6.2 binary.");

                Type type = upstream.GetType(MultiWorldContract.TypeName, true);
                MethodInfo[] methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (HookSpec hook in MultiWorldContract.Hooks)
                {
                    int matches = methods.Count(m => m.Name == hook.Name
                        && m.ReturnType.FullName == hook.ReturnType
                        && m.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(hook.Parameters));
                    if (matches != 1)
                        throw new InvalidOperationException("Missing or ambiguous hook: " + hook.Name);
                }
                Log.Out("[SevenDaysBeyond] UPSTREAM_CONTRACT_OK hooks=" + MultiWorldContract.Hooks.Length
                    + "; game=" + typeof(IModApi).Assembly.ManifestModule.ModuleVersionId
                    + "; upstream binary verified.");
                if (GameManager.IsDedicatedServer) BorderTravel.Install(type);
            }
            catch (Exception error)
            {
                Log.Warning("[SevenDaysBeyond] INITIALIZATION_FAILED: " + error.Message
                    + "; gameplay extensions remain inactive.");
            }
        }
    }
}
