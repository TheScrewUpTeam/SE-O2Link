using VRage.Game.ObjectBuilders.Definitions;
using Sandbox.ModAPI;
using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using VRage.Game;
using System.Collections.Generic;
using Sandbox.ModAPI.Interfaces.Terminal;

namespace TSUT.O2Link
{
    public interface IManagedConsumer
    {
        float GetCurrentO2Consumption(float deltaTime);
        IMyTerminalBlock Block { get; }
    }

    public class ManagedConsumer : IManagedBlock, IManagedConsumer
    {
        protected readonly IMyTerminalBlock _block;
        bool _switchSubscribed = false;
        bool _nextCallINternal = false;
        bool _playerWantsOn;

        public ManagedConsumer(IMyTerminalBlock block)
        {
            _block = block;
            MyAPIGateway.TerminalControls.CustomControlGetter += OnCustomControlGetter;
            if (_block is IMyFunctionalBlock){
                (_block as IMyFunctionalBlock).EnabledChanged += Block_EnabledChanged;
            }
        }

        private void Block_EnabledChanged(IMyTerminalBlock block)
        {
            if (_nextCallINternal)
            {
                _nextCallINternal = false;
                return;
            }
            _playerWantsOn = (block as IMyFunctionalBlock).Enabled;
        }

        private void OnCustomControlGetter(IMyTerminalBlock topBlock, List<IMyTerminalControl> controls)
        {
            if (topBlock != _block || _switchSubscribed)
                return;
            foreach (var control in controls)
            {
                if (control.Id == "OnOff")
                {
                    var onOffControl = control as IMyTerminalControlOnOffSwitch;
                    if (onOffControl != null)
                    {
                        onOffControl.Getter += (block) =>
                        {
                            if (block == _block)
                                return _playerWantsOn;
                            return (block as IMyFunctionalBlock).Enabled;
                        };
                        onOffControl.Setter += (block, value) =>
                        {
                            if (block != _block)
                                return;

                            _playerWantsOn = value;
                        };
                        _switchSubscribed = true;
                    }
                }
            }
        }

        public bool IsWorking => _block.IsWorking;

        public IMyTerminalBlock Block => _block;

        public void Disable()
        {
            if (_block is IMyFunctionalBlock){
                _nextCallINternal = true;
                (_block as IMyFunctionalBlock).Enabled = false;
            }
        }

        public void Dismiss()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter -= OnCustomControlGetter;
            if (_block is IMyFunctionalBlock){
                (_block as IMyFunctionalBlock).EnabledChanged -= Block_EnabledChanged;
            }
        }

        public void Enable()
        {
            if (_block is IMyFunctionalBlock){
                _nextCallINternal = true;
                (_block as IMyFunctionalBlock).Enabled = true;
            }
        }

        public float GetCurrentO2Consumption(float deltaTime)
        {
            float h2consumption = GetCurrentH2Consumption();
            float o2consumption = h2consumption * Config.Instance.O2_FROM_H2_RATIO;
            return o2consumption * deltaTime;
        }

        private float GetCurrentH2Consumption()
        {
            if (_block is IMyThrust)
            {
                var thruster = _block as IMyThrust;
                var def = thruster.SlimBlock.BlockDefinition as MyThrustDefinition;
                var fuelConv = def.FuelConverter;
                
                return thruster.CurrentThrust * fuelConv.Efficiency / 1500f; // Convert from kN to L/s
            }
            else if (_block is IMyPowerProducer)
            {
                var engine = _block as IMyPowerProducer;
                var sink = engine?.Components.Get<MyResourceSinkComponent>();
                if (sink == null)
                    return 0f;

                var hydrogenId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Hydrogen");
                var currentConsumption = sink.CurrentInputByType(hydrogenId);

                return currentConsumption; // L/s
            } else {
                return 0f;
            }
        }
    }
}