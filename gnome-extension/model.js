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
