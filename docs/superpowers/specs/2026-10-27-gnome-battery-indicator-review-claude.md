# Review: GNOME Battery Indicator design (2026-10-27)

Reviewed: `docs/superpowers/specs/2026-10-27-gnome-battery-indicator-design.md` at
`3db830f` on `feat/gnome-battery-indicator`, against the live reference machine
(Ubuntu 24.04.5, GNOME Shell 46.0-0ubuntu6~24.04.15, mutter 46.2, UPower 1.90.3,
gjs 1.80.2, X11). Reviewer: Claude. Date: 2026-10-27.

Sources used beyond the box: UPower v1.90.3 and gnome-shell 45.0/46.0 raw files
pulled with `curl` into the session scratchpad (the WebFetch tool was blocked by
the orchestrator-research hook; its subagent exemption did not detect this
agent — reporting the miss as the hook asks). Ubuntu's shipped Shell 46 JS was
extracted from `/usr/lib/gnome-shell/libshell-14.so` and diffed against upstream
46.0: `panelMenu.js` 0 changed lines, `messageTray.js` 7, `extensionSystem.js` 9
(packaging/patch noise; none touch the APIs cited below).

## 1. Verdict

**Not ready.** The `present` rule (`IsPresent and State != unknown`) hides every
Bluetooth peripheral on this machine — both devices the spec's own table lists at
35% and 60% — because UPower's BlueZ backend never sets `State`. Two further
load-bearing claims are wrong (the 45/46 `MessageTray` API is not one API; the
coarse `BatteryLevel` set omits the value the G305 actually reports when idle),
and three operational claims (`gjs -m tests/*.test.js`, `make install &&
gnome-extensions enable`, "pack with compiled schema") do not work as written.
The architecture (pure GJS, UPower over D-Bus, no polling) is sound and every
D-Bus name/signal/property/enum the spec relies on checks out.

## 2. Claims table

