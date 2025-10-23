using Sandbox.ModAPI;
using VRage.Game.Components;

namespace TSUT.O2Link
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class Session : MySessionComponentBase
    {
        public override void SaveData()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                Config.Instance.Save();
            }
        }
    }
}
