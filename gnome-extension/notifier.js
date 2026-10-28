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
