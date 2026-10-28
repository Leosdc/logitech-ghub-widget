# GNOME Battery Indicator for Logitech Peripherals — Design

Date: 2026-10-27
Status: revised after two adversarial reviews (see `*-review-claude.md`,
`*-review-codex.md` beside this file); awaiting owner approval.

## Problem

The repo ships a Windows 11 widget, a .NET CLI and a Python CLI. All three read
the Logitech G HUB agent's local WebSocket (`ws://127.0.0.1:9010`). That agent
does not exist on Linux, so on Ubuntu there is no data source and no interface.

Ubuntu already exposes Logitech peripherals' batteries through UPower. The
kernel `hid-logitech-hidpp` driver creates a `hidpp_battery_N` power supply for
each device paired to a Lightspeed/Unifying receiver; BlueZ-paired devices
appear as UPower `keyboard`/`headset`/`mouse` devices. Observed on the
reference machine (Ubuntu 24.04.5, GNOME Shell 46.0, UPower 1.90.3, X11):

| Device | Transport | Type | Percentage | BatteryLevel | State | Notes |
|---|---|---|---|---|---|---|
| G305 Lightspeed mouse | receiver 046d:c53f | mouse (5) | 100 awake / 50 asleep | full (8) awake / unknown (0) asleep | fully-charged (4) awake / unknown (0) asleep | no sysfs `capacity`; Percentage is a constant derived from the level |
| G515 LS TKL | Bluetooth | keyboard (6) | 36 (exact) | none (1) | unknown (0), always | object removed from the bus when idle, re-added on reconnect |
| G435 headset | Bluetooth | headset (17) | 60 (exact) | none (1) | unknown (0), always | same removal behaviour when off |

Facts that shape the design (all verified against UPower 1.90.3 sources and
the live bus):
- The BlueZ backend never sets `State`; it sets `Type, Model, Serial,
  PowerSupply, IsPresent, Percentage, UpdateTime` only. `IsPresent` is always
  true for BlueZ devices. Disconnection is a `DeviceRemoved` signal.
- The hidpp (sysfs) backend keeps the object alive while the mouse sleeps and
  publishes `State=unknown, BatteryLevel=unknown, Percentage=50`.
- "Percentage should be ignored" is exactly `BatteryLevel != none` (libupower
  `up-device.c`). The coarse `BatteryLevel` enum is: 0 unknown, 1 none,
  2 discharging, 3 low, 4 critical, 5 action, 6 normal, 7 high, 8 full.
- `State` enum: 0 unknown, 1 charging, 2 discharging, 3 empty,
  4 fully-charged, 5 pending-charge, 6 pending-discharge.
- `Type` enum (0..28): 0 unknown, 1 line-power, 2 battery, 3 ups, 4 monitor,
  5 mouse, 6 keyboard, 7 pda, 8 phone, 9 media-player, 10 tablet, 11 computer,
  12 gaming-input, 13 pen, 14 touchpad, 15 modem, 16 network, 17 headset,
  18 speakers, 19 headphones, 20 video, 21 audio-device, 22 remote-control,
  23 printer, 24 scanner, 25 camera, 26 wearable, 27 toy, 28 bluetooth-generic.
- `Vendor` exists but is the empty string for all three devices.
- `UpdateTime` is epoch seconds and, for BlueZ devices, only moves when the
  percentage changes (hours old is normal). It must never be used as a
  staleness signal.
- `gi://UPowerGlib` is importable both in the Shell and under plain `gjs`, and
  is the single source for enum constants.

## Goal

A GNOME Shell extension that shows each battery-reporting peripheral in the top
bar, updates on UPower events, colors low batteries, and notifies once per
crossing below 20%. Zero runtime dependencies beyond GNOME Shell 46 and
UPower.

## Decisions taken

- Form factor: GNOME top-bar indicator (Shell extension), not a floating window.
- Data source: UPower over D-Bus only. Direct HID++ (exact mouse percentage,
  mW, hours) is a later, separate project. The device record carries a
  `source` field so a second source can be added beside UPower.
- Target: **GNOME Shell 46 only** (what Ubuntu 24.04 ships). Shell 45 has a
  different notification API (positional `Source`/`Notification` constructors
  and `showNotification`) and cannot be exercised on the reference machine,
  so it is out of scope rather than shipped untested. `shell-version: ["46"]`.
- Panel: one item per device (icon + value), neutral color normally, amber and
  red when low. Inactive devices leave the panel and show greyed in the dropdown.
- Alerts: one GNOME notification per device per crossing below 20%, re-armed
  when the device rises above 20% or reports charging/fully-charged. Default
  on, toggle in prefs.
- Architecture: pure GJS (ESM), D-Bus directly to UPower. No helper process,
  no polling, no Python.

## Layout

Repository folder `gnome-extension/` (beside `cli/` and `src/`). Installed
folder name must equal the UUID: `ghub-battery@vinipy12.github.io`.

