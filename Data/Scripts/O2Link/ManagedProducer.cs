using System.Runtime.Remoting.Messaging;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

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
            var sourceComp = _block.Components.Get<MyResourceSourceComponent>();
            var resourceId = MyResourceDistributorComponent.OxygenId;
            var maxOutput = sourceComp.MaxOutputByType(resourceId);
            var currentOutput = sourceComp.CurrentOutputByType(resourceId);
            var availableOutput = maxOutput - currentOutput;
            return availableOutput * deltaTime;
        }
    }
}