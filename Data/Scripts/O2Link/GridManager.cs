using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using System.Linq;
using Sandbox.ModAPI.Interfaces;
using VRage.Collections;

namespace TSUT.O2Link
{
    public class GridManager
    {
        private readonly IMyCubeGrid _grid;
        private readonly List<ConveyorManager> conveyorManagers;
        private readonly Dictionary<IMyTerminalBlock, ConveyorManager> blockToManager;
        private bool _isValid;

        public GridManager(IMyCubeGrid grid)
        {
            _grid = grid;
            conveyorManagers = new List<ConveyorManager>();
            blockToManager = new Dictionary<IMyTerminalBlock, ConveyorManager>();
            _isValid = true;
            
            _grid.OnBlockAdded += OnBlockAdded;
            _grid.OnBlockRemoved += OnBlockRemoved;
            
            Initialize();
        }

        private void Initialize()
        {
            var terminalSystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(_grid);
            if (terminalSystem == null)
                return;

            var relevantBlocks = new List<IMyTerminalBlock>();
            
            // Get only the block types we care about
            terminalSystem.GetBlocksOfType<IMyGasGenerator>(relevantBlocks, b => b.BlockDefinition.SubtypeName.Contains("OxygenHydrogen"));
            terminalSystem.GetBlocksOfType<IMyGasTank>(relevantBlocks, b => b.BlockDefinition.SubtypeName.Contains("Oxygen") || 
                                                                           b.BlockDefinition.SubtypeName.Contains("Hydrogen"));
            terminalSystem.GetBlocksOfType<IMyThrust>(relevantBlocks, b => b.BlockDefinition.SubtypeName.Contains("Hydrogen"));
            terminalSystem.GetBlocksOfType<IMyPowerProducer>(relevantBlocks, b => b.BlockDefinition.SubtypeName.Contains("Hydrogen"));

            // Create initial conveyor networks
            foreach (var block in relevantBlocks)
            {
                if (blockToManager.ContainsKey(block))
                    continue;

                bool added = false;
                
                // Try to add to existing networks first
                foreach (var manager in conveyorManagers)
                {
                    if (manager.TryAddBlock(block))
                    {
                        blockToManager[block] = manager;
                        added = true;
                        break;
                    }
                }

                // If not added to any existing network, create a new one
                if (!added)
                {
                    var newManager = new ConveyorManager();
                    conveyorManagers.Add(newManager);
                    blockToManager[block] = newManager;
                    newManager.TryAddBlock(block);
                }
            }
        }


        private List<IMyTerminalBlock> FindConnectedBlocks(IMyTerminalBlock startBlock)
        {
            var result = new List<IMyTerminalBlock>();
            var toCheck = new List<IMyTerminalBlock>();
            var checked_ = new List<IMyTerminalBlock>();
            
            toCheck.Add(startBlock);
            result.Add(startBlock);
            checked_.Add(startBlock);

            while (toCheck.Count > 0)
            {
                var current = toCheck[0];
                toCheck.RemoveAt(0);
                
                var connected = GetConnectedBlocks(current);

                foreach (var block in connected)
                {
                    if (!checked_.Contains(block))
                    {
                        result.Add(block);
                        toCheck.Add(block);
                        checked_.Add(block);
                    }
                }
            }

            return result;
        }

        private List<IMyTerminalBlock> GetConnectedBlocks(IMyTerminalBlock block)
        {
            var result = new List<IMyTerminalBlock>();
            
            if (!block.HasInventory)
                return result;

            var inventory = block.GetInventory();
            var terminalSystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(_grid);
            
            if (terminalSystem == null)
                return result;

            var candidates = new List<IMyTerminalBlock>();
            
            // Get only the block types we care about
            terminalSystem.GetBlocksOfType<IMyGasGenerator>(candidates);
            terminalSystem.GetBlocksOfType<IMyGasTank>(candidates, b => b.BlockDefinition.SubtypeName.Contains("Oxygen"));
            terminalSystem.GetBlocksOfType<IMyThrust>(candidates, b => b.BlockDefinition.SubtypeName.Contains("HydrogenThrust"));
            terminalSystem.GetBlocksOfType<IMyPowerProducer>(candidates, b => b.BlockDefinition.SubtypeName.Contains("HydrogenEngine"));

            foreach (var otherBlock in candidates)
            {
                if (!otherBlock.HasInventory || otherBlock == block)
                    continue;

                var otherInventory = otherBlock.GetInventory();
                if (inventory.IsConnectedTo(otherInventory))
                {
                    result.Add(otherBlock);
                }
            }

            return result;
        }

