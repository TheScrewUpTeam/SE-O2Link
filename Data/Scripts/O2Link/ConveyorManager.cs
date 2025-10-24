using Sandbox.Game;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;

namespace TSUT.O2Link
{
    public class ConveyorManager
    {
        private readonly List<ManagedProducer> producers = new List<ManagedProducer>();
        private readonly List<ManagedStorage> o2Storage = new List<ManagedStorage>();
        private readonly List<ManagedConsumer> consumers = new List<ManagedConsumer>();
        private readonly List<ManagedCustom> customBlocks = new List<ManagedCustom>();
        private bool isValid;
        private IMyCubeBlock _referenceBlock;
        private readonly GridManager _gridManager;

        public ConveyorManager(GridManager gridManager)
        {
            _gridManager = gridManager;
            isValid = true;
        }

        private string GetBlockName(IMyCubeBlock block)
        {
            return block.Name;
        }

        public bool IsConveyorConnected(IMyCubeBlock block)
        {
            if (!isValid) return false;

            // Check if the new block is connected to our network (try both directions)
            string refName = GetBlockName(_referenceBlock);
            string blockName = GetBlockName(block);

            bool isConnected = MyVisualScriptLogicProvider.IsConveyorConnected(refName, blockName) ||
                             MyVisualScriptLogicProvider.IsConveyorConnected(blockName, refName);

            return isConnected;
        }

        public bool TryAddBlock(IMyCubeBlock block)
        {
            if (!isValid) return false;

            // If this is our first block, set it as reference and add it
            if (_referenceBlock == null)
            {
                _referenceBlock = block;
            }

            AddBlock(block);
            return true;
        }

        public void Update(float deltaTime)
        {
            if (!isValid) return;

            // Calculate O2 production
            float o2Production = CalculateO2Production(deltaTime);

            // Calculate remaining O2 needed from storage
            float o2FromStorage = CalculateO2Storage();

            float o2FromStorageConsumed = 0f;

            foreach (var consumer in consumers)
            {
                if (!consumer.IsWorking)
                    continue;

                float o2Needed = consumer.GetCurrentO2Consumption(deltaTime);
                consumer.UpdateInfo();


                if (o2Production >= o2Needed)
                {
                    o2Production -= o2Needed;
                    o2Needed = 0;
                }
                else
                {
                    o2Needed -= o2Production;
                    o2Production = 0;
                }
                if (o2Needed > 0 && o2FromStorage > o2Needed)
                {
                    o2FromStorageConsumed += o2Needed;
                    o2FromStorage -= o2Needed;
                    o2Needed = 0;
                }

                if (o2Needed > 0)
                {
                    consumer.Disable();
                    continue;
                }

                consumer.Enable();
            }

            ConsumeUsed(o2FromStorageConsumed);
        }

        private void ConsumeUsed(float o2FromStorageConsumed)
        {
            foreach (var storage in o2Storage)
            {
                if (o2FromStorageConsumed <= 0)
                    break;

                float currentStorage = storage.GetCurrentO2Storage();
                if (currentStorage <= 0)
                    continue;

                float amountToConsume = o2FromStorageConsumed > currentStorage ? currentStorage : o2FromStorageConsumed;
                storage.ConsumeO2(amountToConsume);
                o2FromStorageConsumed -= amountToConsume;
            }
        }

        private float CalculateO2Production(float deltaTime)
        {
            return producers.Where(p => p != null && p.IsWorking)
                          .Sum(p => p.GetCurrentO2Production(deltaTime));
        }

        private float CalculateO2Storage()
        {
            return o2Storage.Where(p => p != null && p.IsWorking)
                          .Sum(p => p.GetCurrentO2Storage());
        }

        public void AddBlock(IMyCubeBlock block)
        {
            if (!isValid) return;

            var terminalBlock = block as IMyTerminalBlock;
            if (terminalBlock == null) return; // For now, we still need terminal blocks for functionality

            var generator = terminalBlock as IMyGasGenerator;
            if (generator != null)
            {
                var producer = _gridManager.GetOrCreateProducer(generator);
                if (!producers.Contains(producer)){
                    producers.Add(producer);
                }
                return;
            }

            var vent = terminalBlock as IMyAirVent;
            if (vent != null)
            {
                var producer = _gridManager.GetOrCreateProducer(vent);
                if (!producers.Contains(producer)){
                    producers.Add(producer);
                }
                return;
            }

            var farm = terminalBlock as IMyOxygenFarm;
            if (farm != null)
            {
                var producer = _gridManager.GetOrCreateProducer(farm);
                if (!producers.Contains(producer)){
                    producers.Add(producer);
                }
                return;
            }

            var tank = terminalBlock as IMyGasTank;
            if (tank != null && (tank.BlockDefinition.SubtypeName.Contains("Oxygen") || tank.BlockDefinition.SubtypeName == ""))
            {
                var storage = _gridManager.GetOrCreateStorage(tank);
                if (!o2Storage.Contains(storage)){
                    o2Storage.Add(storage);
                }
                return;
            }

            var thruster = terminalBlock as IMyThrust;
            if (thruster != null && thruster.BlockDefinition.SubtypeName.Contains("HydrogenThrust"))
            {
                var consumer = _gridManager.GetOrCreateConsumer(thruster);
                if (!consumers.Contains(consumer)){
                    consumers.Add(consumer);
                }
                return;
            }

            var engine = terminalBlock as IMyPowerProducer;
            if (engine != null && engine.BlockDefinition.SubtypeName.Contains("HydrogenEngine"))
            {
                var consumer = _gridManager.GetOrCreateConsumer(engine);
                if (!consumers.Contains(consumer))
                {
                    consumers.Add(consumer);
                }
                return;
            }

            var custom = _gridManager.GetOrCreateCustom(terminalBlock);
            if (!customBlocks.Contains(custom))
            {
                customBlocks.Add(custom);
            }
        }

