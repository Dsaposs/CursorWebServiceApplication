export interface AuthResponse {
  token: string;
  expiresAt: string;
}

export interface RulesetResponse {
  code: string;
  displayName: string;
  description: string;
  diceNotation: string;
  isPlaceholder: boolean;
  characterTemplateJson: string;
  definitionJson: string;
}

export interface RulesetDetailResponse extends RulesetResponse {}

export interface RulesetImportResponse {
  ruleset: RulesetDetailResponse;
  created: boolean;
}

export interface GameJoinOptionsResponse {
  inviteCode: string;
  gameName: string;
  rulesetCode: string;
  ruleset: RulesetDetailResponse;
}

export interface RulesetDefinition {
  schemaVersion: number;
  code: string;
  displayName: string;
  description: string;
  diceNotation: string;
  dice: RulesetDiceDefinition[];
  character: RulesetCharacterDefinition;
  actions: RulesetActionDefinition[];
  npcTemplates: Array<Record<string, unknown>>;
}

export interface RulesetDiceDefinition {
  key: string;
  label: string;
  notation: string;
}

export interface RulesetCharacterDefinition {
  vitals: Record<string, unknown>;
  attributes: RulesetAttributeDefinition[];
  gameValues: RulesetGameValueDefinition[];
  classes: RulesetClassDefinition[];
  skills: RulesetSkillDefinition[];
}

export interface RulesetAttributeDefinition {
  key: string;
  label: string;
  default: number;
  min?: number;
  max?: number;
}

export interface RulesetGameValueDefinition {
  key: string;
  label: string;
  type: string;
  default?: unknown;
  min?: number;
}

export interface RulesetClassDefinition {
  key: string;
  label: string;
  description?: string;
  availableSkills: string[];
  startingSkillPoints: number;
}

export interface RulesetSkillDefinition {
  key: string;
  label: string;
  attribute: string;
  default: number;
}

export interface RulesetActionDefinition {
  key: string;
  label: string;
  description?: string;
  allowedClasses?: string[];
  roll: {
    dice: string;
    attribute: string;
    skill: string;
    modifiers: Array<{ source: string; key: string; dicePerPoint?: number }>;
    successRule: string;
  };
}

export interface GameResponse {
  id: string;
  name: string;
  description?: string | null;
  rulesetCode: string;
  rulesetName: string;
  inviteCode: string;
  inviteUrl: string;
  createdAt: string;
  updatedAt: string;
  characters: CharacterResponse[];
  npcsAndMonsters: NpcResponse[];
  sessions: SessionSummaryResponse[];
}

export interface CharacterResponse {
  id: string;
  name: string;
  playerName: string;
  maxHealth: number;
  health: number;
  armor: number;
  attributesJson: string;
  skillsJson: string;
  inventoryJson: string;
  rulesetDataJson: string;
  classKey: string;
}

export interface NpcResponse {
  id: string;
  name: string;
  kind: string;
  maxHealth: number;
  health: number;
  armor: number;
  statBlockJson: string;
  visibility: 'Visible' | 'Obscured' | 'Hidden';
}

export interface JoinGameResponse {
  participantToken: string;
  character: CharacterResponse;
  game: GameResponse;
}

export interface SessionJoinOptionsResponse {
  session: SessionSummaryResponse;
  ruleset: RulesetDetailResponse;
  availableCharacters: CharacterResponse[];
}

export interface SessionSummaryResponse {
  id: string;
  gameId: string;
  joinCode: string;
  joinUrl: string;
  isActive: boolean;
  state: string;
  version: number;
  startedAt: string;
  endedAt?: string | null;
  updatedAt: string;
}

export interface SessionStateResponse extends SessionSummaryResponse {
  game: GameResponse;
  character?: CharacterResponse | null;
  actions: ActionQueueItemResponse[];
  initiative: InitiativeEntryResponse[];
}

export interface ActionQueueItemResponse {
  id: string;
  sequence: number;
  actorName: string;
  actionKey?: string | null;
  actionText: string;
  targetName?: string | null;
  description?: string | null;
  status: string;
  resolutionText?: string | null;
  rollSummary?: string | null;
  additionalActions?: string | null;
  statChangesJson: string;
  submittedAt: string;
  publishedAt?: string | null;
}

export interface InitiativeEntryResponse {
  id: string;
  combatantType: string;
  combatantId: string;
  combatantName: string;
  sortOrder: number;
  isCurrentTurn: boolean;
}

export interface AdminUserReportResponse {
  userId: string;
  userName: string;
  email?: string | null;
  hasPasswordHash: boolean;
  gamesHostedCount: number;
}
