using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using System.Linq;
using VRage.Game.Components;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.ModAPI;
using SpaceEngineers.Game.ModAPI;

namespace TSUT.O2Link
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CubeGrid), false)]
    public class GridManager : MyGameLogicComponent
    {
        private IMyCubeGrid _grid;
        private readonly List<ConveyorManager> conveyorManagers = new List<ConveyorManager>();
        private readonly Dictionary<IMyCubeBlock, ConveyorManager> blockToManager = new Dictionary<IMyCubeBlock, ConveyorManager>();
        private bool _isInitialized = false;
        private int updateCounter = 0;
        private int scheduledProcess = 0;
        private readonly List<IMyCubeBlock> blocksToProcess = new List<IMyCubeBlock>();

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void Close()
        {
            base.Close();
        }

        private void Initialize(IMyCubeGrid grid)
        {
            _grid = grid;
            _isInitialized = true;

            _grid.OnBlockAdded += OnBlockAdded;
            _grid.OnBlockRemoved += OnBlockRemoved;

            var terminalSystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(_grid);
            if (terminalSystem == null)
                return;

            var generators = new List<IMyTerminalBlock>();
            var tanks = new List<IMyTerminalBlock>();
            var thrusters = new List<IMyTerminalBlock>();
            var engines = new List<IMyTerminalBlock>();
            var vents = new List<IMyTerminalBlock>();
            var farms = new List<IMyTerminalBlock>();

            // Get each block type separately
            terminalSystem.GetBlocksOfType<IMyGasGenerator>(generators);
            terminalSystem.GetBlocksOfType<IMyAirVent>(vents);
            terminalSystem.GetBlocksOfType<IMyOxygenFarm>(farms);
            terminalSystem.GetBlocksOfType<IMyGasTank>(tanks/*, b => b.BlockDefinition.SubtypeName.Contains("Oxygen") || b.BlockDefinition.SubtypeName == ""*/);
            terminalSystem.GetBlocksOfType<IMyThrust>(thrusters, b => b.BlockDefinition.SubtypeName.Contains("HydrogenThrust"));
            terminalSystem.GetBlocksOfType<IMyPowerProducer>(engines, b => b.BlockDefinition.SubtypeName.Contains("HydrogenEngine"));

            // Combine all blocks into one list
            var relevantBlocks = new List<IMyTerminalBlock>();
            relevantBlocks.AddRange(generators);
            relevantBlocks.AddRange(vents);
            relevantBlocks.AddRange(farms);
            relevantBlocks.AddRange(tanks);
            relevantBlocks.AddRange(thrusters);
            relevantBlocks.AddRange(engines);

            // Create initial conveyor networks
            foreach (var block in relevantBlocks)
            {
                OnBlockAdded(block.SlimBlock);
            }
        }

        public override void UpdateAfterSimulation()
        {
            if (!_isInitialized)
            {
                Initialize(Entity as IMyCubeGrid);
                return;
            }
            if (scheduledProcess > 0 && updateCounter >= scheduledProcess)
            {
                ProcessScheduledBlocks();
                blocksToProcess.Clear();
                scheduledProcess = 0;
            }
            updateCounter++;
            if (updateCounter % Config.Instance.MAIN_LOOP_INTERVAL != 0) return;
            var deltaTime = updateCounter * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
            Update(deltaTime);
        }

        private bool IsRelevantTerminalBlock(IMyTerminalBlock block)
        {
            return block is IMyAirVent ||
                   block is IMyOxygenFarm ||
                   block is IMyGasGenerator ||
                   block is IMyGasTank ||
                   block is IMyThrust ||
                   block is IMyPowerProducer;
        }

        private void OnBlockAdded(IMySlimBlock block)
        {
            if (block?.FatBlock == null)
                return;

            var cubeBlock = block.FatBlock;

            scheduledProcess = updateCounter + 20;
            blocksToProcess.Add(cubeBlock);
        }

        private void ProcessScheduledBlocks()
        {
            if (!_isInitialized) return;
            foreach (var cubeBlock in blocksToProcess)
            {
                if (blockToManager.ContainsKey(cubeBlock))
                    continue;
                var terminalBlock = cubeBlock as IMyTerminalBlock;
                bool isCoveredBlock = terminalBlock != null && IsRelevantTerminalBlock(terminalBlock);

                string blockName = terminalBlock?.CustomName ?? cubeBlock.DisplayNameText;

                // Try to add to existing networks first
                List<ConveyorManager> connectedManagers = new List<ConveyorManager>();

                foreach (var manager in conveyorManagers)
                {
                    if (manager.IsConveyorConnected(cubeBlock))
                    {
                        connectedManagers.Add(manager);
                        if (isCoveredBlock && terminalBlock != null)
                        {
                            manager.TryAddBlock(terminalBlock);
                        }
                    }
                }

                if (connectedManagers.Count == 0)
                {
                    if (isCoveredBlock && terminalBlock != null)
                    {
                        // No existing networks found, create new one
                        var newManager = new ConveyorManager();
                        conveyorManagers.Add(newManager);
                        blockToManager[cubeBlock] = newManager;
                        newManager.TryAddBlock(terminalBlock);
                    }
                }
                else if (connectedManagers.Count == 1)
                {
                    // Add to single existing network
                    blockToManager[cubeBlock] = connectedManagers[0];
                    if (isCoveredBlock && terminalBlock != null)
                    {
                        connectedManagers[0].TryAddBlock(terminalBlock);
                    }
                }
                else
                {
                    // Multiple networks found, need to merge them
                    var targetManager = connectedManagers[0];
                    blockToManager[cubeBlock] = targetManager;
                    if (isCoveredBlock && terminalBlock != null)
                    {
                        targetManager.TryAddBlock(terminalBlock);
                    }

                    // Merge other networks into the target
                    foreach (var manager in connectedManagers.Skip(1))
                    {
                        MergeNetworks(manager, targetManager);
                        conveyorManagers.Remove(manager);
                    }
                }
            }
            MyAPIGateway.Utilities.ShowMessage("O2Link", $"Blocks added: {blocksToProcess.Count}, Conveyor Networks: {conveyorManagers.Count}");
        }

        private void OnBlockRemoved(IMySlimBlock block)
        {
            if (!_isInitialized || block?.FatBlock == null)
                return;

            var cubeBlock = block.FatBlock;
            if (!blockToManager.ContainsKey(cubeBlock))
                return;

            var terminalBlock = cubeBlock as IMyTerminalBlock;
            
            var oldManager = blockToManager[cubeBlock];
            blockToManager.Remove(cubeBlock);
            
            if (terminalBlock != null)
            {
                oldManager.RemoveBlock(terminalBlock);
            }

            // Check if network needs to be split
            CheckNetworkSplit(oldManager);
            MyAPIGateway.Utilities.ShowMessage("O2Link", $"Block removed: {terminalBlock?.CustomName ?? cubeBlock.DisplayNameText}, Conveyor Networks: {conveyorManagers.Count}");
        }

        private void CheckNetworkSplit(ConveyorManager manager)
        {
            var splitResult = manager.CheckNetworkIntegrity();

            if (splitResult.IsEmpty)
            {
                conveyorManagers.Remove(manager);
                return;
            }

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

        public void Update(float deltaTime)
        {
            if (!_isInitialized) return;

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
            if (!_isInitialized) return;

            _grid.OnBlockAdded -= OnBlockAdded;
            _grid.OnBlockRemoved -= OnBlockRemoved;

            foreach (var manager in conveyorManagers)
            {
                manager.Invalidate();
            }
            conveyorManagers.Clear();
            blockToManager.Clear();
        }

        public bool IsValid => _isInitialized;
        public IMyCubeGrid Grid => _grid;
    }
}