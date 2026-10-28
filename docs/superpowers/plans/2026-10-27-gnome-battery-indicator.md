# GNOME Battery Indicator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A GNOME Shell 46 extension in `gnome-extension/` that shows one top-bar item per battery-reporting peripheral, fed by UPower over D-Bus, with amber/red low-battery colors and one notification per crossing below 20%.

**Architecture:** Pure ESM GJS. `upowerSource.js` is the only module that talks to D-Bus and emits plain snapshots. `model.js` and `menu.js` are Shell-free pure functions tested under `gjs -m`. `extension.js` wires source → model → panel items/notifier and owns all lifecycle. Nothing polls; everything is driven by UPower signals.

**Tech Stack:** GJS 1.80 (GNOME Shell 46.0 on Ubuntu 24.04), `gi://Gio`, `gi://UPowerGlib` (enums only), `gi://St`, `gi://Adw` (prefs), `gnome-extensions` CLI, GitHub Actions.

Spec: `docs/superpowers/specs/2026-10-27-gnome-battery-indicator-design.md`. Read it first; this plan implements it exactly.

## Global Constraints

- Target GNOME Shell **46 only**: `"shell-version": ["46"]`. No Shell 45 code paths.
- UUID `ghub-battery@vinipy12.github.io`; settings schema id `org.gnome.shell.extensions.ghub-battery`; schema keys `notify-low` (bool, default true) and `show-inactive` (bool, default true).
- All D-Bus calls asynchronous on the system bus. No synchronous bus calls anywhere in the extension.
- `model.js` and `menu.js` import nothing from `resource:///org/gnome/shell/...`; they may import `gi://UPowerGlib` only.
- User-facing strings in Portuguese, matching the repo. Coarse level words: `Cheia, Alta, Normal, Baixa, Crítica`, unknown renders the dash character `—` (U+2014), exactly that character.
- Color bands: `p > 50` normal, `20 < p <= 50` amber, `p <= 20` red; `low` amber; `critical`/`action` red; charging always normal.
- Stylesheet colors explicit: amber `#e5a50a`, red `#e01b24`, inactive `opacity: 0.5`. No CSS variables.
- `tests/` is never packed. `make pack` must list every runtime module via `--extra-source`.
- Every commit message ends with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.
- Work on branch `feat/gnome-battery-indicator` in `/home/vinipy/Personal/logitech-ghub-widget-ubuntu`. Never push.
- Run commands from the repo root unless a step says otherwise. Shape test output with `2>&1 | tail -20`.

## File structure

```
gnome-extension/
  metadata.json            identity + shell-version + settings-schema
  extension.js             Extension subclass; lifecycle + wiring only
  upowerSource.js          D-Bus client -> snapshot {available, devices, removed}
  model.js                 pure: isPeripheral, normalize, label, colorBand, sortRecords, alertStep
  menu.js                  pure: buildRows(snapshotRecords, opts) -> row descriptors
  panelItem.js             PanelMenu.Button subclass: icon + label + own menu built from rows
  notifier.js              lazily (re)created MessageTray.Source
  prefs.js                 Adw preferences: two switches
  stylesheet.css           amber/red/inactive classes
  schemas/org.gnome.shell.extensions.ghub-battery.gschema.xml
  Makefile                 test, test-live, schemas, pack, install, nested
  README.md                Portuguese docs
  tests/
    harness.js             test()/assert helpers + registry
    run.js                 single gjs -m entry; imports all *.test.js; exits 1 on failure
    fixtures.js            loads JSON fixtures, exposes named device dicts
    fixtures/upower-enumerate-20261027-1359.json   (already committed)
    model.filter.test.js, model.normalize.test.js, model.display.test.js,
    model.sort.test.js, model.alert.test.js, menu.test.js
    upowerSource.live.test.js   manual only
```

---

### Task 1: Scaffold, schema, test harness

**Files:**
- Create: `gnome-extension/metadata.json`
- Create: `gnome-extension/schemas/org.gnome.shell.extensions.ghub-battery.gschema.xml`
- Create: `gnome-extension/tests/harness.js`
- Create: `gnome-extension/tests/run.js`
- Create: `gnome-extension/tests/fixtures.js`
- Create: `gnome-extension/tests/fixtures.test.js`
- Create: `gnome-extension/Makefile`
- Modify: `.gitignore` (append)

**Interfaces:**
- Produces: `harness.js` exports `test(name, fn)`, `assertEqual(actual, expected, msg?)`, `assertDeepEqual(actual, expected, msg?)`, `assertTrue(cond, msg?)`, `runAll() -> number of failures`.
- Produces: `fixtures.js` exports `loadEnumerate() -> object (path -> props)`, and constants `G305_AWAKE`, `G305_ASLEEP`, `G515_BT`, `G435_BT`, `LAPTOP_BAT`, `LINE_POWER` (plain property objects).

- [ ] **Step 1: Write metadata.json**

```json
{
  "uuid": "ghub-battery@vinipy12.github.io",
  "name": "Logitech G Battery",
  "description": "Mostra a bateria dos periféricos Logitech G na barra superior, via UPower.",
  "shell-version": ["46"],
  "settings-schema": "org.gnome.shell.extensions.ghub-battery",
  "url": "https://github.com/vinipy12/logitech-ghub-widget-ubuntu",
  "version": 1
}
```

- [ ] **Step 2: Write the GSettings schema**

`gnome-extension/schemas/org.gnome.shell.extensions.ghub-battery.gschema.xml`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<schemalist gettext-domain="ghub-battery">
  <schema id="org.gnome.shell.extensions.ghub-battery" path="/org/gnome/shell/extensions/ghub-battery/">
    <key name="notify-low" type="b">
      <default>true</default>
      <summary>Notificar bateria fraca</summary>
      <description>Envia uma notificação quando um dispositivo cai para 20% ou menos.</description>
    </key>
    <key name="show-inactive" type="b">
      <default>true</default>
      <summary>Mostrar dispositivos inativos no menu</summary>
      <description>Mantém dispositivos desligados ou desconectados listados no menu, em cinza.</description>
    </key>
  </schema>
</schemalist>
```

- [ ] **Step 3: Compile the schema to prove it is valid**

Run: `glib-compile-schemas --strict gnome-extension/schemas/ && ls gnome-extension/schemas/`
Expected: no output from the compiler; listing shows `gschemas.compiled` next to the xml.

- [ ] **Step 4: Write the test harness**

`gnome-extension/tests/harness.js`:

```js
// Minimal test registry for `gjs -m`. No Shell imports.
const registry = [];

export function test(name, fn) {
    registry.push({name, fn});
}

export function assertTrue(cond, msg = 'expected truthy') {
    if (!cond)
        throw new Error(msg);
}

export function assertEqual(actual, expected, msg = '') {
    if (actual !== expected)
        throw new Error(`${msg} expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`);
}

