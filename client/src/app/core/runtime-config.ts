export interface RuntimeConfig {
  apiBaseUrl: string;
}

declare global {
  interface Window {
    __APP_CONFIG__?: Partial<RuntimeConfig>;
  }
}

const fallbackConfig: RuntimeConfig = {
  apiBaseUrl: 'http://localhost:5137'
};

export const runtimeConfig: RuntimeConfig = {
  ...fallbackConfig,
  ...(typeof window !== 'undefined' ? window.__APP_CONFIG__ : {})
};
