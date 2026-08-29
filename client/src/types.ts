export type CyclePhase = 'Menstrual' | 'Follicular' | 'Ovulatory' | 'Luteal';

export type FlowIntensity = 'None' | 'Light' | 'Medium' | 'Heavy';

export type Mood = 'Calm' | 'Happy' | 'Sensitive' | 'LowEnergy' | 'Anxious' | 'Irritable';

export interface UserProfile {
  userId: string;
  name: string;
  averageCycleLength: number;
  averagePeriodLength: number;
  lastPeriodStartDate: string;
  partnerTelegramChatId: number | null;
}

export interface CycleStatus {
  userId: string;
  name: string;
  averageCycleLength: number;
  averagePeriodLength: number;
  lastPeriodStartDate: string;
  partnerTelegramChatId: number | null;
  language: string;
  currentDay: number;
  phase: CyclePhase;
  phaseName: string;
  daysUntilNextPeriod: number;
  partnerAdvice: string;
  role: 'PrimaryUser' | 'Partner';
  isLinked: boolean;
  partnerName: string | null;
}

export interface DailyLogRequest {
  userId?: string;
  date?: string;
  flowIntensity: FlowIntensity;
  mood: Mood;
  painLevel: number;
  note: string;
}