export function assertDeepEqual(actual, expected, msg = '') {
    const a = JSON.stringify(actual);
    const e = JSON.stringify(expected);
    if (a !== e)
        throw new Error(`${msg} expected ${e}, got ${a}`);
}

export function runAll() {
    let failures = 0;
    for (const {name, fn} of registry) {
        try {
            fn();
            print(`ok   ${name}`);
        } catch (e) {
            failures++;
            print(`FAIL ${name}: ${e.message}`);
        }
    }
    print(`${registry.length - failures}/${registry.length} passed`);
    return failures;
}
```

- [ ] **Step 5: Write the fixture loader**

`gnome-extension/tests/fixtures.js`:

```js
import Gio from 'gi://Gio';
import GLib from 'gi://GLib';

const here = GLib.path_get_dirname(GLib.filename_from_uri(import.meta.url)[0]);

export function loadEnumerate() {
    const path = GLib.build_filenamev([here, 'fixtures', 'upower-enumerate-20261027-1359.json']);
    const [, bytes] = Gio.File.new_for_path(path).load_contents(null);
    return JSON.parse(new TextDecoder().decode(bytes));
}

const all = loadEnumerate();
const byPath = p => all[`/org/freedesktop/UPower/devices/${p}`];

export const G305_AWAKE = byPath('battery_hidpp_battery_12');
export const G515_BT = byPath('keyboard_dev_AA_BB_CC_00_00_02');
export const G435_BT = byPath('headset_dev_AA_BB_CC_00_00_01');
export const LAPTOP_BAT = byPath('battery_BAT0');
export const LINE_POWER = byPath('line_power_AC0');

// Observed on the reference machine while the mouse slept (reviewer capture,
// 2026-10-27): the object stays on the bus with unknown state/level and a
// synthesised 50%.
export const G305_ASLEEP = {...G305_AWAKE, State: 0, BatteryLevel: 0, Percentage: 50};
```

- [ ] **Step 6: Write a fixture sanity test and the runner**

`gnome-extension/tests/fixtures.test.js`:

```js
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
```

`gnome-extension/tests/run.js`:

```js
// Single entry point: `gjs -m tests/run.js` (gjs -m runs only one module).
import System from 'system';
import {runAll} from './harness.js';

import './fixtures.test.js';

const failures = runAll();
System.exit(failures === 0 ? 0 : 1);
```

- [ ] **Step 7: Write the Makefile (test + schemas targets only for now)**

`gnome-extension/Makefile`:

```make
UUID    := ghub-battery@vinipy12.github.io
RUNTIME := extension.js prefs.js stylesheet.css upowerSource.js model.js panelItem.js menu.js notifier.js
SCHEMA  := schemas/org.gnome.shell.extensions.ghub-battery.gschema.xml

.PHONY: test test-live schemas pack install nested clean

test:
	gjs -m tests/run.js

test-live:
	gjs -m tests/upowerSource.live.test.js

schemas:
	glib-compile-schemas --strict schemas/