| File | Responsibility |
|---|---|
| `metadata.json` | Complete minimal form below. |
| `extension.js` | Default-exports a class extending `Extension` (from `resource:///org/gnome/shell/extensions/extension.js`). `enable()` creates the source, panel items, settings bindings; `disable()` tears all of them down. No logic. |
| `upowerSource.js` | D-Bus client (contract below). Emits `changed(snapshot)`. |
| `model.js` | Shell-free pure functions (see Model). Imports only `gi://UPowerGlib` for enums. |
| `panelItem.js` | One `PanelMenu.Button` per active device. Owns its own `PopupMenu`; the menu content is rebuilt from the shared snapshot each time it opens, via `menu.js`. |
| `menu.js` | Pure builder: snapshot -> list of menu rows (every known device, active and inactive, with value/state) plus a Preferences row. |
| `notifier.js` | Lazily creates a `MessageTray.Source` on first alert, drops its reference on the source's `destroy`, recreates when needed. |
| `prefs.js` + `schemas/org.gnome.shell.extensions.ghub-battery.gschema.xml` | Keys: `notify-low` (b, default true), `show-inactive` (b, default true). |
| `stylesheet.css` | `.ghub-battery-amber`, `.ghub-battery-red`, `.ghub-battery-inactive` with **explicit colors** (no CSS variables; St on 46 does not support them). Amber `#e5a50a`, red `#e01b24`, inactive uses `opacity: 0.5`. Checked visually on Yaru light and dark. |
| `tests/run.js` + `tests/*.test.js` + `tests/fixtures/*.json` | `gjs -m tests/run.js` imports and runs every `*.test.js` (gjs -m takes one entry module). |
| `Makefile` | `test`, `schemas`, `pack`, `install`, `nested`. |
| `README.md` | Portuguese, matching the repo. Root README gains an install section. |

`metadata.json`:

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

## UPower source contract (`upowerSource.js`)

- All bus calls are asynchronous on the system bus. Nothing runs synchronously
  in the Shell.
- Order at `start()`: subscribe to `DeviceAdded`/`DeviceRemoved` on
  `org.freedesktop.UPower` and to `org.freedesktop.DBus.Properties.PropertiesChanged`
  (interface filter `org.freedesktop.UPower.Device`, path filter per known
  device) **before** calling `EnumerateDevices`, so nothing is missed.
- Each device's properties come from
  `Properties.GetAll('org.freedesktop.UPower.Device')`; variants are unpacked at
  this boundary, and the source caches one plain property map per path.
- `PropertiesChanged` merges changed fields into the cached map. Invalidated
  properties trigger a `GetAll` for that path.
- A `generation` counter increments on `start()`, `stop()` and on
  `NameOwnerChanged` for `org.freedesktop.UPower`; replies carrying an older
  generation are dropped, so a late reply can never resurrect a removed device
  or overwrite fresh state.
- `DeviceRemoved` deletes the cached map and emits `changed` with the path in
  `removed`.
- `stop()` disconnects every signal, bumps the generation, clears the cache.
- Name-owner loss sets `available=false`; a new owner restarts the sequence.

Snapshot emitted: `{ available: boolean, devices: Map<path, props>, removed: path[] }`.

## Model (`model.js`)

Output device record:

```
{ id, name, kind, percent, level, state, charging, active, source: "upower" }
```

- `id`: UPower object path. `name`: `Model` if non-empty, else `Serial`, else
  the last path segment.
- `kind`: `mouse | keyboard | headset | gaming-input | other` from `Type`
  (headphones 19 and audio-device 21 map to `headset`; everything else that
  passes the filter is `other`).
- `percent`: finite number rounded to an integer when `BatteryLevel == none`;
  otherwise `null`.
- `level`: `unknown | low | critical | action | normal | high | full | null`
  (`null` when `BatteryLevel == none`). `discharging` (2) is treated as
  `normal`.
- `state`: raw `State` value kept for display; `charging` is true only for
  `charging` and `pending-charge`. `fully-charged` is **not** charging: no
  bolt, normal color, but it does re-arm alerts.
- `active`: true unless `IsPresent` is false or `level == unknown`. Removal
  from the bus is handled by the extension, not by this flag (see Data flow).

Filter rule: a device counts when `PowerSupply` is exactly `false` (missing
counts as not a peripheral, fail closed) and `Type` is not one of
`unknown, line-power, ups, monitor, computer`. `Type == battery` with
`PowerSupply == false` counts as `other` (UPower's generic peripheral battery
representation). Unknown future `Type` values count as `other`. `pen`,
`touchpad`, `phone`, `bluetooth-generic` etc. therefore show in the panel,
which is intended: anything with a battery that is not the laptop.

Display rules:
- Value label: `NN%` when `percent` is a number; otherwise the coarse word:
  `full -> Cheia`, `high -> Alta`, `normal -> Normal`, `low -> Baixa`,
  `critical -> Crítica`, `action -> Crítica`, `unknown -> —` (en dash). A
  charging device prefixes `⚡`.
- Color band on `percent`: `p > 50` normal; `20 < p <= 50` amber;
  `p <= 20` red. On `level`: `low -> amber`, `critical | action -> red`,
  everything else normal. Charging always normal.
