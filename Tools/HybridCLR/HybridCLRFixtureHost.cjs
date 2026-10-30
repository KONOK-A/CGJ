'use strict';
const fs = require('node:fs');
const path = require('node:path');
const https = require('node:https');
const crypto = require('node:crypto');
const [rootArgument, publication, suffix, expectedOwner] = process.argv.slice(2);
if (!rootArgument || !['ServerV2', 'ServerBad', 'ServerMissing', 'ServerBadSignature'].includes(publication) ||
    !/^[A-Za-z0-9-]+$/.test(suffix || '') || !/^[a-f0-9]{64}$/.test(expectedOwner || '')) throw new Error('Exact owned fixture arguments required.');
const root = path.resolve(rootArgument);
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
function noLinks(file) {
  for (let current = path.resolve(file); ; current = path.dirname(current)) {
    if (fs.existsSync(current) && fs.lstatSync(current).isSymbolicLink()) throw new Error('Linked fixture input refused.');
    if (path.dirname(current) === current) break;
  }
}
function read(file) { noLinks(file); return fs.readFileSync(file); }
const ownerBytes = read(path.join(root, 'owner.json'));
const owner = JSON.parse(ownerBytes);
if (hash(ownerBytes) !== expectedOwner || owner.schemaVersion !== 2 || owner.simulation !== false ||
    owner.purpose !== 'YLFramework-HybridCLR-9.0.3-independent-reception' || owner.evidenceRoot !== root) throw new Error('Wrong live fixture owner.');
const ready = path.join(root, `${publication}-${suffix}-ready.json`);
const binding = JSON.parse(read(ready + '.owner.json'));
const credentials = JSON.parse(read(path.join(root, 'TestCredentials', 'owner.json')));
for (const value of [binding, credentials]) {
  if (value.SchemaVersion !== 1 || value.Purpose !== 'YLFramework-HybridCLR-owned-loopback-fixture-v1' ||
      value.Root !== root || value.OwnerSha256 !== expectedOwner || value.Receiver !== owner.receiver ||
      value.UnityVersion !== owner.expectedUnityVersion || value.UnityRevision !== owner.expectedUnityRevision) throw new Error('Foreign credential custody.');
}
const pfx = read(path.join(root, 'TestCredentials', 'local-only.pfx'));
const certificateBytes = Buffer.from(binding.Certificate, 'base64');
if (hash(pfx) !== credentials.PfxSha256 || binding.PfxSha256 !== credentials.PfxSha256 ||
    hash(certificateBytes) !== credentials.CertificateSha256 || binding.CertificateSha256 !== credentials.CertificateSha256 ||
    binding.Publication !== publication || binding.ReadyPath !== ready) throw new Error('Fixture key/certificate bytes changed.');
const certificate = new crypto.X509Certificate(certificateBytes);
if (Date.parse(certificate.validFrom) > Date.now() || Date.parse(certificate.validTo) <= Date.now()) throw new Error('Expired fixture certificate.');
const directory = path.join(root, publication);
if (hash(read(path.join(directory, 'release.json'))) !== binding.EnvelopeSha256) throw new Error('Signed envelope changed.');
const files = new Map();
const pending = [directory];
while (pending.length) {
  const current = pending.pop(); noLinks(current);
  for (const name of fs.readdirSync(current)) {
    const file = path.join(current, name); noLinks(file);
    if (fs.statSync(file).isDirectory()) pending.push(file);
    else files.set('/' + path.relative(directory, file).replaceAll(path.sep, '/'), read(file));
  }
}
for (const file of [ready, ready + '.stop', ready + '.requests.jsonl', ready + '.exit.json']) {
  noLinks(file); if (fs.existsSync(file)) throw new Error('Preserve prior fixture evidence.');
}
const requests = fs.openSync(ready + '.requests.jsonl', 'wx');
let count = 0;
let errors = 0;
let stopping = false;
const record = event => fs.writeSync(requests, JSON.stringify({ utc: new Date().toISOString(), ...event }) + '\n');
const server = https.createServer({ pfx, passphrase: 'hclr-local-fixture', minVersion: 'TLSv1.2' }, (request, response) => {
  const event = { number: ++count, method: request.method, url: request.url, status: 0, bodyLength: 0 };
  let recorded = false;
  const finish = () => { if (!recorded) { recorded = true; record({ ...event, completed: response.writableFinished }); } };
  response.on('finish', finish); response.on('close', finish);
  const bytes = request.method === 'GET' ? files.get(request.url) : undefined;
  event.status = bytes ? 200 : 404; event.bodyLength = bytes ? bytes.length : 0;
  response.writeHead(event.status, { 'Content-Length': event.bodyLength, ETag: '"hclr-stable"' });
  response.end(bytes);
});
server.on('tlsClientError', error => { errors++; record({ kind: 'TLSRejected', error: error.code || error.name }); });
const watcher = fs.watch(root, (_event, file) => { if (file === path.basename(ready + '.stop') && fs.existsSync(ready + '.stop')) stop(); });
function stop() {
  if (stopping) return; stopping = true; watcher.close(); server.closeIdleConnections();
  server.close(() => {
    fs.fsyncSync(requests); fs.closeSync(requests);
    fs.writeFileSync(ready + '.exit.json', JSON.stringify({ Requests: count, Errors: errors, Stopped: true, SystemTrustModified: false, pid: process.pid, OwnerSha256: expectedOwner }), { flag: 'wx' });
    console.log(`Accepted requests=${count}; fixture errors=${errors}`);
  });
}
process.on('SIGTERM', stop); process.on('SIGINT', stop);
server.listen(0, '127.0.0.1', () => {
  const endpoint = `https://127.0.0.1:${server.address().port}/`;
  const key = certificate.publicKey.export({ format: 'jwk' });
  fs.writeFileSync(ready, JSON.stringify({ endpoint, certificate: certificateBytes.toString('base64'),
    modulus: Buffer.from(key.n, 'base64url').toString('base64'), exponent: Buffer.from(key.e, 'base64url').toString('base64') }), { flag: 'wx' });
  console.log(`HybridCLR loopback HTTPS fixture ready: ${endpoint}`);
});
