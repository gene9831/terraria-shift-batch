using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Terraria;
using Terraria.UI;

namespace ShiftBatch
{
    public static class NativePatches
    {
        public static BatchConfig Config;
        public static bool Active;

        public static int GetCraftBatch()
        {
            return Active && Config != null && Config.Enabled ? Config.CraftCount : BatchRules.DefaultBatch;
        }

        public static void BuyAmountPostfix(Item item, ref int __result)
        {
            if (!Active || Config == null || !Config.Enabled || item == null) return;
            __result = item.buyOnce ? Math.Min(Config.PurchaseCount, Math.Max(0, item.stack)) : Config.PurchaseCount;
        }

        // This runs AFTER vanilla multiplies by superFastStack, so acceleration
        // cannot bypass the capacity or finite-stock limit.
        public static int LimitShopCount(int requested, Item item)
        {
            if (!Active || Config == null || !Config.Enabled || item == null || !ItemSlot.CanBulkBuy(item)) return requested;
            int stack;
            if (!TryCursorStack(item, out stack)) return 0;
            return BatchRules.PurchaseLimit(requested, item.maxStack, stack,
                item.buyOnce ? Math.Max(0, item.stack) : Int32.MaxValue);
        }

        public static bool CraftRequestPrefix(Recipe recipe, ref int qty, bool quickCraft)
        {
            if (!Active || Config == null || !Config.Enabled || quickCraft || !ItemSlot.ShiftInUse) return true;
            Item output = recipe == null ? null : recipe.createItem;
            int stack;
            if (output == null || !TryCursorStack(output, out stack)) { qty = 0; return false; }
            qty = BatchRules.CraftLimit(qty, output.maxStack, stack, output.stack);
            return qty > 0;
        }

        public static void CraftAmountPostfix(Item item, ref int __result)
        {
            if (!Active || Config == null || !Config.Enabled || item == null) return;
            int stack;
            __result = TryCursorStack(item, out stack) ?
                BatchRules.CraftLimit(Config.CraftCount, item.maxStack, stack, item.stack) : 0;
        }

        private static bool TryCursorStack(Item output, out int stack)
        {
            stack = 0;
            Item cursor = Main.mouseItem;
            if (cursor == null || cursor.IsAir) return true;
            if (!Item.CanStack(cursor, output)) return false;
            stack = Math.Max(0, cursor.stack);
            return true;
        }

        public static IEnumerable<CodeInstruction> ShopTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(x => new CodeInstruction(x)).ToList();
            int multiplier = -1;
            int target = -1;
            int amountCalls = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (IsCall(code[i], typeof(ItemSlot), "GetBulkBuyAmount"))
                {
                    amountCalls++;
                    // Verify the installed game's batch-count local, rather than
                    // blindly emitting ldloc.1 into an unknown version's method.
                    if (i + 2 < code.Count && code[i + 1].opcode == OpCodes.Mul && code[i + 2].opcode == OpCodes.Stloc_1)
                        multiplier = i;
                }
                if (IsCall(code[i], typeof(Main), "get_LocalPlayer"))
                {
                    if (target != -1) throw new InvalidOperationException("Ambiguous shop player lookup; no patch applied.");
                    target = i;
                }
            }
            if (amountCalls != 1 || multiplier < 0 || target <= multiplier)
                throw new InvalidOperationException("Unsupported HandleShopSlot IL; no patch applied.");

            var guard = new List<CodeInstruction> {
                new CodeInstruction(OpCodes.Ldloc_1),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldelem_Ref),
                new CodeInstruction(OpCodes.Call, typeof(NativePatches).GetMethod("LimitShopCount")),
                new CodeInstruction(OpCodes.Stloc_1)
            };
            // The non-bulk branch targets this instruction. Move its labels so
            // all paths execute the guard with an already-computed loop count.
            guard[0].labels.AddRange(code[target].labels);
            guard[0].blocks.AddRange(code[target].blocks);
            code[target].labels.Clear();
            code[target].blocks.Clear();
            code.InsertRange(target, guard);
            return code;
        }

        public static IEnumerable<CodeInstruction> CraftTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(x => new CodeInstruction(x)).ToList();
            int count = 0;
            for (int i = 3; i + 1 < code.Count; i++)
            {
                if (!code[i].LoadsConstant(10L) || code[i + 1].opcode != OpCodes.Mul ||
                    !IsCall(code[i - 3], typeof(ItemSlot), "get_ShiftInUse") ||
                    (code[i - 2].opcode != OpCodes.Brfalse && code[i - 2].opcode != OpCodes.Brfalse_S) ||
                    (code[i - 1].opcode != OpCodes.Ldloc_S && code[i - 1].opcode != OpCodes.Ldloc)) continue;
                code[i].opcode = OpCodes.Call;
                code[i].operand = typeof(NativePatches).GetMethod("GetCraftBatch");
                count++;
            }
            if (count != 1) throw new InvalidOperationException("Unsupported HoverOverCraftingItemButton IL; no patch applied.");
            return code;
        }

        private static bool IsCall(CodeInstruction instruction, Type owner, string name)
        {
            MethodInfo method = instruction.operand as MethodInfo;
            return (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                method != null && method.DeclaringType == owner && method.Name == name;
        }
    }
}
