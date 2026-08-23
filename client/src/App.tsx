import { useState, useEffect } from 'react';
import { Settings, Plus, Sparkles, AlertCircle, Heart } from 'lucide-react';
import type { CycleStatus, FlowIntensity, Mood } from './types';
import { CycleRing } from './components/CycleRing';
import { PartnerInsights } from './components/PartnerInsights';
import { DailyLogDrawer } from './components/DailyLogDrawer';
import { SettingsModal } from './components/SettingsModal';
import { translations } from './translations';
import type { Language } from './translations';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000';

function App() {
  const [status, setStatus] = useState<CycleStatus | null>(null);
  const [lang, setLang] = useState<Language>('fa');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isLogOpen, setIsLogOpen] = useState(false);
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const t = translations[lang];

  // Auto-fetch status on mount
  useEffect(() => {
    fetchStatus();
  }, []);

  const fetchStatus = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`${API_BASE}/api/cycle/current`);
      if (!res.ok) {
        throw new Error(`Failed to load cycle data (Status: ${res.status})`);
      }
      const data = await res.json();
      setStatus(data);
      if (data.language) {
        setLang(data.language as Language);
      }
    } catch (err: any) {
      console.error(err);
      setError('Could not connect to Darya backend. Please ensure the backend is running.');
    } finally {
      setLoading(false);
    }
  };

  const handleSaveLog = async (logData: {
    flowIntensity: FlowIntensity;
    mood: Mood;
    painLevel: number;
    note: string;
  }) => {
    try {
      const res = await fetch(`${API_BASE}/api/logs`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          userId: status?.userId,
          date: new Date().toISOString(),
          ...logData,
        }),
      });
      if (!res.ok) {
        throw new Error('Failed to save daily log.');
      }
      await fetchStatus();
    } catch (err) {
      console.error(err);
      alert(lang === 'fa' ? 'خطا در ذخیره‌سازی وضعیت. مجدداً تلاش کنید.' : 'Error saving daily check-in. Please try again.');
    }
  };

  const handleSaveSettings = async (settingsData: {
    name: string;
    averageCycleLength: number;
    averagePeriodLength: number;
    lastPeriodStartDate: string;
    partnerTelegramChatId: number | null;
    language: string;
  }) => {
    try {
      const res = await fetch(`${API_BASE}/api/user/setup`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(settingsData),
      });
      if (!res.ok) {
        throw new Error('Failed to update setup.');
      }
      await fetchStatus();
    } catch (err) {
      console.error(err);
      alert(lang === 'fa' ? 'خطا در ذخیره تغییرات. لطفاً فیلدها را چک کنید.' : 'Error updating settings. Please verify inputs.');
    }
  };

  const handleToggleLanguage = async () => {
    if (!status) return;
    const newLang: Language = lang === 'fa' ? 'en' : 'fa';
    setLang(newLang);
    await handleSaveSettings({
      name: status.name,
      averageCycleLength: status.averageCycleLength,
      averagePeriodLength: status.averagePeriodLength,
      lastPeriodStartDate: status.lastPeriodStartDate,
      partnerTelegramChatId: status.partnerTelegramChatId,
      language: newLang,
    });
  };

  const handleSendTestAlert = async () => {
    if (!status) return;
    await handleSaveSettings({
      name: status.name,
      averageCycleLength: status.averageCycleLength,
      averagePeriodLength: status.averagePeriodLength,
      lastPeriodStartDate: status.lastPeriodStartDate,
      partnerTelegramChatId: status.partnerTelegramChatId,
      language: lang,
    });
  };

  return (
    <div 
      className="w-full min-h-screen flex flex-col justify-between max-w-md mx-auto p-4 select-none" 
      dir={lang === 'fa' ? 'rtl' : 'ltr'}
    >
      {/* Upper Navigation & App Identity */}
      <header className="flex items-center justify-between py-3 mb-2">
        <div className="flex items-center gap-2">
          <div className="w-9 h-9 rounded-full bg-gradient-to-tr from-pink-400 to-purple-500 flex items-center justify-center shadow-xs">
            <Heart className="w-4.5 h-4.5 text-white fill-white/80" />
          </div>
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-slate-800 m-0 leading-none">{t.appTitle}</h1>
            <span className="text-[10px] font-semibold text-slate-400 tracking-wider uppercase">{t.appSubtitle}</span>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {/* 1-Tap Language Quick Switcher */}
          <button
            onClick={handleToggleLanguage}
            className="px-2.5 py-1 text-xs rounded-xl font-bold bg-white/60 hover:bg-white border border-purple-100 text-slate-700 transition cursor-pointer shadow-2xs hover:scale-105 active:scale-95"
            title={lang === 'fa' ? 'Switch to English' : 'تغییر به فارسی'}
          >
            {lang === 'fa' ? 'EN' : 'FA'}
          </button>
          
          <button
            onClick={() => setIsSettingsOpen(true)}
            className="p-2 rounded-full bg-white/60 hover:bg-white/90 transition-all border border-purple-50 cursor-pointer shadow-2xs"
            title={lang === 'fa' ? 'تنظیمات' : 'Open Settings'}
          >
            <Settings className="w-5 h-5 text-slate-600" />
          </button>
        </div>
      </header>

      {/* Main Body content */}
      <main className="flex-1 flex flex-col justify-center my-auto">
        {loading && (
          <div className="flex flex-col items-center justify-center p-12 text-slate-500">
            <div className="w-8 h-8 border-4 border-purple-500 border-t-transparent rounded-full animate-spin mb-3" />
            <p className="text-sm font-medium">{t.loadingData}</p>
          </div>
        )}

        {error && (
          <div className="glass-card border-rose-100 text-rose-950 p-6 rounded-2xl text-center shadow-xs my-6">
            <AlertCircle className="w-10 h-10 text-rose-500 mx-auto mb-3" />
            <p className="font-semibold mb-2">{t.errorMsg || error}</p>
            <button
              onClick={fetchStatus}
              className="mt-2 text-xs bg-rose-500 hover:bg-rose-600 text-white font-semibold px-4 py-2 rounded-xl transition cursor-pointer"
            >
              {t.retryBtn}
            </button>
          </div>
        )}

        {!loading && !error && status && (
          <>
            {/* Visual Progress Ring */}
            <CycleRing status={status} lang={lang} />

            {/* Quick 1-Tap Log Trigger */}
            <div className="my-4 px-2">
              <button
                onClick={() => setIsLogOpen(true)}
                className="w-full flex items-center justify-center gap-2 bg-gradient-to-r from-pink-500 to-purple-600 hover:from-pink-600 hover:to-purple-700 text-white font-semibold py-4 rounded-2xl transition shadow-md shadow-purple-600/10 cursor-pointer text-base active:scale-[0.98]"
              >
                <Plus className="w-5 h-5" />
                <span>{t.logTodayBtn}</span>
              </button>
            </div>

            {/* Partner Info and Status Card */}
            <PartnerInsights
              status={status}
              lang={lang}
              onSendTestAlert={handleSendTestAlert}
            />
          </>
        )}
      </main>

      {/* App Footer */}
      <footer className="text-center py-4 text-[10px] text-slate-400 font-semibold tracking-wider uppercase mt-4">
        <div className="flex items-center justify-center gap-1.5 opacity-70">
          <Sparkles className="w-3.5 h-3.5 text-pink-400" />
          <span>{t.footerMsg}</span>
        </div>
      </footer>

      {/* Slide up Symptom Drawer */}
      <DailyLogDrawer
        isOpen={isLogOpen}
        lang={lang}
        onClose={() => setIsLogOpen(false)}
        onSaveLog={handleSaveLog}
      />

      {/* Settings Modal */}
      <SettingsModal
        isOpen={isSettingsOpen}
        onClose={() => setIsSettingsOpen(false)}
        status={status}
        lang={lang}
        onSaveSettings={handleSaveSettings}
      />
    </div>
  );
}

export default App;
