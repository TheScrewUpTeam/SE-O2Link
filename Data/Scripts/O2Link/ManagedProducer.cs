using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;

namespace TSUT.O2Link
{
    public interface IManagedProducer
    {
        float GetCurrentO2Production(float deltaTime);
        bool IsWorking { get; }
        IMyTerminalBlock Block { get; }
    }
    
    public class ManagedProducer : IManagedProducer
    {
        protected readonly IMyTerminalBlock _block;
        
        public ManagedProducer(IMyTerminalBlock block)
        {
            _block = block;
        }

        public bool IsWorking => _block.IsWorking;

        public IMyTerminalBlock Block => _block;

        public float GetCurrentO2Production(float deltaTime)
        {
            if (Block.IsWorking == false)
                return 0f;
            if (Block is IMyAirVent)
            {
                var vent = Block as IMyAirVent;
                if (!vent.Depressurize)
                    return 0f;
            }
            if (Block is IMyOxygenFarm)
            {
                var farm = Block as IMyOxygenFarm;
                if (!farm.CanProduce)
                    return 0f;
            }
            var sourceComp = _block.Components.Get<MyResourceSourceComponent>();
            var resourceId = MyResourceDistributorComponent.OxygenId;
            var maxOutput = sourceComp.MaxOutputByType(resourceId);
            var currentOutput = sourceComp.CurrentOutputByType(resourceId);
            var availableOutput = maxOutput - currentOutput;
            return availableOutput * deltaTime;
        }
    }
}