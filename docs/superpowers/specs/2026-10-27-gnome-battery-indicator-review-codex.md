# Adversarial design review: GNOME battery indicator

Reviewed 2026-10-27 on branch `feat/gnome-battery-indicator`, HEAD `3db830f7f4ecccceb6fbae1df751687f9e08f2f5` (`git branch --show-current`, `git rev-parse HEAD`). Scope: `2026-10-27-gnome-battery-indicator-design.md`, abbreviated **Spec** below. Line references refer to the version read with `nl -ba`. No implementation exists in the proposed folder in the inspected file listing; these are design findings, not claims about implemented defects.

**Disposition: revise before handing this spec to an implementation lane.** Two omissions can prevent loading the delivered extension. The notification, presence, source lifecycle, and test contracts also need correction.

**Verification boundary:** installed APIs and the G305's kernel data were inspectable. Live UPower D-Bus verification was blocked by the execution sandbox. `upower -e`, `upower -i /org/freedesktop/UPower/devices/mouse_hidpp_battery_12`, `busctl --system introspect org.freedesktop.UPower /org/freedesktop/UPower`, and `gdbus introspect --system --dest org.freedesktop.UPower --object-path /org/freedesktop/UPower` all failed with `Operation not permitted`. Explicit `EnumerateDevices` calls through both tools failed identically. This does **not** demonstrate that UPower is unavailable in the desktop session. The three live property sets, signal emission, disconnect behavior, and restart behavior remain unverified. No installation, daemon restart, device disconnect, extension enablement, packaging output, or nested desktop was attempted.

## Blocker findings

### B1. The metadata checklist omits a field required by the installed loader

**Evidence:** Spec line 53 lists `name`, `uuid`, `shell-version`, and `settings-schema`, omitting `description`. The installed Shell loader explicitly requires a nonempty string `description` and throws if it is missing. Verified with:

```sh
gresource extract /usr/lib/gnome-shell/libshell-14.so \
  /org/gnome/shell/ui/extensionSystem.js
```

In that resource, lines 401–427 validate `uuid`, `name`, `description`, and a nonempty string array `shell-version`; the installed directory name must also match the UUID. Lines 553–566 import the entry module and instantiate its default export.

**Impact:** an implementer treating the file-responsibility table as the complete metadata contract produces an extension that fails discovery/loading. This is conditional on following that incomplete checklist literally.

**Suggested spec fix:** supply a complete minimal metadata JSON example, including `description`, the exact schema ID, and the existing UUID. Specify a default-exported class extending `Extension`, using the ESM extension resource import. Distinguish repository folder `gnome-extension/` from installed folder `ghub-battery@vinipy12.github.io/`.

### B2. Plain `gnome-extensions pack` silently omits the proposed runtime modules

**Evidence:** Spec lines 55–58 introduce `upowerSource.js`, `model.js`, `panelItem.js`, and `menu.js`; lines 146–147 promise a working archive without prescribing extra sources. The installed manual explicitly says only the required entry files and recognized optional stylesheet/prefs files are included automatically; additional sources require `--extra-source`. Read using:

```sh
zcat /usr/share/man/man1/gnome-extensions.1.gz
gnome-extensions pack --help
```

The second command prints usage and the `--extra-source=FILE` option, although this subcommand rejects `--help` itself. The manual's `pack` section is the authoritative local evidence here.

**Impact:** a default pack can succeed while producing an archive whose relative imports fail at runtime. Source-tree testing would not catch that omission.

**Suggested spec fix:** prescribe explicit `--extra-source` arguments for all four runtime modules and any subsequently added runtime asset. Require archive-content inspection and an install/load smoke test from the resulting ZIP. Keep `tests/` out of the runtime bundle. Schemas in `schemas/` are discovered automatically; no custom recursive ZIP assumption is needed.

## Major findings

### M1. `State != unknown` is not a demonstrated peripheral-presence test

