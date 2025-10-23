using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace TSUT.O2Link
{
    public interface IManagedStorage
    {
        float GetCurrentO2Storage();
        bool IsWorking { get; }
        IMyTerminalBlock Block { get; }
    }

    public class ManagedStorage : IManagedStorage
    {
        protected readonly IMyTerminalBlock _block;

        public ManagedStorage(IMyTerminalBlock block)
        {
            _block = block;
        }

        public bool IsWorking => _block.IsWorking && !(_block as IMyGasTank).Stockpile;

        public IMyTerminalBlock Block => _block;

        public float GetCurrentO2Storage()
        {
            if (_block is IMyGasTank tank)
            {
                var filledRatio = tank.FilledRatio;
                var capacity = tank.Capacity;
                var currentAmount = filledRatio * capacity;
                return (float)currentAmount;
            }
            return 0f;
        }
    }
}