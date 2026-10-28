// Minimal test registry for `gjs -m`. No Shell imports.
const registry = [];

export function test(name, fn) {
    registry.push({name, fn});
}

export function assertTrue(cond, msg = 'expected truthy') {
    if (!cond)
        throw new Error(msg);
}

export function assertEqual(actual, expected, msg = '') {
    if (actual !== expected)
        throw new Error(`${msg} expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`);
}

export function assertDeepEqual(actual, expected, msg = '') {
    const a = JSON.stringify(actual);
    const e = JSON.stringify(expected);
    if (a !== e)
        throw new Error(`${msg} expected ${e}, got ${a}`);
}

export function runAll() {
    let failures = 0;
    for (const {name, fn} of registry) {
        try {
            fn();
            print(`ok   ${name}`);
        } catch (e) {
            failures++;
            print(`FAIL ${name}: ${e.message}`);
        }
    }
    print(`${registry.length - failures}/${registry.length} passed`);
    return failures;
}
