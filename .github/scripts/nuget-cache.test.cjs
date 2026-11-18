const assert = require('node:assert/strict');
const fs = require('node:fs');
const test = require('node:test');

test('desktop packaging reads the single feed prepared and saved by the NuGet job', () => {
    const workflow = fs.readFileSync('.github/workflows/build.yml', 'utf8');
    const [producer, rest] = workflow.split(/^  prepare-nuget:\r?$/m)[1].split(/^  package:\r?$/m);
    const consumers = rest.split(/^  publish:\r?$/m)[0];
    assert.match(producer, /uses: actions\/cache\/save@v6/);
    assert.match(producer, /github\.ref == 'refs\/heads\/main'/);
    assert.match(producer, /\.github\/nuget\/packages\.lock\.json/);
    assert.match(consumers, /needs: prepare-nuget/);
    assert.match(consumers, /key: \$\{\{ needs\.prepare-nuget\.outputs\.cache-key \}\}/);
    assert.match(consumers, /enableCrossOsArchive: true/);
    assert.match(consumers, /Prepare-NuGetCache\.ps1 -Directory \.nuget\/feed -VerifyOnly/);
    assert.doesNotMatch(consumers, /path: .*NUGET_PACKAGES|Prepare-NuGetCache\.ps1 -Directory \.nuget\/feed\r?\n/);
});

test('the shared dependency lock has one version and archive hash per package ID', () => {
    const sdk = require('../../global.json').sdk;
    const lock = require('../nuget/packages.lock.json');
    assert.equal(sdk.rollForward, 'disable');
    assert.equal(lock.sdkVersion, sdk.version);
    const ids = lock.packages.map(packageEntry => packageEntry.id);
    assert.deepEqual(ids, [...new Set(ids)].sort());
    for (const { id, version, sha512 } of lock.packages) {
        assert.match(id, /^[a-z0-9][a-z0-9._-]*$/);
        assert.match(version, /^[0-9][a-z0-9.-]*$/);
        assert.match(sha512, /^[a-f0-9]{128}$/);
    }
    for (const rid of ['win-x64', 'linux-x64', 'osx-x64', 'osx-arm64']) {
        assert.ok(ids.includes(`microsoft.netcore.app.runtime.${rid}`));
        assert.ok(ids.includes(`microsoft.netcore.app.host.${rid}`));
    }
    assert.ok(ids.includes('dotnet-mgfxc'));
});

test('the shared dependency lock covers every centrally pinned package version', () => {
    const lock = require('../nuget/packages.lock.json');
    const lockedVersions = new Map(lock.packages.map(({ id, version }) => [id, version]));
    for (const file of ['Directory.Packages.props', 'Directory.TransitivePackages.props']) {
        const contents = fs.readFileSync(file, 'utf8');
        const pins = [...contents.matchAll(/<PackageVersion\s+Include="([^"]+)"\s+Version="\[([^\]]+)\]"\s*\/>/g)];
        assert.ok(pins.length > 0, `No central package versions found in ${file}`);
        for (const [, id, version] of pins) {
            assert.equal(lockedVersions.get(id.toLowerCase()), version, `${id} ${version} from ${file} is missing from the shared dependency lock`);
        }
    }
});