        public void RemoveBlock(IMyCubeBlock block)
        {
            if (!isValid) return;

            var terminalBlock = block as IMyTerminalBlock;
            if (terminalBlock == null) return; // For now, we still need terminal blocks for functionality

            if (terminalBlock is IMyGasGenerator)
            {
                producers.RemoveAll(p => p.Block == terminalBlock);
                return;
            }

            if (terminalBlock is IMyAirVent)
            {
                producers.RemoveAll(p => p.Block == terminalBlock);
                return;
            }

            if (terminalBlock is IMyOxygenFarm)
            {
                producers.RemoveAll(p => p.Block == terminalBlock);
                return;
            }

            var tank = terminalBlock as IMyGasTank;
            if (tank != null && tank.BlockDefinition.SubtypeName.Contains("Oxygen"))
            {
                o2Storage.RemoveAll(t => t.Block == terminalBlock);
                return;
            }

            if (terminalBlock is IMyThrust || terminalBlock is IMyPowerProducer)
            {
                var consumersToRemove = consumers.Where(c => c.Block == terminalBlock).ToList();
                foreach (var consumer in consumersToRemove)
                {
                    if (consumer != null)
                    {
                        consumer.Dismiss();
                    }
                }
                consumers.RemoveAll(c => c.Block == terminalBlock);
                return;
            }
            
            if (terminalBlock is IManagedCustom)
            {
                var customsToRemove = customBlocks.Where(c => c.Block == terminalBlock).ToList();
                foreach (var custom in customsToRemove)
                {
                    if (custom != null)
                    {
                        custom.Dismiss();
                    }
                }
                customBlocks.RemoveAll(c => c.Block == terminalBlock);
                return;
            }

            // If this was our reference block, pick a new one if available
            if (block == _referenceBlock)
            {
                _referenceBlock = GetAnyRemainingBlock();
            }
            // MyAPIGateway.Utilities.ShowMessage("O2Link", $"Block removed: {terminalBlock?.CustomName ?? block.DisplayNameText}, Consumers: {consumers.Count}");
        }

        private IMyCubeBlock GetAnyRemainingBlock()
        {
            return producers.FirstOrDefault()?.Block ??
                   o2Storage.FirstOrDefault()?.Block ??
                   consumers.FirstOrDefault()?.Block ??
                   customBlocks.FirstOrDefault()?.Block;
        }

        public class NetworkSplitResult
        {
            public bool IsEmpty { get; set; }
            public bool IsSplit { get; set; }
            public List<IMyCubeBlock> DisconnectedBlocks { get; set; }

            public NetworkSplitResult()
            {
                IsEmpty = false;
                IsSplit = false;
                DisconnectedBlocks = new List<IMyCubeBlock>();
            }
        }

        public NetworkSplitResult CheckNetworkIntegrity()
        {
            if (!isValid || _referenceBlock == null)
                return new NetworkSplitResult() { IsEmpty = true };

            var result = new NetworkSplitResult();
            var allBlocks = GetAllBlocks();

            // If we only have 0-1 blocks, no split is possible
            if (allBlocks.Count <= 1)
            {
                result.IsEmpty = true;
                return result;
            }

            foreach (var block in allBlocks)
            {
                // Skip reference block and already identified disconnected blocks
                if (block == _referenceBlock || result.DisconnectedBlocks.Contains(block))
                    continue;

                bool isConnected = MyVisualScriptLogicProvider.IsConveyorConnected(_referenceBlock.Name, block.Name) ||
                                 MyVisualScriptLogicProvider.IsConveyorConnected(block.Name, _referenceBlock.Name);

                if (!isConnected)
                {
                    result.IsSplit = true;
                    result.DisconnectedBlocks.Add(block);
                }
            }

            return result;
        }

        private List<IMyCubeBlock> GetAllBlocks()
        {
            var blocks = new List<IMyCubeBlock>();
            lock (producers)
            {
                blocks.AddRange(producers.Where(b => b != null).Select(p => p.Block));
            }
            lock (o2Storage)
            {
                blocks.AddRange(o2Storage.Where(b => b != null).Select(s => s.Block));
            }
            lock (consumers)
            {
                blocks.AddRange(consumers.Where(b => b != null).Select(c => c.Block));
            }
            lock (customBlocks)
            {
                blocks.AddRange(customBlocks.Where(b => b != null).Select(c => c.Block));
            }
            return blocks;
        }

        public void Invalidate()
        {
            isValid = false;
            producers.Clear();
            o2Storage.Clear();
            consumers.Clear();
            customBlocks.Clear();
        }

        public bool IsValid => isValid;
        public bool HasConsumers => consumers.Any();
    }
}