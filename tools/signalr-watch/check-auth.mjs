// Verifies hub security. Usage: HUB_URL=... CUSTOMER_TOKEN=... node check-auth.mjs
import * as signalR from "@microsoft/signalr";

const hubUrl = process.env.HUB_URL ?? "http://localhost:5100/hubs/orders";
const build = (token) => new signalR.HubConnectionBuilder()
  .withUrl(hubUrl, token ? { accessTokenFactory: () => token } : {})
  .configureLogging(signalR.LogLevel.None).build();
const results = [];
const check = (name, ok) => { results.push(ok); console.log(`  ${ok ? "PASS" : "FAIL"}: ${name}`); };

try { await build(undefined).start(); check("connecting without a token is rejected", false); }
catch { check("connecting without a token is rejected", true); }

const customer = build(process.env.CUSTOMER_TOKEN);
await customer.start();
try { await customer.invoke("JoinMyOrders"); check("customer can join their own orders", true); }
catch { check("customer can join their own orders", false); }
try { await customer.invoke("JoinOrder", "33333333-3333-3333-3333-333333333333"); check("customer cannot join an arbitrary order", false); }
catch { check("customer cannot join an arbitrary order", true); }
try { await customer.invoke("JoinCustomer", "99999999-9999-9999-9999-999999999999"); check("customer cannot join another customer", false); }
catch { check("customer cannot join another customer", true); }
await customer.stop();
process.exit(results.every(Boolean) ? 0 : 1);