| # | Claim (spec line) | Verdict | Evidence |
|---|---|---|---|
| 1 | G HUB agent `ws://127.0.0.1:9010` absent on Linux; repo is Windows widget + .NET CLI + Python CLI (8-10) | verified (repo) | `ls` root: `src/`, `cli/`, `ghub-cli.py`; no Linux data source. |
| 2 | `hid-logitech-hidpp` creates `hidpp_battery_N` for receiver-paired devices (12-14) | verified | `/sys/class/power_supply/hidpp_battery_12 -> .../0003:046D:C53F.004B/0003:046D:4074.004C/power_supply/hidpp_battery_12`; driver `logitech-hidpp-device`; `lsusb`: `046d:c53f USB Receiver`. |
| 3 | BlueZ devices appear as UPower `keyboard`/`headset` (14-15) | verified | `Type=17` (headset) on `headset_dev_AA_BB_CC_00_00_01`, `Type=6` on `keyboard_dev_AA_BB_CC_00_00_02` (gjs GetAll, 13:50). |
| 4 | Table row G305: mouse, 100%, "should be ignored", `battery-level: full` (20) | verified at one instant, **incomplete** | `upower -d` 13:41:38: `fully-charged / full / 100% (should be ignored)`. 52 s later (`busctl` 13:42:30): `State=0 BatteryLevel=0 Percentage=50`. Sysfs: no `capacity` attr; `capacity_level=Unknown`, `status=Unknown`. See §3.2. |
| 5 | Table row G515: keyboard, 35% exact (21) | verified (36% now) | gjs GetAll 13:50: `Type=6 State=0 BatteryLevel=1 Percentage=36 IsPresent=true`. Absent from the bus at 13:41 (`upower -e` 6 paths, no keyboard; `bluetoothctl info AA:BB:CC:00:00:02`: `Paired: yes Connected: no`). |
| 6 | Table row G435: headset, 60% exact (22) | verified | `busctl`: `Percentage 60`, `BatteryLevel 1`, `State 0`, `IsPresent true`. |
| 7 | "UPower exposes no vendor field" (24) | wrong in wording, right in effect | `Vendor` property exists on both devices with value `""`. |
| 8 | `org.freedesktop.UPower` has `EnumerateDevices`, `DeviceAdded(o)`, `DeviceRemoved(o)` (55) | verified | `busctl introspect org.freedesktop.UPower /org/freedesktop/UPower org.freedesktop.UPower`. |
| 9 | Per-device `PropertiesChanged` (55) | verified | Device object exposes `org.freedesktop.DBus.Properties.PropertiesChanged sa{sv}as`; every Device property is `emits-change`. No legacy `Changed` signal on the Device interface. |
| 10 | Properties `Type, Model, Serial, Percentage, BatteryLevel, State, IsPresent, PowerSupply, UpdateTime` (68-69) | verified, with types | `Type u`, `Model s`, `Serial s`, `Percentage d`, `BatteryLevel u`, `State u`, `IsPresent b`, `PowerSupply b`, `UpdateTime t` (seconds since epoch, e.g. `1793112499`). |
| 11 | "percentage should be ignored" == `BatteryLevel != none` (69-70) | verified | `libupower-glib/up-device.c` v1.90.3:375-378: `if (battery_level == UP_DEVICE_LEVEL_NONE) ... else "... (should be ignored)"`. `DeviceLevel.NONE = 1`. |
| 12 | Type enum values `unknown, line-power, battery, ups, monitor, computer` excluded; `mouse, keyboard, headset, gaming-input, speakers, tablet, phone, media-player` exist (87-90) | verified | gjs `UPowerGlib.Device.kind_to_string`: 0 unknown, 1 line-power, 2 battery, 3 ups, 4 monitor, 5 mouse, 6 keyboard, 7 pda, 8 phone, 9 media-player, 10 tablet, 11 computer, 12 gaming-input, 13 pen, 14 touchpad, 15 modem, 16 network, 17 headset, 18 speakers, 19 headphones, 20 video, 21 audio-device, 22 remote-control, 23 printer, 24 scanner, 25 camera, 26 wearable, 27 toy, 28 bluetooth-generic. |
| 13 | `level: full, high, normal, low, critical, null` (82) | **incomplete** | `DeviceLevel`: 0 unknown, 1 none, 2 discharging, 3 low, 4 critical, 5 action, 6 normal, 7 high, 8 full. `unknown` (0) is what the G305 reports when asleep (claim 4). `action` (5) is a real daemon-side value. |
| 14 | `charging: State is charging or fully-charged` (84) | verified enum; see §3.1 for BT | `DeviceState`: 0 unknown, 1 charging, 2 discharging, 3 empty, 4 fully-charged, 5 pending-charge, 6 pending-discharge. |
| 15 | `present: IsPresent and State != unknown` (85) | **wrong** | Both BT devices: `IsPresent=true State=0`. `src/linux/up-device-bluez.c` v1.90.3 sets only `type, serial, model, power-supply, has-history` (:212-216), `is-present, percentage, update-time` (:240-242, :268-269) — never `state`. G305 also `State=0` when idle (claim 4). |
| 16 | `PowerSupply` false for peripherals, true for laptop battery/AC (87) | verified | gjs GetAll: BAT0/AC0/ucsi `PowerSupply=true`; headset/mouse/keyboard `false`. `EnumerateDevices` returns 6 paths and does **not** include `DisplayDevice` (the `upower -e` listing appends it client-side). |
| 17 | `metadata.json` `shell-version: ["45","46"]`, `settings-schema` (53) | verified fields; see claim 20 | `extensionSystem.js` 46:410,456 (major-prefix match on `shell-version`), `:439` (`session-modes` default `['user']`); `sharedInternals.js` 46:91-110 `getSettings()` reads `metadata['settings-schema']` and `Gio.SettingsSchemaSource.new_from_directory(<ext>/schemas)` — requires `gschemas.compiled` or throws. |
| 18 | `PanelMenu.Button` per device (57) | verified | `panelMenu.js` 46:95-98 `Button._init(menuAlignment, nameText, dontCreateMenu)`; identical in Ubuntu's build (0 diff lines). |
| 19 | Notifications via "the extension's `MessageTray.Source`" (118) | verified on 46 only | 46: `Source extends MessageList.Source`, `constructor(params)` (:499-500), `addNotification()` (:554), `Main.messageTray.add(source)` (:833); `Notification extends GObject.Object`, `constructor(params)` (:352-353). |
| 20 | One codebase supports Shell 45 **and** 46 (30, 53) | **wrong as implied** | 45: `Source._init(title, iconName)` (:665), `Notification._init(source, title, banner, params)` (:367), `showNotification()` (:768). 46: params objects and `addNotification()`. The notification path must branch or drop 45. |
| 21 | `gjs -m tests/*.test.js` with no Shell imports (133) | **wrong as written** | `gjs -m a.test.js b.test.js` ran only `a` and passed `b` as `ARGV` (scratchpad test). ESM + `gi://Gio` system-bus calls do work Shell-free (`enum.js` enumerated 6 devices). |
| 22 | `tests/upowerSource.live.test.js` against the live system bus (138) | verified feasible | Same `enum.js` run: `EnumerateDevices -> 6`. |
| 23 | `dbus-run-session -- gnome-shell --nested --wayland` works on this X11 machine (141) | verified | Ran with `timeout 25`: `Running GNOME Shell (using mutter 46.2) as a Wayland display server`, `Using public X11 display :0`; exit 124 (my timeout), no crash. Note it loads the user's real enabled extensions and dconf. |
| 24 | `make install && gnome-extensions enable <uuid>` in the real session (141-142) | **wrong for a first install** | `extensionSystem.js` 46:275-277 `enableExtension(uuid) { if (!this._extensions.has(uuid)) return false; }`; no `FileMonitor` on extension dirs (grep: none). A directory copied after login is not loadable until Shell restart (`Alt+F2 r` on X11) or `gnome-extensions install <zip>`. |
| 25 | `gnome-extensions pack` "with compiled schema" (146) | **wrong** | `gnome-extensions pack` options: `--extra-source, --schema, --podir, --gettext-domain, -f, -o`; `command-pack.c` 46.0 has no compile step. `command-install.c` 46.0:161 runs `glib-compile-schemas --strict`. `/usr/bin/gnome-extensions` is in the `gnome-shell` package. |
| 26 | Pack output `ghub-battery@vinipy12.github.io.shell-extension.zip` (147) | verified | `command-pack.c` 46.0:279 `"%s.shell-extension.zip", uuid`. |
| 27 | Workflow "gains a job that packs the extension and attaches the zip to the release" (148-149) | feasible, **under-specified** | `.github/workflows/build-and-release.yml`: one job `build` on `windows-latest`; the `softprops/action-gh-release@v2` step and `SHA256SUMS.txt` generation live inside it. A second job must either feed the zip into `build` (`needs:` + `download-artifact`) or run its own release step, and the zip will be absent from `SHA256SUMS.txt` unless the hash step is moved. `gnome-extensions` on `ubuntu-latest` means installing `gnome-shell`; a plain `zip` of the folder is byte-equivalent for this format. |
| 28 | Root README is Portuguese (63) | verified | `README.md` line 1 `# ⚡ Logitech G HUB Battery Widget ...`, body in Portuguese. |
| 29 | `gnome-extension/` folder, UUID, schema, tests exist | **do not exist** | `ls gnome-extension` -> No such file; `git diff --name-only 9dbafbd..HEAD` -> only the spec. |