**Evidence:** Spec line 85 defines `present = IsPresent && State != unknown`. The installed `/usr/share/dbus-1/interfaces/org.freedesktop.UPower.Device.xml`, lines 630–678, describes physical battery presence separately from battery charge state and defines `State=0` as unknown. It does not establish unknown state as a universal disconnection signal; those property descriptions are themselves battery-centric and cannot establish Bluetooth semantics. The requested live reads failed as recorded above.

**Impact:** a device with a valid percentage but no reported charge/discharge state is excluded by construction. **Hypothesis, unverified on the live G515/G435:** this condition may hide one or both target Bluetooth devices. Conversely, a cached known state is not proof of an active connection.

**Suggested spec fix:** make activity a source-level policy based on validated backend behavior: object removal, explicit presence changes where meaningful, and source availability. Preserve unknown charge state independently from activity. Capture the actual Bluetooth `State` and `IsPresent` values before accepting a predicate. Add fixtures for present/unknown-state/valid-percentage, disconnected retained objects, and removed objects. Do not substitute UPower `Online` as a universal peripheral-connected flag without evidence either.

### M2. The stateless alert signature cannot preserve the promised suppression across unknown readings

**Evidence:** Spec lines 105–109 require a pure `(prev, next) -> boolean`, allow unknown previous readings to trigger, and promise no repeat without rearming. The normalized record at line 75 has no armed/latched state. Its literal OR-based rearm rule also treats every exact-percentage device with `level=null` as “no longer critical.” A read-only `gjs -m /dev/stdin` probe translating the specified numeric edge rule produced:

```text
21 -> 19: true
19 -> unknown: false
unknown -> 19: true
literal OR rearm for numeric 19: true
```

These are a counterexample to the written contract, not results from an implementation.

**Impact:** a missing reading can cause a second alert without charging or recovery. A literal combined numeric/coarse rearm condition can continuously rearm numeric devices. Spec line 107 also leaves the charging guard's scope ambiguous for the coarse-critical branch. Lines 117–118 do not explicitly suppress alerts for retained inactive records.

**Suggested spec fix:** define a pure state transition such as `(alertState, reading) -> {alertState, fire}`, with state retained per device outside the normalized reading. Specify separate numeric/coarse recovery rules and require active, valid, noncharging readings for either trigger. Unknown readings must preserve the latch. Define startup-low behavior, numeric/coarse transitions, reconnect/restart behavior, and whether disabling notifications consumes crossings. Test `21,19,unknown,19`, repeated critical readings, critical while charging, inactive-low, and toggle-off/toggle-on sequences.

### M3. GNOME 45 and 46 require different notification construction paths

