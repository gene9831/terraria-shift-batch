using System;
using System.Reflection;
using HarmonyLib;
using TerrariaModder.Core.Config;

namespace ShiftBatch
{
    // Core turns raw JSON doubles into strings before invoking string setters.
    // Reject these first: 1.00000000000000001 must not silently become "1".
    internal static class ConfigInputGuard
    {
        private const string Owner = "gene.terrariamodder.shift-batch.config";
        private static readonly object Gate = new object();
        private static Harmony harmony;

        internal static void EnsureInstalled()
        {
            lock (Gate)
            {
                if (harmony != null) return;
                var instance = new Harmony(Owner);
                // SetValue is tiny and may already be inlined into Core callers.
                // CoerceValue is the larger common conversion boundary and carries
                // metadata identifying our exact declaring config type.
                MethodInfo target = typeof(ConfigPropertyMeta).GetMethod("CoerceValue", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo prefix = typeof(ConfigInputGuard).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                try { instance.Patch(target, prefix: new HarmonyMethod(prefix)); harmony = instance; }
                catch { instance.UnpatchAll(Owner); throw; }
            }
        }

        private static bool Prefix(ConfigPropertyMeta __instance, object value, ref object __result)
        {
            if (__instance.Property.DeclaringType != typeof(BatchConfig) ||
                (__instance.Key != "PurchaseBatch" && __instance.Key != "CraftBatch")) return true;
            if (!(value is double) && !(value is float) && !(value is decimal)) return true;
            // Route through the regular string setter's invalid-input path so
            // the previous value is retained AND its warning is queued normally.
            __result = String.Empty;
            return false;
        }

        internal static void Uninstall()
        {
            lock (Gate)
            {
                if (harmony == null) return;
                harmony.UnpatchAll(Owner);
                harmony = null;
            }
        }
    }
}