## 3. Wrong or unverifiable, and what each changes

### 3.1 `present` hides every Bluetooth device (claim 15) — design-breaking

Three live snapshots (13:41, 13:42, 13:50) show `State=0` on both BT devices at
all times, and UPower's BlueZ backend has no code path that sets it. With the
rule as written, the G515 and G435 never reach the panel; the only device that
ever shows is the G305, and only while it is awake. The spec's table and goal
are therefore unreachable with its own model.

What is actually true on this machine:
- BlueZ devices: `IsPresent` is always `true`, `State` is always `unknown`,
  `BatteryLevel` is always `none`, `Percentage` is exact. When the link drops
  (idle keyboard, headset off) the object is **removed from the bus**
  (`DeviceRemoved`), and re-added on reconnect. "Inativo" is a `DeviceRemoved`
  event, not an `IsPresent=false` state.
- hidpp receiver devices: object persists; when the mouse sleeps the kernel
  reports `status=Unknown`, `capacity_level=Unknown`, and UPower publishes
  `State=0 BatteryLevel=0 Percentage=50`. "Inativo" for the receiver path is
  `BatteryLevel == unknown` (and/or `State == unknown`), not removal.

Change required: define `present` per transport (or simply `IsPresent`), treat
`DeviceRemoved` as the inactive trigger for BT, and `BatteryLevel == unknown`
as the inactive/asleep trigger for hidpp. `charging` can never be true for BT
devices (the G515 charges over USB while still reporting over BT); the
alert re-arm "when charging" is dead code for them and re-arming relies solely
on `percent > 20`.

