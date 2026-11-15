using System;
using System.Globalization;

namespace ShiftBatch
{
    public static class BatchRules
    {
        public const int DefaultBatch = 10;
        public const int MinimumBatch = 10;
        public const int MaximumBatch = 9999;

        public static bool TryNormalize(string text, int previous, out int value)
        {
            value = Clamp(previous);
            int parsed;
            if (!Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return false;
            value = Clamp(parsed);
            return true;
        }

        public static int Clamp(int value)
        {
            return Math.Max(MinimumBatch, Math.Min(MaximumBatch, value));
        }

        private static int Capacity(int maxStack, int cursorStack)
        {
            // Use long for subtraction even if another mod left a corrupt stack value.
            long available = (long)Math.Min(MaximumBatch, Math.Max(0, maxStack)) - Math.Max(0, cursorStack);
            return (int)Math.Max(0L, available);
        }

        public static bool IsAtCapacity(int maxStack, int cursorStack, int nextOutput)
        {
            int limit = Math.Min(MaximumBatch, Math.Max(0, maxStack));
            return cursorStack > 0 && cursorStack <= limit && nextOutput > 0 &&
                Capacity(maxStack, cursorStack) < nextOutput;
        }

        public static int PurchaseLimit(int requested, int maxStack, int cursorStack, int stock)
        {
            return Math.Max(0, Math.Min(Math.Min(requested, Capacity(maxStack, cursorStack)), stock));
        }

        public static int CraftLimit(int requested, int maxStack, int cursorStack, int outputPerCraft)
        {
            if (outputPerCraft <= 0) return 0;
            return Math.Max(0, Math.Min(requested, Capacity(maxStack, cursorStack) / outputPerCraft));
        }
    }
}
