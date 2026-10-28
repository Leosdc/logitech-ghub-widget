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
    constructor(onChanged, bus = Gio.DBus.system) {
        this._onChanged = onChanged;
        this._bus = bus;
        this._devices = new Map();
        this._generation = 0;
        // Per-path staleness tokens: a GetAll reply is applied only if no newer
        // event (removal, merge, newer fetch) touched the path since it was sent.
        this._tokens = new Map();
        this._seq = 0;
        this._enumerating = false;
        this._available = false;
        this._subs = [];
        this._watchId = 0;
    }

    start() {
        if (this._watchId)
            return;
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
        this._tokens.clear();
        this._enumerating = false;
        this._available = false;
    }

    _bump(path) {
        // Monotonic across all paths so a removed-then-re-added path can never
        // reuse a token an older in-flight fetch still holds.
        const token = ++this._seq;
        this._tokens.set(path, token);
        return token;
    }

    _emit(removed = []) {
        if (this._enumerating)
            return;
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
        this._tokens.clear();
        this._available = true;
        this._enumerating = true;

        // Subscribe BEFORE enumerating so nothing is missed.
        this._subscribe('DeviceAdded', ROOT_PATH, (_p, params) => {
            const [path] = params.deepUnpack();
            this._fetchDevice(path, gen);
        });
        this._subscribe('DeviceRemoved', ROOT_PATH, (_p, params) => {
            const [path] = params.deepUnpack();
            this._tokens.delete(path);
            if (this._devices.delete(path))
                this._emit([path]);
        });
        this._subscribe('PropertiesChanged', null, (path, params) => {
            const [, changed, invalidated] = params.deepUnpack();
            const current = this._devices.get(path);
            if (!current) {
                // First GetAll may be in flight: supersede it with a fresh one.
                if (this._tokens.has(path))
                    this._fetchDevice(path, gen);
                return;
            }
            this._bump(path);
            const next = {...current};
            for (const [k, v] of Object.entries(changed))
                next[k] = v.recursiveUnpack();
            this._devices.set(path, next);
            if (invalidated.length > 0)
                this._fetchDevice(path, gen);
            else
                this._emit();
        });

        let ok = false;
        try {
            const reply = await this._bus.call(
                BUS_NAME, ROOT_PATH, ROOT_IFACE, 'EnumerateDevices',
                null, null, Gio.DBusCallFlags.NONE, -1, null);
            if (gen !== this._generation)
                return;
            const [paths] = reply.deepUnpack();
            await Promise.all(paths.map(p => this._fetchDevice(p, gen, false)));
            ok = gen === this._generation;
        } catch (e) {
            if (gen === this._generation)
                console.warn(`ghub-battery: EnumerateDevices failed: ${e.message}`);
        }
        if (gen !== this._generation)
            return;
        this._enumerating = false;
        if (ok)
            this._emit();
    }

    _onOwnerVanished() {
        this._generation++;
        this._unsubscribe();
        this._devices.clear();
        this._tokens.clear();
        this._enumerating = false;
        this._available = false;
        this._emit();
    }

    async _fetchDevice(path, gen, emit = true) {
        const token = this._bump(path);
        try {
            const reply = await this._bus.call(
                BUS_NAME, path, PROPS_IFACE, 'GetAll',
                new GLib.Variant('(s)', [DEVICE_IFACE]),
                null, Gio.DBusCallFlags.NONE, -1, null);
            if (gen !== this._generation || this._tokens.get(path) !== token)
                return;
            const [props] = reply.recursiveUnpack();
            this._devices.set(path, props);
        } catch (e) {
            if (gen === this._generation)
                console.warn(`ghub-battery: GetAll ${path} failed: ${e.message}`);
            return;
        }
        if (emit)
            this._emit();
    }
}
