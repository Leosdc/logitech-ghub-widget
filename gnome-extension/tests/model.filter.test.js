import {test, assertEqual} from './harness.js';
import {isPeripheral, kindOf} from '../model.js';
import {G305_AWAKE, G515_BT, G435_BT, LAPTOP_BAT, LINE_POWER} from './fixtures.js';

test('filter: the three peripherals pass', () => {
    assertEqual(isPeripheral(G305_AWAKE), true);
    assertEqual(isPeripheral(G515_BT), true);
    assertEqual(isPeripheral(G435_BT), true);
});

test('filter: laptop battery and line power are excluded', () => {
    assertEqual(isPeripheral(LAPTOP_BAT), false);
    assertEqual(isPeripheral(LINE_POWER), false);
});

test('filter: Type=battery with PowerSupply=false counts (generic peripheral battery)', () => {
    assertEqual(isPeripheral({Type: 2, PowerSupply: false}), true);
});

test('filter: missing PowerSupply fails closed', () => {
    assertEqual(isPeripheral({Type: 5}), false);
});

test('filter: unknown, ups, monitor, computer are excluded even without power supply', () => {
    for (const t of [0, 3, 4, 11])
        assertEqual(isPeripheral({Type: t, PowerSupply: false}), false, `type ${t}`);
});

test('filter: unknown future type values count as other', () => {
    assertEqual(isPeripheral({Type: 99, PowerSupply: false}), true);
    assertEqual(kindOf(99), 'other');
});

test('kind mapping', () => {
    assertEqual(kindOf(5), 'mouse');
    assertEqual(kindOf(6), 'keyboard');
    assertEqual(kindOf(17), 'headset');
    assertEqual(kindOf(19), 'headset');   // headphones
    assertEqual(kindOf(21), 'headset');   // audio-device
    assertEqual(kindOf(12), 'gaming-input');
    assertEqual(kindOf(13), 'other');     // pen
    assertEqual(kindOf(2), 'other');      // battery
});
