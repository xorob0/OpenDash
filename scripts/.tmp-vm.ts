import { maximiseSimHub } from './gui.ts';
import { resolveHost, sleep } from './vm.ts';
const host = resolveHost();
sleep(12);
console.log(maximiseSimHub(host, 120).stdout.trim());
