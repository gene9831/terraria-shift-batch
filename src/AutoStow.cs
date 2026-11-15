using System;
using System.Reflection;
using Terraria;
using TerrariaModder.Core.Logging;
using TerrariaModder.Core.UI;

namespace ShiftBatch
{
    public static class AutoStow
    {
        public struct Observation
        {
            public object Source;
            public int Slot;
            public Item Output;
            public int PerCraft;
            public int Buttons;
            public bool Crafting;
        }

        private static readonly AutoStowState State = new AutoStowState();
        private static readonly FieldInfo RepeatRecipe = typeof(Main).GetField("_currentRecipeBeingCrafted", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo PreventCraft = typeof(Main).GetField("_preventCraftingBecauseClickWasUsedToChangeFocusedRecipe", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly PropertyInfo PendingCrafts = typeof(Main).Assembly.GetType("Terraria.GameContent.CraftingRequests")
            .GetProperty("HasPendingRequests", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        public static ILogger Logger;

        public static void ShopLeftPrefix(Item[] __0, int __1, int __2, out Observation __state)
        {
            __state = CaptureShop(__0, __1, __2, Main.mouseLeft);
        }

        public static void ShopRightPrefix(Item[] __0, int __1, int __2, out Observation __state)
        {
            // Both native click handlers run every draw. Observe just once if
            // both buttons are down, and ignore the inactive button's callback.
            __state = CaptureShop(__0, __1, __2, !Main.mouseLeft && Main.mouseRight);
        }

        private static Observation CaptureShop(Item[] inventory, int context, int slot, bool pressed)
        {
            // Observe before native click routing: LeftClick skips the inner
            // transaction function once the cursor is full. The small Handle
            // dispatcher can be JIT-inlined, so patch the large click methods.
            if (context != 15 || !pressed || !CanTrack() || inventory == null || slot < 0 || slot >= inventory.Length)
                return new Observation();
            if (Main.LocalPlayerHasPendingInventoryActions()) { Cancel("pending inventory actions"); return new Observation(); }
            int buttons = Buttons();
            Item output = inventory[slot];
            if (output == null || output.IsAir) return new Observation();
            return Capture(inventory, slot, output, 1, buttons, false);
        }

        public static void ShopPostfix(Observation __state)
        {
            // Other slot contexts must not erase a shop observation from the
            // same frame. EndDraw cancels if the shop target was not observed.
            if (__state.Source != null)
            {
                if (Main.LocalPlayerHasPendingInventoryActions()) { Cancel("pending inventory actions"); return; }
                Observe(__state);
            }
        }

        public static void CraftPrefix(int __0, out Observation __state)
        {
            __state = new Observation();
            if (!CanTrack() || !CraftAllowed() || __0 != Main.focusRecipe || __0 < 0 || __0 >= Main.numAvailableRecipes ||
                Main.availableRecipe == null || __0 >= Main.availableRecipe.Length || Main.recipe == null ||
                (Main.guideItem != null && !Main.guideItem.IsAir)) return;
            int index = Main.availableRecipe[__0];
            if (index < 0 || index >= Main.recipe.Length) return;
            Recipe recipe = Main.recipe[index];
            if (recipe == null || recipe.createItem == null || recipe.createItem.IsAir) return;
            __state = Capture(recipe, index, recipe.createItem, recipe.createItem.stack, Buttons(), true);
        }

        public static void CraftPostfix(Observation __state)
        {
            // Native hover processing can set its release-before-crafting flag
            // during this very call, after the prefix captured the target.
            if (!CanTrack() || !CraftAllowed()) { Cancel("crafting interaction unavailable"); return; }
            Observe(__state);
        }

        private static Observation Capture(object source, int slot, Item output, int perCraft, int buttons, bool crafting)
        {
            // A limited shop item may be cleared by the native purchase before
            // the postfix runs. Preserve its metadata for this observation.
            return new Observation { Source = source, Slot = slot, Output = output.Clone(),
                PerCraft = perCraft, Buttons = buttons, Crafting = crafting };
        }

        private static int Buttons() { return (Main.mouseLeft ? 1 : 0) | (Main.mouseRight ? 2 : 0); }

        private static bool CanTrack()
        {
            BatchConfig config = NativePatches.Config;
            if (!NativePatches.Active || config == null || !config.Enabled || !config.AutoStow) return false;
            if (Main.instance != null && !Main.instance.IsActive) return false;
            if (Main.gameMenu || !Main.playerInventory || Main.gamePaused || Main.blockInput || Main.blockMouse ||
                Main.cursorOverride == 2 || !Terraria.UI.ItemSlot.ShiftInUse || Buttons() == 0 ||
                UIRenderer.IsBlocking || UIRenderer.IsWaitingForKeyInput) return false;
            if (Main.player == null || Main.myPlayer < 0 || Main.myPlayer >= Main.player.Length) return false;
            // Never move a cursor that still participates in an asynchronous
            // native crafting grant; start a fresh delay after it settles.
            if (PendingCrafts == null || (bool)PendingCrafts.GetValue(null, null)) return false;
            Player player = Main.player[Main.myPlayer];
            return player != null && player.active && !player.dead && player.itemAnimation == 0 && player.inventory != null;
        }

        private static bool CraftAllowed()
        {
            Player player = Main.player[Main.myPlayer];
            return PreventCraft != null && !(bool)PreventCraft.GetValue(null) &&
                !player.UsingOrReusingItem && !player.IsLockedFromCrafting();
        }

        private static void Observe(Observation observation)
        {
            if (!CanTrack() || observation.Source == null || observation.Output == null ||
                (Buttons() & observation.Buttons) == 0)
            {
                Cancel("input or target unavailable");
                return;
            }
            Item cursor = Main.mouseItem;
            bool compatible = cursor != null && !cursor.IsAir && cursor.type == observation.Output.type &&
                (Item.CanStack(cursor, observation.Output) || (observation.Crafting && cursor.maxStack == 1));
            bool full = compatible && BatchRules.IsAtCapacity(cursor.maxStack, cursor.stack, observation.PerCraft);
            uint tick = Main.GameUpdateCount;
            int key = observation.Slot * 4 + observation.Buttons;
            bool wasPending = State.IsPending;
            StowDecision decision = State.Observe(tick, observation.Source, key, cursor, cursor == null ? 0 : cursor.stack, full);
            if (DebugLog.Enabled)
            {
                if (decision == StowDecision.Started)
                    DebugLog.Write("Auto-stow waiting: source=" + (observation.Crafting ? "craft" : "shop") +
                        "; slot=" + observation.Slot + "; buttons=" + observation.Buttons + "; type=" + cursor.type +
                        "; prefix=" + cursor.prefix + "; held=" + cursor.stack + "; maxStack=" + cursor.maxStack +
                        "; perCraft=" + observation.PerCraft + "; delayTicks=" + AutoStowState.DelayTicks);
                else if (wasPending && !State.IsPending)
                    DebugLog.Write("Auto-stow cancelled: cursor no longer at compatible capacity.");
            }
            if ((decision & StowDecision.Transfer) == 0) return;
            int before = cursor.stack;
            bool moved = TransferToInventory();
            if (DebugLog.Enabled)
            {
                Item remainder = Main.mouseItem;
                int remaining = remainder == null || remainder.IsAir ? 0 : remainder.stack;
                DebugLog.Write("Auto-stow " + (moved ? "transfer" : "blocked") + ": source=" +
                    (observation.Crafting ? "craft" : "shop") + "; type=" + observation.Output.type +
                    "; held=" + before + "; moved=" + (before - remaining) + "; remaining=" + remaining);
            }
            if (moved)
            {
                if (observation.Crafting && observation.Output.maxStack == 1 && RepeatRecipe != null) RepeatRecipe.SetValue(null, -1);
                State.Reset();
            }
            else
            {
                // Rebind after recovery if an external pickup hook replaced the
                // cursor object; a no-progress transfer must not retry every draw.
                Item remainder = Main.mouseItem;
                State.Observe(tick, observation.Source, key, remainder, remainder == null ? 0 : remainder.stack,
                    remainder != null && BatchRules.IsAtCapacity(remainder.maxStack, remainder.stack, observation.PerCraft));
                State.Block();
            }
        }

        private static bool TransferToInventory()
        {
            Player player = Main.player[Main.myPlayer];
            Item cursor = Main.mouseItem;
            Item snapshot = cursor.Clone();
            int before = snapshot.stack;
            long inventoryBefore = CountCompatible(player, snapshot);
            try
            {
                // Use vanilla personal-inventory placement, including coin/ammo
                // slots, but never void-vault routing or GetOrDropItem.
                var settings = new GetItemSettings(false, true, false, true, null, true);
                Item remainder = player.GetItem(cursor, settings);
                if (remainder == null) throw new InvalidOperationException("GetItem returned no remainder object.");
                Main.mouseItem = remainder;
                return remainder.IsAir || remainder.stack < before;
            }
            catch (Exception ex)
            {
                // A third-party pickup callback may throw after native placement.
                // Reconstruct only the unplaced amount; never keep an alias to an
                // inventory slot or resurrect an already placed full stack.
                long placed = Math.Max(0L, Math.Min(before, CountCompatible(player, snapshot) - inventoryBefore));
                int remaining = before - (int)placed;
                if (remaining == 0) Main.mouseItem = new Item();
                else { snapshot.stack = remaining; Main.mouseItem = snapshot; }
                if (Logger != null) Logger.Warn("Auto-stow interrupted; cursor remainder retained: " + ex.Message);
                return false;
            }
        }

        private static long CountCompatible(Player player, Item sample)
        {
            long count = 0;
            for (int i = 0; i < Math.Min(58, player.inventory.Length); i++)
            {
                Item item = player.inventory[i];
                if (item != null && !item.IsAir && Item.CanStack(item, sample)) count += Math.Max(0, item.stack);
            }
            return count;
        }

        private static void Cancel(string reason)
        {
            if (State.IsPending && DebugLog.Enabled) DebugLog.Write("Auto-stow cancelled: " + reason + ".");
            State.Reset();
        }

        public static void CancelIfInactive() { if (!CanTrack()) Cancel("interaction inactive"); }
        public static void EndDraw()
        {
            bool wasPending = State.IsPending;
            State.EndDraw();
            if (wasPending && !State.IsPending) DebugLog.Write("Auto-stow cancelled: target not observed this draw.");
        }
        public static void Reset() { Cancel("configuration or world lifecycle changed"); }
    }
}
