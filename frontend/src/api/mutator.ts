import axios, { type AxiosRequestConfig, type AxiosError } from 'axios';
import { getAccessToken, refreshAccessToken, clearTokens } from '../auth/tokenStore';

const baseURL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';

export const axiosInstance = axios.create({ baseURL });

axiosInstance.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

let refreshPromise: Promise<string | null> | null = null;

axiosInstance.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as (AxiosRequestConfig & { _retry?: boolean }) | undefined;

    if (error.response?.status === 401 && original && !original._retry && !original.url?.includes('/auth/')) {
      original._retry = true;
      refreshPromise ??= refreshAccessToken().finally(() => {
        refreshPromise = null;
      });
      const newToken = await refreshPromise;

      if (newToken) {
        original.headers = { ...original.headers, Authorization: `Bearer ${newToken}` };
        return axiosInstance(original);
      }
      clearTokens();
    }

    return Promise.reject(error);
  },
);

/** Orval's custom-instance mutator — every generated API call goes through this. */
export const customInstance = <T>(config: AxiosRequestConfig): Promise<T> => {
  const controller = new AbortController();
  const promise = axiosInstance({ ...config, signal: controller.signal }).then((response) => response.data as T);
  return promise;
};

export type ErrorType<Error> = AxiosError<Error>;
