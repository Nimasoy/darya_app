import React, { useState } from 'react';
import { X, Check, Activity, MessageSquare } from 'lucide-react';
import type { FlowIntensity, Mood } from '../types';
import { translations } from '../translations';
import type { Language } from '../translations';

interface DailyLogDrawerProps {
  isOpen: boolean;
  lang: Language;
  onClose: () => void;
  onSaveLog: (log: {
    flowIntensity: FlowIntensity;
    mood: Mood;
    painLevel: number;
    note: string;
  }) => Promise<void>;
}

export const DailyLogDrawer: React.FC<DailyLogDrawerProps> = ({ isOpen, lang, onClose, onSaveLog }) => {
  const [mood, setMood] = useState<Mood>('Calm');
  const [painLevel, setPainLevel] = useState<number>(1);
  const [flow, setFlow] = useState<FlowIntensity>('Medium');
  const [note, setNote] = useState<string>('');
  const [submitting, setSubmitting] = useState(false);
  const t = translations[lang];

  if (!isOpen) return null;

  const moodOptions: { type: Mood; emoji: string; label: string }[] = [
    { type: 'Calm', emoji: '🧘', label: t.Calm },
    { type: 'Happy', emoji: '☀️', label: t.Happy },
    { type: 'Sensitive', emoji: '💖', label: t.Sensitive },
    { type: 'LowEnergy', emoji: '🥱', label: t.LowEnergy },
    { type: 'Anxious', emoji: '💭', label: t.Anxious },
    { type: 'Irritable', emoji: '⚡', label: t.Irritable },
  ];

  const flowOptions: { type: FlowIntensity; label: string }[] = [
    { type: 'None', label: t.none },
    { type: 'Light', label: t.Light },
    { type: 'Medium', label: t.Medium },
    { type: 'Heavy', label: t.Heavy },
  ];

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      await onSaveLog({
        flowIntensity: flow,
        mood,
        painLevel,
        note,
      });
      setNote(''); // reset note
      onClose();
    } catch (err) {
      console.error(err);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center animate-fade-in" dir={lang === 'fa' ? 'rtl' : 'ltr'}>
      {/* Backdrop */}
      <div 
        className="absolute inset-0 bg-slate-900/40 backdrop-blur-xs cursor-pointer"
        onClick={onClose}
      />

      {/* Drawer Panel */}
      <div className="relative w-full max-w-md bg-white rounded-t-3xl shadow-2xl p-6 z-10 animate-slide-up max-h-[92vh] overflow-y-auto no-scrollbar border-t border-purple-100">
        
        {/* Pull bar */}
        <div className="w-12 h-1.5 bg-slate-200 rounded-full mx-auto mb-5" />

        {/* Header */}
        <div className="flex items-center justify-between mb-6">
          <div className="flex items-center gap-2">
            <Activity className="w-5 h-5 text-purple-600" />
            <h2 className="text-xl font-bold text-slate-800 tracking-tight">{t.checkinTitle}</h2>
          </div>
          <button 
            onClick={onClose}
            className="p-1.5 rounded-full hover:bg-slate-100 text-slate-400 hover:text-slate-600 transition cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-6">
          {/* Mood Section */}
          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-2.5">
              {t.feelingLabel}
            </label>
            <div className="grid grid-cols-3 gap-2">
              {moodOptions.map((opt) => (
                <button
                  key={opt.type}
                  type="button"
                  onClick={() => setMood(opt.type)}
                  className={`flex flex-col items-center justify-center p-3 rounded-xl border-2 transition-all cursor-pointer ${
                    mood === opt.type
                      ? 'border-purple-500 bg-purple-50/50 scale-[1.02] shadow-xs'
                      : 'border-slate-100 hover:border-slate-200 bg-slate-50/50'
                  }`}
                >
                  <span className="text-2xl mb-1 select-none">{opt.emoji}</span>
                  <span className="text-xs font-semibold text-slate-700">{opt.label}</span>
                </button>
              ))}
            </div>
          </div>

          {/* Flow Intensity Section */}
          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-2.5">
              {t.flowLabel}
            </label>
            <div className="grid grid-cols-4 gap-2">
              {flowOptions.map((opt) => (
                <button
                  key={opt.type}
                  type="button"
                  onClick={() => setFlow(opt.type)}
                  className={`py-2 rounded-xl font-semibold border text-xs text-center transition-all cursor-pointer ${
                    flow === opt.type
                      ? 'bg-purple-600 text-white border-purple-600 shadow-xs'
                      : 'bg-white text-slate-700 border-slate-200 hover:bg-slate-50'
                  }`}
                >
                  {opt.label}
                </button>
              ))}
            </div>
          </div>

          {/* Pain Level Section */}
          <div>
            <div className="flex justify-between items-center mb-1">
              <label className="text-sm font-semibold text-slate-700">
                {t.painLabel}
              </label>
              <span className="text-xs font-bold px-2 py-0.5 bg-slate-100 rounded-full text-slate-700" dir="ltr">
                {painLevel} / 5
              </span>
            </div>
            <div className="flex items-center gap-3">
              <span className="text-xs font-medium text-slate-400">{t.none}</span>
              <input
                type="range"
                min="0"
                max="5"
                value={painLevel}
                onChange={(e) => setPainLevel(parseInt(e.target.value))}
                className="w-full h-2 bg-slate-100 rounded-lg appearance-none cursor-pointer accent-purple-600"
              />
              <span className="text-xs font-medium text-slate-400">{t.severe}</span>
            </div>
          </div>

          {/* Daily Note Section */}
          <div>
            <label className="flex items-center gap-1.5 text-sm font-semibold text-slate-700 mb-2">
              <MessageSquare className="w-4 h-4 text-slate-400" />
              <span>{t.notesLabel}</span>
            </label>
            <textarea
              value={note}
              onChange={(e) => setNote(e.target.value)}
              placeholder={t.notesPlaceholder}
              rows={3}
              className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 text-sm text-slate-700 bg-slate-50/30"
            />
          </div>

          {/* Submit Button */}
          <button
            type="submit"
            disabled={submitting}
            className="w-full flex items-center justify-center gap-2 bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-700 hover:to-indigo-700 text-white font-semibold py-3.5 rounded-xl transition shadow-md shadow-purple-600/10 cursor-pointer text-base active:scale-[0.98] disabled:opacity-70 disabled:cursor-not-allowed"
          >
            <Check className="w-5 h-5" />
            <span>{submitting ? t.savingCheckin : t.saveCheckin}</span>
          </button>
        </form>
      </div>
    </div>
  );
};
