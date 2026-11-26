"""LOCAL Docker HR container only. No SSH, remote host or existing database writes.
Creates and stops its own PostgreSQL cluster under a fresh /tmp directory.
"""
import argparse
import hashlib
from http.client import HTTPResponse
from http.cookies import SimpleCookie
from html.parser import HTMLParser
import importlib.util
import io
import json
import os
from pathlib import Path
import secrets
import subprocess
import sys
import time
import traceback
from urllib.parse import urlencode
import uuid

ROOT=Path(__file__).resolve().parents[2]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value


admin=module('hr_admin',ROOT/'deploy/production/hrprotools_admin.py')
backup=module('hr_backup',ROOT/'deploy/production/hrprotools_backup.py')


class Fields(HTMLParser):
    def __init__(self,text):
        super().__init__();self.values={};self.feed(text)
    def handle_starttag(self,tag,attrs):
        data=dict(attrs)
        if tag=='input' and 'name' in data and data.get('type')=='hidden':self.values[data['name']]=data.get('value','')


def main():
    parser=argparse.ArgumentParser();parser.add_argument('release',type=Path);args=parser.parse_args()
    release=args.release.resolve()
    assert release.is_relative_to(ROOT/'artifacts/production')
    container='hrprotool-postgres';suffix=uuid.uuid4().hex[:10];remote='/tmp/hrprotools-local-qa-'+suffix
    evidence=ROOT/'artifacts'/'production-linux-validation'/suffix;evidence.mkdir(parents=True)
    app=None;log=None;started=False;cookies={};reports={};redactions=[]
    creation={'creationflags':subprocess.CREATE_NO_WINDOW} if os.name=='nt' else {}

    def run(command,data=None,check=True):
        result=subprocess.run(command,input=data,capture_output=True,**creation)
        if check and result.returncode:
            diagnostic=(result.stdout+result.stderr).decode(errors='replace')
            for value in redactions:diagnostic=diagnostic.replace(value,'[REDACTED]')
            (evidence/'diagnostic.log').write_text(diagnostic)
            raise RuntimeError('Local validation command failed: '+command[0]+' '+command[1]+'; sanitized diagnostic in '+str(evidence/'diagnostic.log'))
        return result
    def docker(*arguments,data=None,check=True,user='postgres'):
        return run(['docker','exec','-i','--user',user,container,*arguments],data,check)
    def sql(statement,database='postgres',user='postgres',password=None,check=True):
        prefix=['env','PGPASSWORD='+password] if password else []
        return docker(*prefix,'psql','-X','-q','-t','-A','-v','ON_ERROR_STOP=1','-p','55438','-h','127.0.0.1','-U',user,'-d',database,data=statement.encode(),check=check)
    def start_app(keydir):
        nonlocal app,log
        environment=['ASPNETCORE_ENVIRONMENT=Production','ASPNETCORE_CONTENTROOT='+remote+'/web','ASPNETCORE_URLS=http://127.0.0.1:5091','ConnectionStrings__DefaultConnection='+connection,'DataProtection__KeyPath='+keydir,'BootstrapAdmin__Email=bootstrap-linux@example.test']
        log=open(evidence/'application.log','ab')
        command=['docker','exec','--user','postgres',container,'env',*environment,'bash','-c','echo $$ > "$1"; shift; exec "$@"','--',remote+'/app.pid',remote+'/web/HRProTools.Web']
        app=subprocess.Popen(command,stdout=log,stderr=log,**creation)
        for _ in range(60):
            if app.poll() is not None:raise RuntimeError('Linux application exited before readiness.')
            try:
                if http('GET','/health').status==200:return
            except Exception:pass
            time.sleep(.5)
        raise RuntimeError('Linux application readiness timed out.')
    def stop_app():
        nonlocal app,log
        if app is not None:
            # Verify the executable before signalling only the process we started.
            docker('bash','-c','pid=$(cat "$1"); test "$(readlink /proc/$pid/exe)" = "$2" && kill -TERM "$pid"','--',remote+'/app.pid',remote+'/web/HRProTools.Web',check=False)
            try:app.wait(timeout=15)
            except subprocess.TimeoutExpired:raise RuntimeError('Own application did not stop; inspect QA PID.')
            app=None;log.close();log=None
    def http(method,path,form=None):
        body=urlencode(form).encode() if form is not None else b''
        headers=[f'{method} {path} HTTP/1.1','Host: app.hrprotools.com','X-Forwarded-Proto: https','Connection: close']
        if cookies:headers.append('Cookie: '+'; '.join(k+'='+v for k,v in cookies.items()))
        if form is not None:headers.extend(['Content-Type: application/x-www-form-urlencoded','Content-Length: '+str(len(body))])
        raw=('\r\n'.join(headers)+'\r\n\r\n').encode()+body
        response=docker('bash','-c','exec 3<>/dev/tcp/127.0.0.1/5091; cat >&3; cat <&3',data=raw,check=False)
        class Socket:
            def makefile(self,*_):return io.BytesIO(response.stdout)
        parsed=HTTPResponse(Socket());parsed.begin();parsed.payload=parsed.read()
        for value in parsed.headers.get_all('Set-Cookie',[]):
            item=SimpleCookie();item.load(value)
            for key,morsel in item.items():cookies[key]=morsel.value
        return parsed

    try:
        # Fresh, non-root cluster in the existing HR container. Original data directory is never touched.
        docker('initdb','-D',remote+'/data','-A','trust','--no-instructions')
        docker('pg_ctl','-D',remote+'/data','-l',remote+'/postgres.log','-o','-h 127.0.0.1 -p 55438 -k /tmp','-w','start');started=True
        password='QaDb!26'+secrets.token_urlsafe(24);verifier=admin.scram_verifier(password)
        redactions.append(password)
        sql(f"CREATE ROLE hrprotool_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD '{verifier}';\nCREATE DATABASE hrprotool OWNER hrprotool_app;\nCREATE DATABASE hrprotools_restore_qa OWNER hrprotool_app;\nCREATE DATABASE hrprotools_other_qa;\n")
        hba_path=remote+'/data/pg_hba.conf';original=docker('cat',hba_path).stdout.decode()
        normalized="SELECT coalesce(jsonb_agg(to_jsonb(r)-'line_number'-'rule_number' ORDER BY rule_number),'[]') FROM pg_hba_file_rules r"
        before=json.loads(sql(normalized+';').stdout)
        docker('bash','-c','cat > "$1"','--',hba_path,data=admin.compose_hba(original).encode())
        assert sql('SELECT count(*) FROM pg_hba_file_rules WHERE error IS NOT NULL;').stdout.strip()==b'0'
        after=json.loads(sql(normalized+" WHERE NOT ('hrprotool_app'=ANY(user_name));").stdout)
        assert before==after
        sql('SELECT pg_reload_conf();');time.sleep(.2)
        assert sql('SELECT current_user;','hrprotool','hrprotool_app',password).stdout.strip()==b'hrprotool_app'
        assert sql('SELECT 1;','hrprotools_other_qa','hrprotool_app',password,check=False).returncode!=0
        assert sql('SELECT 1;','hrprotools_other_qa').returncode==0
        reports['hba']='parsed, old rules unchanged, own DB allowed, other DB denied, other user unaffected'
        run(['docker','cp',str(release/'web'),container+':'+remote+'/web'])
        docker('chmod','755',remote+'/web/HRProTools.Web',user='root')
        connection=f'Host=127.0.0.1;Port=55438;Database=hrprotool;Username=hrprotool_app;Password={password};Maximum Pool Size=5'
        env=['ASPNETCORE_ENVIRONMENT=Production','ASPNETCORE_CONTENTROOT='+remote+'/web','ConnectionStrings__DefaultConnection='+connection,'DataProtection__KeyPath='+remote+'/keys','BootstrapAdmin__Email=bootstrap-linux@example.test']
        for mode in ['--migrate-only','--bootstrap-admin-stdin']:
            initial='NewInitial!26'+secrets.token_urlsafe(24) if mode.endswith('stdin') else None
            if initial:redactions.append(initial)
            result=docker('env',*env,remote+'/web/HRProTools.Web',mode,data=(json.dumps({'password':initial})+'\n').encode() if initial else None)
            assert password.encode() not in result.stdout+result.stderr
            if initial:assert initial.encode() not in result.stdout+result.stderr
        assert sql('SELECT count(*) FROM auth."AspNetUsers" WHERE "MustChangePassword";', 'hrprotool').stdout.strip()==b'1'
        assert sql('SELECT count(*) FROM platform.tenant;','hrprotool').stdout.strip()==b'0'
        start_app(remote+'/keys')
        login=http('GET','/Account/Login');assert login.status==200
        form=Fields(login.payload.decode()).values;form.update({'Input.Email':'bootstrap-linux@example.test','Input.Password':initial,'Input.RememberMe':'false'})
        signed=http('POST','/Account/Login',form);assert signed.status==302 and 'ChangePassword' in signed.headers.get('Location','')
        assert any('secure' in v.lower() for v in signed.headers.get_all('Set-Cookie',[]))
        assert http('GET','/admin/grupos').status==302
        assert http('POST','/_blazor/negotiate',{}).status==403
        change=http('GET','/Account/Manage/ChangePassword');assert change.status==200
        form=Fields(change.payload.decode()).values;replacement='NewReplacement!26'+secrets.token_urlsafe(24)
        form.update({'Input.OldPassword':initial,'Input.NewPassword':replacement,'Input.ConfirmPassword':replacement})
        changed=http('POST','/Account/Manage/ChangePassword',form);assert changed.status==302
        assert http('GET','/admin/grupos').status==200
        assert sql('SELECT count(*) FROM auth."AspNetUsers" WHERE "MustChangePassword";','hrprotool').stdout.strip()==b'0'
        reports['linux_http']='self-contained startup, HTTPS forwarded, secure cookie, mandatory change, Blazor denial, admin access after change'
        stop_app()
        run(['docker','cp',container+':'+remote+'/keys',str(evidence/'keys')])
        (evidence/'production.env').write_text('ASPNETCORE_ENVIRONMENT=Production\nApplication__PublicBaseUrl=https://app.hrprotools.com\n')
        created=backup.create_backup(evidence/'backup',['docker','exec','--user','postgres',container,'pg_dump','-h','/tmp','-p','55438','-d','hrprotool','-Fc','--no-owner','--no-privileges'],evidence/'keys',evidence/'production.env')
        run(['docker','exec','-i','--user','postgres',container,'pg_restore','-h','/tmp','-p','55438','-U','postgres','--role=hrprotool_app','-d','hrprotools_restore_qa','--no-owner','--no-privileges','--exit-on-error'],data=(evidence/'backup/hrprotool.dump').read_bytes())
        hash_sql="""CREATE TEMP TABLE qa_hashes(n text,c bigint,h text); DO $body$ DECLARE r record; BEGIN FOR r IN SELECT table_schema,table_name FROM information_schema.tables WHERE table_schema IN ('auth','platform') AND table_type='BASE TABLE' LOOP EXECUTE format('INSERT INTO qa_hashes SELECT %L,count(*),md5(coalesce(string_agg(v,''|'' ORDER BY v),'''')) FROM (SELECT to_jsonb(t)::text v FROM %I.%I t) x',r.table_schema||'.'||r.table_name,r.table_schema,r.table_name); END LOOP; END $body$; SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY n),'[]') FROM qa_hashes t;"""
        assert json.loads(sql(hash_sql,'hrprotool').stdout)==json.loads(sql(hash_sql,'hrprotools_restore_qa').stdout)
        docker('install','-d','-m','700',remote+'/restored',remote+'/restored/keys')
        docker('tar','-xz','--no-same-owner','-C',remote+'/restored',data=(evidence/'backup/persistent-state.tar.gz').read_bytes())
        docker('chmod','700',remote+'/restored/keys')
        start_app(remote+'/restored/keys')
        assert http('GET','/admin/grupos').status==200 # Same cookie decrypted using the restored key ring.
        reports['recovery']='real pg_restore, all auth/platform table hashes equal, restored Data Protection keys accept previous session'
        stop_app()
        # Immediate HBA rollback affects no original rule and reopens only the QA role's test access.
        docker('bash','-c','cat > "$1"','--',hba_path,data=original.encode());sql('SELECT pg_reload_conf();');time.sleep(.2)
        assert sql('SELECT 1;','hrprotools_other_qa','hrprotool_app',password).returncode==0
        assert json.loads(sql(normalized+';').stdout)==before
        reports['hba_rollback']='original file/rules restored, no PostgreSQL restart'
        for value in redactions+[replacement]:assert value.encode() not in (evidence/'application.log').read_bytes()
        reports['result']='passed'
        (evidence/'report.json').write_text(json.dumps(reports,indent=2)+'\n')
        print(json.dumps(reports,indent=2))
    finally:
        stop_app()
        if started:docker('pg_ctl','-D',remote+'/data','-m','fast','-w','stop',check=False)
        if log:log.close()
        print('Only the owned local QA cluster was stopped; remote path retained for inspection:',remote)


if __name__=='__main__':
    try:main()
    except Exception as error:
        frame=traceback.extract_tb(error.__traceback__)[-1]
        print('LOCAL VALIDATION FAILED:',type(error).__name__,frame.name,'line',frame.lineno,file=sys.stderr)
        if isinstance(error,RuntimeError):print(str(error),file=sys.stderr)
        raise SystemExit(1)
