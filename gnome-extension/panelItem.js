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
