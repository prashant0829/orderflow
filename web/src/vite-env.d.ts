/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Base URL of the gateway. Defaults to http://localhost:5100 */
  readonly VITE_API_URL?: string
  readonly VITE_GRAFANA_URL?: string
  readonly VITE_JAEGER_URL?: string
  readonly VITE_SEQ_URL?: string
  readonly VITE_RABBITMQ_URL?: string
}
