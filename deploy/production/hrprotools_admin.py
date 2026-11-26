#!/usr/bin/env python3
"""Run on the target only after explicit deployment approval. Never invokes SSH."""
import argparse
import base64
import datetime as dt
import getpass
import hashlib
import hmac
import json
import os
from pathlib import Path
try:
    import pwd
except ImportError:
    pwd = None  # Pure HBA functions can be validated on Windows; apply is Linux-only.
import secrets
import shlex
import shutil
import socket
import subprocess
import sys
import tempfile

HBA = Path('/etc/postgresql/18/main/pg_hba.conf')
ENV = Path('/etc/hrprotools/production.env')
ROLE = 'hrprotool_app'
DB = 'hrprotool'
BEGIN = '# BEGIN HRPROTOOLS MANAGED BLOCK'
END = '# END HRPROTOOLS MANAGED BLOCK'


def hba_block():
    return f'''{BEGIN}
local {DB} {ROLE} scram-sha-256
local all {ROLE} reject
host {DB} {ROLE} 127.0.0.1/32 scram-sha-256
host all {ROLE} 0.0.0.0/0 reject
host {DB} {ROLE} ::1/128 scram-sha-256
host all {ROLE} ::/0 reject
{END}
'''


def compose_hba(original):
    if BEGIN in original or ROLE in original:
        raise RuntimeError('HBA already mentions HR; inspect instead of overwriting it.')
    return hba_block() + original


def execute(command, *, data=None, env=None, user=None):
    result = subprocess.run(command, input=data, text=True, capture_output=True, env=env, cwd=str(Path(command[0]).parent) if user else None,
                            **({'user': user, 'group': pwd.getpwnam(user).pw_gid, 'extra_groups': []} if user else {}))
    if result.returncode:
        # Database/Identity exceptions or psql context must never echo secret input.
        raise RuntimeError('Operation failed; secret-bearing command output was suppressed.')
    return result.stdout.strip()


def sql(statement, database='postgres'):
    env = dict(os.environ, PGOPTIONS='-c log_statement=none -c log_min_duration_statement=-1 -c log_min_error_statement=panic')
    return execute(['runuser','-u','postgres','--','psql','-X','-q','-t','-A','-v','ON_ERROR_STOP=1','-p','5432','-d',database], data=statement, env=env)


def private_write(path, content, mode=0o600):
    path = Path(path)
    if path.is_symlink():
        raise RuntimeError('Refusing symlink destination.')
    path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, mode)
    with os.fdopen(fd, 'w', encoding='utf-8', newline='\n') as out:
        out.write(content)


def atomic_hba(content):
    if HBA.is_symlink():
        raise RuntimeError('Refusing symlink HBA destination.')
    metadata=HBA.stat()
    fd,name=tempfile.mkstemp(prefix='.hrprotools-hba-',dir=HBA.parent)
    try:
        with os.fdopen(fd,'wb') as out:
            out.write(content);out.flush();os.fsync(out.fileno())
        os.chown(name,metadata.st_uid,metadata.st_gid);os.chmod(name,metadata.st_mode & 0o777)
        os.replace(name,HBA)
    finally:
        if os.path.exists(name):os.unlink(name)


def scram_verifier(password):
    salt=secrets.token_bytes(16)
    salted=hashlib.pbkdf2_hmac('sha256',password.encode(),salt,4096)
    stored=hashlib.sha256(hmac.new(salted,b'Client Key','sha256').digest()).digest()
    server=hmac.new(salted,b'Server Key','sha256').digest()
    b64=lambda value:base64.b64encode(value).decode('ascii')
    return f'SCRAM-SHA-256$4096:{b64(salt)}${b64(stored)}:{b64(server)}'


def require_target(args):
    if not args.apply or os.name != 'posix' or os.geteuid() != 0 or socket.gethostname() != args.expected_host:
        raise RuntimeError('Requires explicit --apply, root and matching --expected-host on the approved target.')
    if 'VERSION_ID="26.04"' not in Path('/etc/os-release').read_text():
        raise RuntimeError('Re-audit OS before applying this plan.')
    if sql('SHOW server_version_num;')[:2] != '18':
        raise RuntimeError('Expected PostgreSQL 18/main on 5432.')


