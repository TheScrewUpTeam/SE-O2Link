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
        private readonly Dictionary<IMyTerminalBlock, ConveyorManager> blockToManager = new Dictionary<IMyTerminalBlock, ConveyorManager>();
        private bool _isValid = false;
        private int updateCounter = 0;
        private int scheduledProcess = 0;
        private readonly List<IMyTerminalBlock> blocksToProcess = new List<IMyTerminalBlock>();

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void Close()
        {
            base.Close();
        }

        private void TryInitialize(IMyCubeGrid grid)
        {
            MyAPIGateway.Utilities.ShowMessage("O2Link", $"Start processing grid '{grid.DisplayName}'.");

            _grid = grid;
            _isValid = true;

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

            MyAPIGateway.Utilities.ShowMessage("O2Link", $"Initialized GridManager for grid '{_grid.DisplayName}' with {conveyorManagers.Count} conveyor networks.");
        }

        public override void UpdateAfterSimulation()
        {
            if (!_isValid)
            {
                TryInitialize(Entity as IMyCubeGrid);
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

        private void OnBlockAdded(IMySlimBlock block)
        {
            if (!_isValid || block?.FatBlock == null)
                return;

            var terminalBlock = block.FatBlock as IMyTerminalBlock;
            if (terminalBlock == null)
                return;

            scheduledProcess = updateCounter + 20;
            blocksToProcess.Add(terminalBlock);
        }

        private void ProcessScheduledBlocks()
        {
            foreach (var terminalBlock in blocksToProcess)
            {
                var isCoveredBlock = terminalBlock is IMyAirVent ||
                      terminalBlock is IMyOxygenFarm ||
                      terminalBlock is IMyGasGenerator ||
                      terminalBlock is IMyGasTank ||
                      terminalBlock is IMyThrust ||
                      terminalBlock is IMyPowerProducer;

                MyAPIGateway.Utilities.ShowMessage("O2Link", $"[Grid] Processing added block '{terminalBlock.CustomName}'.");

                // Try to add to existing networks first
                List<ConveyorManager> connectedManagers = new List<ConveyorManager>();

                foreach (var manager in conveyorManagers)
                {
                    if (manager.IsConveyorConnected(terminalBlock))
                    {
                        connectedManagers.Add(manager);
                        if (isCoveredBlock)
                        {
                            manager.TryAddBlock(terminalBlock);
                        }
                    }
                }

                MyAPIGateway.Utilities.ShowMessage("O2Link", $"[Grid] -> Connected to {connectedManagers.Count} existing networks.");

                if (connectedManagers.Count == 0 && isCoveredBlock)
                {
                    MyAPIGateway.Utilities.ShowMessage("O2Link", $"[Grid] -> Created mew network.");
                    // No existing networks found, create new one
                    var newManager = new ConveyorManager();
                    conveyorManagers.Add(newManager);
                    blockToManager[terminalBlock] = newManager;
                    newManager.TryAddBlock(terminalBlock);
                }
                else if (connectedManagers.Count == 1 && isCoveredBlock)
                {
                    MyAPIGateway.Utilities.ShowMessage("O2Link", $"[Grid] -> Added to existing.");
                    // Add to single existing network
                    blockToManager[terminalBlock] = connectedManagers[0];
                }
                else
                {
                    MyAPIGateway.Utilities.ShowMessage("O2Link", $"[Grid] -> Merged networks.");
                    // Multiple networks found, need to merge them
                    var targetManager = connectedManagers[0];
                    if (!isCoveredBlock)
                    {
                        blockToManager[terminalBlock] = targetManager;
                    }

                    // Merge other networks into the target
                    foreach (var manager in connectedManagers.Skip(1))
                    {
                        MergeNetworks(manager, targetManager);
                        conveyorManagers.Remove(manager);
                    }
                }
            }

            MyAPIGateway.Utilities.ShowMessage("O2Link", $"[Grid] -> Conveyors after process: {conveyorManagers.Count}.");
        }

        private void OnBlockRemoved(IMySlimBlock block)
        {
            if (!_isValid || block?.FatBlock == null)
                return;

            var terminalBlock = block.FatBlock as IMyTerminalBlock;
            if (terminalBlock == null || !blockToManager.ContainsKey(terminalBlock))
                return;

            MyAPIGateway.Utilities.ShowMessage("O2Link", $"[Grid] Processing removed block '{terminalBlock.CustomName}'.");

            var oldManager = blockToManager[terminalBlock];
            blockToManager.Remove(terminalBlock);
            oldManager.RemoveBlock(terminalBlock);

            // Check if network needs to be split
            CheckNetworkSplit(oldManager);

            MyAPIGateway.Utilities.ShowMessage("O2Link", $"[Grid] -> Conveyors after process: {conveyorManagers.Count}.");
        }

        private void CheckNetworkSplit(ConveyorManager manager)
        {
            var splitResult = manager.CheckNetworkIntegrity();

            MyAPIGateway.Utilities.ShowMessage("O2Link", $"[Grid] -> Check split: {splitResult.IsSplit}.");

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

            MyAPIGateway.Utilities.ShowMessage("O2Link", $"[Grid] -> Conveyors after split: {conveyorManagers.Count}.");
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