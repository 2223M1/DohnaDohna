// Audit exactly the Git index, not ignored commercial inputs or local archives.
import { execFileSync } from 'node:child_process';
const files = execFileSync('git', ['ls-files', '--cached', '-z'], { encoding: 'utf8' }).split('\0').filter(Boolean);
if (!files.length) throw new Error('No staged/tracked public source files to audit.');
const errors = [];
const banned = /^(?:build\/|\.local(?:-reference)?\/|\.godot\/|Reference\/(?!\.gdignore$)|DohnaDohna\/(?:roles\/|images\/(?:original|shadows)\/)|Docs\/(?:migration-manifest|excluded-source-files)\.json$)|(?:^|\/)(?:node_modules|__pycache__)\/|\.(?:dll|pdb|pck|bank|wav|ogg|mp3|mp4|avi|exe|pyc|pem|p12|key|save)$/i;
const secrets = [
  /\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})\b/g,
  /\bAKIA[0-9A-Z]{16}\b/g,
  /-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/g,
  /\b(?:sk-proj-|sk-ant-api\d+-)[A-Za-z0-9_-]{30,}\b/g,
];
for (const file of files) {
  if (banned.test(file) || /(?:^|\/)\.env(?:\.|$)/.test(file) && !file.endsWith('.env.example')) errors.push(file + ': excluded input');
  const blob = execFileSync('git', ['show', ':' + file], { maxBuffer: 10 * 1024 * 1024 });
  if (blob.length > 5 * 1024 * 1024) errors.push(file + ': oversized public source file');
  if (blob.includes(0)) { errors.push(file + ': binary input needs explicit publication review'); continue; }
  const text = blob.toString('utf8');
  for (const pattern of secrets) {
    pattern.lastIndex = 0;
    if (pattern.test(text)) errors.push(file + ': possible credential (value withheld)');
  }
}
if (errors.length) throw new Error(errors.join('\n'));
console.log(`PASS: ${files.length} indexed text files; no excluded private/binary inputs or recognized credentials.`);
