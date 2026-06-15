import { Command } from 'commander';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { getAgentById, getAgentIds, listAgentTable } from '../utils/agents.js';
import { setupSkills } from '../lib/setup-skills.js';

interface SetupSkillsCliOptions {
  list?: boolean;
}

function listAgentsWithSkills(): void {
  listAgentTable('AI Agents — Skills Support', 'Skills Path', (a) => a.skillsPath ?? '—');
}

export const setupSkillsCommand = new Command('setup-skills')
  .description('Generate Godot-MCP skill files for an AI agent under its skills path')
  .argument('[agent-id]', 'Agent to generate skills for (use --list to see all)')
  .argument('[path]', 'Godot project path (defaults to cwd)')
  .option('--list', 'List all agents and their skills-support status')
  .action(
    async (
      agentId: string | undefined,
      positionalPath: string | undefined,
      options: SetupSkillsCliOptions,
    ) => {
      if (options.list) {
        listAgentsWithSkills();
        return;
      }

      if (!agentId) {
        ui.error('Missing required argument: agent-id');
        ui.info(`Available agent IDs: ${getAgentIds().join(', ')}`);
        process.exit(1);
      }

      const agent = getAgentById(agentId);
      if (!agent) {
        ui.error(`Unknown agent: "${agentId}"`);
        ui.info(`Available agent IDs: ${getAgentIds().join(', ')}`);
        process.exit(1);
      }

      if (!agent.skillsPath) {
        ui.error(`Agent "${agent.name}" does not support skills.`);
        process.exit(1);
      }

      const spinner = ui.startSpinner(`Generating skills for ${agent.name}...`);

      const result = await setupSkills({
        agentId,
        godotProjectPath: positionalPath,
      });

      if (result.kind === 'failure') {
        spinner.error('Failed to generate skills');
        ui.error(result.error.message);
        process.exit(1);
      }

      if (positionalPath) {
        verbose(`Project path: ${positionalPath}`);
      }
      verbose(`Skills directory: ${result.skillsDir}`);
      for (const file of result.filesWritten) {
        verbose(`Wrote ${file}`);
      }

      spinner.success(`Skills generated for ${agent.name}`);

      console.log('');
      ui.label('Agent', agent.name);
      ui.label('Skills path', result.skillsDir);
      ui.label('Files written', String(result.filesWritten.length));

      for (const warning of result.warnings) {
        console.log('');
        ui.warn(warning);
      }
    },
  );