```

(The `pack`, `install`, `nested`, `clean` targets are added in Task 12.)

- [ ] **Step 8: Run the tests**

Run: `cd gnome-extension && make test 2>&1 | tail -6`
Expected: four `ok` lines and `4/4 passed`, exit 0.

- [ ] **Step 9: Ignore compiled schema and commit**

Append to `.gitignore`:

```
gnome-extension/schemas/gschemas.compiled
gnome-extension/*.shell-extension.zip
```

```bash
git add .gitignore gnome-extension
git commit -m "feat(gnome): scaffold extension metadata, schema and gjs test harness

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: model.js — peripheral filter and kind

**Files:**
- Create: `gnome-extension/model.js`
- Create: `gnome-extension/tests/model.filter.test.js`
- Modify: `gnome-extension/tests/run.js`

**Interfaces:**
- Produces: `isPeripheral(props) -> boolean`, `kindOf(typeValue) -> 'mouse'|'keyboard'|'headset'|'gaming-input'|'other'`. `props` is a plain UPower.Device property object (keys `Type`, `PowerSupply`, ...).

- [ ] **Step 1: Write the failing tests**

`gnome-extension/tests/model.filter.test.js`:

```js
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
```

Add to `tests/run.js` after the fixtures import: `import './model.filter.test.js';`

- [ ] **Step 2: Run to verify failure**

Run: `cd gnome-extension && make test 2>&1 | tail -3`
Expected: an import error mentioning `model.js` (module not found).

- [ ] **Step 3: Implement**

`gnome-extension/model.js`:

```js
// Pure functions over UPower.Device property objects. No Shell imports.
import UPowerGlib from 'gi://UPowerGlib';

const Kind = UPowerGlib.DeviceKind;
const Level = UPowerGlib.DeviceLevel;
const State = UPowerGlib.DeviceState;

const EXCLUDED_TYPES = new Set([
    Kind.UNKNOWN, Kind.LINE_POWER, Kind.UPS, Kind.MONITOR, Kind.COMPUTER,
]);

export function isPeripheral(props) {
    if (props.PowerSupply !== false)
        return false;
    if (typeof props.Type !== 'number')
        return false;
    return !EXCLUDED_TYPES.has(props.Type);
}

export function kindOf(type) {
    switch (type) {
    case Kind.MOUSE: return 'mouse';
    case Kind.KEYBOARD: return 'keyboard';
    case Kind.HEADSET:
    case Kind.HEADPHONES:
    case Kind.OTHER_AUDIO: return 'headset';
    case Kind.GAMING_INPUT: return 'gaming-input';
    default: return 'other';
    }
}

export {Kind, Level, State};
```

- [ ] **Step 4: Run to verify pass**

Run: `cd gnome-extension && make test 2>&1 | tail -3`
Expected: `11/11 passed`.

- [ ] **Step 5: Commit**

```bash
git add gnome-extension/model.js gnome-extension/tests
git commit -m "feat(gnome): peripheral filter and kind mapping

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: model.js — normalize

**Files:**
- Modify: `gnome-extension/model.js`
- Create: `gnome-extension/tests/model.normalize.test.js`
- Modify: `gnome-extension/tests/run.js`

**Interfaces:**
- Produces: `normalize(path, props) -> Record` where
  `Record = {id: string, name: string, kind, percent: number|null, level: 'unknown'|'low'|'critical'|'action'|'normal'|'high'|'full'|null, state: number, charging: boolean, active: boolean, source: 'upower'}`.

- [ ] **Step 1: Write the failing tests**

`gnome-extension/tests/model.normalize.test.js`:

```js
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
```

Add to `tests/run.js`: `import './model.normalize.test.js';`

- [ ] **Step 2: Run to verify failure**

Run: `cd gnome-extension && make test 2>&1 | tail -3`
Expected: import error, `normalize` is not exported.

- [ ] **Step 3: Implement**

Append to `gnome-extension/model.js`:

```js
const LEVEL_NAMES = new Map([
    [Level.UNKNOWN, 'unknown'],
    [Level.DISCHARGING, 'normal'],
    [Level.LOW, 'low'],
    [Level.CRITICAL, 'critical'],
    [Level.ACTION, 'action'],
    [Level.NORMAL, 'normal'],
    [Level.HIGH, 'high'],
    [Level.FULL, 'full'],
]);

function nameOf(path, props) {
    if (typeof props.Model === 'string' && props.Model !== '')
        return props.Model;
    if (typeof props.Serial === 'string' && props.Serial !== '')
        return props.Serial;
    return path.split('/').pop();
}

export function normalize(path, props) {
    const coarse = props.BatteryLevel !== Level.NONE;
    let percent = null;
    if (!coarse && Number.isFinite(props.Percentage))
        percent = Math.round(props.Percentage);
    const level = coarse ? (LEVEL_NAMES.get(props.BatteryLevel) ?? 'unknown') : null;
    const state = typeof props.State === 'number' ? props.State : State.UNKNOWN;
    const charging = state === State.CHARGING || state === State.PENDING_CHARGE;
    const active = props.IsPresent !== false && level !== 'unknown';
    return {
        id: path,
        name: nameOf(path, props),
        kind: kindOf(props.Type),
        percent,
        level,
        state,
        charging,
        active,
        source: 'upower',
    };
}
```

- [ ] **Step 4: Run to verify pass**

Run: `cd gnome-extension && make test 2>&1 | tail -3`
Expected: `19/19 passed`.

- [ ] **Step 5: Commit**

```bash
git add gnome-extension
git commit -m "feat(gnome): normalize UPower properties into device records

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: model.js — label and color band

**Files:**
- Modify: `gnome-extension/model.js`
- Create: `gnome-extension/tests/model.display.test.js`
- Modify: `gnome-extension/tests/run.js`

**Interfaces:**
- Produces: `labelFor(record) -> string`, `colorBand(record) -> 'normal'|'amber'|'red'`, `iconNameFor(kind) -> string`.

- [ ] **Step 1: Write the failing tests**

`gnome-extension/tests/model.display.test.js`:

```js
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
```

Add to `tests/run.js`: `import './model.display.test.js';`

- [ ] **Step 2: Run to verify failure**

Run: `cd gnome-extension && make test 2>&1 | tail -3`
Expected: import error for `labelFor`.

- [ ] **Step 3: Implement**

Append to `gnome-extension/model.js`:

```js
const LEVEL_WORDS = {
    full: 'Cheia', high: 'Alta', normal: 'Normal', low: 'Baixa',
    critical: 'Crítica', action: 'Crítica',
};
const NO_DATA = '—';

export function labelFor(record) {
    let value;
    if (record.percent !== null)
        value = `${record.percent}%`;
    else
        value = LEVEL_WORDS[record.level] ?? NO_DATA;
    return record.charging ? `⚡ ${value}` : value;
}

export function colorBand(record) {
    if (record.charging)
        return 'normal';
    if (record.percent !== null) {
        if (record.percent <= 20)
            return 'red';
        if (record.percent <= 50)
            return 'amber';
        return 'normal';
    }
    if (record.level === 'critical' || record.level === 'action')
        return 'red';
    if (record.level === 'low')
        return 'amber';
    return 'normal';
}

const ICONS = {
    mouse: 'input-mouse-symbolic',
    keyboard: 'input-keyboard-symbolic',
    headset: 'audio-headset-symbolic',
    'gaming-input': 'input-gaming-symbolic',
    other: 'battery-symbolic',
};

export function iconNameFor(kind) {
    return ICONS[kind] ?? ICONS.other;
}
```

- [ ] **Step 4: Run to verify pass**

Run: `cd gnome-extension && make test 2>&1 | tail -3`
Expected: `27/27 passed`.

- [ ] **Step 5: Commit**

```bash
git add gnome-extension
git commit -m "feat(gnome): label, color band and icon rules

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: model.js — ordering

**Files:**
- Modify: `gnome-extension/model.js`
- Create: `gnome-extension/tests/model.sort.test.js`
- Modify: `gnome-extension/tests/run.js`

**Interfaces:**
- Produces: `sortRecords(records: Record[]) -> Record[]` (new array; mouse, keyboard, headset, gaming-input, other; alphabetical by `name` within a kind, locale-insensitive).

- [ ] **Step 1: Write the failing test**

`gnome-extension/tests/model.sort.test.js`:

```js
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
```

Add to `tests/run.js`: `import './model.sort.test.js';`

- [ ] **Step 2: Run to verify failure**

Run: `cd gnome-extension && make test 2>&1 | tail -3`
Expected: import error for `sortRecords`.

- [ ] **Step 3: Implement**

Append to `gnome-extension/model.js`:

```js
const KIND_ORDER = ['mouse', 'keyboard', 'headset', 'gaming-input', 'other'];

export function sortRecords(records) {
    return [...records].sort((a, b) => {
        const ka = KIND_ORDER.indexOf(a.kind);
        const kb = KIND_ORDER.indexOf(b.kind);
        if (ka !== kb)
            return ka - kb;
        return a.name.localeCompare(b.name, undefined, {sensitivity: 'base'});
    });
}
```

- [ ] **Step 4: Run to verify pass**

Run: `cd gnome-extension && make test 2>&1 | tail -3`
Expected: `29/29 passed`.

- [ ] **Step 5: Commit**

```bash
git add gnome-extension
git commit -m "feat(gnome): stable panel ordering

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: model.js — alert state machine

**Files:**
- Modify: `gnome-extension/model.js`
- Create: `gnome-extension/tests/model.alert.test.js`
- Modify: `gnome-extension/tests/run.js`

**Interfaces:**
- Produces: `alertStep(armed: boolean, record) -> {armed: boolean, fire: boolean}`. Caller stores `armed` per device id; initial value `true`.

- [ ] **Step 1: Write the failing tests**

`gnome-extension/tests/model.alert.test.js`:

```js
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
```

Add to `tests/run.js`: `import './model.alert.test.js';`

- [ ] **Step 2: Run to verify failure**

Run: `cd gnome-extension && make test 2>&1 | tail -3`
Expected: import error for `alertStep`.

- [ ] **Step 3: Implement**

Append to `gnome-extension/model.js`:

```js
const LOW_THRESHOLD = 20;
const RED_LEVELS = new Set(['critical', 'action']);
const OK_LEVELS = new Set(['normal', 'high', 'full']);

export function alertStep(armed, record) {
    const noData = record.percent === null && (record.level === null || record.level === 'unknown');
    if (noData)
        return {armed, fire: false};

    const isLow = record.percent !== null
        ? record.percent <= LOW_THRESHOLD
        : RED_LEVELS.has(record.level);
    const isOk = record.percent !== null
        ? record.percent > LOW_THRESHOLD
        : OK_LEVELS.has(record.level);
    const recovering = record.charging || record.state === State.FULLY_CHARGED || isOk;

    if (recovering)
        return {armed: true, fire: false};
    if (armed && isLow)
        return {armed: false, fire: true};
    return {armed, fire: false};
}
```

- [ ] **Step 4: Run to verify pass**

Run: `cd gnome-extension && make test 2>&1 | tail -3`
Expected: `35/35 passed`.

- [ ] **Step 5: Commit**

```bash
git add gnome-extension
git commit -m "feat(gnome): per-device low-battery alert arming

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 7: menu.js — pure row builder

**Files:**
- Create: `gnome-extension/menu.js`
- Create: `gnome-extension/tests/menu.test.js`
- Modify: `gnome-extension/tests/run.js`

**Interfaces:**
- Consumes: `labelFor`, `sortRecords` from `model.js`; `Record` shape.
- Produces: `buildRows(records: Record[], opts: {available: boolean, showInactive: boolean}) -> Row[]` where `Row = {type: 'message', text} | {type: 'device', text, inactive: boolean, iconName} | {type: 'separator'} | {type: 'prefs', text}`.

- [ ] **Step 1: Write the failing tests**

`gnome-extension/tests/menu.test.js`:

```js
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
```

Add to `tests/run.js`: `import './menu.test.js';`

- [ ] **Step 2: Run to verify failure**

Run: `cd gnome-extension && make test 2>&1 | tail -3`
Expected: import error, `menu.js` not found.

- [ ] **Step 3: Implement**

`gnome-extension/menu.js`:

```js
// Pure row descriptors for the dropdown. No Shell imports.
import {labelFor, sortRecords, iconNameFor} from './model.js';

const SEP = {type: 'separator'};
const PREFS = {type: 'prefs', text: 'Preferências'};

export function buildRows(records, {available, showInactive}) {
    if (!available)
        return [{type: 'message', text: 'UPower indisponível'}, SEP, PREFS];

    const shown = sortRecords(records.filter(r => r.active || showInactive));
    if (shown.length === 0)
        return [{type: 'message', text: 'Nenhum periférico com bateria'}, SEP, PREFS];

    const rows = shown.map(r => ({
        type: 'device',
        text: r.active ? `${r.name} — ${labelFor(r)}` : `${r.name} — Inativo`,
        inactive: !r.active,
        iconName: iconNameFor(r.kind),
    }));
    return [...rows, SEP, PREFS];
}
```

- [ ] **Step 4: Run to verify pass**

Run: `cd gnome-extension && make test 2>&1 | tail -3`
Expected: `40/40 passed`.

- [ ] **Step 5: Commit**

```bash
git add gnome-extension
git commit -m "feat(gnome): dropdown row builder

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 8: upowerSource.js — D-Bus client

**Files:**
- Create: `gnome-extension/upowerSource.js`
- Create: `gnome-extension/tests/upowerSource.live.test.js` (manual only)

**Interfaces:**
- Produces: `class UPowerSource { constructor(onChanged: (snapshot) => void); start(); stop(); }` with `snapshot = {available: boolean, devices: Map<path, props>, removed: path[]}`. `props` values are plain JS (variants unpacked). Every emission is a fresh `Map` copy.
- Imports only `gi://Gio`, `gi://GLib`.

- [ ] **Step 1: Write the live test (manual, documents the contract)**

`gnome-extension/tests/upowerSource.live.test.js`:

```js
// Manual: `make test-live`. Talks to the real system bus; exits 0 if at least
// one peripheral is enumerated within 5 seconds, 2 if UPower is absent.
import GLib from 'gi://GLib';
import System from 'system';
import {UPowerSource} from '../upowerSource.js';
import {isPeripheral} from '../model.js';

const loop = new GLib.MainLoop(null, false);
let code = 1;

const source = new UPowerSource(snapshot => {
    if (!snapshot.available) {
        print('UPower unavailable');
        code = 2;
        loop.quit();
        return;
    }
    const peripherals = [...snapshot.devices.entries()].filter(([, p]) => isPeripheral(p));
    print(`devices=${snapshot.devices.size} peripherals=${peripherals.length} removed=${snapshot.removed.length}`);
    for (const [path, p] of peripherals)
        print(`  ${path}: Type=${p.Type} Percentage=${p.Percentage} BatteryLevel=${p.BatteryLevel} State=${p.State}`);
    if (peripherals.length > 0) {
        code = 0;
        loop.quit();
    }
});

GLib.timeout_add_seconds(GLib.PRIORITY_DEFAULT, 5, () => { loop.quit(); return GLib.SOURCE_REMOVE; });
source.start();
loop.run();
source.stop();
System.exit(code);
```

- [ ] **Step 2: Run to verify failure**

Run: `cd gnome-extension && make test-live 2>&1 | tail -3`
Expected: import error, `upowerSource.js` not found.

- [ ] **Step 3: Implement**

`gnome-extension/upowerSource.js`:

```js
// Async D-Bus client for org.freedesktop.UPower. Emits plain snapshots.
import Gio from 'gi://Gio';
import GLib from 'gi://GLib';

Gio._promisify(Gio.DBusConnection.prototype, 'call');

const BUS_NAME = 'org.freedesktop.UPower';
const ROOT_PATH = '/org/freedesktop/UPower';
const ROOT_IFACE = 'org.freedesktop.UPower';
const DEVICE_IFACE = 'org.freedesktop.UPower.Device';
const PROPS_IFACE = 'org.freedesktop.DBus.Properties';

export class UPowerSource {
    constructor(onChanged) {
        this._onChanged = onChanged;
        this._bus = Gio.DBus.system;
        this._devices = new Map();
        this._generation = 0;
        this._available = false;
        this._subs = [];
        this._watchId = 0;
    }

    start() {
        this._watchId = Gio.bus_watch_name(
            Gio.BusType.SYSTEM, BUS_NAME, Gio.BusNameWatcherFlags.NONE,
            () => this._onOwnerAppeared(),
            () => this._onOwnerVanished());
    }

    stop() {
        this._generation++;
        this._unsubscribe();
        if (this._watchId) {
            Gio.bus_unwatch_name(this._watchId);
            this._watchId = 0;
        }
        this._devices.clear();
        this._available = false;
    }

    _emit(removed = []) {
        this._onChanged({
            available: this._available,
            devices: new Map(this._devices),
            removed,
        });
    }

    _subscribe(member, path, callback) {
        const iface = member === 'PropertiesChanged' ? PROPS_IFACE : ROOT_IFACE;
        const arg0 = member === 'PropertiesChanged' ? DEVICE_IFACE : null;
        const id = this._bus.signal_subscribe(
            BUS_NAME, iface, member, path, arg0,
            Gio.DBusSignalFlags.NONE,
            (conn, sender, objPath, ifaceName, signalName, params) => callback(objPath, params));
        this._subs.push(id);
    }

    _unsubscribe() {
        for (const id of this._subs)
            this._bus.signal_unsubscribe(id);
        this._subs = [];
    }

    async _onOwnerAppeared() {
        this._generation++;
        const gen = this._generation;
        this._unsubscribe();
        this._devices.clear();
        this._available = true;

        // Subscribe BEFORE enumerating so nothing is missed.
        this._subscribe('DeviceAdded', ROOT_PATH, (_p, params) => {
            const [path] = params.deepUnpack();
            this._fetchDevice(path, gen);
        });
        this._subscribe('DeviceRemoved', ROOT_PATH, (_p, params) => {
            const [path] = params.deepUnpack();
            if (this._devices.delete(path))
                this._emit([path]);
        });
        this._subscribe('PropertiesChanged', null, (path, params) => {
            if (!this._devices.has(path))
                return;
            const [, changed, invalidated] = params.deepUnpack();
            const current = this._devices.get(path);
            const next = {...current};
            for (const [k, v] of Object.entries(changed))
                next[k] = v.recursiveUnpack();
            this._devices.set(path, next);
            if (invalidated.length > 0)
                this._fetchDevice(path, gen);
            else
                this._emit();
        });

        try {
            const reply = await this._bus.call(
                BUS_NAME, ROOT_PATH, ROOT_IFACE, 'EnumerateDevices',
                null, null, Gio.DBusCallFlags.NONE, -1, null);
            if (gen !== this._generation)
                return;
            const [paths] = reply.deepUnpack();
            await Promise.all(paths.map(p => this._fetchDevice(p, gen, false)));
            if (gen !== this._generation)
                return;
            this._emit();
        } catch (e) {
            if (gen === this._generation)
                console.warn(`ghub-battery: EnumerateDevices failed: ${e.message}`);
        }
    }

    _onOwnerVanished() {
        this._generation++;
        this._unsubscribe();
        this._devices.clear();
        this._available = false;
        this._emit();
    }

    async _fetchDevice(path, gen, emit = true) {
        try {
            const reply = await this._bus.call(
                BUS_NAME, path, PROPS_IFACE, 'GetAll',
                new GLib.Variant('(s)', [DEVICE_IFACE]),
                null, Gio.DBusCallFlags.NONE, -1, null);
            if (gen !== this._generation)
                return;
            const [props] = reply.recursiveUnpack();
            this._devices.set(path, props);
            if (emit)
                this._emit();
        } catch (e) {
            if (gen === this._generation)
                console.warn(`ghub-battery: GetAll ${path} failed: ${e.message}`);
        }
    }
}
```

- [ ] **Step 4: Run the live test**

Run: `cd gnome-extension && make test-live 2>&1 | tail -6; echo "exit=$?"`
Expected: a `devices=N peripherals=M` line with `M >= 1`, one line per peripheral showing numeric `Type`/`Percentage`/`BatteryLevel`/`State` (not `[object Object]`), and `exit=0`. The unit suite (`make test`) must still pass `40/40`.

- [ ] **Step 5: Commit**

```bash
git add gnome-extension
git commit -m "feat(gnome): async UPower D-Bus source with generation guard

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 9: notifier.js

**Files:**
- Create: `gnome-extension/notifier.js`

**Interfaces:**
- Produces: `class Notifier { notify(title: string, body: string); destroy(); }`.
- Uses GNOME 46 API: `new MessageTray.Source({title, iconName})`, `Main.messageTray.add(source)`, `new MessageTray.Notification({source, title, body, urgency})`, `source.addNotification(n)`. The Source destroys itself when its last notification is destroyed (verified in Shell 46 `messageTray.js`, `_onNotificationDestroy`), hence lazy recreation.

- [ ] **Step 1: Implement**

`gnome-extension/notifier.js`:

```js
import * as Main from 'resource:///org/gnome/shell/ui/main.js';
import * as MessageTray from 'resource:///org/gnome/shell/ui/messageTray.js';

const SOURCE_TITLE = 'Logitech G Battery';

export class Notifier {
    constructor() {
        this._source = null;
    }

    _ensureSource() {
        if (this._source)
            return this._source;
        const source = new MessageTray.Source({
            title: SOURCE_TITLE,
            iconName: 'battery-caution-symbolic',
        });
        // GNOME destroys a source once its last notification is dismissed.
        source.connect('destroy', () => {
            if (this._source === source)
                this._source = null;
        });
        Main.messageTray.add(source);
        this._source = source;
        return source;
    }

    notify(title, body) {
        const source = this._ensureSource();
        const notification = new MessageTray.Notification({
            source,
            title,
            body,
            urgency: MessageTray.Urgency.NORMAL,
        });
        source.addNotification(notification);
    }

    destroy() {
        const source = this._source;
        this._source = null;
        source?.destroy();
    }
}
```

- [ ] **Step 2: Syntax check (cannot import Shell resources outside the Shell)**

Run: `cd gnome-extension && gjs -c "$(sed 's#^import .*resource.*##' notifier.js | sed 's/MessageTray\.//g; s/Main\.messageTray\.add(source);//')" 2>&1 | tail -2`
Expected: no `SyntaxError`. (This only parses; behaviour is verified in Task 12's nested session.)

- [ ] **Step 3: Commit**

```bash
git add gnome-extension/notifier.js
git commit -m "feat(gnome): lazily recreated notification source

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 10: panelItem.js and stylesheet

**Files:**
- Create: `gnome-extension/panelItem.js`
- Create: `gnome-extension/stylesheet.css`

**Interfaces:**
- Consumes: `labelFor`, `colorBand`, `iconNameFor` from `model.js`; `Row` descriptors from `menu.js`.
- Produces: `PanelItem` (GObject class extending `PanelMenu.Button`) with `constructor(record, {getRows: () => Row[], openPrefs: () => void})`, `update(record)`, and the inherited `destroy()`. The `record.id` is exposed as `item.recordId`.

- [ ] **Step 1: Write the stylesheet**

`gnome-extension/stylesheet.css`:

```css
/* Explicit colors: St on GNOME 46 does not support CSS variables. */
.ghub-battery-item {
    spacing: 4px;
}
.ghub-battery-label {
    font-feature-settings: "tnum";
}
.ghub-battery-amber .ghub-battery-label,
.ghub-battery-amber StIcon {
    color: #e5a50a;
}
.ghub-battery-red .ghub-battery-label,
.ghub-battery-red StIcon {
    color: #e01b24;
}
.ghub-battery-inactive {
    opacity: 0.5;
}
```

- [ ] **Step 2: Implement panelItem.js**

`gnome-extension/panelItem.js`:

```js
import GObject from 'gi://GObject';
import St from 'gi://St';
import Clutter from 'gi://Clutter';
import * as PanelMenu from 'resource:///org/gnome/shell/ui/panelMenu.js';
import * as PopupMenu from 'resource:///org/gnome/shell/ui/popupMenu.js';
import {labelFor, colorBand, iconNameFor} from './model.js';