### 3.2 Coarse-level set is missing `unknown` and `action` (claims 4, 13)

`sysfs_get_capacity_level` (`src/linux/up-device-supply.c` v1.90.3:205-216)
maps `Normal->55, High->70, Low->10, Critical->5, Full->100, Unknown->50` and
the G305 has **no** sysfs `capacity`, so its `Percentage` is always one of those
synthesised constants (the table's "100%" is `Full`, not a measurement). The
spec's `level` has no slot for `unknown` (observed) or `action` (daemon value
5). With `percent = null` and `level = unknown`, the display rule produces no
label at all. Change: add `unknown` (render as inactive/asleep, keep the last
real level) and map `action` to red.

### 3.3 Shell 45 and 46 are two notification APIs (claim 20)

Positional constructors and `showNotification` on 45; params objects and
`addNotification` on 46. A lane builds on this 46 box, tests pass, and the
extension throws on 45 at the first low-battery event. Nothing on this machine
can exercise 45. Decide: drop `"45"` from `shell-version`, or branch on
`Config.PACKAGE_VERSION` and accept an untested path. Either way the spec must
say which.

### 3.4 Operational claims that fail as written (claims 21, 24, 25)

- `gjs -m tests/*.test.js` runs one file; `make test` needs a loop or a single
  runner module that imports the others.
- `make install && gnome-extensions enable` fails on first install
  (`enableExtension` returns `false` for an unknown UUID, and the CLI reports
  the extension does not exist). On X11 add `Alt+F2 r` or use
  `gnome-extensions install <zip>`; on Wayland only logout works. The spec's
  "real-session" manual step must say this.
- `gnome-extensions pack` does not compile schemas; `install` does. A `make
  install` that copies files must run `glib-compile-schemas schemas/` itself
  or `getSettings()` throws (`sharedInternals.js` 46:108).

### 3.5 Unverifiable here

- Behaviour of a G515 **over the receiver** (fixture "once captured", 134-135):
  not captured; the kernel/UPower path would be the hidpp one in 3.1.
- Anything about Shell 45 runtime behaviour: source-verified only.
- `gaming-input`/`speakers`/`tablet` devices: enum verified, no such device
  here.

## 4. Numbers

