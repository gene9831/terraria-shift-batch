using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

internal static class Tests
{
    private static int count;
    private static string gamePath;

    public static int Main(string[] args)
    {
        gamePath = args[0];
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        try { PrepareGameEnvironment(); Run(); Console.WriteLine("PASS: " + count + " assertions"); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void PrepareGameEnvironment()
    {
        // Main's static initializer combines Program.SavePath with Players/Worlds.
        // Supply a work-directory sandbox BEFORE any method touching Main is JITted.
        string sandbox = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "game-sandbox");
        Directory.CreateDirectory(sandbox);
        Assembly game = Assembly.LoadFrom(Path.Combine(gamePath, "Terraria.exe"));
        game.GetType("Terraria.Program", true).GetField("SavePath").SetValue(null, sandbox);
    }

    private static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name;
        string path = name == "Terraria" ? Path.Combine(gamePath, "Terraria.exe") :
            name == "TerrariaModder.Core" ? Path.Combine(gamePath, "TerrariaModder/core/TerrariaModder.Core.dll") :
            name == "0Harmony" ? Path.Combine(gamePath, "TerrariaModder/core/deps/0Harmony.dll") : null;
        if (path != null) return Assembly.LoadFrom(path);
        // Terraria embeds ReLogic and its other managed dependencies; its normal
        // entry point installs a resolver. Tests inspect IL without running that entry point.
        Assembly game = Assembly.LoadFrom(Path.Combine(gamePath, "Terraria.exe"));
        string resource = game.GetManifestResourceNames().FirstOrDefault(x => x.EndsWith("." + name + ".dll", StringComparison.OrdinalIgnoreCase));
        if (resource == null) return null;
        using (Stream input = game.GetManifestResourceStream(resource))
        using (var bytes = new MemoryStream())
        {
            input.CopyTo(bytes);
            return Assembly.Load(bytes.ToArray());
        }
    }

    private static void Equal<T>(T expected, T actual, string name)
    {
        count++;
        if (!Object.Equals(expected, actual)) throw new Exception(name + ": expected " + expected + ", got " + actual);
    }

    // These fixtures catch decimal rounding, invalid-value loss, and stack overshoot.
    private static void Run()
    {
        Type rules = Find("ShiftBatch.BatchRules");
        Equal(true, rules != null, "batch rules exist");
        if (rules == null) return;
        Normalize(rules, "10", 100, true, 10);
        Normalize(rules, "1000", 10, true, 1000);
        Normalize(rules, "9999", 10, true, 9999);
        Normalize(rules, "10000", 10, true, 9999);
        Normalize(rules, "0", 10, true, 1);
        Normalize(rules, "-100", 10, true, 1);
        Normalize(rules, " 100 ", 10, true, 100);
        foreach (string text in new string[] {null,""," ","abc","1.5","1e3","999999999999999999999999"})
            Normalize(rules, text, 100, false, 100);
        Equal(9999, InvokeInt(rules, "PurchaseLimit", 50000, 9999, 0, Int32.MaxValue), "accelerated purchase cap");
        Equal(99, InvokeInt(rules, "PurchaseLimit", 9999, 9999, 9900, Int32.MaxValue), "nearly full purchase");
        Equal(1, InvokeInt(rules, "PurchaseLimit", 9999, 1, 0, Int32.MaxValue), "nonstacking purchase");
        Equal(7, InvokeInt(rules, "PurchaseLimit", 9999, 9999, 0, 7), "finite stock");
        Equal(0, InvokeInt(rules, "PurchaseLimit", 9999, 9999, 9999, 7), "full cursor");
        Equal(399, InvokeInt(rules, "CraftLimit", 9999, 9999, 0, 25), "25 outputs per craft");
        Equal(3, InvokeInt(rules, "CraftLimit", 9999, 9999, 9900, 25), "existing craft stack");
        Equal(0, InvokeInt(rules, "CraftLimit", 9999, 9999, 9990, 25), "incomplete recipe capacity");
        Equal(1, InvokeInt(rules, "CraftLimit", 9999, 1, 0, 1), "nonstacking craft");
        Equal(0, InvokeInt(rules, "CraftLimit", 9999, 9999, 0, 0), "invalid recipe output");
        Equal(0, InvokeInt(rules, "CraftLimit", 9999, 9999, 0, Int32.MaxValue), "huge recipe output");
        Equal(1, InvokeInt(rules, "CraftLimit", 9999, 9999, 9998, 1), "one remaining slot");
        Equal(0, InvokeInt(rules, "CraftLimit", Int32.MaxValue, 9999, Int32.MaxValue, 25), "corrupt cursor cannot overflow arithmetic");
        CheckConfig();
        CheckConfigFiles();
        CheckNativeIntegration();
        CheckModLifecycle();
    }

