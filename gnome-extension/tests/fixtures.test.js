import {test, assertEqual} from './harness.js';
import {G305_AWAKE, G515_BT, G435_BT, LAPTOP_BAT, G305_ASLEEP} from './fixtures.js';

test('fixtures: G305 awake is a mouse with coarse level full', () => {
    assertEqual(G305_AWAKE.Type, 5);
    assertEqual(G305_AWAKE.BatteryLevel, 8);
    assertEqual(G305_AWAKE.PowerSupply, false);
});

test('fixtures: Bluetooth devices have State unknown and BatteryLevel none', () => {
    assertEqual(G515_BT.State, 0);
    assertEqual(G515_BT.BatteryLevel, 1);
    assertEqual(G435_BT.State, 0);
    assertEqual(G435_BT.BatteryLevel, 1);
});

test('fixtures: laptop battery is a power supply', () => {
    assertEqual(LAPTOP_BAT.PowerSupply, true);
});

test('fixtures: G305 asleep has unknown level', () => {
    assertEqual(G305_ASLEEP.BatteryLevel, 0);
    assertEqual(G305_ASLEEP.Percentage, 50);
});
