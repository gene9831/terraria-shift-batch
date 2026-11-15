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
        Normalize(rules, "0", 10, true, 10);
        Normalize(rules, "-100", 10, true, 10);
        Normalize(rules, "1", 10, true, 10);
        Normalize(rules, "9", 10, true, 10);
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
        CheckAutoStowState();
        CheckConfigFiles();
        CheckNativeIntegration();
        CheckAutoStowIntegration();
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
        Equal(false, (bool)type.GetProperty("AutoStow").GetValue(config, null), "auto-stow defaults off");
        Equal(true, (bool)type.GetProperty("LimitSound").GetValue(config, null), "limit sound available when auto-stow enabled");
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

    private static void CheckAutoStowState()
    {
        Type rules = Find("ShiftBatch.BatchRules");
        MethodInfo capacity = rules.GetMethod("IsAtCapacity");
        Equal(true, capacity != null, "effective capacity detector exists");
        Equal(true, (bool)capacity.Invoke(null, new object[] {9999,9975,25}), "multi-output effective full");
        Equal(false, (bool)capacity.Invoke(null, new object[] {9999,9974,25}), "one more complete output still fits");
        Equal(true, (bool)capacity.Invoke(null, new object[] {9999,9999,1}), "purchase full");
        Equal(true, (bool)capacity.Invoke(null, new object[] {1,1,1}), "nonstacking full");
        Equal(false, (bool)capacity.Invoke(null, new object[] {9999,0,25}), "empty cursor is not full");
        Equal(false, (bool)capacity.Invoke(null, new object[] {9999,10000,1}), "corrupt overstack is not auto-transferred");

        Type type = Find("ShiftBatch.AutoStowState");
        Equal(true, type != null, "auto-stow state exists");
        Equal(true,type.GetMethod("EndDraw")!=null,"draw-activity cancellation exists without assuming 60fps rendering");
        object state = Activator.CreateInstance(type);
        object source = new object(), cursor = new object();
        Equal(1, Observe(type,state,0,source,1,cursor,9999,true), "sound notification on reaching limit");
        Equal(0, Observe(type,state,0,source,1,cursor,9999,true), "no repeated notification in same frame");
        for(uint tick=1;tick<18;tick++) Equal(0, Observe(type,state,tick,source,1,cursor,9999,true), "wait before transfer "+tick);
        Equal(2, Observe(type,state,18,source,1,cursor,9999,true), "transfer only after 18 ticks");
        type.GetMethod("Block").Invoke(state,null);
        Equal(0, Observe(type,state,19,source,1,cursor,9999,true), "full inventory stops retry");
        Equal(0, Observe(type,state,20,source,1,cursor,9999,true), "full inventory does not repeat sound");
        type.GetMethod("Reset").Invoke(state,null);
        Equal(1, Observe(type,state,21,source,1,cursor,9999,true), "release and restart rearms notification");
        Equal(1, Observe(type,state,22,new object(),1,cursor,9999,true), "changed source starts fresh delay");
        Equal(1, Observe(type,state,23,source,2,new object(),9999,true), "changed cursor starts fresh delay");
        Equal(0, Observe(type,state,24,source,2,cursor,100,false), "not-full cursor cancels pending transfer");
        Equal(1, Observe(type,state,25,source,2,cursor,9999,true), "new full stack rearms");
        type.GetMethod("EndDraw").Invoke(state,null);
        type.GetMethod("EndDraw").Invoke(state,null);
        Equal(1, Observe(type,state,27,source,2,cursor,9999,true), "a draw without activity cancels pending work");
        type.GetMethod("Reset").Invoke(state,null);
        uint start=UInt32.MaxValue-8;
        Equal(1, Observe(type,state,start,source,1,cursor,9975,true), "wraparound starts normally");
        for(uint delta=1;delta<=18;delta++) Equal(delta==18?2:0, Observe(type,state,unchecked(start+delta),source,1,cursor,9975,true), "tick wraparound "+delta);
    }

    private static int Observe(Type type,object state,uint tick,object source,int slot,object cursor,int stack,bool full)
    { return Convert.ToInt32(type.GetMethod("Observe").Invoke(state,new object[]{tick,source,slot,cursor,stack,full})); }

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
            // A schema migration may resave the retained value. Restore the raw
            // fixture before separately testing a fresh configuration instance.
            File.WriteAllText(path, "{\"_version\":1,\"PurchaseBatch\":" + token + "}");
            var initial = TerrariaModder.Core.Config.ConfigManager.LoadForMod("shift-batch", Assembly.GetExecutingAssembly(), root, null, null);
            Equal("10", (string)type.GetProperty("PurchaseBatch").GetValue(initial, null), "invalid raw token on first load defaults: " + token);
        }
        File.WriteAllText(path,"{\"_version\":1,\"PurchaseBatch\":\"1\",\"CraftBatch\":\"9\"}");
        var migrated = (ShiftBatch.BatchConfig)TerrariaModder.Core.Config.ConfigManager.LoadForMod("shift-batch", Assembly.GetExecutingAssembly(), root, null, null);
        Equal(10,migrated.PurchaseCount,"old low purchase migrates to 10");
        Equal(10,migrated.CraftCount,"old low craft migrates to 10");
        Equal(false,migrated.AutoStow,"old config does not enable auto-stow");
        Equal(2,migrated.Version,"new config schema");
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

    private static int limitSounds;
    private static int inventoryCalls;
    private static bool throwAfterPickup;
    private static bool RecordLimitSound() { limitSounds++; return false; }
    private static void RecordInventoryCall() { inventoryCalls++; }
    private static void FailAfterPickup(){if(throwAfterPickup)throw new InvalidOperationException("test pickup callback failure");}

    private static Terraria.Item MakeItem(int type,int stack,int max)
    {
        var item=(Terraria.Item)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Terraria.Item));
        item.type=type; item.stack=stack; item.maxStack=max; return item;
    }

    private static void CheckAutoStowIntegration()
    {
        Type stow=Find("ShiftBatch.AutoStow");
        Equal(true,stow!=null,"native auto-stow integration exists");
        if(stow==null)return;
        var oldFields=new Dictionary<FieldInfo,object>();
        foreach(string name in new string[]{"gameMenu","playerInventory","gamePaused","blockInput","mouseLeft","mouseRight","myPlayer","mouseItem","cursorOverride","focusRecipe","numAvailableRecipes","recipe","availableRecipe","_gameUpdateCount","_currentRecipeBeingCrafted","_preventCraftingBecauseClickWasUsedToChangeFocusedRecipe","instance"})
        {
            FieldInfo field=typeof(Terraria.Main).GetField(name,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
            oldFields.Add(field,field.GetValue(null));
        }
        Terraria.Player oldPlayer=Terraria.Main.player[0];
        bool oldShift=Terraria.UI.ItemSlot.ShiftForcedOn;
        var harness=new Harmony("shift-batch.auto-stow-tests");
        try
        {
            MethodInfo play=typeof(Terraria.Audio.SoundEngine).GetMethod("PlaySound",new Type[]{typeof(int),typeof(int),typeof(int),typeof(int),typeof(float),typeof(float)});
            harness.Patch(play,prefix:new HarmonyMethod(typeof(Tests).GetMethod("RecordLimitSound",BindingFlags.Static|BindingFlags.NonPublic)));
            harness.Patch(typeof(Terraria.Player).GetMethod("GetItem"),prefix:new HarmonyMethod(typeof(Tests).GetMethod("RecordInventoryCall",BindingFlags.Static|BindingFlags.NonPublic)),postfix:new HarmonyMethod(typeof(Tests).GetMethod("FailAfterPickup",BindingFlags.Static|BindingFlags.NonPublic)));
            var player=(Terraria.Player)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Terraria.Player));
            player.inventory=new Terraria.Item[59]; player.inventoryChestStack=new bool[59]; player.disableVoidBag=-1; player.active=true; player.dead=false;
            Terraria.Main.player[0]=player; Terraria.Main.myPlayer=0;
            var fakeMain=(Terraria.Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Terraria.Main));
            GC.SuppressFinalize(fakeMain);
            FieldInfo activeGame=typeof(Microsoft.Xna.Framework.Game).GetField("isActive",BindingFlags.Instance|BindingFlags.NonPublic);
            activeGame.SetValue(fakeMain,true); Terraria.Main.instance=fakeMain;
            Terraria.Main.gameMenu=false; Terraria.Main.playerInventory=true; Terraria.Main.gamePaused=false; Terraria.Main.blockInput=false; Terraria.Main.cursorOverride=0;
            Terraria.Main.mouseLeft=true; Terraria.Main.mouseRight=false; Terraria.UI.ItemSlot.ShiftForcedOn=true;
            var config=new ShiftBatch.BatchConfig(); config.AutoStow=true; config.LimitSound=true;
            ShiftBatch.NativePatches.Config=config; ShiftBatch.NativePatches.Active=true;
            var shop=new Terraria.Item[]{MakeItem(3,9999,9999)}; shop[0].isAShopItem=true;
            ResetStow(stow); EmptyInventory(player); limitSounds=0; inventoryCalls=0;
            Terraria.Main.mouseItem=MakeItem(3,9999,9999);
            ObserveShop(stow,shop,0);
            Equal(1,limitSounds,"limit sound immediately before any transfer");
            Equal(0,inventoryCalls,"no immediate transfer");
            for(uint tick=1;tick<18;tick++)ObserveShop(stow,shop,tick);
            Equal(9999,Terraria.Main.mouseItem.stack,"held stack remains during warning delay");
            ObserveShop(stow,shop,18);
            Equal(true,Terraria.Main.mouseItem.IsAir,"full stack enters inventory after delay");
            Equal(9999,InventoryCount(player,3),"complete transfer conserves quantity");
            Equal(1,inventoryCalls,"exactly one native transfer");
            Equal(1,limitSounds,"transfer itself plays no new sound");

            ResetStow(stow); EmptyInventory(player); Terraria.Main.mouseItem=MakeItem(3,9999,9999); limitSounds=0; inventoryCalls=0;
            ObserveShop(stow,shop,100);
            Terraria.Main.mouseLeft=false;
            stow.GetMethod("CancelIfInactive").Invoke(null,null);
            Terraria.Main.mouseLeft=true;
            ObserveShop(stow,shop,101);
            Equal(0,inventoryCalls,"release cancels expired work");
            for(uint tick=102;tick<119;tick++)ObserveShop(stow,shop,tick);
            Equal(0,inventoryCalls,"restarted interaction gets a complete fresh delay");
            ObserveShop(stow,shop,119);
            Equal(1,inventoryCalls,"continued hold transfers after fresh delay");

            ResetStow(stow); FillInventory(player); player.inventory[0].stack=9994;
            Terraria.Main.mouseItem=MakeItem(3,9999,9999); inventoryCalls=0; limitSounds=0;
            long before=InventoryCount(player,3)+(long)Terraria.Main.mouseItem.stack;
            for(uint tick=200;tick<=218;tick++)ObserveShop(stow,shop,tick);
            Equal(9994,Terraria.Main.mouseItem.stack,"partial transfer keeps cursor remainder");
            Equal(9999,player.inventory[0].stack,"partial transfer fills available capacity");
            Equal(before,InventoryCount(player,3)+(long)Terraria.Main.mouseItem.stack,"partial transfer conserves all items");
            Terraria.Main.mouseItem.stack=9999;
            for(uint tick=219;tick<=260;tick++)ObserveShop(stow,shop,tick);
            Equal(9999,Terraria.Main.mouseItem.stack,"full inventory preserves held item");
            Equal(2,inventoryCalls,"failed transfer is not retried each frame");
            Equal(2,limitSounds,"one sound per full-stack cycle even when inventory blocked");

            ResetStow(stow); EmptyInventory(player); inventoryCalls=0; limitSounds=0;
            var recipe=(Terraria.Recipe)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Terraria.Recipe));
            recipe.createItem=MakeItem(3,25,9999);
            Terraria.Main.recipe=new Terraria.Recipe[]{recipe}; Terraria.Main.availableRecipe=new int[]{0}; Terraria.Main.numAvailableRecipes=1; Terraria.Main.focusRecipe=0;
            Terraria.Main.mouseItem=MakeItem(3,9975,9999);
            typeof(Terraria.Main).GetField("_currentRecipeBeingCrafted",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,0);
            for(uint tick=300;tick<=318;tick++)ObserveCraft(stow,tick);
            Equal(9975,InventoryCount(player,3),"effectively full multi-output recipe stows below numeric max");
            Equal(true,Terraria.Main.mouseItem.IsAir,"multi-output craft can continue with empty cursor");
            Equal(1,limitSounds,"multi-output effective limit notifies once");
            Equal(0,(int)typeof(Terraria.Main).GetField("_currentRecipeBeingCrafted",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null),"stacking craft preserves native recipe-change guard");

            ResetStow(stow); EmptyInventory(player); recipe.createItem=MakeItem(3,1,1); Terraria.Main.mouseItem=MakeItem(3,1,1); Terraria.Main.mouseItem.prefix=5;
            typeof(Terraria.Main).GetField("_currentRecipeBeingCrafted",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,0);
            for(uint tick=400;tick<=418;tick++)ObserveCraft(stow,tick);
            Equal(true,Terraria.Main.mouseItem.IsAir,"nonstacking crafted prefix can stow");
            Equal((byte)5,player.inventory[49].prefix,"native transfer preserves prefix");
            Equal(-1,(int)typeof(Terraria.Main).GetField("_currentRecipeBeingCrafted",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null),"nonstacking recipe repeat rearms");

            ResetStow(stow); EmptyInventory(player); config.AutoStow=false; Terraria.Main.mouseItem=MakeItem(3,9999,9999); inventoryCalls=0; limitSounds=0;
            for(uint tick=500;tick<=530;tick++)ObserveShop(stow,shop,tick);
            Equal(0,inventoryCalls,"disabled auto-stow never moves cursor");
            Equal(0,limitSounds,"default mode adds no limit sound");
            config.AutoStow=true; config.LimitSound=false;
            for(uint tick=600;tick<=618;tick++)ObserveShop(stow,shop,tick);
            Equal(1,inventoryCalls,"sound toggle does not disable transfer");
            Equal(0,limitSounds,"limit sound can be disabled");

            ResetStow(stow); EmptyInventory(player); Terraria.Main.mouseItem=MakeItem(3,9999,9999); inventoryCalls=0;
            for(uint tick=700;tick<=718;tick+=2)ObserveShop(stow,shop,tick);
            Equal(1,inventoryCalls,"30fps rendering still allows an 18-simulation-tick delay");

            ResetStow(stow); EmptyInventory(player); Terraria.Main.mouseItem=MakeItem(3,9999,9999); inventoryCalls=0;
            ObserveShop(stow,shop,800);
            stow.GetMethod("EndDraw").Invoke(null,null);
            for(uint tick=802;tick<820;tick++)ObserveShop(stow,shop,tick);
            Equal(0,inventoryCalls,"moving away for a draw cancels previous countdown");
            ObserveShop(stow,shop,820);
            Equal(1,inventoryCalls,"returning to target requires new full delay");

            ResetStow(stow); EmptyInventory(player); Terraria.Main.mouseItem=MakeItem(3,9999,9999); inventoryCalls=0; throwAfterPickup=true;
            for(uint tick=900;tick<=918;tick++)ObserveShop(stow,shop,tick);
            Equal(9999,InventoryCount(player,3),"pickup exception after placement preserves inventory");
            Equal(true,Terraria.Main.mouseItem.IsAir,"pickup exception does not duplicate held stack or retain slot alias");
            ResetStow(stow); FillInventory(player); player.inventory[0].stack=9994; Terraria.Main.mouseItem=MakeItem(3,9999,9999);
            before=InventoryCount(player,3)+(long)Terraria.Main.mouseItem.stack;
            for(uint tick=1000;tick<=1018;tick++)ObserveShop(stow,shop,tick);
            Equal(9994,Terraria.Main.mouseItem.stack,"pickup exception preserves partially transferred remainder");
            Equal(before,InventoryCount(player,3)+(long)Terraria.Main.mouseItem.stack,"pickup exception conserves quantity");
            throwAfterPickup=false;

            ResetStow(stow); EmptyInventory(player); Terraria.Main.mouseItem=MakeItem(3,9999,9999); inventoryCalls=0;
            for(uint tick=1100;tick<1118;tick++)ObserveShop(stow,shop,tick);
            activeGame.SetValue(fakeMain,false);
            ObserveShop(stow,shop,1118);
            Equal(0,inventoryCalls,"losing focus at expiry prevents transfer");
            activeGame.SetValue(fakeMain,true);

            ResetStow(stow); EmptyInventory(player); Terraria.Main.mouseItem=MakeItem(3,9999,9999); inventoryCalls=0;
            ObserveShop(stow,shop,1200);
            Terraria.Main.gamePaused=true; stow.GetMethod("CancelIfInactive").Invoke(null,null); Terraria.Main.gamePaused=false;
            for(uint tick=1201;tick<1219;tick++)ObserveShop(stow,shop,tick);
            Equal(0,inventoryCalls,"pause cancels prior countdown");
            ObserveShop(stow,shop,1219); Equal(1,inventoryCalls,"unpaused target waits a new delay");

            ResetStow(stow); EmptyInventory(player); Terraria.Main.mouseItem=MakeItem(3,9999,9999); inventoryCalls=0;
            ObserveShop(stow,shop,1300);
            new ShiftBatch.Mod().OnWorldUnload();
            for(uint tick=1301;tick<1319;tick++)ObserveShop(stow,shop,tick);
            Equal(0,inventoryCalls,"world-unload callback cancels prior countdown");
            ObserveShop(stow,shop,1319); Equal(1,inventoryCalls,"world change requires new delay");

            ResetStow(stow); EmptyInventory(player); Terraria.Main.mouseItem=MakeItem(3,9999,9999); inventoryCalls=0;
            Terraria.Main.mouseLeft=false; Terraria.Main.mouseRight=true;
            for(uint tick=1400;tick<=1418;tick++){
                SetTick(tick); object[] right={shop,0,true,false,null}; stow.GetMethod("ShopPrefix").Invoke(null,right);
                stow.GetMethod("ShopPostfix").Invoke(null,new object[]{right[4]}); stow.GetMethod("EndDraw").Invoke(null,null);
            }
            Equal(1,inventoryCalls,"right-button continuous purchase can auto-stow");
            Terraria.Main.mouseLeft=true; Terraria.Main.mouseRight=false;

            FieldInfo preventCraft=typeof(Terraria.Main).GetField("_preventCraftingBecauseClickWasUsedToChangeFocusedRecipe",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
            recipe.createItem=MakeItem(3,25,9999); config.LimitSound=true;
            for(int guard=0;guard<5;guard++){
                ResetStow(stow); EmptyInventory(player); player.inventory[0]=MakeItem(3,1,9999);
                Terraria.Main.mouseItem=MakeItem(3,9975,9999); inventoryCalls=0; limitSounds=0;
                if(guard==0)player.reuseDelay=1;
                if(guard==1)player.channel=true;
                if(guard==2)player.pendingItemReuse=true;
                if(guard==3)player.inventoryChestStack[0]=true;
                if(guard==4)preventCraft.SetValue(null,true);
                uint start=(uint)(1500+guard*100);
                for(uint tick=start;tick<=start+30;tick++)ObserveCraft(stow,tick);
                Equal(0,inventoryCalls,"native craft guard prevents transfer: "+guard);
                Equal(0,limitSounds,"native craft guard prevents limit event: "+guard);
                player.reuseDelay=0; player.channel=false; player.pendingItemReuse=false; player.inventoryChestStack[0]=false; preventCraft.SetValue(null,false);
            }
            ResetStow(stow); EmptyInventory(player); Terraria.Main.mouseItem=MakeItem(3,9975,9999); limitSounds=0;
            SetTick(2100); object[] selection={0,null}; stow.GetMethod("CraftPrefix").Invoke(null,selection);
            preventCraft.SetValue(null,true);
            stow.GetMethod("CraftPostfix").Invoke(null,new object[]{selection[1]}); stow.GetMethod("EndDraw").Invoke(null,null);
            Equal(0,limitSounds,"native selection guard set during hover is rechecked after it");
            preventCraft.SetValue(null,false);

            Type requests=typeof(Terraria.Main).Assembly.GetType("Terraria.GameContent.CraftingRequests",true);
            FieldInfo pending=requests.GetField("_pendingCrafts",BindingFlags.Static|BindingFlags.NonPublic);
            object queue=pending.GetValue(null);
            object request=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(queue.GetType().GetGenericArguments()[0]);
            queue.GetType().GetMethod("Enqueue").Invoke(queue,new object[]{request});
            try {
                ResetStow(stow); EmptyInventory(player); Terraria.Main.mouseItem=MakeItem(3,9975,9999); inventoryCalls=0; limitSounds=0;
                for(uint tick=2200;tick<=2230;tick++)ObserveCraft(stow,tick);
                Equal(0,inventoryCalls,"pending remote craft grants prevent cursor transfer");
                Equal(0,limitSounds,"pending remote craft grants defer capacity notification");
            } finally {queue.GetType().GetMethod("Dequeue").Invoke(queue,null);}
        }
        finally
        {
            throwAfterPickup=false; ResetStow(stow); harness.UnpatchAll(harness.Id);
            ShiftBatch.NativePatches.Active=false; ShiftBatch.NativePatches.Config=null;
            foreach(var entry in oldFields)entry.Key.SetValue(null,entry.Value);
            Terraria.Main.player[0]=oldPlayer; Terraria.UI.ItemSlot.ShiftForcedOn=oldShift;
        }
    }

    private static void ResetStow(Type stow){stow.GetMethod("Reset").Invoke(null,null);}
    private static void SetTick(uint tick){typeof(Terraria.Main).GetField("_gameUpdateCount",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,tick);}
    private static void ObserveShop(Type stow,Terraria.Item[] inventory,uint tick)
    {
        SetTick(tick); object[] args={inventory,0,false,true,null};
        stow.GetMethod("ShopPrefix").Invoke(null,args);
        stow.GetMethod("ShopPostfix").Invoke(null,new object[]{args[4]});
        MethodInfo end=stow.GetMethod("EndDraw"); if(end!=null)end.Invoke(null,null);
    }
    private static void ObserveCraft(Type stow,uint tick)
    {
        SetTick(tick); object[] args={0,null};
        stow.GetMethod("CraftPrefix").Invoke(null,args);
        stow.GetMethod("CraftPostfix").Invoke(null,new object[]{args[1]});
        MethodInfo end=stow.GetMethod("EndDraw"); if(end!=null)end.Invoke(null,null);
    }
    private static void EmptyInventory(Terraria.Player player){for(int i=0;i<player.inventory.Length;i++)player.inventory[i]=MakeItem(0,0,1);}
    private static void FillInventory(Terraria.Player player){for(int i=0;i<player.inventory.Length;i++)player.inventory[i]=MakeItem(3,9999,9999);}
    private static long InventoryCount(Terraria.Player player,int type){return player.inventory.Where(x=>x!=null&&x.type==type).Sum(x=>(long)x.stack);}

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
