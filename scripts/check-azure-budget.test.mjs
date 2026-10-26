import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const root=fileURLToPath(new URL('..',import.meta.url));
const bash=process.platform==='win32'?'C:/Program Files/Git/bin/bash.exe':'bash';
const mock=`
az() {
  case "$1 $2" in
    'lock list')
      if [[ "\${MOCK_LOCK_ERROR:-0}" == 1 ]]; then return 37; fi
      printf '%s\\n' "\${MOCK_LOCKS-0}" ;;
    'containerapp show')
      if [[ "\${MOCK_STATUS_ERROR:-0}" == 1 ]]; then return 38; fi
      printf '%s\\n' "\${MOCK_STATUS-Running}" ;;
    'containerapp secret')
      [[ "$3" == set ]] || return 39
      echo 'MUTATION:secret'
      if [[ "\${MOCK_LOCK_AFTER_SECRET:-0}" == 1 ]]; then export MOCK_LOCKS=1; fi ;;
    'containerapp update') echo 'MUTATION:update' >&2; echo 'test-revision' ;;
    *) echo 'Unexpected Azure command' >&2; return 40 ;;
  esac
}
export -f az
`;

function run(script,environment={}){
 const result=spawnSync(bash,['-e','-u','-o','pipefail','-c',mock+'\n'+script],{
  cwd:root,encoding:'utf8',env:{...process.env,AZURE_RESOURCE_GROUP:'rg-test',
   AZURE_CONTAINER_APP:'ca-test',AZURE_SUBSCRIPTION_ID:'test-subscription',
   GHCR_IMAGE:'test/image',IMAGE_TAG:'test',GITHUB_OUTPUT:'/dev/null',
   POSTGRES_CONNECTION_STRING:'synthetic-test-value',...environment}
 });
 assert.ifError(result.error);
 assert(result.status!==null&&result.status<=255,result.stderr||'Bash did not start');
 return {...result,output:result.stdout+result.stderr};
}

function mutationStep(file,name){
 const lines=readFileSync(new URL('../.github/workflows/'+file,import.meta.url),'utf8').split(/\r?\n/);
 const start=lines.indexOf('    - name: '+name);
 assert(start>=0,'workflow step exists');
 let end=lines.findIndex((line,index)=>index>start&&line.startsWith('    - name:'));
 if(end<0)end=lines.length;
 const step=lines.slice(start,end),run=start+step.indexOf('      run: |');
 assert(run>=start,'workflow has a shell body');
 return lines.slice(run+1,end).map(line=>line.startsWith('        ')?line.slice(8):line).join('\n');
}

test('the guard permits only zero budget locks and an explicitly Running app',()=>{
 assert.equal(run('bash scripts/check-azure-budget.sh').status,0);
 for(const environment of [
  {MOCK_LOCKS:'1'}, // The named lock blocks both CanNotDelete and ReadOnly phases.
  {MOCK_LOCKS:''},{MOCK_LOCKS:'null'},{MOCK_LOCK_ERROR:'1'},
  {MOCK_STATUS:'Stopped'},{MOCK_STATUS:'Progressing'},{MOCK_STATUS:''},{MOCK_STATUS_ERROR:'1'}
 ]){
  const result=run('bash scripts/check-azure-budget.sh',environment);
  assert.notEqual(result.status,0,JSON.stringify(environment));
  assert.match(result.stdout,/::error::/);
 }
});

const workflows=[
 ['deploy-container-app.yml','Deploy image to Container Apps'],
 ['configure-database.yml','Configure PostgreSQL']
];
for(const [file,step] of workflows){
 const script=mutationStep(file,step);
 test(file+': running, unlocked apps can be updated',()=>{
  const result=run(script);
  assert.equal(result.status,0,result.stderr);
  assert.match(result.output,/MUTATION:update/);
 });
 test(file+': a stop or unreadable state prevents every mutation',()=>{
  for(const environment of [{MOCK_LOCKS:'1'},{MOCK_STATUS:'Stopped'},{MOCK_LOCK_ERROR:'1'},{MOCK_STATUS_ERROR:'1'}]){
   const result=run(script,environment);
   assert.notEqual(result.status,0);
   assert.match(result.output,/::error::/);
   assert.doesNotMatch(result.output,/MUTATION:/);
  }
 });
}

test('a budget lock set after secret storage prevents the subsequent app update',()=>{
 const script=mutationStep('configure-database.yml','Configure PostgreSQL');
 const result=run(script,{MOCK_LOCK_AFTER_SECRET:'1'});
 assert.notEqual(result.status,0);
 assert.match(result.output,/MUTATION:secret/);
 assert.doesNotMatch(result.output,/MUTATION:update/);
});

test('deployment and database changes use the same non-cancelling concurrency group',()=>{
 for(const [file] of workflows){
  const source=readFileSync(new URL('../.github/workflows/'+file,import.meta.url),'utf8');
  assert.match(source,/group: simplexflow-production\r?\n\s+cancel-in-progress: false/);
  assert.match(source,/uses: actions\/checkout@v4/);
 }
});
