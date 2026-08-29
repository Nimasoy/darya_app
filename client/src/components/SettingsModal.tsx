import React, { useState, useEffect } from 'react';
import { X, Save, Calendar, RefreshCw, Send, LogOut, Link, Check, Code } from 'lucide-react';
import type { CycleStatus } from '../types';
import { translations } from '../translations';
import type { Language } from '../translations';

interface SettingsModalProps {
  isOpen: boolean;
  onClose: () => void;
  status: CycleStatus | null;
  lang: Language;
  partnerCodeState: string | null;
  onSaveSettings: (settings: {
    name: string;
    averageCycleLength: number;
    averagePeriodLength: number;
    lastPeriodStartDate: string;
    partnerTelegramChatId: number | null;
    language: string;
  }) => Promise<void>;
  onGenerateCode: () => Promise<void>;
  onLinkPartner: (code: string) => Promise<void>;
  onLogout: () => void;
}

export const SettingsModal: React.FC<SettingsModalProps> = ({
  isOpen,
  onClose,
  status,
  lang,
  partnerCodeState,
  onSaveSettings,
  onGenerateCode,
  onLinkPartner,
  onLogout,
}) => {
  const [name, setName] = useState('User');
  const [cycleLength, setCycleLength] = useState(28);
  const [periodLength, setPeriodLength] = useState(5);
  const [lastPeriodDate, setLastPeriodDate] = useState('');
  const [chatId, setChatId] = useState<string>('');
  const [language, setLanguage] = useState<string>('fa');
  const [linkInputCode, setLinkInputCode] = useState('');
  
  const [submitting, setSubmitting] = useState(false);
  const [linking, setLinking] = useState(false);
  const [showNotificationTip, setShowNotificationTip] = useState(false);
  const t = translations[lang];

  useEffect(() => {
    if (status) {
      setName(status.name || 'User');
      setCycleLength(status.averageCycleLength || 28);
      setPeriodLength(status.averagePeriodLength || 5);
      setLanguage(status.language || 'fa');
      
      if (status.lastPeriodStartDate) {
        const dateObj = new Date(status.lastPeriodStartDate);
        const formattedDate = dateObj.toISOString().split('T')[0];
        setLastPeriodDate(formattedDate);
      }
      
      setChatId(status.partnerTelegramChatId ? String(status.partnerTelegramChatId) : '');
    }
  }, [status, isOpen]);

  if (!isOpen) return null;

  const isPrimary = status?.role === 'PrimaryUser';
  const isLinked = status?.isLinked;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      const parsedChatId = chatId.trim() ? parseInt(chatId.trim(), 10) : null;
      await onSaveSettings({
        name,
        averageCycleLength: cycleLength,
        averagePeriodLength: periodLength,
        lastPeriodStartDate: lastPeriodDate,
        partnerTelegramChatId: isNaN(parsedChatId as any) ? null : parsedChatId,
        language,
      });
      onClose();
    } catch (err) {
      console.error(err);
    } finally {
      setSubmitting(false);
    }
  };

  const handleLinkSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!linkInputCode.trim()) return;
    setLinking(true);
    try {
      await onLinkPartner(linkInputCode.trim());
      setLinkInputCode('');
    } catch (err) {
      console.error(err);
    } finally {
      setLinking(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 animate-fade-in" dir={lang === 'fa' ? 'rtl' : 'ltr'}>
      {/* Backdrop */}
      <div 
        className="absolute inset-0 bg-slate-900/40 backdrop-blur-xs cursor-pointer"
        onClick={onClose}
      />

      {/* Modal Container */}
      <div className="relative w-full max-w-md bg-white rounded-2xl shadow-2xl p-6 z-10 max-h-[92vh] overflow-y-auto no-scrollbar border border-purple-100">
        
        {/* Header */}
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-xl font-bold text-slate-800 tracking-tight">{t.settingsTitle}</h2>
          <button 
            onClick={onClose}
            className="p-1.5 rounded-full hover:bg-slate-100 text-slate-400 hover:text-slate-600 transition cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Profile and Preferences Form */}
        <form onSubmit={handleSubmit} className="space-y-4">
          
          {/* Name Field */}
          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-1">
              {t.yourName}
            </label>
            <input
              type="text"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="w-full px-3.5 py-2 rounded-xl border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 text-slate-800 text-sm bg-slate-50/20"
            />
          </div>

          {/* Language Selection Dropdown */}
          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-1">
              {t.languageLabel}
            </label>
            <select
              value={language}
              onChange={(e) => setLanguage(e.target.value)}
              className="w-full px-3.5 py-2 rounded-xl border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 text-slate-800 text-sm bg-slate-50/20 cursor-pointer"
            >
              <option value="fa">{t.persian}</option>
              <option value="en">{t.english}</option>
            </select>
          </div>

          {/* Cycle Configurations - ONLY show for PrimaryUser trackers */}
          {isPrimary && (
            <>
              {/* Last Period Start Date Field */}
              <div>
                <label className="flex items-center gap-1 text-sm font-semibold text-slate-700 mb-1">
                  <Calendar className="w-4 h-4 text-purple-500" />
                  <span>{t.lastPeriodStart}</span>
                </label>
                <input
                  type="date"
                  required
                  value={lastPeriodDate}
                  onChange={(e) => setLastPeriodDate(e.target.value)}
                  className="w-full px-3.5 py-2 rounded-xl border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 text-slate-800 text-sm bg-slate-50/20"
                />
              </div>

              {/* Double column lengths */}
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="flex items-center gap-1 text-sm font-semibold text-slate-700 mb-1">
                    <RefreshCw className="w-3.5 h-3.5 text-purple-500" />
                    <span>{t.cycleLen}</span>
                  </label>
                  <div className="relative">
                    <input
                      type="number"
                      required
                      min="20"
                      max="45"
                      value={cycleLength}
                      onChange={(e) => setCycleLength(parseInt(e.target.value) || 28)}
                      className="w-full pl-3.5 pr-12 py-2 rounded-xl border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 text-slate-800 text-sm bg-slate-50/20"
                    />
                    <span className="absolute right-3.5 top-2.5 text-xs text-slate-400 font-semibold">{t.days}</span>
                  </div>
                </div>

                <div>
                  <label className="flex items-center gap-1 text-sm font-semibold text-slate-700 mb-1">
                    <Calendar className="w-3.5 h-3.5 text-purple-500" />
                    <span>{t.periodLen}</span>
                  </label>
                  <div className="relative">
                    <input
                      type="number"
                      required
                      min="3"
                      max="12"
                      value={periodLength}
                      onChange={(e) => setPeriodLength(parseInt(e.target.value) || 5)}
                      className="w-full pl-3.5 pr-12 py-2 rounded-xl border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 text-slate-800 text-sm bg-slate-50/20"
                    />
                    <span className="absolute right-3.5 top-2.5 text-xs text-slate-400 font-semibold">{t.days}</span>
                  </div>
                </div>
              </div>

              {/* Partner Telegram Chat ID Field */}
              <div>
                <div className="flex justify-between items-center mb-1">
                  <label className="flex items-center gap-1 text-sm font-semibold text-slate-700">
                    <Send className="w-3.5 h-3.5 text-purple-500" />
                    <span>{t.partnerChatId}</span>
                  </label>
                  <button
                    type="button"
                    onClick={() => setShowNotificationTip(!showNotificationTip)}
                    className="text-xs text-purple-600 hover:text-purple-700 font-semibold underline cursor-pointer"
                  >
                    {t.howToGetId}
                  </button>
                </div>
                <input
                  type="text"
                  placeholder="e.g. 581938928 (optional)"
                  value={chatId}
                  onChange={(e) => setChatId(e.target.value)}
                  className="w-full px-3.5 py-2 rounded-xl border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 text-slate-800 text-sm bg-slate-50/20"
                />
                
                {showNotificationTip && (
                  <div className="mt-2 p-3 bg-purple-50 border border-purple-100 rounded-xl text-xs text-purple-950 leading-relaxed animate-fade-in">
                    <p className="font-semibold mb-1">{t.howToLinkTitle}</p>
                    <ol className="list-decimal pl-4 space-y-1">
                      {t.howToLinkSteps.map((step, idx) => (
                        <li key={idx}>{step}</li>
                      ))}
                    </ol>
                  </div>
                )}
              </div>
            </>
          )}

          {/* Submit Action */}
          <button
            type="submit"
            disabled={submitting}
            className="w-full flex items-center justify-center gap-2 bg-purple-600 hover:bg-purple-700 text-white font-semibold py-2.5 rounded-xl transition shadow-md shadow-purple-600/10 cursor-pointer mt-1 disabled:opacity-75"
          >
            <Save className="w-4 h-4" />
            <span>{submitting ? t.savingSettings : t.saveSettings}</span>
          </button>
        </form>

        {/* ========================================== */}
        {/* PARTNER LINKING SECTION (Sprint 1 Refactor) */}
        {/* ========================================== */}
        <div className="mt-6 pt-5 border-t border-slate-100">
          <div className="flex items-center gap-2 mb-3">
            <Link className="w-4 h-4 text-purple-600" />
            <h3 className="text-sm font-bold text-slate-800">{t.partnerLinkSection}</h3>
          </div>

          {isLinked ? (
            <div className="p-3 bg-emerald-50 border border-emerald-100 rounded-xl text-xs text-emerald-800 font-semibold flex items-center gap-2">
              <Check className="w-4 h-4 text-emerald-600" />
              <span>{t.linkedStatus.replace('{name}', status?.partnerName || 'Partner')}</span>
            </div>
          ) : (
            <div>
              {/* Case A: Primary user - generates code */}
              {isPrimary ? (
                <div className="space-y-3">
                  <p className="text-xs text-slate-500 leading-relaxed">{t.linkCodeDesc}</p>
                  
                  {partnerCodeState ? (
                    <div className="flex items-center justify-between p-3.5 bg-purple-50 border-2 border-dashed border-purple-200 rounded-xl">
                      <span className="text-xs font-semibold text-slate-500 uppercase">{t.linkCodeLabel}</span>
                      <span className="text-lg font-extrabold text-purple-700 tracking-widest uppercase font-mono select-all">
                        {partnerCodeState}
                      </span>
                    </div>
                  ) : (
                    <button
                      type="button"
                      onClick={onGenerateCode}
                      className="w-full flex items-center justify-center gap-1.5 py-2.5 bg-white border border-purple-200 text-purple-700 hover:bg-purple-50 text-xs font-semibold rounded-xl transition cursor-pointer"
                    >
                      <Code className="w-4 h-4" />
                      <span>{t.generateCodeBtn}</span>
                    </button>
                  )}
                </div>
              ) : (
                /* Case B: Partner user - enters code to link */
                <form onSubmit={handleLinkSubmit} className="space-y-3">
                  <p className="text-xs text-slate-500 leading-relaxed">{t.notLinkedStatus}</p>
                  <div className="flex gap-2">
                    <input
                      type="text"
                      maxLength={6}
                      required
                      placeholder={t.partnerCodePlaceholder}
                      value={linkInputCode}
                      onChange={(e) => setLinkInputCode(e.target.value.toUpperCase())}
                      className="flex-1 px-3 py-2 rounded-xl border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 text-center uppercase tracking-widest font-mono text-sm bg-slate-50/20"
                    />
                    <button
                      type="submit"
                      disabled={linking || !linkInputCode.trim()}
                      className="px-4 py-2 bg-purple-600 hover:bg-purple-700 text-white font-semibold text-xs rounded-xl transition cursor-pointer disabled:opacity-50"
                    >
                      {linking ? t.connecting : t.linkPartnerBtn}
                    </button>
                  </div>
                </form>
              )}
            </div>
          )}
        </div>

        {/* LOGOUT BUTTON */}
        <div className="mt-5 pt-4 border-t border-slate-100 flex justify-end">
          <button
            type="button"
            onClick={onLogout}
            className="flex items-center gap-1.5 px-4 py-2 rounded-xl border border-rose-200 hover:bg-rose-50 text-rose-700 text-xs font-bold transition cursor-pointer active:scale-95"
          >
            <LogOut className="w-3.5 h-3.5" />
            <span>{t.logoutBtn}</span>
          </button>
        </div>

      </div>
    </div>
  );
};