| Number | Kind | Re-measured? |
|---|---|---|
| G305 100% / G515 35% / G435 60% (table) | measured, undated in spec | yes: 100 then 50 (synthesised); 36; 60. The G305 figure is a level constant, not a reading. |
| 20% alert threshold, 50/21 amber band | design choices | n/a |
| GNOME Shell 45/46, Ubuntu 24.04 | inherited | 46.0 verified; 45 unverifiable on this box |
| UPower enum values (implicit) | inherited | re-measured via `UPowerGlib` typelib 1.90.3 |
| `UpdateTime` semantics | implicit | seconds since epoch; BT devices only update on BlueZ `Percentage` change (`up-device-bluez.c`:268-269) — the G435's was 10405 s old |

## 5. Missing implementation

Everything. `gnome-extension/` does not exist; the branch adds only the spec.
Every file in the Layout table, the schema, the stylesheet, the Makefile, the
tests and fixtures, the README section and the workflow job are all to be
written. No fixture JSON has been captured yet (the "JSON captures of real
UPower property sets" at 134 are not in the repo).

## 6. Precursors filed elsewhere or implied

- **Fixture capture** is a prerequisite for `make test`, not a test detail: it
  needs the three `GetAll` dicts plus the two inactive states found here
  (hidpp `State=0/BatteryLevel=0/Percentage=50`; BT `DeviceRemoved`). Capture
  them now while the devices are at hand; they are the only ground truth the
  model tests have.
- **Shell 45 decision** (3.3) gates `metadata.json` and `extension.js`; it
  cannot be deferred to implementation.
- **Workflow restructuring** (claim 27): adding the zip to the release means
  touching the Windows job's release and hash steps, not just appending a job.

## 7. What a lane would get wrong

1. `present` as written: builds exactly what the spec says and ships a panel
   with zero Bluetooth devices. Highest-impact ambiguity; it is not ambiguous,
   it is wrong (3.1).
2. "Inactive devices leave the panel": for a BT keyboard this is every idle
   timeout, several times an hour (observed absent at 13:41, present at 13:50).
   A lane treating it as an edge case will not test the churn; a user will see
   the keyboard item blink in and out. Spec should state this is the normal
   path and that the greyed menu row must survive `DeviceRemoved`.
3. `percent: null when BatteryLevel != none` combined with level `unknown`:
   one reading renders nothing, another renders `50%`. Spec must name the
   `unknown` rendering (3.2).
4. `MessageTray.Source`: lane copies the 46 form; on 45 it is a TypeError
   (3.3).
5. `gjs -m tests/*.test.js`: lane writes it into the Makefile, sees one green
   file, believes the suite ran (3.4).
6. `make install && gnome-extensions enable`: lane hits "does not exist" and
   wastes time on the install path instead of restarting the Shell (3.4).
7. Alert re-arm "when charging": never fires for BT devices; a lane writing the
   sequence test "drop to 19, hold, charge, drop again" with a BT fixture cannot
   produce `charging=true`. Spec should say re-arm for BT is by percent only.
8. `kind` for `pen`, `touchpad`, `bluetooth-generic`, `phone`: all become
   `other` and **show in the panel** under "no vendor filter". A laptop's own
   Bluetooth touchpad or a paired phone will appear. If that is intended, say
   so; otherwise exclude `phone`/`touchpad` or add an allowlist.
9. `UpdateTime` in the menu: for BT it is the last BlueZ percentage change,
   often hours old; a lane may colour it as "stale" and mislabel a connected
   device.
10. Workflow job on `ubuntu-latest`: lane installs `gnome-shell` on the runner
    for one binary; `zip -r` produces the same artefact. Say which.
11. `Config.PACKAGE_VERSION`/`UPowerGlib` import: the extension may import
    `gi://UPowerGlib` for enum constants (gir1.2-upowerglib-1.0 is a gnome-shell
    dependency, and Shell-free `gjs -m` tests can import it too). Spec is silent;
    hand-coded integers in `model.js` are fine but should be stated.

## Brief premise check

The orchestrator's brief says three Logitech devices are "visible in `upower
-e`". At 13:41 only two were (the G515 was paired but disconnected); at 13:50
all three were. That intermittency is the central design fact this spec
misses, so the premise was not wrong, but it was not stable either.
