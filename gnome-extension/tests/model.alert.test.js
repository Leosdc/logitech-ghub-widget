import {test, assertDeepEqual} from './harness.js';
import {alertStep} from '../model.js';

const rec = over => ({
    id: 'x', name: 'n', kind: 'mouse', percent: null, level: null,
    state: 0, charging: false, active: true, source: 'upower', ...over,
});

function run(seq) {
    let armed = true;
    return seq.map(r => {
        const s = alertStep(armed, r);
        armed = s.armed;
        return s.fire;
    });
}

test('alert: drop to 19, hold, sleep reading, rise to 25, drop again', () => {
    const fires = run([
        rec({percent: 40}),
        rec({percent: 19}),             // fire
        rec({percent: 19}),             // hold, no fire
        rec({level: 'unknown'}),        // asleep: no change
        rec({percent: 19}),             // still disarmed
        rec({percent: 25}),             // re-arm
        rec({percent: 18}),             // fire again
    ]);
    assertDeepEqual(fires, [false, true, false, false, false, false, true]);
});

test('alert: exactly 20 fires, 21 does not', () => {
    assertDeepEqual(run([rec({percent: 21})]), [false]);
    assertDeepEqual(run([rec({percent: 20})]), [true]);
});

test('alert: charging suppresses and re-arms', () => {
    const fires = run([
        rec({percent: 10}),                    // fire
        rec({percent: 10, charging: true}),    // re-arm, no fire
        rec({percent: 10, charging: false}),   // fire again
    ]);
    assertDeepEqual(fires, [true, false, true]);
});

test('alert: fully-charged re-arms without firing', () => {
    const fires = run([
        rec({percent: 10}),
        rec({percent: 10, state: 4}),          // fully-charged (odd but possible)
        rec({percent: 10}),
    ]);
    assertDeepEqual(fires, [true, false, true]);
});

test('alert: coarse critical and action fire; normal/high/full re-arm; low does not', () => {
    const fires = run([
        rec({level: 'critical'}),   // fire
        rec({level: 'critical'}),   // hold
        rec({level: 'low'}),        // no re-arm, no fire
        rec({level: 'normal'}),     // re-arm
        rec({level: 'action'}),     // fire
    ]);
    assertDeepEqual(fires, [true, false, false, false, true]);
});

test('alert: no-data reading changes nothing', () => {
    assertDeepEqual(alertStep(false, rec({})), {armed: false, fire: false});
    assertDeepEqual(alertStep(true, rec({})), {armed: true, fire: false});
});
