import React, { useState } from 'react';
import { Send, Heart, ShieldAlert, Sparkles, MessageSquareHeart } from 'lucide-react';
import type { CycleStatus } from '../types';
import { translations } from '../translations';
import type { Language } from '../translations';

interface PartnerInsightsProps {
  status: CycleStatus;
  lang: Language;
  onSendTestAlert: () => Promise<void>;
}

export const PartnerInsights: React.FC<PartnerInsightsProps> = ({ status, lang, onSendTestAlert }) => {
  const { partnerName, partnerTelegramChatId, partnerAdvice, phase } = status;
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);
  const t = translations[lang];

  const handleTestClick = async () => {
    setLoading(true);
    setSuccess(false);
    try {
      await onSendTestAlert();
      setSuccess(true);
      setTimeout(() => setSuccess(false), 4000);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const getPhaseColor = (p: string) => {
    switch (p) {
      case 'Menstrual': return 'border-rose-200 bg-rose-50/50 text-rose-800';
      case 'Follicular': return 'border-purple-200 bg-purple-50/50 text-purple-800';
      case 'Ovulatory': return 'border-pink-200 bg-pink-50/50 text-pink-800';
      case 'Luteal': return 'border-indigo-200 bg-indigo-50/50 text-indigo-800';
      default: return 'border-slate-200 bg-slate-50/50 text-slate-800';
    }
  };

  // Safe helper to replace name token in string
  const formatHowToSupport = (str: string, userName: string) => {
    return str.replace('{name}', userName);
  };

  const formatTelegramActive = (str: string, id: number) => {
    return str.replace('{id}', String(id));
  };

  return (
    <div className="glass-card rounded-2xl p-5 my-3 relative overflow-hidden transition-all duration-300">
      <div className="absolute top-0 right-0 w-24 h-24 bg-purple-200/20 rounded-full blur-2xl pointer-events-none" />
      
      <div className="flex items-center gap-2 mb-3">
        <Heart className="w-5 h-5 text-rose-500 fill-rose-500 animate-pulse" />
        <h2 className="text-lg font-bold text-slate-800 tracking-tight">{t.partnerInsights}</h2>
      </div>

      <div className={`border rounded-xl p-4 text-sm leading-relaxed ${getPhaseColor(phase)}`}>
        <div className="flex items-start gap-2.5">
          <MessageSquareHeart className="w-5 h-5 mt-0.5 shrink-0 opacity-80" />
          <div>
            <p className="font-semibold mb-1">{formatHowToSupport(t.howToSupport, partnerName || 'Partner')}</p>
            <p className="opacity-90">{partnerAdvice}</p>
          </div>
        </div>
      </div>

      <div className="mt-4 pt-3 border-t border-purple-50 flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs">
        <div className="flex items-center gap-2">
          {partnerTelegramChatId ? (
            <>
              <div className="w-2.5 h-2.5 rounded-full bg-emerald-500 animate-ping absolute" />
              <div className="w-2.5 h-2.5 rounded-full bg-emerald-500" />
              <span className="text-slate-600 font-medium">
                {formatTelegramActive(t.telegramActive, partnerTelegramChatId)}
              </span>
            </>
          ) : (
            <>
              <ShieldAlert className="w-4 h-4 text-amber-500" />
              <span className="text-slate-500 font-medium">
                {t.telegramInactive}
              </span>
            </>
          )}
        </div>

        {partnerTelegramChatId && (
          <button
            onClick={handleTestClick}
            disabled={loading || success}
            className={`flex items-center justify-center gap-1.5 px-3 py-1.5 rounded-lg font-medium transition-all shadow-xs border cursor-pointer ${
              success
                ? 'bg-emerald-50 border-emerald-200 text-emerald-700'
                : 'bg-white hover:bg-purple-50 text-purple-700 border-purple-100'
            }`}
          >
            {success ? (
              <>
                <Sparkles className="w-3.5 h-3.5 text-emerald-500" />
                <span>{t.testAlertSent}</span>
              </>
            ) : (
              <>
                <Send className={`w-3.5 h-3.5 ${loading ? 'animate-bounce' : ''}`} />
                <span>{loading ? t.sending : t.testAlertBtn}</span>
              </>
            )}
          </button>
        )}
      </div>
    </div>
  );
};
