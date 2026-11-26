#!/usr/bin/env python3
"""Own HR database plus persistent keys/configuration. Never backs up another project."""
import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import subprocess
import socket
import tarfile


def digest(path):
    with open(path,'rb') as source:
        return hashlib.file_digest(source,'sha256').hexdigest()


def create_backup(directory, dump_command, keys, configuration):
    directory=Path(directory);keys=Path(keys);configuration=Path(configuration)
    if not keys.is_dir() or not configuration.is_file() or keys.is_symlink() or configuration.is_symlink():
        raise RuntimeError('Persistent HR keys and protected configuration are required.')
    directory.mkdir(mode=0o700,parents=True,exist_ok=False)
    dump=directory/'hrprotool.dump'
    with open(dump,'xb') as target:
        os.chmod(dump,0o600)
        result=subprocess.run(dump_command,stdout=target,stderr=subprocess.PIPE)
    if result.returncode:
        raise RuntimeError('Own database backup failed; diagnostic output suppressed. Incomplete directory retained.')
    archive=directory/'persistent-state.tar.gz'
    def protected(info):
        info.mode=0o600;info.uid=info.gid=0;info.uname=info.gname='root';return info
    with tarfile.open(archive,'w:gz') as tar:
        for path in sorted(keys.iterdir()):
            if not path.is_file() or path.is_symlink():
                raise RuntimeError('Unexpected key directory entry; refusing to follow links.')
            tar.add(path,arcname='keys/'+path.name,recursive=False,filter=protected)
        tar.add(configuration,arcname='production.env',recursive=False,filter=protected)
    os.chmod(archive,0o600)
    manifest={'database':'hrprotool','format':'pg_dump-custom','files':{p.name:digest(p) for p in (dump,archive)}}
    (directory/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    os.chmod(directory/'manifest.json',0o600)
    verify_backup(directory)
    return manifest


def verify_backup(directory):
    directory=Path(directory)
    manifest=json.loads((directory/'manifest.json').read_text())
    if set(manifest['files'])!={'hrprotool.dump','persistent-state.tar.gz'}:
        raise RuntimeError('Unexpected backup manifest.')
    for name,expected in manifest['files'].items():
        if (directory/name).is_symlink() or digest(directory/name)!=expected:
            raise RuntimeError('Backup integrity failed.')
    with tarfile.open(directory/'persistent-state.tar.gz','r:gz') as tar:
        names=[]
        for member in tar.getmembers():
            if not member.isfile() or '\\' in member.name or not (member.name=='production.env' or (member.name.startswith('keys/') and '/' not in member.name[5:] and member.name[5:] not in ('.','..',''))):
                raise RuntimeError('Unsafe backup archive member.')
            names.append(member.name)
        if 'production.env' not in names or not any(n.startswith('keys/') for n in names):
            raise RuntimeError('Backup lacks configuration or persisted Data Protection keys.')
    return manifest


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--verify',type=Path)
    parser.add_argument('--apply',action='store_true')
    parser.add_argument('--expected-host')
    args=parser.parse_args()
    if args.verify:
        verify_backup(args.verify);print('Backup manifest and safe archive layout verified. A restore drill is still required.');return
    if not args.apply or os.name!='posix' or os.geteuid()!=0 or args.expected_host!=socket.gethostname():
        raise RuntimeError('Backup creation requires explicit --apply and root on the HR host.')
    stamp=dt.datetime.now(dt.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    destination=Path('/var/backups/hrprotools')/stamp
    create_backup(destination,['runuser','-u','postgres','--','pg_dump','-p','5432','-d','hrprotool','--format=custom','--no-owner','--no-privileges'],Path('/var/lib/hrprotools/keys'),Path('/etc/hrprotools/production.env'))
    print('HR-only backup created:',destination)


if __name__=='__main__':
    try:main()
    except Exception:raise SystemExit('Backup operation failed; no secret content was printed.')
