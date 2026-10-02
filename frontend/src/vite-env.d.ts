/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string;
  /** Map tile URL template ({z}/{x}/{y}); default OpenStreetMap. */
  readonly VITE_MAP_TILE_URL?: string;
  /** HTML attribution the tile provider requires. */
  readonly VITE_MAP_TILE_ATTRIBUTION?: string;
  /** "true" when the provider's tiles are already dark (skips the CSS inversion). */
  readonly VITE_MAP_TILES_DARK?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
