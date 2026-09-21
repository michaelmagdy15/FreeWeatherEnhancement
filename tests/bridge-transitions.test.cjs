const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../bridge/SkyWeaveWeatherBridge/html_ui/InGamePanels/SkyWeaveWeatherBridge/SkyWeaveWeatherBridge.js'), 'utf8');
let Bridge;
test('packaged transition method matches the tested source', () => {
    const method = text => text.slice(text.indexOf('    stepInterpolation() {'), text.indexOf('    renderShell() {')).replace(/\r\n/g, '\n');
    assert.equal(method(fs.readFileSync(path.join(__dirname, '../bridge/Packages/skyweave-weather-bridge-package/html_ui/InGamePanels/SkyWeaveWeatherBridge/SkyWeaveWeatherBridge.js'), 'utf8')), method(source));
});
vm.runInNewContext(source, {
    HTMLElement: class {}, customElements: { define: (_, type) => { Bridge = type; } },
    document: { querySelector: () => true }
});
const dv = value => ({ value });
const wind = (alt, degrees, speed) => ({ dvAltitude: dv(alt), dvAngleRad: dv(degrees * Math.PI / 180), dvSpeed: dv(speed), gustWaveData: { dvAngleRad: dv(degrees * Math.PI / 180), dvSpeedMultiplier: dv(1) } });
const cloud = (alt, coverage = 1) => ({ dvAltitudeBot: dv(alt), dvAltitudeTop: dv(alt + 1000), dvCoverageRatio: dv(coverage), dvDensityMultiplier: dv(coverage), dvCloudScatteringRatio: dv(0.5) });
function setup(current, target) {
    const b = new Bridge();
    b.currentPreset = current; b.targetPreset = target; b.smoothingFactor = 0.2;
    b.weatherListener = { updateTempWeatherPreset: () => {} };
    return b;
}
test('wind and gust cross north by the short arc in both directions', () => {
    for (const [from, to, expected] of [[350, 10, 354], [10, 350, 6]]) {
        const b = setup({ tWindLayers: [wind(0, from, 10)] }, { tWindLayers: [wind(0, to, 10)] });
        b.stepInterpolation();
        const result = b.currentPreset.tWindLayers[0];
        assert.ok(Math.abs(result.dvAngleRad.value * 180 / Math.PI - expected) < 1e-8);
        assert.ok(Math.abs(result.gustWaveData.dvAngleRad.value * 180 / Math.PI - expected) < 1e-8);
    }
});
test('additional wind layers start from the existing vertical profile', () => {
    const b = setup({ tWindLayers: [wind(0, 0, 10), wind(10000, 0, 30)] },
        { tWindLayers: [wind(0, 0, 10), wind(5000, 0, 40), wind(10000, 0, 30)] });
    b.stepInterpolation();
    assert.equal(b.currentPreset.tWindLayers.length, 3);
    assert.equal(b.currentPreset.tWindLayers[1].dvSpeed.value, 24);
});
test('obsolete wind layers disappear and target order is altitude order', () => {
    const b = setup({ tWindLayers: [wind(0, 0, 10), wind(5000, 0, 20), wind(10000, 0, 30)] },
        { tWindLayers: [wind(10000, 0, 30), wind(0, 0, 10)] });
    b.stepInterpolation();
    assert.deepEqual(Array.from(b.currentPreset.tWindLayers, w => w.dvAltitude.value), [0, 10000]);
});
test('new clouds fade in; removed clouds fade out then disappear', () => {
    const b = setup({ tCloudLayers: [] }, { tCloudLayers: [cloud(3000)] });
    b.stepInterpolation();
    assert.equal(b.currentPreset.tCloudLayers.length, 1);
    assert.equal(b.currentPreset.tCloudLayers[0].dvCoverageRatio.value, 0.2);
    b.targetPreset = { tCloudLayers: [] };
    b.stepInterpolation();
    assert.ok(b.currentPreset.tCloudLayers[0].dvCoverageRatio.value < 0.2);
    for (let i = 0; i < 60; i++) b.stepInterpolation();
    assert.equal(b.currentPreset.tCloudLayers.length, 0);
});
test('interpolation does not mutate the target preset', () => {
    const target = { tCloudLayers: [cloud(3000)], tWindLayers: [wind(0, 10, 20)] };
    const before = JSON.stringify(target);
    const b = setup({ tCloudLayers: [], tWindLayers: [] }, target);
    b.stepInterpolation();
    assert.equal(JSON.stringify(target), before);
});
test('inserting a low cloud preserves the existing high deck', () => {
    const b = setup({ tCloudLayers: [cloud(10000)] }, { tCloudLayers: [cloud(3000), cloud(10000)] });
    b.stepInterpolation();
    assert.equal(b.currentPreset.tCloudLayers[0].dvCoverageRatio.value, 0.2);
    assert.equal(b.currentPreset.tCloudLayers[1].dvAltitudeBot.value, 10000);
    assert.equal(b.currentPreset.tCloudLayers[1].dvCoverageRatio.value, 1);
});
test('reordered clouds retain altitude and the bridge caps clouds at 24', () => {
    const b = setup({ tCloudLayers: [cloud(10000), cloud(3000)] }, { tCloudLayers: [cloud(3000), cloud(10000)] });
    b.stepInterpolation();
    assert.deepEqual(Array.from(b.currentPreset.tCloudLayers, c => c.dvAltitudeBot.value), [3000, 10000]);
    b.targetPreset = { tCloudLayers: Array.from({ length: 30 }, (_, i) => cloud(i * 1000)) };
    b.stepInterpolation();
    assert.equal(b.currentPreset.tCloudLayers.length, 24);
});
test('structural changes are submitted even when numeric values already match', () => {
    const b = setup({ tWindLayers: [wind(0, 0, 10), wind(5000, 0, 10)] }, { tWindLayers: [wind(0, 0, 10)] });
    let calls = 0;
    b.weatherListener.updateTempWeatherPreset = () => { calls++; };
    b.stepInterpolation();
    assert.equal(calls, 1);
});
test('repeated steps converge to the full requested wind profile', () => {
    const b = setup({ tWindLayers: [wind(0, 350, 10)] }, { tWindLayers: [wind(0, 10, 20), wind(10000, 270, 50)] });
    for (let i = 0; i < 80; i++) b.stepInterpolation();
    assert.equal(b.currentPreset.tWindLayers.length, 2);
    assert.equal(b.currentPreset.tWindLayers[1].dvSpeed.value, 50);
    assert.equal(b.currentPreset.tWindLayers[1].dvAngleRad.value, 270 * Math.PI / 180);
});