        private void OnBlockAdded(IMySlimBlock block)
        {
            if (!_isValid || block?.FatBlock == null)
                return;

            var terminalBlock = block.FatBlock as IMyTerminalBlock;
            if (terminalBlock == null)
                return;

            // Check if it's one of our relevant block types
            if (!(terminalBlock is IMyGasGenerator || 
                  terminalBlock is IMyGasTank ||
                  terminalBlock is IMyThrust ||
                  terminalBlock is IMyPowerProducer))
                return;

            // Try to add to existing networks first
            List<ConveyorManager> connectedManagers = new List<ConveyorManager>();
            
            foreach (var manager in conveyorManagers)
            {
                if (manager.TryAddBlock(terminalBlock))
                {
                    connectedManagers.Add(manager);
                }
            }

            if (connectedManagers.Count == 0)
            {
                // No existing networks found, create new one
                var newManager = new ConveyorManager();
                conveyorManagers.Add(newManager);
                blockToManager[terminalBlock] = newManager;
                newManager.TryAddBlock(terminalBlock);
            }
            else if (connectedManagers.Count == 1)
            {
                // Add to single existing network
                blockToManager[terminalBlock] = connectedManagers[0];
            }
            else
            {
                // Multiple networks found, need to merge them
                var targetManager = connectedManagers[0];
                blockToManager[terminalBlock] = targetManager;

                // Merge other networks into the target
                foreach (var manager in connectedManagers.Skip(1))
                {
                    MergeNetworks(manager, targetManager);
                }
            }
        }

        private void OnBlockRemoved(IMySlimBlock block)
        {
            if (!_isValid || block?.FatBlock == null)
                return;

            var terminalBlock = block.FatBlock as IMyTerminalBlock;
            if (terminalBlock == null || !blockToManager.ContainsKey(terminalBlock))
                return;

            var oldManager = blockToManager[terminalBlock];
            blockToManager.Remove(terminalBlock);
            oldManager.RemoveBlock(terminalBlock);

            // Check if network needs to be split
            CheckNetworkSplit(oldManager);
        }

        private void CheckNetworkSplit(ConveyorManager manager)
        {
            var splitResult = manager.CheckNetworkIntegrity();
            
            if (!splitResult.IsSplit)
                return;

            // Create a new network for disconnected blocks
            var newManager = new ConveyorManager();
            conveyorManagers.Add(newManager);

            // Move disconnected blocks to the new network
            foreach (var block in splitResult.DisconnectedBlocks)
            {
                blockToManager[block] = newManager;
                newManager.TryAddBlock(block);
                manager.RemoveBlock(block);
            }
        }

        private void MergeNetworks(ConveyorManager source, ConveyorManager target)
        {
            // Update block-to-manager mapping
            foreach (var kvp in blockToManager.ToList())
            {
                if (kvp.Value == source)
                {
                    blockToManager[kvp.Key] = target;
                }
            }

            // Let the source manager clean up
            source.Invalidate();
        }

        public void Update(float deltaTime = 1f/60f)
        {
            if (!_isValid) return;

            foreach (var manager in conveyorManagers)
            {
                if (manager.IsValid)
                {
                    manager.Update(deltaTime);
                }
            }
        }

        public void Invalidate()
        {
            if (!_isValid) return;
            
            _isValid = false;
            _grid.OnBlockAdded -= OnBlockAdded;
            _grid.OnBlockRemoved -= OnBlockRemoved;

            foreach (var manager in conveyorManagers)
            {
                manager.Invalidate();
            }
            conveyorManagers.Clear();
            blockToManager.Clear();
        }

        public bool IsValid => _isValid;
        public IMyCubeGrid Grid => _grid;
    }
}