// Manual ordering test: DeviceRemoved(P) fires before GetAll(P) reply lands.
// Run: gjs -m tests/upowerSource.race.test.js
import {UPowerSource} from '../upowerSource.js';

const P = '/org/freedesktop/UPower/devices/mouse_X';
const subs = new Map(); // member -> callback
const pending = [];     // {method, resolve}
let nextId = 1;
const bus = {
    signal_subscribe(_n, _i, member, _p, _a, _f, cb) { subs.set(member, cb); return nextId++; },
    signal_unsubscribe() {},
    call(_n, _path, _i, method) {
        return new Promise(resolve => pending.push({method, resolve}));
    },
};
const variant = (o) => ({deepUnpack: () => o, recursiveUnpack: () => o});
const flush = async () => { for (let i = 0; i < 20; i++) await Promise.resolve(); };
const take = (m) => {
    const i = pending.findIndex(c => c.method === m);
    if (i === -1) throw new Error(`no pending call: ${m}`);
    return pending.splice(i, 1)[0];
};

const snaps = [];
const source = new UPowerSource(s => snaps.push(s), bus);
let ok = false;
try {
    // start() skipped: it would watch the real system bus; drive the handler directly.
    source._onOwnerAppeared();
    take('EnumerateDevices').resolve(variant([[P]]));
    await flush();
    const getAll = take('GetAll');
    subs.get('DeviceRemoved')(null, null, '/org/freedesktop/UPower', '', 'DeviceRemoved',
        variant([P]));
    getAll.resolve(variant([{Percentage: 50}]));
    await flush();
    const last = snaps[snaps.length - 1];
    ok = !!last && !last.devices.has(P) && !source._devices.has(P);
} catch (e) {
    console.log(`FAIL: ${e.message}`);
}
console.log(ok ? 'PASS: removed device not resurrected by late GetAll' : 'FAIL: device P present');
source.stop();
imports.system.exit(ok ? 0 : 1);
