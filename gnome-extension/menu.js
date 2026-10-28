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
