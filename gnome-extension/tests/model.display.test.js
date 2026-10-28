import {test, assertEqual} from './harness.js';
import {labelFor, colorBand, iconNameFor} from '../model.js';

const rec = over => ({
    id: 'x', name: 'n', kind: 'mouse', percent: null, level: null,
    state: 0, charging: false, active: true, source: 'upower', ...over,
});

test('label: percent renders NN%', () => {
    assertEqual(labelFor(rec({percent: 36})), '36%');
});

test('label: coarse levels render Portuguese words', () => {
    assertEqual(labelFor(rec({level: 'full'})), 'Cheia');
    assertEqual(labelFor(rec({level: 'high'})), 'Alta');
    assertEqual(labelFor(rec({level: 'normal'})), 'Normal');
    assertEqual(labelFor(rec({level: 'low'})), 'Baixa');
    assertEqual(labelFor(rec({level: 'critical'})), 'Crítica');
    assertEqual(labelFor(rec({level: 'action'})), 'Crítica');
});

test('label: unknown and no data render an en dash', () => {
    assertEqual(labelFor(rec({level: 'unknown'})), '—');
    assertEqual(labelFor(rec({percent: null, level: null})), '—');
});

test('label: charging prefixes a bolt', () => {
    assertEqual(labelFor(rec({percent: 40, charging: true})), '⚡ 40%');
});

test('color: percent bands are p>50 normal, 20<p<=50 amber, p<=20 red', () => {
    assertEqual(colorBand(rec({percent: 51})), 'normal');
    assertEqual(colorBand(rec({percent: 50})), 'amber');
    assertEqual(colorBand(rec({percent: 21})), 'amber');
    assertEqual(colorBand(rec({percent: 20})), 'red');
    assertEqual(colorBand(rec({percent: 0})), 'red');
});

test('color: coarse levels', () => {
    assertEqual(colorBand(rec({level: 'low'})), 'amber');
    assertEqual(colorBand(rec({level: 'critical'})), 'red');
    assertEqual(colorBand(rec({level: 'action'})), 'red');
    assertEqual(colorBand(rec({level: 'full'})), 'normal');
    assertEqual(colorBand(rec({level: 'unknown'})), 'normal');
});

test('color: charging is always normal', () => {
    assertEqual(colorBand(rec({percent: 5, charging: true})), 'normal');
    assertEqual(colorBand(rec({level: 'critical', charging: true})), 'normal');
});

test('icons per kind', () => {
    assertEqual(iconNameFor('mouse'), 'input-mouse-symbolic');
    assertEqual(iconNameFor('keyboard'), 'input-keyboard-symbolic');
    assertEqual(iconNameFor('headset'), 'audio-headset-symbolic');
    assertEqual(iconNameFor('gaming-input'), 'input-gaming-symbolic');
    assertEqual(iconNameFor('other'), 'battery-symbolic');
});
