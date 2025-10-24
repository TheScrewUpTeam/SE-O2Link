using Sandbox.ModAPI;
using Sandbox.Definitions;
using System.Collections.Generic;
using Sandbox.ModAPI.Interfaces.Terminal;
using System.Text;

namespace TSUT.O2Link
{
    public interface IManagedConsumer
    {
        float GetCurrentO2Consumption(float deltaTime);
        IMyTerminalBlock Block { get; }
        void UpdateInfo();
    }

    public class ManagedConsumer : IManagedBlock, IManagedConsumer
    {
        protected IMyTerminalBlock _block;
        bool _switchSubscribed = false;
        bool _nextCallINternal = false;
        float _cachedO2Consumption = 0f;
        bool _playerWantsOn;
        bool _isValid = true;

        public ManagedConsumer(IMyTerminalBlock block)
        {
            _block = block;
            MyAPIGateway.TerminalControls.CustomControlGetter += OnCustomControlGetter;
            if (_block is IMyFunctionalBlock)
            {
                (_block as IMyFunctionalBlock).EnabledChanged += Block_EnabledChanged;
                _playerWantsOn = Storage.LoadBlockState(_block as IMyFunctionalBlock);
            }
            _block.AppendingCustomInfo += AppendCustomInfo;
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder builder)
        {
            if (!_isValid || _block == null)
                return;

            var functionalBlock = _block as IMyFunctionalBlock;
            if (functionalBlock == null)
                return;

            var isOn = functionalBlock.Enabled;
            builder.AppendLine("--- O2Link ---");
            if (_playerWantsOn)
            {
                var cons = _cachedO2Consumption > 0f ? _cachedO2Consumption : GetCurrentO2Consumption(1f);
                builder.AppendLine($"O2 consumption: {cons:F2} L/s");
                builder.AppendLine($"O2 supply: {(isOn ? "Supplied" : "Not Enough")}");
            } else {
                builder.AppendLine($"O2 consumption: 0 L/s");
            }
        }

        private void Block_EnabledChanged(IMyTerminalBlock block)
        {
            if (!_isValid || _block == null)
                return;

            if (_nextCallINternal)
            {
                _nextCallINternal = false;
                return;
            }
            var functionalBlock = block as IMyFunctionalBlock;
            if (functionalBlock != null)
            {
                _playerWantsOn = functionalBlock.Enabled;
            }
            Storage.SaveBlockState(block as IMyFunctionalBlock, _playerWantsOn);
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

        public bool IsWorking => _playerWantsOn;

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
            _isValid = false;
            if (_block == null) {
                return;
            }
            _block.RefreshCustomInfo();
            _block.AppendingCustomInfo -= AppendCustomInfo;
            MyAPIGateway.TerminalControls.CustomControlGetter -= OnCustomControlGetter;
            if (_block is IMyFunctionalBlock){
                (_block as IMyFunctionalBlock).EnabledChanged -= Block_EnabledChanged;
            }
            _block.ClearDetailedInfo();
            _block.SetDetailedInfoDirty();
            _block = null;
        }

        public void Enable()
        {
            if (_playerWantsOn == false)
                return;
            if (_block is IMyFunctionalBlock){
                _nextCallINternal = true;
                (_block as IMyFunctionalBlock).Enabled = true;
                _cachedO2Consumption = 0f;
            }
        }

        public float GetCurrentO2Consumption(float deltaTime)
        {
            if (_cachedO2Consumption > 0f)
                return _cachedO2Consumption * deltaTime;

            float h2consumption = GetCurrentH2Consumption();
            _cachedO2Consumption = h2consumption * Config.Instance.O2_FROM_H2_RATIO;
            return _cachedO2Consumption * deltaTime;
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
                if (engine == null)
                    return 0f;

                var def = engine.SlimBlock.BlockDefinition as MyHydrogenEngineDefinition;
                if (def == null)
                    return 0f;

                // Get current power output (in MW)
                float currentPowerOutput = engine.CurrentOutput;
                float maxPowerOutput = def.MaxPowerOutput;
                float fuelCapacity = def.FuelCapacity;
                float multiplier = def.FuelProductionToCapacityMultiplier;

                // Calculate consumption based on power output
                float consumption = (currentPowerOutput / maxPowerOutput) * fuelCapacity * multiplier / 2;

                return consumption;
            } else {
                return 0f;
            }
        }

        public void UpdateInfo()
        {
            if (!_isValid || _block == null)
                return;
            _block.RefreshCustomInfo();
            _block.SetDetailedInfoDirty();
        }
    }
}