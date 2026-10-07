const gateway = process.env.VITE_API_URL ?? 'http://localhost:5100'

/** Fail fast with a clear message instead of 20 confusing timeouts when the backend is not running. */
export default async function globalSetup() {
  try {
    const response = await fetch(`${gateway}/health/ready`)
    if (!response.ok) throw new Error(`gateway answered ${response.status}`)
  } catch (error) {
    throw new Error(
      `The backend is not reachable at ${gateway} (${String(error)}).\n` +
        'Start it first:  FakePaymentGateway__DeclineRate=0 FakePaymentGateway__TransientFailureRate=0 RateLimit__Auth__Limit=1000 scripts/dev-up.sh',
    )
  }
}
