# O2Link

A Space Engineers mod that enables hydrogen engines and thrusters to consume oxygen from oxygen tanks, vents,farms or whatever, creating a more realistic and interconnected gas system.

## Features

- Hydrogen engines and thrusters can now consume oxygen when connected to an oxygen network
- Real-time calculation of oxygen consumption based on:
  - Actual power output for hydrogen engines
  - Current thrust for hydrogen thrusters
- Automatic network detection and management:
  - Tracks conveyor connections in real-time
  - Supports multiple independent networks on the same grid
  - Auto-merges networks when connections are made
  - Auto-splits networks when connections are broken
- Visual feedback:
  - Custom info panel shows current O2 consumption rates
  - Network status indicators (Supplied/Not enough)
  - Automatic block shutdown when O2 supply is insufficient

## How it Works

The mod continuously monitors your ship's oxygen and hydrogen systems:

1. **O2 Production**: From oxygen generators, O2/H2 generators, and oxygen farms
2. **O2 Storage**: Tracks available oxygen in all connected O2 tanks
3. **O2 Consumption**: Calculates real-time consumption for hydrogen engines and thrusters
4. **Network Management**: Automatically handles conveyor connections and disconnections

### Consumption Calculation

- **Hydrogen Engines**: Consumption is based on actual power output
  - Uses engine's real MaxPowerOutput and FuelCapacity values
  - Linear scaling between power output and consumption
  - Accurate simulation of engine efficiency

- **Hydrogen Thrusters**: Consumption scales with actual thrust
  - Based on thruster's real thrust values and fuel efficiency
  - Direct correlation between thrust power and O2 consumption

## Usage

1. Add the mod to your world
2. Build your ships/stations as normal
3. Connect O2 tanks to hydrogen engines/thrusters via conveyors
4. The mod will automatically handle the rest!

### Tips

- Check block info panels for consumption rates
- Ensure sufficient O2 production/storage for your needs
- Blocks will automatically shut down if O2 supply is insufficient
- Monitor your O2 tank levels more carefully now!

## Requirements

- Space Engineers game
- Can be used in both single and multiplayer
- Works with existing saves
- Compatible with most other mods

## Configuration

The mod uses sensible defaults but can be configured through the config file:

- O2_FROM_H2_RATIO: Conversion ratio between H2 and O2 consumption
- MAIN_LOOP_INTERVAL: Update frequency for network checks

## Known Issues

- None currently reported

## Contributing

Feel free to submit issues or pull requests to the project repository.

## Credits

Created by TSUT
Inspired by real-world rocket engine technologies that have to use both hydrogen and oxygen as fuel.