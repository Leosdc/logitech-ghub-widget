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
