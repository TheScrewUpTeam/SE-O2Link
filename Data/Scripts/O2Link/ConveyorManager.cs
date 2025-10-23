using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using System.Linq;

namespace TSUT.O2Link
{
    public class ConveyorManager
    {
        private readonly List<ManagedProducer> producers = new List<ManagedProducer>();
        private readonly List<ManagedStorage> o2Storage = new List<ManagedStorage>();
        private readonly List<ManagedConsumer> consumers = new List<ManagedConsumer>();
        private bool isValid;
        private IMyTerminalBlock _referenceBlock;

        public ConveyorManager()
        {
            isValid = true;
        }

        public bool TryAddBlock(IMyTerminalBlock block)
        {
            if (!isValid) return false;

            // If this is our first block, set it as reference and add it
            if (_referenceBlock == null)
            {
                _referenceBlock = block;
                AddBlock(block);
                return true;
            }

            // Check if the new block is connected to our network
            if (!Sandbox.Game.MyVisualScriptLogicProvider.IsConveyorConnected(_referenceBlock.Name, block.Name))
            {
                return false;
            }

            // Block is connected, add it to our network
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

            foreach (var consumer in consumers)
            {
                float o2Needed = consumer.GetCurrentO2Consumption(deltaTime);
                if (o2Production >= o2Needed)
                {
                    o2Production -= o2Needed;
                }
                else
                {
                    float remainingNeeded = o2Needed - o2Production;
                    o2Production = 0;
                    o2FromStorage -= remainingNeeded;
                }

                if (o2FromStorage < 0)
                {
                    consumer.Disable();
                    continue;
                }

                consumer.Enable();
            }
        }

        private float CalculateO2Production(float deltaTime)
        {
            return producers.Where(p => p.IsWorking)
                          .Sum(p => p.GetCurrentO2Production(deltaTime));
        }

        private float CalculateO2Storage()
        {
            return o2Storage.Where(p => p.IsWorking)
                          .Sum(p => p.GetCurrentO2Storage());
        }

        public void AddBlock(IMyTerminalBlock block)
        {
            if (!isValid) return;

            if (block is IMyGasGenerator generator)
            {
                producers.Add(new ManagedProducer(generator));
            }
            else if (block is IMyGasTank tank && tank.BlockDefinition.SubtypeName.Contains("Oxygen"))
            {
                o2Storage.Add(new ManagedStorage(tank));
            }
            else if (block is IMyThrust thruster && thruster.BlockDefinition.SubtypeName.Contains("HydrogenThrust"))
            {
                consumers.Add(new ManagedConsumer(thruster));
            }
            else if (block is IMyPowerProducer engine && engine.BlockDefinition.SubtypeName.Contains("HydrogenEngine"))
            {
                consumers.Add(new ManagedConsumer(engine));
            }
        }

        public void RemoveBlock(IMyTerminalBlock block)
        {
            if (!isValid) return;

            if (block is IMyGasGenerator)
            {
                producers.RemoveAll(p => p.Block == block);
            }
            else if (block is IMyGasTank tank && tank.BlockDefinition.SubtypeName.Contains("Oxygen"))
            {
                o2Storage.RemoveAll(t => t.Block == block);
            }
            else if (block is IMyThrust || block is IMyPowerProducer)
            {
                consumers.RemoveAll(c => c.Block == block);
            }

            // If this was our reference block, pick a new one if available
            if (block == _referenceBlock)
            {
                _referenceBlock = GetAnyRemainingBlock();
            }
        }

        private IMyTerminalBlock GetAnyRemainingBlock()
        {
            return producers.FirstOrDefault()?.Block ?? 
                   o2Storage.FirstOrDefault()?.Block ?? 
                   consumers.FirstOrDefault()?.Block;
        }

        public class NetworkSplitResult
        {
            public bool IsSplit { get; set; }
            public List<IMyTerminalBlock> DisconnectedBlocks { get; set; }

            public NetworkSplitResult()
            {
                IsSplit = false;
                DisconnectedBlocks = new List<IMyTerminalBlock>();
            }
        }

        public NetworkSplitResult CheckNetworkIntegrity()
        {
            if (!isValid || _referenceBlock == null)
                return new NetworkSplitResult();

            var result = new NetworkSplitResult();
            var allBlocks = GetAllBlocks();

            // If we only have 0-1 blocks, no split is possible
            if (allBlocks.Count <= 1)
                return result;

            foreach (var block in allBlocks)
            {
                // Skip reference block and already identified disconnected blocks
                if (block == _referenceBlock || result.DisconnectedBlocks.Contains(block))
                    continue;

                if (!Sandbox.Game.MyVisualScriptLogicProvider.IsConveyorConnected(_referenceBlock.Name, block.Name))
                {
                    result.IsSplit = true;
                    result.DisconnectedBlocks.Add(block);
                }
            }

            return result;
        }

        private List<IMyTerminalBlock> GetAllBlocks()
        {
            var blocks = new List<IMyTerminalBlock>();
            blocks.AddRange(producers.Select(p => p.Block));
            blocks.AddRange(o2Storage.Select(s => s.Block));
            blocks.AddRange(consumers.Select(c => c.Block));
            return blocks;
        }

        public void Invalidate()
        {
            isValid = false;
            producers.Clear();
            o2Storage.Clear();
            consumers.Clear();
        }

        public bool IsValid => isValid;
        public bool HasConsumers => consumers.Any();
    }
}