const BAND_CLASSES = ['ghub-battery-amber', 'ghub-battery-red'];

export const PanelItem = GObject.registerClass(
class PanelItem extends PanelMenu.Button {
    _init(record, {getRows, openPrefs}) {
        super._init(0.0, `ghub-battery ${record.name}`, false);
        this.recordId = record.id;
        this._getRows = getRows;
        this._openPrefs = openPrefs;

        this._box = new St.BoxLayout({style_class: 'ghub-battery-item'});
        this._icon = new St.Icon({style_class: 'system-status-icon'});
        this._label = new St.Label({
            style_class: 'ghub-battery-label',
            y_align: Clutter.ActorAlign.CENTER,
        });
        this._box.add_child(this._icon);
        this._box.add_child(this._label);
        this.add_child(this._box);

        this._openStateId = this.menu.connect('open-state-changed', (_m, open) => {
            if (open)
                this._rebuildMenu();
        });

        this.update(record);
    }

    update(record) {
        this.recordId = record.id;
        this._icon.icon_name = iconNameFor(record.kind);
        this._label.text = labelFor(record);
        for (const c of BAND_CLASSES)
            this.remove_style_class_name(c);
        const band = colorBand(record);
        if (band === 'amber')
            this.add_style_class_name('ghub-battery-amber');
        else if (band === 'red')
            this.add_style_class_name('ghub-battery-red');
        if (record.active)
            this.remove_style_class_name('ghub-battery-inactive');
        else
            this.add_style_class_name('ghub-battery-inactive');
    }

    _rebuildMenu() {
        this.menu.removeAll();
        for (const row of this._getRows()) {
            if (row.type === 'separator') {
                this.menu.addMenuItem(new PopupMenu.PopupSeparatorMenuItem());
            } else if (row.type === 'prefs') {
                const item = new PopupMenu.PopupMenuItem(row.text);
                item.connect('activate', () => this._openPrefs());
                this.menu.addMenuItem(item);
            } else if (row.type === 'device') {
                const item = new PopupMenu.PopupImageMenuItem(row.text, row.iconName);
                item.setSensitive(false);
                if (row.inactive)
                    item.add_style_class_name('ghub-battery-inactive');
                this.menu.addMenuItem(item);
            } else {
                const item = new PopupMenu.PopupMenuItem(row.text);
                item.setSensitive(false);
                this.menu.addMenuItem(item);
            }
        }
    }

    destroy() {
        if (this._openStateId && this.menu) {
            this.menu.disconnect(this._openStateId);
            this._openStateId = 0;
        }
        super.destroy();
    }
});
```

- [ ] **Step 3: Syntax check**

Run: `cd gnome-extension && node -e "" 2>/dev/null; gjs -c "$(sed -E 's#^import .*resource.*##; s#^import .*gi://.*##' panelItem.js | sed -E 's/GObject\.registerClass\(/(/; s/extends PanelMenu\.Button//')" 2>&1 | tail -2`
Expected: no `SyntaxError`.

- [ ] **Step 4: Commit**

```bash
git add gnome-extension/panelItem.js gnome-extension/stylesheet.css
git commit -m "feat(gnome): panel item with own menu and color classes

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 11: extension.js and prefs.js

