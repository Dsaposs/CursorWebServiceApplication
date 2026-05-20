export interface RollResultDieGroup {
  notation: string;
  label?: string;
  values: number[];
  isStress?: boolean;
}

export interface RollResultData {
  rollerKey?: string;
  resultKind?: 'PassFail' | 'Total' | string;
  groups: RollResultDieGroup[];
  total?: number;
  successes?: number;
  naturalDie?: number;
  pushed?: boolean;
  stressGained?: number;
}
