"""Inspect and package locally. Contains no upload/deployment operation."""
import hashlib
import json
from pathlib import Path
import sys
import tarfile

root=Path(sys.argv[1]).resolve()
assert root.parent.name=='production' and root.parent.parent.name=='artifacts'
assert (root/'web/HRProTools.Web').read_bytes()[:4]==b'\x7fELF', 'Linux ELF executable required'
runtime=json.loads((root/'web/HRProTools.Web.runtimeconfig.json').read_text())['runtimeOptions']
assert runtime['tfm']=='net10.0' and runtime.get('includedFrameworks'), 'Self-contained .NET 10 required'
assert (root/'site/index.html').is_file()
files=sorted(p for p in root.rglob('*') if p.is_file())
for path in files:
    name=path.name.lower()
    assert not path.is_symlink() and '..' not in path.relative_to(root).parts
    assert name not in ('production.env','.env','secrets.json','appsettings.development.json','appsettings.local.json')
    assert not name.endswith('.pdb')
    assert not ('keys' in path.relative_to(root).parts or name.startswith('key-') and name.endswith('.xml'))
checks=[]
for path in files:
    with path.open('rb') as source: checksum=hashlib.file_digest(source,'sha256').hexdigest()
    checks.append(checksum+'  '+path.relative_to(root).as_posix())
(root/'SHA256SUMS').write_text('\n'.join(checks)+'\n',encoding='utf-8')
archive=root.parent/(root.name+'.tar.gz')
with tarfile.open(archive,'w:gz') as output:
    for path in files+[root/'SHA256SUMS']:
        info=output.gettarinfo(str(path),arcname=path.relative_to(root).as_posix())
        info.uid=info.gid=0;info.uname=info.gname='root'
        info.mode=0o755 if path.name=='HRProTools.Web' or path.suffix in ('.sh','.py') else 0o644
        with path.open('rb') as source:output.addfile(info,source)
with archive.open('rb') as source:checksum=hashlib.file_digest(source,'sha256').hexdigest()
archive.with_suffix(archive.suffix+'.sha256').write_text(checksum+'  '+archive.name+'\n',encoding='utf-8')
print(f'Inspected {len(files)} files; Linux self-contained bundle ready. No remote operations.')