def provision(args):
    require_target(args)
    original = HBA.read_bytes()
    if ENV.exists() or Path('/var/lib/hrprotools').exists():
        raise RuntimeError('Initial HR paths already exist; inspect rather than overwriting them.')
    if not args.expected_hba_sha256 or hashlib.sha256(original).hexdigest() != args.expected_hba_sha256:
        raise RuntimeError('HBA baseline differs; re-inspect before modifying anything.')
    if sql('SELECT count(*) FROM pg_hba_file_rules WHERE error IS NOT NULL;') != '0':
        raise RuntimeError('Existing HBA is invalid; no changes made.')
    if sql("SELECT count(*) FROM pg_database WHERE datname='hrprotool';") != '0' or sql("SELECT count(*) FROM pg_roles WHERE rolname='hrprotool_app';") != '0':
        raise RuntimeError('HR database/role already exists; initial provisioning refuses to overwrite it.')
    try:
        pwd.getpwnam('hrprotools')
        raise RuntimeError('Service user already exists; re-inspect before initial provisioning.')
    except KeyError:
        pass
    updated = compose_hba(original.decode('utf-8'))
    backup = Path('/var/backups/hrprotools') / ('preflight-' + dt.datetime.now(dt.timezone.utc).strftime('%Y%m%dT%H%M%SZ'))
    backup.mkdir(parents=True, mode=0o700)
    private_write(backup / 'pg_hba.conf', original.decode('utf-8'))
    private_write(backup / 'baseline.sha256', hashlib.sha256(original).hexdigest() + '\n')
    prior_rules = sql("SELECT coalesce(jsonb_agg(to_jsonb(r)-'line_number'-'rule_number' ORDER BY rule_number),'[]') FROM pg_hba_file_rules r;")
    # Install a block matching only the new role, then prove all old rules remain identical.
    try:
        atomic_hba(updated.encode('utf-8'))
        if sql('SELECT count(*) FROM pg_hba_file_rules WHERE error IS NOT NULL;') != '0':
            raise RuntimeError('New HBA block failed validation.')
        after_rules = sql("SELECT coalesce(jsonb_agg(to_jsonb(r)-'line_number'-'rule_number' ORDER BY rule_number),'[]') FROM pg_hba_file_rules r WHERE user_name IS NULL OR NOT ('hrprotool_app'=ANY(user_name));")
        if json.loads(prior_rules) != json.loads(after_rules):
            raise RuntimeError('An existing HBA rule changed.')
        if sql('SELECT pg_reload_conf();') != 't':
            raise RuntimeError('HBA reload failed.')
    except BaseException:
        atomic_hba(original)
        sql('SELECT pg_reload_conf();')
        raise
    execute(['useradd','--system','--user-group','--home-dir','/var/lib/hrprotools','--shell','/usr/sbin/nologin','hrprotools'])
    identity = pwd.getpwnam('hrprotools')
    for name in ('/var/lib/hrprotools','/var/lib/hrprotools/keys'):
        path=Path(name); path.mkdir(mode=0o700,parents=True,exist_ok=True);os.chown(path,identity.pw_uid,identity.pw_gid);os.chmod(path,0o700)
    password = secrets.token_urlsafe(40)
    # This password is freshly generated, never a previous admin/SMTP credential.
    verifier=scram_verifier(password)
    sql(f"CREATE ROLE {ROLE} LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS CONNECTION LIMIT 20 PASSWORD '{verifier}';")
    sql(f'CREATE DATABASE {DB} OWNER {ROLE} TEMPLATE template0;')
    sql(f'REVOKE ALL ON DATABASE {DB} FROM PUBLIC;')
    sql('REVOKE CREATE ON SCHEMA public FROM PUBLIC;', database=DB)
    private_write(ENV, '\n'.join([
        'ASPNETCORE_ENVIRONMENT=Production', 'ASPNETCORE_URLS=http://127.0.0.1:5091',
        'AllowedHosts=app.hrprotools.com', 'Application__PublicBaseUrl=https://app.hrprotools.com',
        'DataProtection__KeyPath=/var/lib/hrprotools/keys', 'Antivirus__ClamAv__Required=true',
        'Antivirus__ClamAv__UnixSocketPath=/run/clamav/clamd.ctl', 'Database__ApplyMigrations=false',
        'BootstrapAdmin__Email=orloch314@gmail.com',
        f'ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=5432;Database={DB};Username={ROLE};Password={password};Maximum Pool Size=5;Minimum Pool Size=0;Connection Idle Lifetime=60"', '' ]))
    print('HR isolated user/database/configuration created. No service started. HBA rollback copy:', backup)