    private static Type Find(string name) { return Assembly.GetExecutingAssembly().GetType(name); }
    private static int InvokeInt(Type type, string method, params object[] args)
    { return (int)type.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args); }
    private static void Normalize(Type type, string text, int previous, bool accepted, int expected)
    {
        object[] args = { text, previous, 0 };
        bool actual = (bool)type.GetMethod("TryNormalize").Invoke(null, args);
        Equal(accepted, actual, "accept " + text);
        Equal(expected, (int)args[2], "value " + text);
    }
    private static void CheckConfig()
    {
        Type type = Find("ShiftBatch.BatchConfig");
        Equal(true, type != null, "config exists");
        object config = Activator.CreateInstance(type);
        PropertyInfo buy = type.GetProperty("PurchaseBatch");
        PropertyInfo craft = type.GetProperty("CraftBatch");
        Equal("10", (string)buy.GetValue(config, null), "purchase default");
        Equal("10", (string)craft.GetValue(config, null), "craft default");
        buy.SetValue(config, "1000", null);
        craft.SetValue(config, "100", null);
        buy.SetValue(config, "abc", null);
        Equal("1000", (string)buy.GetValue(config, null), "invalid edit retains purchase");
        Equal("100", (string)craft.GetValue(config, null), "separate craft setting");
        buy.SetValue(config, "10000", null);
        Equal("9999", (string)buy.GetValue(config, null), "config clamp");
        // Test the REAL Core conversion route, rather than just our property setter.
        var modConfig = (TerrariaModder.Core.Config.ModConfig)config;
        foreach (var meta in modConfig.GetPropertyMetadata())
        {
            if (meta.Key == "PurchaseBatch")
            {
                meta.SetValue(modConfig, 1.5);
                Equal("9999", (string)buy.GetValue(config, null), "raw JSON fractional number rejected");
                meta.SetValue(modConfig, false);
                Equal("9999", (string)buy.GetValue(config, null), "raw JSON boolean rejected");
                meta.SetValue(modConfig, null);
                Equal("9999", (string)buy.GetValue(config, null), "raw JSON null rejected");
                meta.SetValue(modConfig, 1000);
                Equal("1000", (string)buy.GetValue(config, null), "raw JSON integer accepted");
                meta.SetValue(modConfig, 1.00000000000000001);
                Equal("1000", (string)buy.GetValue(config, null), "raw floating number cannot round into valid integer");
            }
        }
        var other = new OtherModConfig();
        other.GetPropertyMetadata().First(x => x.Key == "PurchaseBatch").SetValue(other, 1.5);
        Equal(2, other.PurchaseBatch, "raw-number guard does not change another mod's settings");
    }

    private static void CheckNativeIntegration()
    {
        Type patches = Find("ShiftBatch.NativePatches");
        Equal(true, patches != null, "native integration exists");
        if (patches == null) return;
        Assembly game = Assembly.LoadFrom(Path.Combine(gamePath, "Terraria.exe"));
        Type itemSlot = game.GetType("Terraria.UI.ItemSlot", true);
        Type main = game.GetType("Terraria.Main", true);
        BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        MethodInfo buy = itemSlot.GetMethod("HandleShopSlot", flags);
        MethodInfo craft = main.GetMethod("HoverOverCraftingItemButton", flags);
        List<CodeInstruction> buyIL = PatchProcessor.GetOriginalInstructions(buy).ToList();
        List<CodeInstruction> craftIL = PatchProcessor.GetOriginalInstructions(craft).ToList();
        List<CodeInstruction> changedBuy = Transform(patches, "ShopTranspiler", buyIL);
        List<CodeInstruction> changedCraft = Transform(patches, "CraftTranspiler", craftIL);
        Equal(1, CountCalls(changedBuy, "LimitShopCount"), "native purchase loop receives capacity guard");
        Equal(1, CountCalls(changedCraft, "GetCraftBatch"), "native Shift multiplier is configurable");
        Equal(1, CountCalls(changedCraft, "CraftItem"), "native crafting transaction retained");
        Equal(1, CountCalls(changedBuy, "BuyItem"), "native payment retained");
        Equal(1, CountCalls(changedBuy, "GetItemExpectedPrice"), "native dynamic price retained");
        Equal(1, CountCalls(changedBuy, "RefreshStackSplitCooldown"), "native purchase repeat retained");
        Equal(1, CountCalls(changedCraft, "RefreshStackSplitCooldown"), "native crafting repeat retained");
        Equal(true, changedBuy.Count > buyIL.Count, "purchase IL changes only by insertion");
        Equal(craftIL.Count, changedCraft.Count, "craft IL substitutes one multiplier instruction");
        foreach (string name in new string[] {"ShopTranspiler","CraftTranspiler"})
        {
            bool rejected = false;
            try { Transform(patches, name, new List<CodeInstruction> { new CodeInstruction(OpCodes.Ret) }); }
            catch (TargetInvocationException e) { rejected = e.InnerException is InvalidOperationException; }
            Equal(true, rejected, "unsupported IL rejected: " + name);
        }
        CheckHarmonyInstall(patches, game, itemSlot, main, flags);
        CheckRuntimeGuards();
    }

    private static void CheckConfigFiles()
    {
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-configs");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "shift-batch.client.json");
        File.WriteAllText(path, "{\"_version\":1,\"Enabled\":true,\"PurchaseBatch\":1.5,\"CraftBatch\":\"1000\"}");
        var config = TerrariaModder.Core.Config.ConfigManager.LoadForMod("shift-batch", Assembly.GetExecutingAssembly(), root, null, null);
        Type type = config.GetType();
        Equal("10", (string)type.GetProperty("PurchaseBatch").GetValue(config, null), "invalid disk value defaults to 10");
        Equal("1000", (string)type.GetProperty("CraftBatch").GetValue(config, null), "separate disk value loads");
        type.GetProperty("PurchaseBatch").SetValue(config, "100", null);
        File.WriteAllText(path, "{\"_version\":1,\"PurchaseBatch\":\"abc\",\"CraftBatch\":100000}");
        config.Reload();
        Equal("100", (string)type.GetProperty("PurchaseBatch").GetValue(config, null), "invalid disk reload retains 100");
        Equal("9999", (string)type.GetProperty("CraftBatch").GetValue(config, null), "disk reload clamps high number");
        config.Save();
        var restored = TerrariaModder.Core.Config.ConfigManager.LoadForMod("shift-batch", Assembly.GetExecutingAssembly(), root, null, null);
        Equal("100", (string)type.GetProperty("PurchaseBatch").GetValue(restored, null), "saved purchase round trip");
        Equal("9999", (string)type.GetProperty("CraftBatch").GetValue(restored, null), "saved craft round trip");
        foreach (string token in new string[] {"1.00000000000000001","1000.000000000001","1e3"})
        {
            File.WriteAllText(path, "{\"_version\":1,\"PurchaseBatch\":" + token + "}");
            config.Reload();
            Equal("100", (string)type.GetProperty("PurchaseBatch").GetValue(config, null), "raw numeric token retained: " + token);
            var initial = TerrariaModder.Core.Config.ConfigManager.LoadForMod("shift-batch", Assembly.GetExecutingAssembly(), root, null, null);
            Equal("10", (string)type.GetProperty("PurchaseBatch").GetValue(initial, null), "invalid raw token on first load defaults: " + token);
        }
    }

    private static void CheckHarmonyInstall(Type patches, Assembly game, Type itemSlot, Type main, BindingFlags flags)
    {
        string owner = "shift-batch.offline-tests";
        var harmony = new Harmony(owner);
        var installed = new List<MethodInfo>();
        try
        {
            MethodInfo[] targets = {
                itemSlot.GetMethod("GetBulkBuyAmount", flags),
                itemSlot.GetMethod("HandleShopSlot", flags),
                main.GetMethod("HoverOverCraftingItemButton", flags),
                game.GetType("Terraria.GameContent.CraftingRequests").GetMethod("CraftItem", flags),
                itemSlot.GetMethod("GetBulkCraftAmount", flags)
            };
            string[] names = {"BuyAmountPostfix","ShopTranspiler","CraftTranspiler","CraftRequestPrefix","CraftAmountPostfix"};
            for (int i = 0; i < targets.Length; i++)
            {
                var hook = new HarmonyMethod(patches.GetMethod(names[i]));
                if (i == 0 || i == 4) harmony.Patch(targets[i], postfix: hook);
                else if (i == 3) harmony.Patch(targets[i], prefix: hook);
                else harmony.Patch(targets[i], transpiler: hook);
                installed.Add(targets[i]);
                Equal(true, Harmony.GetPatchInfo(targets[i]).Owners.Contains(owner), "actual Harmony install " + names[i]);
            }
        }
        finally
        {
            harmony.UnpatchAll(owner);
        }
        foreach (MethodInfo target in installed)
        {
            Patches info = Harmony.GetPatchInfo(target);
            Equal(false, info != null && info.Owners.Contains(owner), "actual Harmony uninstall " + target.Name);
        }
    }

    private static void CheckRuntimeGuards()
    {
        // Real Terraria objects in this isolated test process; no game entry point,
        // world, player, disk save, or Steam connection is started.
        var config = new ShiftBatch.BatchConfig();
        ShiftBatch.NativePatches.Config = config;
        ShiftBatch.NativePatches.Active = true;
        Terraria.UI.ItemSlot.ShiftForcedOn = true;
        try
        {
            var item = (Terraria.Item)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Terraria.Item));
            item.type = 1; item.stack = 9999; item.maxStack = 9999; item.isAShopItem = true;
            var cursor = (Terraria.Item)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Terraria.Item));
            cursor.type = 0; cursor.stack = 0;
            Terraria.Main.mouseItem = cursor;
            config.PurchaseBatch = "9999";
            Equal(9999, ShiftBatch.NativePatches.LimitShopCount(50000, item), "real purchase guard caps acceleration");
            cursor.type = 1; cursor.stack = 9900; cursor.maxStack = 9999;
            Equal(99, ShiftBatch.NativePatches.LimitShopCount(50000, item), "real purchase guard considers existing stack");
            item.buyOnce = true; item.stack = 7;
            Equal(7, ShiftBatch.NativePatches.LimitShopCount(50000, item), "real purchase guard considers finite stock");
            cursor.type = 2;
            Equal(0, ShiftBatch.NativePatches.LimitShopCount(9999, item), "incompatible purchase cursor stops");
            var recipe = (Terraria.Recipe)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Terraria.Recipe));
            item.stack = 25; recipe.createItem = item;
            cursor.type = 0; cursor.stack = 0;
            int qty = 9999;
            Equal(true, ShiftBatch.NativePatches.CraftRequestPrefix(recipe, ref qty, false), "real multi-output craft allowed");
            Equal(399, qty, "real multi-output craft capped");
            cursor.type = 1; cursor.stack = 9900;
            qty = 9999;
            ShiftBatch.NativePatches.CraftRequestPrefix(recipe, ref qty, false);
            Equal(3, qty, "real multi-output craft with existing stack");
            cursor.stack = 9990;
            qty = 9999;
            Equal(false, ShiftBatch.NativePatches.CraftRequestPrefix(recipe, ref qty, false), "incomplete final craft does not execute");
            Equal(0, qty, "incomplete final craft count zero");
            cursor.type = 2; qty = 9999;
            Equal(false, ShiftBatch.NativePatches.CraftRequestPrefix(recipe, ref qty, false), "incompatible craft cursor stops");
            config.Enabled = false; qty = 10;
            Equal(true, ShiftBatch.NativePatches.CraftRequestPrefix(recipe, ref qty, false), "disabled uses native craft");
            Equal(10, qty, "disabled craft count unchanged");
            Equal(10000, ShiftBatch.NativePatches.LimitShopCount(10000, item), "disabled buy count unchanged");
            Equal(10, ShiftBatch.NativePatches.GetCraftBatch(), "disabled multiplier is vanilla ten");
            config.Enabled = true; config.CraftBatch = "1000";
            Equal(1000, ShiftBatch.NativePatches.GetCraftBatch(), "craft multiplier uses separate setting");
            Terraria.UI.ItemSlot.ShiftForcedOn = false;
            qty = 1;
            Equal(true, ShiftBatch.NativePatches.CraftRequestPrefix(recipe, ref qty, false), "non-Shift craft remains native");
            Equal(1, qty, "non-Shift craft count unchanged");
            Equal(5, ShiftBatch.NativePatches.LimitShopCount(5, item), "non-Shift buy count unchanged");
        }
        finally
        {
            Terraria.UI.ItemSlot.ShiftForcedOn = false;
            ShiftBatch.NativePatches.Active = false;
            ShiftBatch.NativePatches.Config = null;
        }
    }

    private static List<CodeInstruction> Transform(Type patches, string method, List<CodeInstruction> input)
    {
        return ((IEnumerable<CodeInstruction>)patches.GetMethod(method, BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] {input})).ToList();
    }

    private static int CountCalls(List<CodeInstruction> il, string method)
    {
        return il.Count(x => (x.opcode == OpCodes.Call || x.opcode == OpCodes.Callvirt) &&
            x.operand is MethodInfo && ((MethodInfo)x.operand).Name == method);
    }

    private static void CheckModLifecycle()
    {
        string root = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).FullName;
        var manifest = TerrariaModder.Core.Manifest.ManifestParser.Parse(Path.Combine(root, "manifest.json"));
        if (!manifest.IsValid) Console.WriteLine("Manifest errors: " + String.Join("; ", manifest.ValidationErrors));
        Equal(true, manifest.IsValid, "real Core manifest parser accepts package");
        Equal("shift-batch", manifest.Id, "manifest identity");
        var logger = new TestLogger();
        var config = new ShiftBatch.BatchConfig();
        var context = new TerrariaModder.Core.ModContext(logger, root, manifest, config);
        var mod = new ShiftBatch.Mod();
        try
        {
            mod.Initialize(context);
            Equal(false, ShiftBatch.NativePatches.Active, "patches deferred until content ready");
            mod.OnContentReady(context);
            Equal(true, ShiftBatch.NativePatches.Active, "actual mod lifecycle activates patches");
            Equal(0, logger.Errors.Count, "actual mod lifecycle logs no errors");
            config.PurchaseBatch = "100"; config.CraftBatch = "1000";
            mod.OnConfigChanged();
            Equal(1000, ShiftBatch.NativePatches.GetCraftBatch(), "live config update used without restart");
            var output = (Terraria.Item)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Terraria.Item));
            output.type = 1; output.stack = 25; output.maxStack = 9999;
            var empty = (Terraria.Item)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Terraria.Item));
            Terraria.Main.mouseItem = empty;
            Equal(100, Terraria.UI.ItemSlot.GetBulkBuyAmount(output), "actual detoured purchase helper reads config");
            Equal(399, Terraria.UI.ItemSlot.GetBulkCraftAmount(output), "actual detoured craft tooltip respects multi-output cap");
            mod.OnWorldLoad(); mod.OnWorldUnload();
        }
        finally { mod.Unload(); }
        Equal(false, ShiftBatch.NativePatches.Active, "mod unload deactivates callbacks");
        Equal(true, ShiftBatch.NativePatches.Config == null, "mod unload releases config");
        Equal(false, Harmony.GetAllPatchedMethods().Any(x => Harmony.GetPatchInfo(x).Owners.Any(owner => owner.StartsWith("gene.terrariamodder.shift-batch", StringComparison.Ordinal))), "mod unload removes native and config detours");
        try
        {
            mod.Initialize(context);
            config.PurchaseBatch = "100";
            config.GetPropertyMetadata().First(x => x.Key == "PurchaseBatch").SetValue(config, 1.00000000000000001);
            Equal("100", config.PurchaseBatch, "reinitializing an existing config reinstalls validation");
        }
        finally { mod.Unload(); }
        var recreated = new ShiftBatch.BatchConfig();
        recreated.PurchaseBatch = "100";
        foreach (var meta in recreated.GetPropertyMetadata())
            if (meta.Key == "PurchaseBatch") meta.SetValue(recreated, 1.00000000000000001);
        Equal("100", recreated.PurchaseBatch, "config guard reinstalls after unload/recreate");
        ShiftBatch.ConfigInputGuard.Uninstall();
    }

    private sealed class TestLogger : TerrariaModder.Core.Logging.ILogger
    {
        public readonly List<string> Errors = new List<string>();
        public TerrariaModder.Core.Logging.LogLevel MinLevel { get; set; }
        public string ModId { get { return "shift-batch-tests"; } }
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { Errors.Add(message); }
        public void Error(string message, Exception error) { Errors.Add(message + ": " + error); }
    }

    private sealed class OtherModConfig : TerrariaModder.Core.Config.ModConfig
    {
        public override int Version { get { return 1; } }
        public int PurchaseBatch { get; set; }
    }
}