- Panel order: mouse, keyboard, headset, gaming-input, other; alphabetical by
  name within a kind. `extension.js` re-inserts actors into `Main.panel` at
  computed indices when the sorted order changes, so the bar order matches.

Alert rule is **stateful per device**: `armed` starts true. Fire when
`armed && !charging && (percent <= 20 || level in {critical, action})`; then
`armed=false`. Re-arm when `percent > 20`, or `level in {normal, high, full}`,
or `charging`, or `state == fully-charged`. Readings with `percent == null &&
level in {unknown, null}` (asleep, or no data) change nothing. A device that
is removed and re-added keeps its `armed` flag for the session.

## Data flow

1. `enable()` -> `UPowerSource.start()` -> subscriptions -> `EnumerateDevices`
   -> per-device `GetAll` -> `changed(snapshot)`.
2. `extension.js` runs `model.normalize` on each device and keeps a session
   map `known: Map<path, record>` plus the alert `armed` flags. Paths listed in
   `removed` are kept in `known` with `active=false` (last-seen values), so a
   Bluetooth keyboard that disconnects stays in the dropdown greyed as
   `Inativo`. The diff against current panel items: newly active -> new
   `PanelItem`; no longer active or removed -> destroy its item; changed ->
   update label and color in place.
3. If no device is active, a single grey panel item with a generic battery
   icon remains so the dropdown stays reachable.
4. For each active device, `model.shouldAlert(armedFlag, record)`; on true,
   `notifier.notify(name, value)` if `notify-low`.
5. Every subsequent UPower signal repeats steps 2-4.

## Error handling

- UPower not on the bus at enable, or owner lost: `available=false`; the
  fallback panel item shows, menu says `UPower indisponível`. `NameOwnerChanged`
  restarts the source.
- Partial `PropertiesChanged` payloads merge into the cache; a non-finite or
  missing `Percentage` with `BatteryLevel == none` renders `—` and never `NaN`
  or `0%`.
- `disable()` stops the source first (so no callback can touch actors
  afterwards), destroys every panel item, destroys the notifier's live source
  if any, disconnects settings, nulls all references.

## Testing

- `make test` -> `gjs -m tests/run.js`. Fixtures are JSON captures of real
  `GetAll` dicts: G305 awake and asleep, G515 and G435 over Bluetooth, plus
  G515 over the receiver when captured. Tests cover filter (including
  `Type=battery` peripheral, missing `PowerSupply`), normalize, label, color
  band, ordering, and alert arming across sequences: drop to 19, hold at 19,
  asleep reading (no change), rise to 25, drop again; and the
  fully-charged re-arm path.
- `tests/upowerSource.live.test.js` runs against the live system bus and is
  **manual only** (`make test-live`), never a CI gate.
- Manual: `make nested` (`dbus-run-session -- gnome-shell --nested --wayland`,
  verified to run on the X11 reference machine; note it loads the user's real
  extension list). Real-session install:
  `make pack && gnome-extensions install -f <zip>`, then on X11 `Alt+F2`, `r`
  (the Shell does not watch extension dirs, so `enable` fails on a first
  install without a restart), then `gnome-extensions enable <uuid>`.
- Manual alert check: trigger an alert, dismiss it, re-arm, trigger again
  (guards the lazily recreated notification source).

## Packaging

- `make schemas` -> `glib-compile-schemas --strict schemas/`.
- `make pack` -> `gnome-extensions pack -f --extra-source=upowerSource.js
  --extra-source=model.js --extra-source=panelItem.js --extra-source=menu.js
  --extra-source=notifier.js --schema=schemas/...gschema.xml`, output
  `ghub-battery@vinipy12.github.io.shell-extension.zip`. `tests/` is never
  packed. `make pack` lists the archive and fails if any runtime module is
  missing.
- `make install` -> `gnome-extensions install -f` of that zip (which compiles
  schemas itself).
- CI: `.github/workflows/build-and-release.yml` gains a `pack-extension` job
  on `ubuntu-latest` that compiles schemas with `glib-compile-schemas`, builds
  the zip with plain `zip -r` over the explicit file list (byte-equivalent to
  `pack`, avoids installing `gnome-shell` on the runner), verifies
  `metadata.json` and all modules are in the archive, and uploads it as an
  artifact. The existing Windows `build` job gains `needs: pack-extension`,
  downloads the artifact before its `SHA256SUMS.txt` step, so the zip is
  listed in the checksum file and attached by the existing release step.

## Out of scope

HID++ direct access, power draw (mW), estimated hours, the floating window,
autostart handling, GNOME Shell other than 46, Ubuntu before 24.04,
extensions.gnome.org submission.

## Precursor before implementation

Capture fixtures now, while the devices are at hand: `GetAll` dicts for the
three devices (G305 awake and asleep), and the `DeviceRemoved`/`DeviceAdded`
sequence for the keyboard going idle. G515-over-receiver capture is deferred
by the owner.