**Files:**
- Create: `gnome-extension/extension.js`
- Create: `gnome-extension/prefs.js`

**Interfaces:**
- Consumes: `UPowerSource`, `isPeripheral`, `normalize`, `sortRecords`, `alertStep`, `labelFor`, `buildRows`, `PanelItem`, `Notifier`.
- Behaviour per spec "Data flow": session `known` map keeps removed devices as inactive; `armed` map per id; panel shows active devices in sorted order; fallback item when none active; rebuild all items when order changes.

- [ ] **Step 1: Implement extension.js**

`gnome-extension/extension.js`:

```js
import {Extension} from 'resource:///org/gnome/shell/extensions/extension.js';
import * as Main from 'resource:///org/gnome/shell/ui/main.js';

import {UPowerSource} from './upowerSource.js';
import {isPeripheral, normalize, sortRecords, alertStep, labelFor} from './model.js';
import {buildRows} from './menu.js';
import {PanelItem} from './panelItem.js';
import {Notifier} from './notifier.js';

const ROLE_PREFIX = 'ghub-battery-';
const FALLBACK_RECORD = {
    id: '__fallback__', name: 'Logitech G Battery', kind: 'other',
    percent: null, level: null, state: 0, charging: false, active: false, source: 'upower',
};

export default class GhubBatteryExtension extends Extension {
    enable() {
        this._settings = this.getSettings();
        this._known = new Map();     // id -> record (removed devices kept inactive)
        this._armed = new Map();     // id -> boolean
        this._items = [];            // PanelItem[] in panel order
        this._available = false;
        this._notifier = new Notifier();
        this._source = new UPowerSource(snapshot => this._onSnapshot(snapshot));
        this._render();
        this._source.start();
    }

    disable() {
        this._source?.stop();
        this._source = null;
        this._destroyItems();
        this._notifier?.destroy();
        this._notifier = null;
        this._known = null;
        this._armed = null;
        this._settings = null;
    }

    _onSnapshot({available, devices, removed}) {
        this._available = available;
        for (const id of removed) {
            const prev = this._known.get(id);
            if (prev)
                this._known.set(id, {...prev, active: false});
        }
        for (const [path, props] of devices) {
            if (!isPeripheral(props)) {
                this._known.delete(path);
                continue;
            }
            const record = normalize(path, props);
            this._known.set(path, record);
            this._maybeAlert(record);
        }
        this._render();
    }

    _maybeAlert(record) {
        const armed = this._armed.get(record.id) ?? true;
        const step = alertStep(armed, record);
        this._armed.set(record.id, step.armed);
        if (step.fire && this._settings.get_boolean('notify-low'))
            this._notifier.notify(`${record.name}: bateria fraca`, `Nível atual: ${labelFor(record)}`);
    }

    _rows() {
        return buildRows([...this._known.values()], {
            available: this._available,
            showInactive: this._settings.get_boolean('show-inactive'),
        });
    }

    _itemOpts() {
        return {getRows: () => this._rows(), openPrefs: () => this.openPreferences()};
    }

    _render() {
        const active = sortRecords([...this._known.values()].filter(r => r.active));
        const wanted = active.length > 0 ? active : [FALLBACK_RECORD];
        const currentIds = this._items.map(i => i.recordId);
        const wantedIds = wanted.map(r => r.id);
        const sameOrder = currentIds.length === wantedIds.length
            && currentIds.every((id, i) => id === wantedIds[i]);

        if (sameOrder) {
            wanted.forEach((r, i) => this._items[i].update(r));
            return;
        }
        this._destroyItems();
        wanted.forEach((r, i) => {
            const item = new PanelItem(r, this._itemOpts());
            Main.panel.addToStatusArea(`${ROLE_PREFIX}${i}`, item, i, 'right');
            this._items.push(item);
        });
    }

    _destroyItems() {
        for (const item of this._items)
            item.destroy();
        this._items = [];
    }
}
```

