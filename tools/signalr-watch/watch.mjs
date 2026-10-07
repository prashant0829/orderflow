// Usage: TOKEN=<jwt> node tools/signalr-watch/watch.mjs [me | orderId | customerId=<guid>] [seconds]
//   me          -> all orders of the signed-in customer (the normal case)
//   orderId     -> one order (admin tokens only)
//   customerId= -> one customer's orders (admin tokens only)
import * as signalR from "@microsoft/signalr";

const target = process.argv[2] ?? "me";
const seconds = Number(process.argv[3] ?? 20);
const hubUrl = process.env.HUB_URL ?? "http://localhost:5100/hubs/orders";
const token = process.env.TOKEN;

const connection = new signalR.HubConnectionBuilder()
  .withUrl(hubUrl, { accessTokenFactory: () => token })
  .configureLogging(signalR.LogLevel.Warning)
  .withAutomaticReconnect()
  .build();
connection.on("orderStatusChanged", (change) =>
  console.log(`${new Date().toISOString()}  order ${change.orderId.slice(0, 8)}  ${change.state.padEnd(16)} ${change.message}`));

await connection.start();
if (target === "me") await connection.invoke("JoinMyOrders");
else if (target.startsWith("customerId=")) await connection.invoke("JoinCustomer", target.split("=")[1]);
else await connection.invoke("JoinOrder", target);
console.log(`connected, listening for ${seconds}s ...`);
setTimeout(async () => { await connection.stop(); process.exit(0); }, seconds * 1000);
