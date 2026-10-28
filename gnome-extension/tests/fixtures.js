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
