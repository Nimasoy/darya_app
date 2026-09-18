import { useState, useEffect } from 'react';
import { Settings, Plus, Sparkles, AlertCircle, Heart, User as UserIcon, Mail, Lock, Link } from 'lucide-react';
import { Capacitor } from '@capacitor/core';
import { PushNotifications } from '@capacitor/push-notifications';
import type { CycleStatus, FlowIntensity, Mood } from './types';
import { CycleRing } from './components/CycleRing';
import { PartnerInsights } from './components/PartnerInsights';
import { DailyLogDrawer } from './components/DailyLogDrawer';
import { SettingsModal } from './components/SettingsModal';
import { translations } from './translations';
import type { Language } from './translations';

const API_BASE = import.meta.env.VITE_API_URL || 
  (Capacitor.isNativePlatform() || (typeof window !== 'undefined' && window.location.hostname !== 'localhost' && window.location.hostname !== '127.0.0.1')
    ? 'https://api.darya-app.space'
    : 'http://localhost:5000');

function App() {
  const [token, setToken] = useState<string | null>(localStorage.getItem('darya_token'));
  const [status, setStatus] = useState<CycleStatus | null>(null);
  const [lang, setLang] = useState<Language>('fa');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  // Auth state
  const [isRegister, setIsRegister] = useState(false);
  const [authEmail, setAuthEmail] = useState('');
  const [authPassword, setAuthPassword] = useState('');
  const [authName, setAuthName] = useState('');
  const [authRole, setAuthRole] = useState<'PrimaryUser' | 'Partner'>('PrimaryUser');
  const [authLoading, setAuthLoading] = useState(false);
  const [authError, setAuthError] = useState<string | null>(null);
  
  // Modal / Drawer toggle state
  const [isLogOpen, setIsLogOpen] = useState(false);
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [partnerCodeState, setPartnerCodeState] = useState<string | null>(null);

  const t = translations[lang];

  // Push notifications auto-registration for native Android / iOS and Web
  useEffect(() => {
    if (!token) return;

    let isMounted = true;

    if (!Capacitor.isNativePlatform()) {
      // Web Browser Notification Permission
      if (typeof window !== 'undefined' && 'Notification' in window) {
        if (Notification.permission === 'default') {
          Notification.requestPermission().then(res => {
            console.log('Web notification permission result:', res);
          }).catch(err => {
            console.warn('Web notification permission error:', err);
          });
        }
      }
      return;
    }

    // Native Capacitor Push Notifications
    const regSub = PushNotifications.addListener('registration', async (fcmToken) => {
      if (!isMounted) return;
      console.log('FCM registration token successfully acquired:', fcmToken.value);
      try {
        const res = await fetch(`${API_BASE}/api/notifications/device-token`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${token}`
          },
          body: JSON.stringify({
            token: fcmToken.value,
            platform: Capacitor.getPlatform()
          })
        });
        console.log('Server device token registration response status:', res.status);
      } catch (err) {
        console.error('Failed to sync device push token to backend:', err);
      }
    });

    const errSub = PushNotifications.addListener('registrationError', (error) => {
      console.error('Push notification registration error:', error);
    });

    const receiveSub = PushNotifications.addListener('pushNotificationReceived', (notif) => {
      console.log('Push notification received in foreground:', notif);
    });

    // Request permissions and register
    const setupPush = async () => {
      try {
        let permStatus = await PushNotifications.checkPermissions();
        if (permStatus.receive !== 'granted') {
          permStatus = await PushNotifications.requestPermissions();
        }

        if (permStatus.receive === 'granted') {
          await PushNotifications.register();
        } else {
          console.warn('Push notification permission was not granted:', permStatus.receive);
        }
      } catch (err) {
        console.warn('Push notification initialization error:', err);
      }
    };

    setupPush();

    return () => {
      isMounted = false;
      regSub.then(sub => sub.remove()).catch(() => {});
      errSub.then(sub => sub.remove()).catch(() => {});
      receiveSub.then(sub => sub.remove()).catch(() => {});
    };
  }, [token]);

  // Auto-fetch status on mount if token exists
  useEffect(() => {
    if (token) {
      fetchStatus();
    }
  }, [token]);

  const fetchStatus = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`${API_BASE}/api/cycle/current`, {
        headers: {
          'Authorization': `Bearer ${token}`
        }
      });
      if (res.status === 401) {
        handleLogout();
        return;
      }
      if (!res.ok) {
        throw new Error(`Failed to load cycle data`);
      }
      const data = await res.json();
      setStatus(data);
      if (data.language) {
        setLang(data.language as Language);
      }
      // If code was generated in current session, keep it, otherwise clear or pull
      if (data.partnerCode) {
        setPartnerCodeState(data.partnerCode);
      }
    } catch (err: any) {
      console.error(err);
      setError(t.errorMsg || 'Could not connect to Darya backend. Please ensure the backend is running.');
    } finally {
      setLoading(false);
    }
  };

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    setAuthLoading(true);
    setAuthError(null);
    try {
      const res = await fetch(`${API_BASE}/api/auth/register`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          email: authEmail,
          password: authPassword,
          name: authName,
          role: authRole
        })
      });
      const data = await res.json();
      if (!res.ok) {
        throw new Error(data.message || 'Registration failed.');
      }
      localStorage.setItem('darya_token', data.token);
      setToken(data.token);
    } catch (err: any) {
      console.error(err);
      setAuthError(err.message || 'Registration failed. Try again.');
    } finally {
      setAuthLoading(false);
    }
  };

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setAuthLoading(true);
    setAuthError(null);
    try {
      const res = await fetch(`${API_BASE}/api/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          email: authEmail,
          password: authPassword
        })
      });
      const data = await res.json();
      if (!res.ok) {
        throw new Error(data.message || 'Login failed.');
      }
      localStorage.setItem('darya_token', data.token);
      setToken(data.token);
    } catch (err: any) {
      console.error(err);
      setAuthError(err.message || 'Invalid email or password.');
    } finally {
      setAuthLoading(false);
    }
  };

  const handleLogout = () => {
    localStorage.removeItem('darya_token');
    setToken(null);
    setStatus(null);
    setPartnerCodeState(null);
    setAuthEmail('');
    setAuthPassword('');
    setAuthName('');
    setIsRegister(false);
    setIsSettingsOpen(false);
    setIsLogOpen(false);
  };

  const handleGenerateCode = async () => {
    try {
      const res = await fetch(`${API_BASE}/api/partner/generate-code`, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${token}` }
      });
      if (!res.ok) throw new Error('Failed to generate code.');
      const data = await res.json();
      setPartnerCodeState(data.code);
    } catch (err) {
      console.error(err);
      alert('Error generating partner link code.');
    }
  };

  const handleLinkPartner = async (code: string) => {
    try {
      const res = await fetch(`${API_BASE}/api/partner/link`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`
        },
        body: JSON.stringify({ code })
      });
      const data = await res.json();
      if (!res.ok) {
        throw new Error(data.message || 'Linking failed.');
      }
      alert(t.successLinked);
      await fetchStatus();
    } catch (err: any) {
      console.error(err);
      alert(err.message || 'Error linking account.');
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
          'Authorization': `Bearer ${token}`
        },
        body: JSON.stringify({
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
          'Authorization': `Bearer ${token}`
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

  const handleSendTestPush = async () => {
    if (!token) return;

    if (!Capacitor.isNativePlatform() && typeof window !== 'undefined' && 'Notification' in window) {
      if (Notification.permission === 'granted') {
        new Notification('🌸 Darya Push Test', {
          body: 'Web browser notifications are working successfully!',
          icon: '/favicon.ico'
        });
      }
    }

    const res = await fetch(`${API_BASE}/api/notifications/test-push`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${token}`
      }
    });

    if (!res.ok) {
      const errData = await res.json().catch(() => ({}));
      throw new Error(errData.message || 'Failed to send test push notification');
    }
  };

  // ==========================================
  // VIEW RENDERER A: Authentication screens
  // ==========================================
  if (!token) {
    return (
      <div 
        className="w-full min-h-screen flex flex-col justify-center items-center max-w-md mx-auto p-6"
        dir={lang === 'fa' ? 'rtl' : 'ltr'}
      >
        <div className="w-full glass-card rounded-2xl p-6 shadow-2xl border border-purple-100 animate-fade-in">
          {/* Brand Logo & Name */}
          <div className="flex flex-col items-center mb-6 text-center select-none">
            <div className="w-12 h-12 rounded-full bg-gradient-to-tr from-pink-400 to-purple-500 flex items-center justify-center shadow-md mb-2">
              <Heart className="w-6 h-6 text-white fill-white/80" />
            </div>
            <h1 className="text-3xl font-extrabold tracking-tight text-slate-800 m-0 leading-none">{t.appTitle}</h1>
            <span className="text-xs font-semibold text-slate-400 tracking-wider uppercase mt-1">{t.appSubtitle}</span>
          </div>

          <h2 className="text-lg font-bold text-slate-700 mb-4 tracking-tight border-b border-purple-50 pb-2">
            {isRegister ? t.registerTitle : t.loginTitle}
          </h2>

          <form onSubmit={isRegister ? handleRegister : handleLogin} className="space-y-4">
            {authError && (
              <div className="p-3 bg-rose-50 border border-rose-100 text-rose-700 text-xs font-medium rounded-xl flex items-center gap-1.5 animate-fade-in">
                <AlertCircle className="w-4 h-4 shrink-0" />
                <span>{authError}</span>
              </div>
            )}

            {isRegister && (
              <div>
                <label className="block text-xs font-bold text-slate-500 uppercase mb-1">{t.yourName}</label>
                <div className="relative">
                  <span className="absolute inset-y-0 left-0 pl-3.5 flex items-center pointer-events-none text-slate-400">
                    <UserIcon className="w-4 h-4" />
                  </span>
                  <input
                    type="text"
                    required
                    value={authName}
                    onChange={(e) => setAuthName(e.target.value)}
                    className="w-full pl-10 pr-3.5 py-2.5 rounded-xl border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 text-slate-800 text-sm bg-slate-50/20"
                  />
                </div>
              </div>
            )}

            <div>
              <label className="block text-xs font-bold text-slate-500 uppercase mb-1">{t.emailLabel}</label>
              <div className="relative">
                <span className="absolute inset-y-0 left-0 pl-3.5 flex items-center pointer-events-none text-slate-400">
                  <Mail className="w-4 h-4" />
                </span>
                <input
                  type="email"
                  required
                  value={authEmail}
                  onChange={(e) => setAuthEmail(e.target.value)}
                  className="w-full pl-10 pr-3.5 py-2.5 rounded-xl border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 text-slate-800 text-sm bg-slate-50/20"
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-500 uppercase mb-1">{t.passwordLabel}</label>
              <div className="relative">
                <span className="absolute inset-y-0 left-0 pl-3.5 flex items-center pointer-events-none text-slate-400">
                  <Lock className="w-4 h-4" />
                </span>
                <input
                  type="password"
                  required
                  value={authPassword}
                  onChange={(e) => setAuthPassword(e.target.value)}
                  className="w-full pl-10 pr-3.5 py-2.5 rounded-xl border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 text-slate-800 text-sm bg-slate-50/20"
                />
              </div>
            </div>

            {isRegister && (
              <div>
                <label className="block text-xs font-bold text-slate-500 uppercase mb-1">{t.roleLabel}</label>
                <select
                  value={authRole}
                  onChange={(e) => setAuthRole(e.target.value as any)}
                  className="w-full px-3 py-2.5 rounded-xl border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 text-slate-800 text-sm bg-slate-50/20 cursor-pointer"
                >
                  <option value="PrimaryUser">{t.rolePrimary}</option>
                  <option value="Partner">{t.rolePartner}</option>
                </select>
              </div>
            )}

            <button
              type="submit"
              disabled={authLoading}
              className="w-full bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-700 hover:to-indigo-700 text-white font-semibold py-3 rounded-xl transition shadow-md shadow-purple-600/10 cursor-pointer disabled:opacity-70 mt-2"
            >
              {authLoading ? t.sending : (isRegister ? t.registerBtn : t.loginBtn)}
            </button>
          </form>

          {/* Toggle register/login links */}
          <div className="mt-5 text-center text-xs text-slate-500">
            <span>{isRegister ? t.alreadyHaveAccount : t.noAccountText}{' '}</span>
            <button
              onClick={() => {
                setIsRegister(!isRegister);
                setAuthError(null);
              }}
              className="text-purple-600 hover:text-purple-700 font-bold underline cursor-pointer"
            >
              {isRegister ? t.loginLink : t.registerLink}
            </button>
          </div>
        </div>

        {/* Floating language selector on login */}
        <button
          onClick={() => setLang(lang === 'fa' ? 'en' : 'fa')}
          className="mt-6 px-3 py-1.5 text-[10px] rounded-xl font-bold bg-white/60 hover:bg-white/90 border border-purple-50 text-slate-500 transition shadow-2xs cursor-pointer"
        >
          {lang === 'fa' ? 'Switch to English' : 'تغییر به فارسی'}
        </button>
      </div>
    );
  }

  // ==========================================
  // VIEW RENDERER B: Main Application Interface
  // ==========================================
  return (
    <div 
      className="w-full min-h-screen flex flex-col justify-between max-w-md mx-auto p-4 select-none animate-fade-in" 
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
          <div className="glass-card border-rose-100 text-rose-950 p-6 rounded-2xl text-center shadow-xs my-6 animate-fade-in">
            <AlertCircle className="w-10 h-10 text-rose-500 mx-auto mb-3" />
            <p className="font-semibold mb-2">{error}</p>
            <div className="flex justify-center gap-3">
              <button
                onClick={fetchStatus}
                className="mt-2 text-xs bg-rose-500 hover:bg-rose-600 text-white font-semibold px-4 py-2 rounded-xl transition cursor-pointer"
              >
                {t.retryBtn}
              </button>
              <button
                onClick={handleLogout}
                className="mt-2 text-xs border border-rose-300 hover:bg-rose-50 text-rose-700 font-semibold px-4 py-2 rounded-xl transition cursor-pointer"
              >
                {t.logoutBtn}
              </button>
            </div>
          </div>
        )}

        {!loading && !error && status && (
          <>
            {/* If Partner is NOT linked: show linking dashboard placeholder */}
            {status.role === 'Partner' && !status.isLinked ? (
              <div className="glass-card p-6 rounded-2xl text-center shadow-xs my-6 animate-fade-in">
                <Link className="w-12 h-12 text-purple-500 mx-auto mb-3" />
                <h2 className="text-lg font-bold text-slate-800 mb-2">{t.partnerLinkSection}</h2>
                <p className="text-sm text-slate-500 mb-4">{t.notLinkedStatus}</p>
                <button
                  onClick={() => setIsSettingsOpen(true)}
                  className="bg-purple-600 hover:bg-purple-700 text-white font-semibold text-sm px-5 py-2.5 rounded-xl transition cursor-pointer"
                >
                  {t.linkPartnerBtn}
                </button>
              </div>
            ) : (
              /* If linked/Primary: render core cycle tracking ring & insights */
              <>
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

                {status.role === 'Partner' && (
                  <PartnerInsights
                    status={status}
                    lang={lang}
                    onSendTestAlert={handleSendTestAlert}
                  />
                )}
              </>
            )}
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
        partnerCodeState={partnerCodeState}
        onSaveSettings={handleSaveSettings}
        onGenerateCode={handleGenerateCode}
        onLinkPartner={handleLinkPartner}
        onLogout={handleLogout}
        onSendTestAlert={handleSendTestAlert}
        onSendTestPush={handleSendTestPush}
      />
    </div>
  );
}

export default App;
