import {test, assertDeepEqual} from './harness.js';
import {sortRecords} from '../model.js';

const r = (name, kind) => ({id: name, name, kind, percent: null, level: null, state: 0, charging: false, active: true, source: 'upower'});

test('sort: kind order then name', () => {
    const input = [r('Z pen', 'other'), r('G435', 'headset'), r('G515', 'keyboard'), r('b mouse', 'mouse'), r('A mouse', 'mouse'), r('pad', 'gaming-input')];
    const out = sortRecords(input).map(x => x.name);
    assertDeepEqual(out, ['A mouse', 'b mouse', 'G515', 'G435', 'pad', 'Z pen']);
});

test('sort: does not mutate input', () => {
    const input = [r('b', 'mouse'), r('a', 'mouse')];
    sortRecords(input);
    assertDeepEqual(input.map(x => x.name), ['b', 'a']);
});