**Evidence:** Spec claims both versions at lines 30 and 53, but lines 54 and 117–118 only name `MessageTray.Source`. The retrieved upstream [GNOME 45 branch source](https://raw.githubusercontent.com/GNOME/gnome-shell/gnome-45/js/ui/messageTray.js) has `Source._init(title, iconName)` at lines 629–639, `Notification._init(source, title, banner, params)` at lines 347–368, and `source.showNotification(notification)` at lines 726–733. This is branch-source verification, not a local GNOME 45 runtime test.

The installed GNOME 46 resource, extracted with:

```sh
gresource extract /usr/lib/gnome-shell/libshell-14.so \
  /org/gnome/shell/ui/messageTray.js
```

uses property-object constructors for `Source` and `Notification` and exposes `source.addNotification(notification)` (lines 330–354, 504–512, 559–580). Current guide examples also use the newer API; they should not be copied unconditionally into the claimed 45 implementation.

**Impact:** implementing only one generation's constructor/method combination breaks the other advertised version.

**Suggested spec fix:** name a notification adapter with explicit 45/46 branches or documented feature detection. For 45 use positional constructors and `showNotification`; for 46 use property dictionaries and `addNotification`. Register the source with `Main.messageTray.add(source)`. Require smoke coverage on both advertised versions, or restrict metadata/support to the verified target version. Pure model tests cannot prove this compatibility.

### M4. A notification source created only in `enable()` will not necessarily survive until `disable()`

**Evidence:** Spec line 54 creates the notification source at enable time. Installed `messageTray.js` lines 546–556 destroy a source when its last notification is destroyed; lines 583–594 destroy its policy and dispose it. The 45 source cited in M3 has equivalent empty-source destruction at lines 701–709. The [notification guide](https://gjs.guide/extensions/topics/notifications.html) explicitly requires handling source destruction to reuse notifications safely.

**Impact:** after the user dismisses the first low-battery notification, the extension can retain a disposed source and fail on a later legitimate crossing.

**Suggested spec fix:** obtain/create the source lazily for each notification, clear its reference on `destroy`, and recreate/register it when necessary. `disable()` destroys only a currently live source. Add a manual test: alert, dismiss the last notification, recover/rearm, alert again.

### M5. Source initialization, invalidation, and asynchronous teardown lack a complete consistency contract

**Evidence:** Spec lines 55 and 113 describe enumeration followed by event handling; line 125 only specifies merging partial payloads. Neither defines initial `GetAll`, subscription ordering, invalidated properties, in-flight calls, or stale replies after restart/disable. Installed root XML lines 39–43 returns paths (`ao`) from `EnumerateDevices`, not property maps. The [D-Bus Properties specification](https://dbus.freedesktop.org/doc/dbus-specification.html#standard-interfaces-properties) defines a third `PropertiesChanged` argument containing invalidated property names, whose new values are absent from the payload.

**Impact, design inference:** an implementer could miss a device event during startup, retain an invalidated percentage indefinitely, resurrect a removed device from a late reply, or update actors after disable. Naming owner-change recovery alone does not prevent old-owner replies overwriting new state.

**Suggested spec fix:** require asynchronous system-bus calls; subscribe before enumeration; obtain each device's properties through `org.freedesktop.DBus.Properties.GetAll('org.freedesktop.UPower.Device')`; filter property signals by interface and path; unpack variants at the source boundary. Merge changed fields, invalidate/refetch invalidated fields, and preserve unknown values while refetching. Define source generations/cancellation so old-owner, removed-device, and disabled-source callbacks cannot publish. On owner loss mark data unavailable, detach old subscriptions, and re-enumerate on recovery. Add deterministic source tests for these interleavings. These races were not reproduced against the blocked live bus.

### M6. Sharing a `PopupMenu` among independently destroyed `PanelMenu.Button`s needs an ownership design

**Evidence:** Spec line 57 says every button opens “the shared menu.” Installed `panelMenu.js`, read via `gresource extract /usr/lib/gnome-shell/libshell-14.so /org/gnome/shell/ui/panelMenu.js`, creates a menu per button at lines 98–110; `setMenu()` destroys the old menu at lines 122–135; button destruction destroys its menu at lines 190–193. Installed `popupMenu.js` lines 543–550 and 871–895 attach the menu to a source/focus actor. Simply assigning the same menu to several buttons is therefore not a neutral operation.

Spec lines 40, 102–103, and 115 destroy all panel items when every device becomes inactive; only service absence has a placeholder (lines 123–124). There is no specified click target for viewing the retained inactive rows or preferences when UPower is healthy but no peripheral is active.

**Impact, design inference:** naive sharing lets removal of one device destroy the menu used by surviving devices, and all-inactive state can make the promised dropdown unreachable.

**Suggested spec fix:** choose either independent menu instances backed by a shared data model, or one explicitly owned popup with correct anchor/focus/event handling. Specify the owner, who destroys it, and behavior when its anchor disappears. Keep an accessible fallback button for empty/all-inactive state, and define how `show-inactive` affects its rows. Test removal of the menu's current anchor while open.

### M7. The proposed test command runs one script, and the live test is unsuitable as an unconditional CI gate

**Evidence:** Spec line 133 says `gjs -m tests/*.test.js`. Installed `gjs --help` defines one file to execute as a module. Executing `gjs -m /dev/stdin second.test.js` with a small ESM assertion program printed `remaining arguments: ["second.test.js"]`: subsequent names are arguments, not additional programs. Thus shell glob expansion does not implement a test runner.

Spec lines 138–139 require at least one peripheral and skip only if UPower is absent. The workflow at `.github/workflows/build-and-release.yml:17` runs on a Windows runner; a proposed separate Linux job is not given any guaranteed physical peripheral fixture. In this review, access denial also demonstrated a failure category distinct from daemon absence.

**Impact:** tests after the first file can be silently unexecuted. **Hypothesis about a future hosted Linux runner:** UPower may be installed/running with zero peripherals, causing a false failure. A broad skip could instead conceal implementation errors.

**Suggested spec fix:** run every offline test explicitly, e.g. a fail-fast shell loop invoking `gjs -m "$test"` once per file, and keep `test-live` separate/opt-in. Use deterministic fixtures or a fake source for CI; distinguish absent daemon, denied bus, zero devices, and API errors. On the reference machine, live acceptance should identify expected devices rather than merely assert a nonempty list. Test packaging contents separately. The G515 receiver capture at lines 134–135 must be an explicit pending hardware fixture, not an invented capture or a required unattended action.

### M8. Excluding every `Type=battery` also excludes legitimate peripheral batteries

**Evidence:** Spec lines 87–91 exclude `Type=battery` even when `PowerSupply=false`. Installed Device XML lines 399–403 explicitly explains that `Type=Battery` with `PowerSupply=false` is likely an unknown-type device battery. Lines 409–415 define `PowerSupply` as supplying the computer. The goal at lines 28–29 is each battery-reporting peripheral.

**Impact:** the stated filter intentionally loses a documented peripheral representation. This is a coverage mismatch, not evidence that any of the three named devices has this type.

**Suggested spec fix:** include `Type=2 && PowerSupply===false` as `other`, or explicitly narrow the goal and document this exclusion. Supply numeric enum constants and tests; fail closed while `PowerSupply`/`Type` are unavailable rather than treating missing values as false. Specify how unsupported future enum values are handled.

### M9. Adding an isolated Linux packaging job does not define how its ZIP reaches the existing release and checksum manifest

**Evidence:** `.github/workflows/build-and-release.yml` has one `build` job on `windows-latest` (lines 15–17), computes a Windows-local `SHA256SUMS.txt` (54–81), uploads a fixed artifact list (83–94), and releases a fixed local file list only on `v*` tags (96–108). There is no existing artifact download, cross-job dependency, or aggregate release job. Spec lines 148–150 say only to add a packing job and attach the ZIP. Root `README.md:162–175` promises hashes for release artifacts.

**Impact, design inference:** a new job can upload a workflow artifact without attaching it to the release; the extension hash can be missing. Independently publishing another `SHA256SUMS.txt` under the same asset name can conflict with the existing manifest.

**Suggested spec fix:** define a Linux test/pack job with explicit build tools, a ZIP-content gate, and a uniquely named uploaded artifact. A single release job should depend on both build jobs, download their outputs, aggregate checksums including the extension ZIP, and publish once under the existing tag condition. Retain PR/main build validation, keep hardware tests opt-in, and give write permissions only where release publication needs them. An alternate explicit serialized asset-upload design is possible, but must resolve manifest ownership. No hosted workflow was run in this review.

## Minor findings

### N1. “UPower exposes no vendor field” confuses an absent property with an empty value

**Evidence:** Spec line 24 makes this claim. Installed Device XML lines 263–270 declares `Vendor` as a read-only string. The G305 kernel data has `POWER_SUPPLY_MANUFACTURER=Logitech` (`cat /sys/class/power_supply/hidpp_battery_12/uevent`). Actual UPower `Vendor` values remain unverified because the bus was inaccessible.

**Suggested spec fix:** say “do not rely on Vendor being populated” and retain the intentional no-vendor-filter policy. If claiming the three values are empty, include actual `GetAll` captures. Kernel manufacturer is not proof of the exported UPower value.

### N2. Unknown/coarse data, names, timestamps, and fractional percentages have incomplete display rules

**Evidence:** Spec lines 78–83 and 95–99 do not define a label when both `percent` and `level` are null, or both Model and Serial are empty. Device XML lines 133–134 allows unset values; lines 293–298 defines `UpdateTime` as seconds since the Epoch. `Percentage` has D-Bus type `d` (line 591), so fractions are possible. The stated intervals leave `20 < p < 21` unspecified. `BatteryLevel=0` means unknown, while `NONE=1`, not zero (XML lines 795–814 and local enum probe below).

**Suggested spec fix:** give an explicit unknown label and stable fallback name; use no-data states without turning missing percentage into zero. Accept only finite valid percentages, define rounding independently of alert/color comparison, and use complete intervals (`p<=20`, `20<p<=50`, `p>50`). Treat unknown BatteryLevel separately from NONE; unknown coarse readings must not become fabricated exact readings. Convert timestamp seconds explicitly and define zero/unknown and stale last-seen display behavior. Spec wording “below 20%” should agree with its actual `<=20` rule.

### N3. Fully charged is folded into “charging,” making the bolt and recovery semantics misleading

**Evidence:** Spec line 84 treats `charging` and `fully-charged` identically; lines 96, 99, and 108 use that boolean for the bolt, color override, and rearming. Installed Device XML lines 654–672 assigns distinct states: charging 1, discharging 2, empty 3, fully charged 4, pending charge 5, pending discharge 6.

**Suggested spec fix:** retain raw state or an explicit normalized state; reserve “charging”/bolt for the intended states and describe fully charged separately. Decide pending-state behavior. If fully charged is intentionally a recovery condition, state that independently of whether to show charging UI. No incorrect live state was observed; this finding is about the specified semantics.

### N4. The palette-variable instruction names no verified GNOME 45/46 CSS mechanism

**Evidence:** Spec line 60 asks for “GNOME palette variables” without names or syntax. The inspected installed `/usr/share/gnome-shell/theme/Yaru/gnome-shell.css` uses concrete values, including `warning-color: #f9990c` at line 738. `rg -n '(var\(|warning_color|error_color|warning-color|error-color)'` found that property but no `var(...)` references in that file.

**Impact:** browser-style CSS variables or theme-build-time names may be copied into runtime Shell CSS. **Hypothesis:** such a choice may fail on the target; this review did not exercise the St CSS parser or rendering, and absence from one theme does not prove universal lack of support.

**Suggested spec fix:** name the actual runtime-supported syntax/tokens, or use explicit extension colors verified for contrast on the target light/dark themes. Add a visual acceptance check rather than assuming palette availability.

### N5. Sorting the model does not specify ordering the actual panel actors

**Evidence:** Spec lines 100–101 require kind/name ordering, while lines 114–116 describe creating new items and updating existing items in place. Installed `panel.js` lines 935–950 defaults `addToStatusArea` position to zero and requires a unique role. Merely adding every newly discovered device at the default position can disagree with normalized sort order.

**Suggested spec fix:** define the panel box, unique role key per device, explicit actor positions/reordering after add/remove/rename, and an ID tie-breaker for equal names. Clarify that stable order means deterministic sorting, including when names change. Exercise reversed enumeration and hotplug arrival order.

### N6. The real-session install recipe omits extension discovery/reload

**Evidence:** Spec lines 140–142 immediately enable the extension after install. The installed `gnome-extensions(1)` manual's `install` section says the files are installed for loading in the next session. Installed `extensionSystem.js` lines 553–557 also records that imported extension modules cannot simply be re-imported. `gnome-shell --help` confirms `--nested` and `--wayland` flags, but does not prove a working nested session in this sandbox.

**Suggested spec fix:** document a fresh-session/discovery step after first installation and a reload/relogin procedure for updated code. For the reference X11 desktop, describe the supported Shell restart or logout/login route; distinguish that from a Wayland session. State where the ZIP is installed and how to inspect load errors. The nested command is plausible but remains unexecuted, and must not be treated as completed acceptance evidence.

## Verified-correct claims and concrete API facts

### Reference environment

The following commands were run:

```sh
cat /etc/os-release
gnome-shell --version
gjs --version
printf '%s\n' "$XDG_SESSION_TYPE"
dpkg-query -W gnome-shell gnome-shell-common gjs upower libupower-glib3
```

They confirm Ubuntu **24.04.5 LTS**, Shell **46.0** (`46.0-0ubuntu6~24.04.15`), GJS **1.80.2**, X11, and installed UPower/libupower-glib **1.90.3-1**. The daemon's live `DaemonVersion` was not readable; installed package version is not a live daemon response.

### UPower interface, property, and enum contract

Read `/usr/share/dbus-1/interfaces/org.freedesktop.UPower.xml` and `org.freedesktop.UPower.Device.xml` with `nl -ba`. The installed definitions support the basic D-Bus architecture in Spec lines 44 and 55:

| Contract | Verified installed definition |
|---|---|
| Bus and service | System bus; `org.freedesktop.UPower` |
| Root path/interface | `/org/freedesktop/UPower`; `org.freedesktop.UPower` |
| Enumeration | `EnumerateDevices() -> ao` |
| Root hotplug signals | `DeviceAdded(o)`, `DeviceRemoved(o)` |
| Device interface | `org.freedesktop.UPower.Device` |
| Strings | `Model`, `Serial`, `Vendor`: `s`, read-only |
| Enums | `Type`, `State`, `BatteryLevel`: `u`, read-only |
| Percentage | `Percentage`: `d`, read-only |
| Presence/system supply | `IsPresent`, `PowerSupply`: `b`, read-only |
| Measurement time | `UpdateTime`: `t`, read-only, Unix seconds |

The standard property signal belongs to `org.freedesktop.DBus.Properties`, **not** the device interface. Its signature is `(s, a{sv}, as)` for interface name, changed values, and invalidated names; `GetAll` takes the device interface name. This was checked against the [D-Bus specification](https://dbus.freedesktop.org/doc/dbus-specification.html#standard-interfaces-properties); a local Gio XML-parsing probe also accepted that signature, though that probe was synthetic and did not introspect a live service. CamelCase names above are the installed property declarations; the older hyphenated example embedded near the top of the Device XML should not be copied as the current wire contract.

Numeric enums were independently verified without connecting to UPower:

```sh
gjs -c 'const UPower = imports.gi.UPowerGlib; for (const k of ["DeviceKind", "DeviceState", "DeviceLevel"]) print(k + "=" + JSON.stringify(UPower[k]));'
```

| Type values | Meaning |
|---|---|
| 0, 1, 2, 3, 4 | unknown, line-power, battery, UPS, monitor |
| 5, 6, 7, 8, 9, 10, 11, 12 | mouse, keyboard, PDA, phone, media-player, tablet, computer, gaming-input |
| 13, 14, 15, 16, 17, 18, 19 | pen, touchpad, modem, network, headset, speakers, headphones |
| 20, 21, 22, 23, 24, 25, 26, 27, 28 | video, other-audio, remote-control, printer, scanner, camera, wearable, toy, Bluetooth-generic |

`DeviceState`: unknown **0**, charging **1**, discharging **2**, empty **3**, fully-charged **4**, pending-charge **5**, pending-discharge **6**.

The documented coarse `BatteryLevel` values are unknown **0**, none **1**, low **3**, critical **4**, normal **6**, high **7**, full **8**. The shared GI level enum additionally contains discharging **2** and action **5**; those are not listed as ordinary BatteryLevel values by the installed Device XML. Do not confuse `BatteryLevel` with `WarningLevel` just because both use related numeric constants. The current target kind mappings (mouse **5**, keyboard **6**, headset **17**, gaming-input **12**) are supported.

These are installed API facts. Live property availability and actual signal delivery were not verified. In particular, no live G515/G435 percentage was reconfirmed in this review.

### G305 coarse percentage: correct principle, partially verified derivation

Read-only commands:

```sh
ls -l /sys/class/power_supply
cat /sys/class/power_supply/hidpp_battery_12/uevent
```

The symlink path includes receiver `046D:C53F`. Its data reports model `G305 Lightspeed Wireless Gaming Mouse`, manufacturer `Logitech`, `STATUS=Discharging`, `ONLINE=1`, `CAPACITY_LEVEL=Full`, and `SCOPE=Device`. The inspected attribute listing has `capacity_level` but no `capacity` or `present` attribute. This supports the kernel-side coarse-reading premise on this machine.

Installed Device XML lines 591–612 and 787–814 establish that a non-NONE BatteryLevel makes Percentage approximate, retained for compatibility, and that NONE is **1**. Thus the spec is correct to suppress the numeric percentage in favor of a known coarse level. `IsPresent` describes presence; it is not the percentage-precision flag. There is no separate “should be ignored” boolean among the installed device property declarations.

**Unverified hypothesis for the exact live chain:** the observed kernel `Full` is exported as `BatteryLevel=8`, with a compatibility `Percentage=100`, and `upower` renders the ignored-percentage annotation from the non-NONE level. This matches the supplied capture and installed interface semantics, but the daemon mapping and CLI formatting implementation were not directly verified here. No claim is made that the sysfs read independently proves those exact live D-Bus values. Capturing `GetAll` through an unrestricted desktop terminal remains necessary to close that gap.

### ESM, panel buttons, preferences metadata, and model-only execution are viable

The [GNOME 45 upgrade guide](https://gjs.guide/extensions/upgrading/gnome-shell-45.html), retrieved through Context7 and the web tool, establishes ESM imports for GI libraries, Shell resources, and local modules. The [GNOME 46 upgrade guide](https://gjs.guide/extensions/upgrading/gnome-shell-46.html) reports no relevant entry-point or metadata changes. This supports the overall ESM direction, subject to the notification differences above.

Installed `panelMenu.js` exports `Button`; installed `panel.js:935–950` accepts a `PanelMenu.Button` in `Main.panel.addToStatusArea`. One button per device is therefore supported in principle; actual layout, shared-menu ownership, and rendering were not exercised.

`shell-version: ["45", "46"]` uses the documented major-version string format. `settings-schema` is an appropriate metadata field for `Extension.getSettings()`/preferences, as documented in the [extension anatomy](https://gjs.guide/extensions/overview/anatomy.html) and [preferences guide](https://gjs.guide/extensions/development/preferences.html), retrieved through Context7. Runtime correctness still depends on the schema ID matching and a compiled schema being available. The installed pack manual documents automatic discovery of `schemas/` and schema compilation; no archive was created to test that behavior.

This exact model-style probe passed locally without Shell imports or a desktop connection:

```sh
gjs -m /dev/stdin <<'JS'
import GLib from 'gi://GLib';
export function band(p) {
    return p <= 20 ? 'red' : p <= 50 ? 'amber' : 'normal';
}
for (const [p, expected] of [[19, 'red'], [21, 'amber'], [51, 'normal']]) {
    if (band(p) !== expected) throw Error('assertion failed');
}
print('Pure ESM model-style assertions: PASS');
print('GNOME Shell global available: ' + ('global' in globalThis));
JS
```

Output: `Pure ESM model-style assertions: PASS`, `GNOME Shell global available: false`. This establishes feasibility of the proposed Shell-free model tests, not passing tests for a nonexistent `model.js`. Ordinary on-disk fixture/module loading was not exercised because creating test fixtures was outside the allowed write scope. An exploratory dynamic `data:` module import crashed this GJS build; that unsupported probe is not evidence against normal file-based ESM tests and is not counted as a spec defect.

### Workflow fit and remaining acceptance evidence

Reading the full `.github/workflows/build-and-release.yml` confirms that a separate Linux job is structurally appropriate beside the existing Windows build. Existing triggers cover pushes to main, `v*` tags, PRs targeting main, and workflow dispatch; the present release step is tag-gated. A feature-branch push alone does not match the listed push branches unless it carries a matching tag. The integration and checksum ownership need the explicit contract in M9.

Before claiming hardware acceptance, the evidence still needed is: live introspection/GetAll for all three target devices; observed PropertiesChanged delivery; physical disconnect/reconnect behavior including retained versus removed objects; runtime load from the ZIP; notification dismissal followed by another crossing; enable/disable during initialization; empty/all-inactive menu access; and real 45/46 Shell smoke tests for every advertised version. These are suggested verification gates derived from the findings, not tests reported as completed.
