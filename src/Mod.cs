using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Terraria;
using Terraria.UI;
using TerrariaModder.Core;
using TerrariaModder.Core.Events;
using TerrariaModder.Core.Logging;

namespace ShiftBatch
{
    public sealed class Mod : ModBase, IModLifecycle
    {
        private const string HarmonyId = "gene.terrariamodder.shift-batch";
        private Harmony harmony;
        private ILogger log;
        private BatchConfig config;
        private string failure;

        public override void Initialize(ModContext context)
        {
            log = context.Logger;
            config = context.GetConfig<BatchConfig>();
            if (config == null) throw new InvalidOperationException("Shift Batch configuration was not loaded by Core.");
            DebugLog.Initialize(log, config);
            // Core may re-enable a mod with its existing config instance after
            // Unload removed the guard; construction is not guaranteed to repeat.
            ConfigInputGuard.EnsureInstalled();
            NativePatches.Config = config;
            NativePatches.Active = false;
            AutoStow.Logger = log;
            AutoStow.Reset();
            FrameEvents.OnPostUpdate += Update;
            FrameEvents.OnPostDraw += AutoStow.EndDraw;
            log.Info("Shift Batch: buy=" + config.PurchaseCount + ", craft=" + config.CraftCount + "; waiting for game content.");
            LogConfiguration("Loaded");
        }

        public void OnContentReady(ModContext context)
        {
            if (context.IsServer) return;
            try
            {
                string version = FileVersionInfo.GetVersionInfo(typeof(Main).Assembly.Location).FileVersion;
                LogRuntime();
                if (version != "1.4.5.8") throw new InvalidOperationException("This build targets Terraria 1.4.5.8; found " + version);
                BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                MethodInfo shop = typeof(ItemSlot).GetMethod("HandleShopSlot", flags);
                Type[] clickSignature = {typeof(Item[]), typeof(int), typeof(int)};
                MethodInfo shopLeft = typeof(ItemSlot).GetMethod("LeftClick", flags, null, clickSignature, null);
                MethodInfo shopRight = typeof(ItemSlot).GetMethod("RightClick", flags, null, clickSignature, null);
                MethodInfo crafting = typeof(Main).GetMethod("HoverOverCraftingItemButton", flags);
                Type requests = typeof(Main).Assembly.GetType("Terraria.GameContent.CraftingRequests", true);
                MethodInfo request = requests.GetMethod("CraftItem", flags, null, new Type[] {typeof(Recipe), typeof(int), typeof(bool)}, null);
                if (shop == null || shopLeft == null || shopRight == null || crafting == null || request == null) throw new MissingMethodException("Native bulk transaction methods not found.");

                // Validate both patterns before installing the first patch.
                NativePatches.ShopTranspiler(PatchProcessor.GetCurrentInstructions(shop)).ToList();
                NativePatches.CraftTranspiler(PatchProcessor.GetCurrentInstructions(crafting)).ToList();
                harmony = new Harmony(HarmonyId);
                harmony.Patch(typeof(ItemSlot).GetMethod("GetBulkBuyAmount", flags), postfix: Hook("BuyAmountPostfix"));
                harmony.Patch(shop, transpiler: Hook("ShopTranspiler"));
                harmony.Patch(shopLeft, prefix: StowHook("ShopLeftPrefix"), postfix: StowHook("ShopPostfix"));
                harmony.Patch(shopRight, prefix: StowHook("ShopRightPrefix"), postfix: StowHook("ShopPostfix"));
                harmony.Patch(crafting, prefix: StowHook("CraftPrefix"), postfix: StowHook("CraftPostfix"), transpiler: Hook("CraftTranspiler"));
                harmony.Patch(request, prefix: Hook("CraftRequestPrefix"));
                harmony.Patch(typeof(ItemSlot).GetMethod("GetBulkCraftAmount", flags), postfix: Hook("CraftAmountPostfix"));
                NativePatches.Active = true;
                log.Info("Shift Batch active: 7 patches installed; capacity guards enabled.");
                DebugLog.Write("Native patches ready: buy amount, shop capacity, left/right shop activity, craft hover, craft request and craft amount.");
            }
            catch (Exception ex)
            {
                NativePatches.Active = false;
                AutoStow.Reset();
                if (harmony != null) harmony.UnpatchAll(HarmonyId);
                ConfigInputGuard.Uninstall();
                failure = "Shift Batch disabled: " + ex.Message;
                log.Error(failure + Environment.NewLine + ex);
            }
        }

        private static HarmonyMethod Hook(string name)
        {
            return new HarmonyMethod(typeof(NativePatches).GetMethod(name, BindingFlags.Static | BindingFlags.Public));
        }

        private static HarmonyMethod StowHook(string name)
        {
            return new HarmonyMethod(typeof(AutoStow).GetMethod(name, BindingFlags.Static | BindingFlags.Public));
        }

        public void OnConfigChanged()
        {
            DebugLog.ApplySettings();
            AutoStow.Reset();
            log.Info("Shift Batch config: enabled=" + config.Enabled + ", buy=" + config.PurchaseCount + ", craft=" + config.CraftCount);
            LogConfiguration("Changed");
            LogRuntime();
        }

        private void LogConfiguration(string phase)
        {
            DebugLog.Write(phase + " configuration: enabled=" + config.Enabled + ", buy=" + config.PurchaseCount +
                ", craft=" + config.CraftCount + ", autoStow=" + config.AutoStow + ", debug=" + config.DebugLogging);
        }

        private void LogRuntime()
        {
            if (!DebugLog.Enabled) return;
            DebugLog.Write("Runtime: mod=" + typeof(Mod).Assembly.GetName().Version +
                "; build=" + typeof(Mod).Module.ModuleVersionId +
                "; Terraria=" + FileVersionInfo.GetVersionInfo(typeof(Main).Assembly.Location).FileVersion +
                "; Core=" + typeof(ModBase).Assembly.GetName().Version +
                "; Harmony=" + typeof(Harmony).Assembly.GetName().Version + "; configSchema=" + config.Version);
        }

        private void Update()
        {
            AutoStow.CancelIfInactive();
            foreach (string warning in config.TakeWarnings())
            {
                log.Warn(warning);
                if (!Main.gameMenu) Main.NewText("[Shift Batch] " + warning, 255, 200, 90);
            }
            if (failure != null && !Main.gameMenu)
            {
                Main.NewText(failure, 255, 120, 90);
                failure = null;
            }
        }

        public void OnWorldLoad() { AutoStow.Reset(); DebugLog.Write("World loaded; auto-stow reset."); }
        public void OnWorldUnload() { AutoStow.Reset(); DebugLog.Write("World unloaded; auto-stow reset."); }

        public override void Unload()
        {
            NativePatches.Active = false;
            AutoStow.Reset();
            AutoStow.Logger = null;
            FrameEvents.OnPostUpdate -= Update;
            FrameEvents.OnPostDraw -= AutoStow.EndDraw;
            if (harmony != null) harmony.UnpatchAll(HarmonyId);
            ConfigInputGuard.Uninstall();
            NativePatches.Config = null;
            DebugLog.Write("Mod unloaded; owned hooks removed.");
            DebugLog.Unload();
        }
    }
}
