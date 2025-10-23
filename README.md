# O2Link Space Engineers Mod

This mod enables H2 thrusters and engines to consume oxygen as part of their operation, making the system more realistic and challenging.

## Architecture Plan

### Core Classes

1. **GridManager**
   - Tracks overall grid state
   - Maintains list of ConveyorManagers for the grid
   - Handles grid merge/split events
   - Dictionary<long, GridManager> to track all grids

2. **ConveyorManager**
   - Maps connected conveyor networks within a grid
   - Multiple instances possible per grid (separate networks)
   - Tracks connected blocks:
     * H2 Consumers (thrusters, engines)
     * O2 Storage/Tanks
     * H2 Storage/Tanks
     * O2 Producers (O2/H2 generators)
   - Handles block addition/removal events
   - Updates network topology when blocks change

### Resource Management Loop (Every 1/2 Second)

1. **Resource Calculation**
   - Calculate H2 consumption from all consumers in network
   - Convert H2 consumption to O2 requirement
   - Subtract available O2 production from generators
   - Draw remaining O2 from storage tanks
   - Disable thrusters/engines if O2 is unavailable

2. **Network Updates**
   - Monitor conveyor connection changes
   - Update block lists when new blocks are added/removed
   - Recalculate network topology on changes

### Event Handling

1. **Grid Events**
   - Grid creation/destruction
   - Grid merge/split scenarios

2. **Block Events**
   - Block placement/removal
   - Conveyor connection changes

## Implementation Priority

1. Basic GridManager and ConveyorManager structure
2. Resource calculation logic
3. Network topology handling
4. Event system integration
5. Testing and balancing