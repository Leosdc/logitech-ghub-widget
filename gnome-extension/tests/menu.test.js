import {test, assertDeepEqual, assertEqual} from './harness.js';
import {buildRows} from '../menu.js';

const rec = over => ({
    id: over.name, name: 'n', kind: 'mouse', percent: null, level: null,
    state: 0, charging: false, active: true, source: 'upower', ...over,
});

test('menu: unavailable UPower shows a single message plus prefs', () => {
    const rows = buildRows([], {available: false, showInactive: true});
    assertDeepEqual(rows.map(r => r.type), ['message', 'separator', 'prefs']);
    assertEqual(rows[0].text, 'UPower indisponível');
});

test('menu: no devices shows a message', () => {
    const rows = buildRows([], {available: true, showInactive: true});
    assertEqual(rows[0].text, 'Nenhum periférico com bateria');
});

test('menu: active rows carry name and label, inactive rows say Inativo', () => {
    const rows = buildRows([
        rec({name: 'G515', kind: 'keyboard', percent: 36}),
        rec({name: 'G305', kind: 'mouse', level: 'unknown', active: false}),
    ], {available: true, showInactive: true});
    assertDeepEqual(rows.map(r => r.type), ['device', 'device', 'separator', 'prefs']);
    assertEqual(rows[0].text, 'G305 — Inativo');
    assertEqual(rows[0].inactive, true);
    assertEqual(rows[0].iconName, 'input-mouse-symbolic');
    assertEqual(rows[1].text, 'G515 — 36%');
    assertEqual(rows[1].inactive, false);
});

test('menu: showInactive=false hides inactive rows', () => {
    const rows = buildRows([
        rec({name: 'G305', level: 'unknown', active: false}),
    ], {available: true, showInactive: false});
    assertEqual(rows[0].type, 'message');
});

test('menu: prefs row text', () => {
    const rows = buildRows([], {available: true, showInactive: true});
    assertEqual(rows.at(-1).text, 'Preferências');
});
