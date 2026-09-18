import { captureDashboard } from './gui.ts';
import { resolveHost } from './vm.ts';
console.log(captureDashboard(resolveHost(), process.argv[2]!, process.argv[3]!).stdout.trim());
