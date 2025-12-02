export interface DayOfWeekCount {
  dayName: string;
  count: number;
}

export interface StatsOverview {
  totalDraws: number;
  firstDrawDate?: string | null;
  lastDrawDate?: string | null;
  drawsPerDayOfWeek: DayOfWeekCount[];
}

export interface NumberFrequency {
  number: number;
  count: number;
  frequency: number;
}

export interface StatsFrequencies {
  mainNumbers: NumberFrequency[];
  luckyNumbers: NumberFrequency[];
}

export interface SumBucket {
  minInclusive: number;
  maxInclusive: number;
  count: number;
}

export interface PatternDistribution {
  sumBuckets: SumBucket[];
  evenCountDistribution: { [evenCount: number]: number };
  lowCountDistribution: { [lowCount: number]: number };
}

export interface CooccurringNumber {
  number: number;
  cooccurrenceCount: number;
  conditionalProbability: number;
  globalProbability: number;
}

export interface CooccurrenceStats {
  baseNumber: number;
  totalDraws: number;
  drawsContainingBase: number;
  cooccurrences: CooccurringNumber[];
}
