using System;

namespace ShiftBatch
{
    [Flags]
    public enum StowDecision { None = 0, ReachedLimit = 1, Transfer = 2 }

    // Evaluated only by freshly observed native shop/craft interactions. A timer
    // never transfers on its own, so leaving the target cannot fire stale work.
    public sealed class AutoStowState
    {
        public const uint DelayTicks = 18;
        private object source;
        private int slot;
        private object cursor;
        private int stack;
        private uint started;
        private bool pending;
        private bool blocked;
        private bool seenThisDraw;

        public StowDecision Observe(uint now, object newSource, int newSlot, object newCursor, int newStack, bool atLimit)
        {
            if (!atLimit || newSource == null || newCursor == null || newStack <= 0)
            {
                Reset();
                return StowDecision.None;
            }
            seenThisDraw = true;
            bool changed = !pending || !Object.ReferenceEquals(source, newSource) || slot != newSlot ||
                !Object.ReferenceEquals(cursor, newCursor) || stack != newStack;
            if (changed)
            {
                source = newSource; slot = newSlot; cursor = newCursor; stack = newStack;
                started = now; pending = true; blocked = false;
                return StowDecision.ReachedLimit;
            }
            return !blocked && unchecked(now - started) >= DelayTicks ? StowDecision.Transfer : StowDecision.None;
        }

        public void Block() { blocked = true; }

        public void EndDraw()
        {
            // Rendering may run below or above 60fps. Cancel on a rendered frame
            // without the target, rather than assuming one draw per update tick.
            if (!seenThisDraw) Reset();
            else seenThisDraw = false;
        }

        public void Reset()
        {
            source = null; cursor = null; slot = 0; stack = 0;
            started = 0; pending = false; blocked = false; seenThisDraw = false;
        }
    }
}
