using Sandbox.ModAPI;
using VRage.Game.Components;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace TSUT.O2Link
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class Session : MySessionComponentBase
    {
        private Dictionary<long, GridManager> gridManagers = new Dictionary<long, GridManager>();
        private int updateCounter = 0;
        private const int UPDATE_FREQUENCY = 30; // Update every 30 ticks (1/2 second)

        public override void LoadData()
        {
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdded;
            MyAPIGateway.Entities.OnEntityRemove += OnEntityRemoved;
        }

        private void OnEntityAdded(IMyEntity entity)
        {
            if (!(entity is IMyCubeGrid)) return;
            var grid = entity as IMyCubeGrid;
            if (gridManagers.ContainsKey(grid.EntityId)) return;

            gridManagers[grid.EntityId] = new GridManager(grid);
        }

        private void OnEntityRemoved(IMyEntity entity)
        {
            if (!(entity is IMyCubeGrid)) return;
            var grid = entity as IMyCubeGrid;
            if (!gridManagers.ContainsKey(grid.EntityId)) return;
            
            gridManagers[grid.EntityId].Invalidate();
            gridManagers.Remove(grid.EntityId);
        }

        public override void UpdateAfterSimulation()
        {
            updateCounter++;
            if (updateCounter < UPDATE_FREQUENCY) return;
            
            updateCounter = 0;
            foreach (var manager in gridManagers.Values)
            {
                if (manager.IsValid)
                {
                    manager.Update();
                }
            }
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdded;
            MyAPIGateway.Entities.OnEntityRemove -= OnEntityRemoved;
            
            foreach (var manager in gridManagers.Values)
            {
                manager.Invalidate();
            }
            gridManagers.Clear();
        }

        public override void SaveData()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                Config.Instance.Save();
            }
        }
    }
}