- [ ] **Step 2: Implement prefs.js**

`gnome-extension/prefs.js`:

```js
import Adw from 'gi://Adw';
import Gio from 'gi://Gio';
import {ExtensionPreferences} from 'resource:///org/gnome/Shell/Extensions/js/extensions/prefs.js';

export default class GhubBatteryPreferences extends ExtensionPreferences {
    fillPreferencesWindow(window) {
        const settings = this.getSettings();
        const page = new Adw.PreferencesPage();
        const group = new Adw.PreferencesGroup({title: 'Comportamento'});

        const notify = new Adw.SwitchRow({
            title: 'Notificar bateria fraca',
            subtitle: 'Uma notificação quando um dispositivo cai para 20% ou menos',
        });
        settings.bind('notify-low', notify, 'active', Gio.SettingsBindFlags.DEFAULT);

        const inactive = new Adw.SwitchRow({
            title: 'Mostrar dispositivos inativos no menu',
            subtitle: 'Mantém dispositivos desligados listados em cinza',
        });
        settings.bind('show-inactive', inactive, 'active', Gio.SettingsBindFlags.DEFAULT);

        group.add(notify);
        group.add(inactive);
        page.add(group);
        window.add(page);
    }
}
```

- [ ] **Step 3: Syntax check both files**

