import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import cs from './locales/cs.json';

// Czech is the only shipped locale for the MVP; the app is structured so adding another
// language is just another locale file + entry here, no component changes required.
void i18n.use(initReactI18next).init({
  resources: { cs: { translation: cs } },
  lng: 'cs',
  fallbackLng: 'cs',
  interpolation: { escapeValue: false },
});

export default i18n;