def load_environment():
    if ENV.is_symlink() or not ENV.is_file() or ENV.stat().st_uid != 0 or ENV.stat().st_mode & 0o077:
        raise RuntimeError('Production environment must be a private root-owned regular file.')
    result = {'PATH':'/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin','HOME':'/var/lib/hrprotools','LANG':'C.UTF-8'}
    for line in ENV.read_text().splitlines():
        if line and not line.startswith('#'):
            key,value=line.split('=',1);result[key]=shlex.split(value)[0]
    if 'BootstrapAdmin__Password' in result:
        raise RuntimeError('Persistent bootstrap password is forbidden.')
    return result


def application_command(args, bootstrap=False, smtp=False):
    require_target(args)
    binary = Path('/opt/hrprotools/current/web/HRProTools.Web')
    if not binary.is_file():
        raise RuntimeError('Install and verify the HR release first.')
    env=load_environment();env['ASPNETCORE_CONTENTROOT']=str(binary.parent)
    payload=None
    if bootstrap or smtp:
        if not sys.stdin.isatty():
            raise RuntimeError('Bootstrap wrapper requires an interactive terminal; no password arguments or files.')
        first=getpass.getpass('Contraseña NUEVA de info@hrprotools.com (Aruba): ' if smtp else 'Nueva credencial inicial de admin (no reutilizar anteriores): ')
        second=getpass.getpass('Repita la nueva credencial: ')
        if first!=second or not first:
            raise RuntimeError('Credentials did not match.')
        payload=json.dumps({'password':first})+'\n'
    result=execute([str(binary),'--bootstrap-admin-stdin' if bootstrap else '--configure-smtp-stdin' if smtp else '--migrate-only'],data=payload,env=env,user='hrprotools')
    print('SMTP configuration protected; single test message accepted. Inbox receipt not confirmed.' if smtp else 'Bootstrap complete; mandatory password change remains active.' if bootstrap else 'HR migrations applied; no workers started.')


def rollback_hba(args):
    require_target(args)
    current=HBA.read_text()
    block=hba_block()
    if not current.startswith(block):
        raise RuntimeError('Managed block is not unchanged at the beginning; manual review required.')
    atomic_hba(current[len(block):].encode('utf-8'))
    if sql('SELECT count(*) FROM pg_hba_file_rules WHERE error IS NOT NULL;')!='0':
        atomic_hba(current.encode('utf-8'));raise RuntimeError('Rollback validation failed; managed file preserved.')
    sql('SELECT pg_reload_conf();')
    print('Only the HR HBA block was removed. No database or role was deleted.')


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action',choices=['provision','migrate','bootstrap','smtp','rollback-hba'])
    parser.add_argument('--apply',action='store_true');parser.add_argument('--expected-host',required=True)
    parser.add_argument('--expected-hba-sha256')
    args=parser.parse_args()
    try:
        {'provision':provision,'migrate':application_command,'bootstrap':lambda a:application_command(a,True),'smtp':lambda a:application_command(a,smtp=True),'rollback-hba':rollback_hba}[args.action](args)
    except Exception as error:
        print(str(error) if isinstance(error,RuntimeError) else 'Deployment step failed; inspect its state before retrying.',file=sys.stderr)
        return 1
    return 0

if __name__=='__main__':
    raise SystemExit(main())