Run: `cd gnome-extension && for f in extension.js prefs.js; do gjs -c "$(sed -E 's#^import .*##' $f | sed -E 's/extends (Extension|ExtensionPreferences)//')" 2>&1 | tail -1; done`
Expected: no `SyntaxError` lines.

- [ ] **Step 4: Commit**

```bash
git add gnome-extension/extension.js gnome-extension/prefs.js
git commit -m "feat(gnome): extension lifecycle, panel diffing and preferences

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 12: Makefile pack/install/nested and real verification

**Files:**
- Modify: `gnome-extension/Makefile`

- [ ] **Step 1: Add the remaining targets**

Replace the Makefile's `.PHONY` line and append after `schemas:`:

```make
pack: schemas
	gnome-extensions pack -f \
	  $(foreach f,$(filter-out extension.js prefs.js stylesheet.css,$(RUNTIME)),--extra-source=$(f)) \
	  --schema=$(SCHEMA) \
	  -o .
	@unzip -l $(UUID).shell-extension.zip > /tmp/ghub-pack.lst
	@for f in metadata.json $(RUNTIME) schemas/gschemas.compiled; do \
	  grep -q " $$f$$" /tmp/ghub-pack.lst || { echo "MISSING $$f in zip"; exit 1; }; done
	@echo "packed $(UUID).shell-extension.zip"

install: pack
	gnome-extensions install -f $(UUID).shell-extension.zip
	@echo "Installed. On X11 press Alt+F2, type r, Enter; then: gnome-extensions enable $(UUID)"

nested:
	dbus-run-session -- gnome-shell --nested --wayland

clean:
	rm -f $(UUID).shell-extension.zip schemas/gschemas.compiled
```

- [ ] **Step 2: Pack and verify archive contents**

Run: `cd gnome-extension && make pack 2>&1 | tail -3`
Expected: `packed ghub-battery@vinipy12.github.io.shell-extension.zip` and no `MISSING` line. Then `unzip -l ghub-battery@vinipy12.github.io.shell-extension.zip | grep -c tests` must print `0`.

- [ ] **Step 3: Install and test in a nested session**

Run: `cd gnome-extension && make install 2>&1 | tail -2`
Then start a nested shell in the background and watch its log:

```bash
cd gnome-extension && (make nested > /tmp/ghub-nested.log 2>&1 &) ; sleep 8
gnome-extensions enable ghub-battery@vinipy12.github.io
sleep 3; grep -iE "ghub-battery|error|exception" /tmp/ghub-nested.log | tail -10
```

Expected: no `Error`/`TypeError` lines mentioning `ghub-battery`. In the nested window: one panel item per active device (G305 "Cheia", keyboard and headset percentages), the keyboard item in amber if below 50%, and the dropdown listing all devices plus Preferências. Close the nested shell window afterwards.

If a stack trace appears, fix the named file in place, `make install` again, and repeat. Do not proceed with a trace in the log.

- [ ] **Step 4: Manual alert check (real session)**

In the real session: `Alt+F2`, `r`, Enter; then `gnome-extensions enable ghub-battery@vinipy12.github.io`. Open Looking Glass (`Alt+F2`, `lg`), tab Extensions: ghub-battery must show state ACTIVE with no error. A low-battery reading cannot be faked on the real bus, so the notification path is exercised directly in the next step.

- [ ] **Step 5: Notification smoke test via Looking Glass**

In Looking Glass "Evaluator" tab run:

```js
imports.ui.main.extensionManager.lookup('ghub-battery@vinipy12.github.io').stateObj._notifier.notify('Teste', 'Notificação de teste')
```

Expected: a banner appears. Dismiss it. Run the same line again: a second banner appears (proves the lazily recreated source). If the second call throws, `notifier.js` is wrong.

- [ ] **Step 6: Commit**

```bash
git add gnome-extension/Makefile
git commit -m "build(gnome): pack, install and nested targets with archive verification

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 13: Documentation

**Files:**
- Create: `gnome-extension/README.md`
- Modify: `README.md` (root; add a section before `## 🚀 Instalação Rápida do Widget (1 Clique)`)
- Modify: `CHANGELOG.md` (prepend an Unreleased entry)

- [ ] **Step 1: Write gnome-extension/README.md**

```markdown
# 🔋 Logitech G Battery — extensão para GNOME Shell (Ubuntu 24.04)

Mostra a bateria dos seus periféricos Logitech G na barra superior do GNOME,
um item por dispositivo, com cores âmbar (≤ 50%) e vermelha (≤ 20%) e uma
notificação quando um dispositivo cai para 20% ou menos.

Não precisa do Logitech G HUB: os dados vêm do **UPower**, que o Ubuntu já
usa para a bateria do notebook. Dispositivos ligados por receptor Lightspeed
aparecem pelo driver `hid-logitech-hidpp` do kernel; dispositivos Bluetooth
aparecem pelo BlueZ.

## Requisitos

- Ubuntu 24.04 (GNOME Shell 46). Outras versões do Shell não são suportadas.
- UPower (já instalado no Ubuntu).

## Instalação

```bash
cd gnome-extension
make install
```

Em sessão X11: `Alt+F2`, digite `r`, Enter (reinicia o Shell). Em Wayland:
encerre a sessão e entre de novo. Depois:

```bash
gnome-extensions enable ghub-battery@vinipy12.github.io
```

Ou baixe o `.shell-extension.zip` em Releases e rode
`gnome-extensions install -f <arquivo>.zip`.

## O que aparece

| Situação | Barra superior |
|---|---|
| Dispositivo com porcentagem exata (ex.: G515 por Bluetooth) | `36%` |
| Dispositivo que só informa nível (ex.: G305 pelo receptor) | `Cheia`, `Alta`, `Normal`, `Baixa`, `Crítica` |
| Carregando | `⚡ 40%` |
| Dormindo / desconectado | sai da barra; fica em cinza no menu como *Inativo* |

## Limitações

- Mouses mais antigos pelo receptor (como o G305) só informam nível, não
  porcentagem exata. Leitura direta via HID++ fica para um projeto futuro.
- Sem consumo em mW nem horas restantes (o UPower não expõe isso).

## Desenvolvimento

```bash
make test        # testes puros (gjs -m), sem Shell
make test-live   # lê o UPower real desta máquina
make pack        # gera o zip e confere o conteúdo
make nested      # GNOME Shell aninhado para testar sem reiniciar a sessão
```
```

