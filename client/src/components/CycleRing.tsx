import React from 'react';
import type { CycleStatus, CyclePhase } from '../types';
import { translations } from '../translations';
import type { Language } from '../translations';

interface CycleRingProps {
  status: CycleStatus;
  lang: Language;
}

export const CycleRing: React.FC<CycleRingProps> = ({ status, lang }) => {
  const { currentDay, averageCycleLength, phase, daysUntilNextPeriod } = status;
  const t = translations[lang];
  
  // SVG configuration
  const radius = 110;
  const strokeWidth = 14;
  const circumference = 2 * Math.PI * radius;
  
  // Calculate percentage of cycle completed
  const percentage = Math.min(1, Math.max(0, currentDay / averageCycleLength));
  const strokeDashoffset = circumference - percentage * circumference;

  // Visual tokens based on cycle phase
  const getPhaseStyles = (p: CyclePhase) => {
    switch (p) {
      case 'Menstrual':
        return {
          gradientId: 'menstrual-grad',
          gradientStart: '#fda4af', // rose-300
          gradientEnd: '#f43f5e',   // rose-500
          textClass: 'text-rose-500',
          bgClass: 'bg-rose-50 border-rose-200 text-rose-700',
          phaseLabel: t.Menstrual,
          desc: t.MenstrualDesc
        };
      case 'Follicular':
        return {
          gradientId: 'follicular-grad',
          gradientStart: '#d8b4fe', // lavender
          gradientEnd: '#8b5cf6',   // violet-500
          textClass: 'text-purple-600',
          bgClass: 'bg-purple-50 border-purple-200 text-purple-700',
          phaseLabel: t.Follicular,
          desc: t.FollicularDesc
        };
      case 'Ovulatory':
        return {
          gradientId: 'ovulatory-grad',
          gradientStart: '#fed7aa', // orange-200
          gradientEnd: '#db2777',   // pink-600
          textClass: 'text-pink-600',
          bgClass: 'bg-pink-50 border-pink-200 text-pink-700',
          phaseLabel: t.Ovulatory,
          desc: t.OvulatoryDesc
        };
      case 'Luteal':
        return {
          gradientId: 'luteal-grad',
          gradientStart: '#a5f3fc', // cyan-200
          gradientEnd: '#6366f1',   // indigo-500
          textClass: 'text-indigo-600',
          bgClass: 'bg-indigo-50 border-indigo-200 text-indigo-700',
          phaseLabel: t.Luteal,
          desc: t.LutealDesc
        };
      default:
        return {
          gradientId: 'default-grad',
          gradientStart: '#cbd5e1',
          gradientEnd: '#64748b',
          textClass: 'text-slate-600',
          bgClass: 'bg-slate-50 border-slate-200 text-slate-700',
          phaseLabel: 'Unknown Phase',
          desc: ''
        };
    }
  };

  const styles = getPhaseStyles(phase);

  return (
    <div className="flex flex-col items-center justify-center p-6 my-4 animate-fade-in">
      <div className="relative w-[280px] h-[280px] flex items-center justify-center">
        {/* Soft decorative background glow */}
        <div 
          className="absolute inset-4 rounded-full filter blur-xl opacity-20 transition-all duration-700"
          style={{
            background: `radial-gradient(circle, ${styles.gradientStart} 0%, ${styles.gradientEnd} 100%)`
          }}
        />

        {/* SVG Progress Ring */}
        <svg className="w-full h-full transform -rotate-90 drop-shadow-sm select-none">
          <defs>
            <linearGradient id={styles.gradientId} x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stopColor={styles.gradientStart} />
              <stop offset="100%" stopColor={styles.gradientEnd} />
            </linearGradient>
          </defs>
          {/* Background circle track */}
          <circle
            cx="140"
            cy="140"
            r={radius}
            stroke="rgba(241, 237, 247, 0.7)"
            strokeWidth={strokeWidth}
            fill="transparent"
          />
          {/* Progress circle */}
          <circle
            cx="140"
            cy="140"
            r={radius}
            stroke={`url(#${styles.gradientId})`}
            strokeWidth={strokeWidth}
            fill="transparent"
            strokeDasharray={circumference}
            strokeDashoffset={strokeDashoffset}
            strokeLinecap="round"
            className="transition-all duration-1000 ease-out"
          />
        </svg>

        {/* Center Text Panel */}
        <div className="absolute inset-0 flex flex-col items-center justify-center text-center select-none">
          <span className="text-slate-400 text-xs font-semibold uppercase tracking-wider">
            {t.cycleDay}
          </span>
          <div className="flex items-baseline mt-1 mb-0.5" dir="ltr">
            <span className={`text-5xl font-bold tracking-tight ${styles.textClass}`}>
              {currentDay}
            </span>
            <span className="text-slate-400 text-sm font-semibold ml-1">
              / {averageCycleLength}
            </span>
          </div>
          
          <span className={`px-3 py-1 rounded-full text-xs font-semibold border ${styles.bgClass} mt-2 shadow-xs transition-all duration-300`}>
            {styles.phaseLabel}
          </span>
          
          <span className="text-slate-500 text-xs mt-1.5 font-medium italic">
            {styles.desc}
          </span>
        </div>
      </div>

      {/* Countdown subtitle */}
      <div className="mt-5 text-center">
        <p className="text-sm text-slate-500 font-medium">
          {t.daysUntilNext}{' '}
          <span className={`font-semibold text-base ${styles.textClass}`}>
            {daysUntilNextPeriod}
          </span>{' '}
          {daysUntilNextPeriod === 1 ? t.day : t.days}
        </p>
      </div>
    </div>
  );
};
