export interface AuthResponse {
  token: string;
  expiresAt: string;
}

export interface AuthWithRefreshResponse extends AuthResponse {
  refreshToken: string;
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

export interface RulesetTheme {
  bg?: string;
  surface?: string;
  panel?: string;
  panelAlt?: string;
  panelHover?: string;
  border?: string;
  borderStrong?: string;
  ink?: string;
  inkBright?: string;
  muted?: string;
  mutedLight?: string;
  accent?: string;
  accentDark?: string;
  accentDim?: string;
  secondary?: string;
  secondaryDim?: string;
  danger?: string;
  dangerDim?: string;
  success?: string;
  successDim?: string;
  warn?: string;
  warnDim?: string;
  fontFamily?: string;
}

export interface RulesetDefinition {
  schemaVersion: number;
  code: string;
  displayName: string;
  description: string;
  /** Key of the shared dice roller implementation (e.g. "d6-pool", "d20-check"). */
  diceRollerKey?: string;
  diceNotation: string;
  dice: RulesetDiceDefinition[];
  character: RulesetCharacterDefinition;
  actions: RulesetActionDefinition[];
  items?: RulesetItemDefinition[];
  npcTemplates: RulesetNpcTemplateDefinition[];
  /** Ruleset-specific mechanics for skill and attribute checks. */
  rollMechanics?: RulesetRollMechanics;
  /** Named status conditions that can be applied to characters and NPCs. */
  statusEffects?: RulesetStatusEffectDefinition[];
  /** How initiative is rolled when combat starts. */
  initiative?: RulesetInitiativeDefinition;
  /** Visual theme tokens applied to session screens for this ruleset. */
  theme?: RulesetTheme;
}

export interface RulesetStatusEffectDefinition {
  key: string;
  label: string;
  description?: string | null;
  /** True = harmful (red/orange badge); false = buff (green/blue badge). */
  isNegative: boolean;
  /** HP threshold at which this status is auto-applied (null = manual only). */
  autoApplyAtHpThreshold?: number | null;
}

export interface RulesetRollMechanics {
  skillCheck?: RulesetCheckMechanics;
  attributeCheck?: RulesetCheckMechanics;
}

export interface RulesetCheckMechanics {
  /** Key into the `dice` array. */
  diceKey: string;
  /** "attribute+skill" | "attribute" | "fixed" */
  poolMode: string;
  modifiers: Array<{
    source: string;
    key: string;
    dicePerPoint?: number;
    isStressDice?: boolean;
  }>;
  successRule?: string;
  /** Default DC for d20 checks when the success rule does not specify one. */
  difficultyClass?: number;
}

export interface RulesetDiceDefinition {
  key: string;
  label: string;
  notation: string;
  /** When set, dice meeting/exceeding this value each count as one success (pool systems). */
  successTarget?: number;
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
  maxSkillRank?: number;
  startingItemOptions?: string[];
}

export interface RulesetNpcTemplateDefinition {
  key: string;
  label: string;
  scenario?: string;
  description?: string;
  kind?: string;
  maxHealth?: number;
  health?: number;
  armor?: number;
  defaultStats?: Record<string, unknown>;
}

export interface RulesetSkillDefinition {
  key: string;
  label: string;
  attribute: string;
  default: number;
  /** If set, this skill is only available when the actor has this item in their inventory. */
  requiredItemKey?: string;
}

export interface RulesetItemDefinition {
  key: string;
  label: string;
  description?: string;
  category?: string;
  modifiers?: RulesetRollModifier[];
  attackRoll?: RulesetActionDefinition['roll'];
  damageRoll?: {
    notation: string;
    bonusAttribute?: string;
    flatBonus?: number;
    description?: string;
  };
}

export interface RulesetRollModifier {
  source: string;
  key: string;
  dicePerPoint?: number;
  isStressDice?: boolean;
  flatDice?: number;
  attackBonus?: number;
}

export interface RulesetRollChainStepDefinition {
  step: string;
  label?: string;
  checkMode?: string;
  resultKind?: string;
  guidanceText?: string;
  customCheckText?: string;
  diceSource?: string;
  autoResolve?: { condition: string; fallback?: string };
  onSuccess?: string;
  onFailure?: string;
  onComplete?: string;
  applyEffects?: Array<{
    target: string;
    stat: string;
    operation: string;
    value: string;
    label?: string;
    minResult?: number;
  }>;
}

export interface RulesetActionDefinition {
  key: string;
  label: string;
  description?: string;
  /** 'combat' actions require a target and are only available during a combatant's turn.
   *  'exploration' (or absent) actions can be taken freely.
   *  Maps to category 'combat' | 'normal' in UI helpers. */
  context?: 'combat' | 'exploration';
  allowedClasses?: string[];
  requiredItemKey?: string;
  roll: {
    dice: string;
    /** "attribute+skill" means total dice = attribute value + skill value */
    dicePoolMode?: string;
    attribute: string;
    skill: string;
    modifiers: RulesetRollModifier[];
    successRule: string;
    difficultyClass?: number;
  };
  rollChain?: RulesetRollChainStepDefinition[];
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
  inventoryJson: string;
  rulesetDataJson: string;
  statusEffectsJson: string;
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
  statusEffectsJson: string;
  visibility: 'Visible' | 'Hidden';
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

export type DiceRollMode = 'App' | 'Manual' | 'Hybrid';

export type ActionStatus =
  | 'Pending'
  | 'Published'
  | 'Rejected'
  | 'Cancelled'
  | 'DmReviewing'
  | 'AwaitingRoll'
  | 'RollReceived'
  | 'AwaitingReaction'
  | 'ReactionPending'
  | 'Resolving'
  | 'AwaitingFollowUpRoll';

export interface DiceRollResponse {
  spec: string;
  rolls: number[];
  total: number;
}

export interface SessionSummaryResponse {
  id: string;
  gameId: string;
  joinCode: string;
  joinUrl: string;
  isActive: boolean;
  state: string;
  diceRollMode: DiceRollMode;
  activeTurnParticipantId?: string | null;
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
  rollPrompts: RollPromptResponse[];
  combatEncounters?: CombatEncounterResponse[];
}

export interface CombatEncounterResponse {
  id: string;
  sequence: number;
  round: number;
  startedAt: string;
  endedAt?: string | null;
  isActive: boolean;
  promptedTurnCharacterId?: string | null;
}

export interface RollPromptResponse {
  id: string;
  isSessionPrompt?: boolean;
  actionRequestId?: string | null;
  actionSequence?: number | null;
  targetCharacterId: string;
  targetCharacterName: string;
  promptLabel?: string | null;
  guidanceText?: string | null;
  checkMode: 'Action' | 'Skill' | 'Attribute' | 'Custom' | string;
  resultKind?: 'PassFail' | 'Total' | string;
  actionKey?: string | null;
  skillKey?: string | null;
  attributeKey?: string | null;
  customCheckText?: string | null;
  status: 'Pending' | 'Completed' | 'Cancelled' | string;
  rollSummary?: string | null;
  rollResultJson?: string | null;
  chainStepKey?: string | null;
  autoResolveOutcome?: string | null;
  autoResolveMessage?: string | null;
  resultActionRequestId?: string | null;
  dc?: number | null;
  dmRolled?: boolean;
  createdAt: string;
  completedAt?: string | null;
}

export interface ActionRollData {
  individualRolls: number[];
  baseModifier: number;
  modifierKeys: string[];
  total: number;
  rollSummary?: string | null;
}

export interface ActionQueueItemResponse {
  id: string;
  sequence: number;
  actorName: string;
  actorCharacterId?: string | null;
  actorNpcId?: string | null;
  actionKey?: string | null;
  actionText: string;
  flavourText?: string | null;
  targetNpcId?: string | null;
  targetName?: string | null;
  description?: string | null;
  status: ActionStatus;
  resolutionText?: string | null;
  outcome?: 'Pass' | 'Fail' | null;
  rollSummary?: string | null;
  rollDataJson?: string | null;
  additionalActions?: string | null;
  statChangesJson: string;
  pendingChainEffectsJson?: string;
  followUpRolls?: RollPromptResponse[];
  combatEncounterId?: string | null;
  combatEncounterSequence?: number | null;
  isSkillCheckResponse?: boolean;
  skillCheckBatchId?: string | null;
  skillCheckGroupLabel?: string | null;
  rollMode: DiceRollMode;
  dmDifficultyModifier?: number | null;
  effectiveDc?: number | null;
  parentActionId?: string | null;
  followUpType?: 'reaction' | 'chain' | null;
  chainStep?: number | null;
  sessionModeAtSubmit?: string | null;
  combatRound?: number | null;
  submittedAt: string;
  publishedAt?: string | null;
  resolvedAt?: string | null;
}

export interface InitiativeEntryResponse {
  id: string;
  combatantType: string;
  combatantId: string;
  combatantName: string;
  sortOrder: number;
  initiativeScore: number;
  isCurrentTurn: boolean;
}

export interface CombatStartResponse {
  initiative: InitiativeEntryResponse[];
  rolls: InitiativeRollSummaryResponse[];
  guidanceText?: string | null;
}

export interface InitiativeRollSummaryResponse {
  combatantType: string;
  combatantId: string;
  combatantName: string;
  score: number;
  summary: string;
}

export interface RulesetInitiativeDefinition {
  attribute: string;
  skill: string;
  tieBreakerDice?: string;
  guidanceText?: string | null;
}

export interface SessionNoteResponse {
  id: string;
  sessionId: string;
  content: string;
  createdAt: string;
  updatedAt: string;
  sessionStartedAt: string;
  sessionEndedAt?: string | null;
  sessionIsActive: boolean;
  canEdit: boolean;
}

export interface SessionNotesContextResponse {
  sessionId: string;
  isSessionActive: boolean;
  currentNote?: SessionNoteResponse | null;
  previousNotes: SessionNoteResponse[];
}

export interface GameSessionNotesResponse {
  gameId: string;
  notes: SessionNoteResponse[];
}

export interface AdminUserReportResponse {
  userId: string;
  userName: string;
  email?: string | null;
  hasPasswordHash: boolean;
  gamesHostedCount: number;
}
