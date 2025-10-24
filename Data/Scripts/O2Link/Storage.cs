using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace TSUT.O2Link
{
    public static class Storage
    {
        public static void SaveBlockState(IMyFunctionalBlock block, bool enabled)
        {
            if (block.Storage == null)
            {
                block.Storage = new MyModStorageComponent();
            }
            block.Storage.SetValue(Config.EnabledStorageGuid, enabled ? "1" : "0");
        }

        public static bool LoadBlockState(IMyFunctionalBlock block)
        {
            if (block.Storage == null)
            {
                return block.Enabled;
            }
            string value;
            if (block.Storage.TryGetValue(Config.EnabledStorageGuid, out value))
            {
                return value == "1";
            }
            return block.Enabled;
        }
    }
}