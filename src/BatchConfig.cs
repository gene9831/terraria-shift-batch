using System;
using System.Collections.Generic;
using System.Globalization;
using TerrariaModder.Core.Config;

namespace ShiftBatch
{
    public sealed class BatchConfig : ModConfig
    {
        private volatile int purchase = BatchRules.DefaultBatch;
        private volatile int craft = BatchRules.DefaultBatch;
        private readonly List<string> warnings = new List<string>();

        public override int Version { get { return 2; } }

        [Client, Label("Enabled / 启用"), Description("Restore vanilla behavior when disabled. 关闭后恢复原版行为。")]
        public bool Enabled { get; set; }

        // Core's int coercion rounds JSON doubles. Text-backed fields let us reject
        // fractional/invalid input on EVERY route (F6, manual JSON and hot reload).
        [Client, Label("Shift buy / 购买数量"), Description("10-9999 items per Shift batch; default 10. 输入整数，回车确认；默认10。")]
        public string PurchaseBatch
        {
            get { return purchase.ToString(CultureInfo.InvariantCulture); }
            set { purchase = Normalize(value, purchase, "购买数量 / Purchase batch"); }
        }

        [Client, Label("Shift craft / 制作次数"), Description("10-9999 recipe executions per Shift batch; default 10. 按配方次数计数，自动限制产物堆叠。")]
        public string CraftBatch
        {
            get { return craft.ToString(CultureInfo.InvariantCulture); }
            set { craft = Normalize(value, craft, "制作次数 / Craft batch"); }
        }

        public int PurchaseCount { get { return purchase; } }
        public int CraftCount { get { return craft; } }

        [Client, Label("Auto-stow / 满堆叠收包"), Description("Off by default. Keep holding Shift+mouse at the same shop/recipe to stow after 0.3s. 默认关闭；持续长按约0.3秒后收包，松键取消。")]
        public bool AutoStow { get; set; }

        [Client, Label("Limit sound / 满堆叠提示音"), Description("With auto-stow enabled, play once when capacity is reached, BEFORE the delay. 收包功能启用时，达到容量上限立即提示，方便及时松键。")]
        public bool LimitSound { get; set; }

        public BatchConfig()
        {
            // ConfigManager creates us BEFORE applying disk values. Install the
            // scoped raw-number guard here so first load is protected as well.
            ConfigInputGuard.EnsureInstalled();
            Enabled = true;
            AutoStow = false;
            LimitSound = true;
        }

        private int Normalize(string text, int previous, string field)
        {
            int value;
            if (!BatchRules.TryNormalize(text, previous, out value))
            {
                AddWarning(field + ": enter an integer 10-9999; kept " + previous + ". 请输入10-9999的整数，已保留原值。");
                return value;
            }
            int original;
            if (Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out original) && original != value)
                AddWarning(field + ": limited to " + value + ". 已限制到有效范围。 ");
            return value;
        }

        private void AddWarning(string message)
        {
            lock (warnings)
            {
                // Bound errors if a broken external editor repeatedly reloads the file.
                if (warnings.Count < 8) warnings.Add(message);
            }
        }

        public string[] TakeWarnings()
        {
            lock (warnings)
            {
                string[] result = warnings.ToArray();
                warnings.Clear();
                return result;
            }
        }
    }
}
