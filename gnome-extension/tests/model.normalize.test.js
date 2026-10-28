import {test, assertEqual, assertDeepEqual} from './harness.js';
import {normalize} from '../model.js';
import {G305_AWAKE, G305_ASLEEP, G515_BT, G435_BT} from './fixtures.js';

const P = '/org/freedesktop/UPower/devices/x';

test('normalize: Bluetooth keyboard has exact percent, null level, active', () => {
    const r = normalize(P, G515_BT);
    assertEqual(r.id, P);
    assertEqual(r.name, 'G515 LS TKL');
    assertEqual(r.kind, 'keyboard');
    assertEqual(r.percent, G515_BT.Percentage);
    assertEqual(r.level, null);
    assertEqual(r.charging, false);
    assertEqual(r.active, true);
    assertEqual(r.source, 'upower');
});

test('normalize: G305 awake has null percent and level full', () => {
    const r = normalize(P, G305_AWAKE);
    assertEqual(r.percent, null);
    assertEqual(r.level, 'full');
    assertEqual(r.charging, false);   // fully-charged is not charging
    assertEqual(r.active, true);
});

test('normalize: G305 asleep is inactive with unknown level', () => {
    const r = normalize(P, G305_ASLEEP);
    assertEqual(r.percent, null);
    assertEqual(r.level, 'unknown');
    assertEqual(r.active, false);
});

test('normalize: charging and pending-charge are charging', () => {
    assertEqual(normalize(P, {...G435_BT, State: 1}).charging, true);
    assertEqual(normalize(P, {...G435_BT, State: 5}).charging, true);
    assertEqual(normalize(P, {...G435_BT, State: 2}).charging, false);
});

test('normalize: percent is rounded and non-finite becomes null', () => {
    assertEqual(normalize(P, {...G435_BT, Percentage: 59.6}).percent, 60);
    assertEqual(normalize(P, {...G435_BT, Percentage: NaN}).percent, null);
    assertEqual(normalize(P, {...G435_BT, Percentage: undefined}).percent, null);
});

test('normalize: IsPresent=false is inactive', () => {
    assertEqual(normalize(P, {...G435_BT, IsPresent: false}).active, false);
});

test('normalize: name falls back to Serial then path segment', () => {
    assertEqual(normalize(P, {...G435_BT, Model: '', Serial: 'AA:BB'}).name, 'AA:BB');
    assertEqual(normalize(P, {...G435_BT, Model: '', Serial: ''}).name, 'x');
});

test('normalize: discharging level counts as normal; action kept', () => {
    assertEqual(normalize(P, {...G305_AWAKE, BatteryLevel: 2}).level, 'normal');
    assertEqual(normalize(P, {...G305_AWAKE, BatteryLevel: 5}).level, 'action');
});