- [ ] **Step 2: Add the root README section**

Insert before the line `## 🚀 Instalação Rápida do Widget (1 Clique)`:

```markdown
## 🐧 Ubuntu / GNOME: extensão para a barra superior

No Linux não existe o agente do G HUB, mas o Ubuntu já expõe a bateria dos
periféricos Logitech pelo UPower. A pasta **[`gnome-extension/`](gnome-extension/README.md)**
traz uma extensão para GNOME Shell 46 (Ubuntu 24.04) que mostra um item por
dispositivo na barra superior, com cores de alerta e notificação de bateria
fraca. Instalação: `cd gnome-extension && make install`.

---

```

- [ ] **Step 3: Prepend the changelog entry**

Insert after the first heading line of `CHANGELOG.md`:

```markdown

## [Unreleased]

### Adicionado
- Extensão para GNOME Shell 46 (`gnome-extension/`): bateria dos periféricos na barra superior via UPower, cores âmbar/vermelha, notificação abaixo de 20%, preferências.
```

- [ ] **Step 4: Commit**

```bash
git add README.md CHANGELOG.md gnome-extension/README.md
git commit -m "docs: GNOME extension README, root README section and changelog

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 14: CI packaging job

**Files:**
- Modify: `.github/workflows/build-and-release.yml`

- [ ] **Step 1: Add the Linux job before `build:`**

Insert directly under `jobs:` (before `  build:`):

```yaml
  pack-extension:
    name: Pack GNOME extension
    runs-on: ubuntu-latest
    steps:
      - name: Checkout Code
        uses: actions/checkout@v4

      - name: Install tools
        run: sudo apt-get update && sudo apt-get install -y libglib2.0-bin zip gjs gir1.2-upowerglib-1.0

      - name: Unit tests
        working-directory: gnome-extension
        run: gjs -m tests/run.js

      - name: Compile schemas
        run: glib-compile-schemas --strict gnome-extension/schemas/

      - name: Build zip (same layout as gnome-extensions pack)
        working-directory: gnome-extension
        run: |
          zip -r "../ghub-battery@vinipy12.github.io.shell-extension.zip" \
            metadata.json extension.js prefs.js stylesheet.css \
            upowerSource.js model.js panelItem.js menu.js notifier.js \
            schemas/org.gnome.shell.extensions.ghub-battery.gschema.xml schemas/gschemas.compiled
          unzip -l "../ghub-battery@vinipy12.github.io.shell-extension.zip"
          for f in metadata.json extension.js prefs.js stylesheet.css upowerSource.js model.js panelItem.js menu.js notifier.js schemas/gschemas.compiled; do
            unzip -l "../ghub-battery@vinipy12.github.io.shell-extension.zip" | grep -q " $f$" || { echo "MISSING $f"; exit 1; }
          done

      - name: Upload extension artifact
        uses: actions/upload-artifact@v4
        with:
          name: gnome-extension
          path: ghub-battery@vinipy12.github.io.shell-extension.zip
```

- [ ] **Step 2: Make the Windows job consume it**

Under `  build:` add `    needs: pack-extension` directly after `    runs-on: windows-latest`.

Insert a step immediately before the step that generates `SHA256SUMS.txt` (the one containing `$zipHash = (Get-FileHash 'Logitech-GHub-Widget-Setup.zip'`):

```yaml
      - name: Download GNOME extension artifact
        uses: actions/download-artifact@v4
        with:
          name: gnome-extension
          path: .
```

In that checksum step, add after the line `$pyHash = (Get-FileHash 'ghub-cli.py' -Algorithm SHA256).Hash`:

```powershell
          $gnomeHash = (Get-FileHash 'ghub-battery@vinipy12.github.io.shell-extension.zip' -Algorithm SHA256).Hash
```

and after the line `"$pyHash  ghub-cli.py" | Out-File -Encoding utf8 -Append SHA256SUMS.txt`:

```powershell
          "$gnomeHash  ghub-battery@vinipy12.github.io.shell-extension.zip" | Out-File -Encoding utf8 -Append SHA256SUMS.txt
```

In both `path: |` (Upload Build Artifacts) and `files: |` (Create GitHub Release) lists, add a line `            ghub-battery@vinipy12.github.io.shell-extension.zip` before `            SHA256SUMS.txt`.

- [ ] **Step 3: Validate the YAML parses and the names match**

Run: `python3 -I -c "import yaml,sys; d=yaml.safe_load(open('.github/workflows/build-and-release.yml')); print(list(d['jobs']), d['jobs']['build'].get('needs'))" 2>&1 | tail -2`
Expected: `['pack-extension', 'build'] pack-extension`.

Run: `grep -c "ghub-battery@vinipy12.github.io.shell-extension.zip" .github/workflows/build-and-release.yml`
Expected: `7` or more (zip build, unzip checks, artifact path, download not counted, hash, sums line, upload list, release list).

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/build-and-release.yml
git commit -m "ci: pack and test the GNOME extension on Linux and attach it to releases

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Self-review notes

- Spec coverage: filter (T2), normalize incl. per-transport activity (T3, T11 handles `removed`), display rules (T4), ordering (T5), alert arming incl. fully-charged re-arm (T6), menu rows and fallback states (T7, T11), source contract with subscribe-before-enumerate and generation guard (T8), lazy notifier (T9), own-menu-per-item and explicit CSS (T10), lifecycle/disable (T11), pack with `--extra-source` and verification, install recipe with Shell restart, nested session (T12), docs (T13), CI feeding checksums and release (T14). Fixture capture: done before this plan; `G305_ASLEEP` derived in T1 from the reviewer's observation.
- Type consistency: `Record` fields `{id,name,kind,percent,level,state,charging,active,source}` used identically in T3–T7 and T11; `Row` types `message|device|separator|prefs` in T7 and T10; `snapshot {available, devices, removed}` in T8 and T11; `PanelItem(record, {getRows, openPrefs})` and `recordId` in T10 and T11.
- Known limitation stated in T12: the "Inativo" row for a removed Bluetooth device and the amber/red rendering are checked visually in the nested session, not by unit test.
