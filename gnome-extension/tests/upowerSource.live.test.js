// Manual: `make test-live`. Talks to the real system bus; exits 0 if at least
// one peripheral is enumerated within 5 seconds, 2 if UPower is absent.
import GLib from 'gi://GLib';
import System from 'system';
import {UPowerSource} from '../upowerSource.js';
import {isPeripheral} from '../model.js';

const loop = new GLib.MainLoop(null, false);
let code = 1;

const source = new UPowerSource(snapshot => {
    if (!snapshot.available) {
        print('UPower unavailable');
        code = 2;
        loop.quit();
        return;
    }
    const peripherals = [...snapshot.devices.entries()].filter(([, p]) => isPeripheral(p));
    print(`devices=${snapshot.devices.size} peripherals=${peripherals.length} removed=${snapshot.removed.length}`);
    for (const [path, p] of peripherals)
        print(`  ${path}: Type=${p.Type} Percentage=${p.Percentage} BatteryLevel=${p.BatteryLevel} State=${p.State}`);
    if (peripherals.length > 0) {
        code = 0;
        loop.quit();
    }
});

GLib.timeout_add_seconds(GLib.PRIORITY_DEFAULT, 5, () => { loop.quit(); return GLib.SOURCE_REMOVE; });
source.start();
loop.run();
source.stop();
System.exit(code);
