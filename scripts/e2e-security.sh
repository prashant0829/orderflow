#!/usr/bin/env bash
# Security checks through the gateway. Needs all services running (scripts/dev-up.sh).
# Auth calls mostly go straight to Identity (:5105) so the gateway's strict login rate limit is only exercised on purpose at the end.
cd "$(dirname "$0")/.."; source scripts/lib-e2e.sh
IDENTITY=${IDENTITY:-http://localhost:5105}
ok() { echo "  PASS: $1"; }; bad() { echo "  FAIL: $1"; FAILED=1; }
expect() { [ "$2" = "$3" ] && ok "$1 = $3" || bad "$1: expected '$3' got '$2'"; }
code() { curl -s -o /dev/null -w "%{http_code}" "$@"; }
json() { curl -s -H 'Content-Type: application/json' "$@"; }
RUN=$(date +%s)

echo "1) Registration"
EMAIL_A="alice$RUN@example.com"; EMAIL_B="bob$RUN@example.com"
A=$(json -X POST $IDENTITY/api/v1/auth/register -d "{\"email\":\"$EMAIL_A\",\"password\":\"Passw0rd!x\",\"displayName\":\"Alice\"}")
TOKEN_A=$(echo "$A" | jget "['accessToken']"); REFRESH_A=$(echo "$A" | jget "['refreshToken']"); ID_A=$(echo "$A" | jget "['user']['id']")
expect "new user is a Customer" "$(echo "$A" | jget "['user']['role']")" Customer
DUP_BODY="{\"email\":\"$EMAIL_A\",\"password\":\"Passw0rd!x\"}"
expect "duplicate email" "$(code -X POST $IDENTITY/api/v1/auth/register -H 'Content-Type: application/json' -d "$DUP_BODY")" 409
expect "weak password rejected" "$(code -X POST $IDENTITY/api/v1/auth/register -H 'Content-Type: application/json' -d '{"email":"weak@example.com","password":"short"}')" 400
TOKEN_B=$(json -X POST $IDENTITY/api/v1/auth/register -d "{\"email\":\"$EMAIL_B\",\"password\":\"Passw0rd!x\",\"displayName\":\"Bob\"}" | jget "['accessToken']")
expect "GET /auth/me" "$(curl -s -H "Authorization: Bearer $TOKEN_A" $IDENTITY/api/v1/auth/me | jget "['email']")" "$EMAIL_A"

echo "2) Tokens are checked"
expect "no token" "$(code $GATEWAY/api/v1/orders)" 401
expect "garbage token" "$(code -H 'Authorization: Bearer not.a.jwt' $GATEWAY/api/v1/orders)" 401
FORGED=$(python3 - <<'PY'
import base64,hmac,hashlib,json,time
b=lambda d: base64.urlsafe_b64encode(d).rstrip(b'=').decode()
head=b(json.dumps({"alg":"HS256","typ":"JWT"}).encode())
body=b(json.dumps({"sub":"11111111-1111-1111-1111-111111111111","role":"Admin","iss":"orderflow-identity","aud":"orderflow-api","exp":int(time.time())+3600}).encode())
sig=b(hmac.new(b"attacker-guessed-key-attacker-guessed-key",f"{head}.{body}".encode(),hashlib.sha256).digest())
print(f"{head}.{body}.{sig}")
PY
)
expect "admin token signed with the wrong key" "$(code -H "Authorization: Bearer $FORGED" $GATEWAY/api/v1/payments)" 401
expect "token sent straight to a service (no gateway)" "$(code http://localhost:5101/api/v1/orders)" 401

echo "3) Roles"
expect "customer -> payments (admin only)" "$(code -H "Authorization: Bearer $TOKEN_A" $GATEWAY/api/v1/payments)" 403
expect "customer -> dead letters (admin only)" "$(code -H "Authorization: Bearer $TOKEN_A" $GATEWAY/api/v1/admin/dead-letters)" 403
expect "customer -> create product (admin only)" "$(code -X POST -H "Authorization: Bearer $TOKEN_A" -H 'Content-Type: application/json' $GATEWAY/api/v1/stock -d '{"name":"x","unitPrice":1,"initialQuantity":1}')" 403
expect "admin -> payments" "$(code -H "Authorization: Bearer $ADMIN_TOKEN" $GATEWAY/api/v1/payments)" 200
expect "admin -> dead letters" "$(code -H "Authorization: Bearer $ADMIN_TOKEN" $GATEWAY/api/v1/admin/dead-letters)" 200
expect "anonymous -> public catalog" "$(code $GATEWAY/api/v1/stock)" 200

echo "4) Customers only see their own orders"
P=$(new_product "Security Widget $RUN" 10 20)
ORDER=$(curl -s -X POST $GATEWAY/api/v1/orders -H "Authorization: Bearer $TOKEN_A" -H 'Content-Type: application/json' -H "Idempotency-Key: sec-$RUN" \
  -d "{\"customerId\":\"$CUSTOMER\",\"lines\":[{\"productId\":\"$P\",\"quantity\":1}]}")
OID=$(echo "$ORDER" | jget "['id']")
expect "customerId in the body is ignored (order belongs to the token user)" "$(echo "$ORDER" | jget "['customerId']")" "$ID_A"
expect "owner can read" "$(code -H "Authorization: Bearer $TOKEN_A" $GATEWAY/api/v1/orders/$OID)" 200
expect "other customer cannot read (404, not 403)" "$(code -H "Authorization: Bearer $TOKEN_B" $GATEWAY/api/v1/orders/$OID)" 404
expect "other customer cannot see progress" "$(code -H "Authorization: Bearer $TOKEN_B" $GATEWAY/api/v1/orders/$OID/progress)" 404
expect "other customer cannot cancel" "$(code -X POST -H "Authorization: Bearer $TOKEN_B" -H 'Content-Type: application/json' $GATEWAY/api/v1/orders/$OID/cancel -d '{"reason":"x"}')" 404
expect "admin can read" "$(code -H "Authorization: Bearer $ADMIN_TOKEN" $GATEWAY/api/v1/orders/$OID)" 200
expect "other customer's list is empty" "$(curl -s -H "Authorization: Bearer $TOKEN_B" "$GATEWAY/api/v1/orders?customerId=$ID_A" | jget "['totalCount']")" 0
expect "a customer cannot list someone else by id" "$(curl -s -H "Authorization: Bearer $TOKEN_A" "$GATEWAY/api/v1/orders?customerId=$CUSTOMER" | jget "['totalCount']")" 1
sleep 4
expect "other customer cannot read timeline" "$(code -H "Authorization: Bearer $TOKEN_B" $GATEWAY/api/v1/timeline/$OID)" 404
expect "owner can read timeline" "$(code -H "Authorization: Bearer $TOKEN_A" $GATEWAY/api/v1/timeline/$OID)" 200

echo "5) Refresh tokens rotate, and a stolen one is detected"
R1=$(json -X POST $IDENTITY/api/v1/auth/refresh -d "{\"refreshToken\":\"$REFRESH_A\"}")
REFRESH_NEW=$(echo "$R1" | jget "['refreshToken']")
expect "first use of a refresh token" "$([ -n "$REFRESH_NEW" ] && echo ok)" ok
expect "the new refresh token differs" "$([ "$REFRESH_NEW" != "$REFRESH_A" ] && echo ok)" ok
expect "re-using the OLD refresh token" "$(code -X POST $IDENTITY/api/v1/auth/refresh -H 'Content-Type: application/json' -d "{\"refreshToken\":\"$REFRESH_A\"}")" 401
expect "...which also kills the NEW one (theft assumed)" "$(code -X POST $IDENTITY/api/v1/auth/refresh -H 'Content-Type: application/json' -d "{\"refreshToken\":\"$REFRESH_NEW\"}")" 401
LOGIN2=$(json -X POST $IDENTITY/api/v1/auth/login -d "{\"email\":\"$EMAIL_A\",\"password\":\"Passw0rd!x\"}"); R2=$(echo "$LOGIN2" | jget "['refreshToken']")
expect "logout" "$(code -X POST $IDENTITY/api/v1/auth/logout -H 'Content-Type: application/json' -d "{\"refreshToken\":\"$R2\"}")" 204
expect "refresh after logout" "$(code -X POST $IDENTITY/api/v1/auth/refresh -H 'Content-Type: application/json' -d "{\"refreshToken\":\"$R2\"}")" 401

echo "6) Account lockout after 5 wrong passwords"
for _ in 1 2 3 4 5; do code -X POST $IDENTITY/api/v1/auth/login -H 'Content-Type: application/json' -d "{\"email\":\"$EMAIL_B\",\"password\":\"wrong\"}" >/dev/null; done
GOOD_B="{\"email\":\"$EMAIL_B\",\"password\":\"Passw0rd!x\"}"
expect "correct password while locked" "$(code -X POST $IDENTITY/api/v1/auth/login -H 'Content-Type: application/json' -d "$GOOD_B")" 401
expect "unknown email gives the same answer as a wrong password" "$(curl -s -X POST $IDENTITY/api/v1/auth/login -H 'Content-Type: application/json' -d '{"email":"nobody@example.com","password":"x"}' | jget "['detail']")" "Invalid email or password."

echo "7) Browser protections (CORS and headers)"
HEADERS=$(curl -s -D - -o /dev/null $GATEWAY/api/v1/stock)
echo "$HEADERS" | grep -qi "x-content-type-options: nosniff" && ok "nosniff header" || bad "nosniff header"
echo "$HEADERS" | grep -qi "x-frame-options: DENY" && ok "frame denied" || bad "frame denied"
echo "$HEADERS" | grep -qi "^server:" && bad "Server header should be hidden" || ok "Server header hidden"
echo "$HEADERS" | grep -qi "x-correlation-id" && ok "correlation id returned" || bad "correlation id returned"
expect "client correlation id is kept" "$(curl -s -D - -o /dev/null -H 'X-Correlation-Id: my-trace-1' $GATEWAY/api/v1/stock | grep -i x-correlation-id | tr -d '\r' | awk '{print $2}')" my-trace-1
ALLOWED=$(curl -s -D - -o /dev/null -X OPTIONS -H 'Origin: http://localhost:5173' -H 'Access-Control-Request-Method: POST' -H 'Access-Control-Request-Headers: authorization,content-type' $GATEWAY/api/v1/orders | grep -i "access-control-allow-origin" | tr -d '\r')
EVIL=$(curl -s -D - -o /dev/null -X OPTIONS -H 'Origin: http://evil.example' -H 'Access-Control-Request-Method: POST' $GATEWAY/api/v1/orders | grep -ci "access-control-allow-origin")
echo "$ALLOWED" | grep -q "http://localhost:5173" && ok "allowed origin gets CORS headers" || bad "allowed origin gets CORS headers"
expect "unknown origin gets no CORS headers" "$EVIL" 0

echo "8) Live feed is protected too"
HUB_URL=$GATEWAY/hubs/orders CUSTOMER_TOKEN=$TOKEN_A node tools/signalr-watch/check-auth.mjs | sed 's/^/  /' | tee /tmp/hubcheck.txt; grep -q FAIL /tmp/hubcheck.txt && FAILED=1

echo "9) Rate limiting (strict on login, shared through Redis)"
CODES=""; for _ in $(seq 1 14); do CODES="$CODES $(code -X POST $GATEWAY/api/v1/auth/login -H 'Content-Type: application/json' -d '{"email":"nobody@example.com","password":"x"}')"; done
echo "   login attempts:$CODES"
echo "$CODES" | grep -q 429 && ok "login is rate limited with 429" || bad "login is rate limited with 429"
RETRY=$(curl -s -D - -o /dev/null -X POST $GATEWAY/api/v1/auth/login -H 'Content-Type: application/json' -d '{"email":"a@b.c","password":"x"}' | grep -i "^retry-after" | tr -d '\r')
[ -n "$RETRY" ] && ok "Retry-After header present ($RETRY)" || bad "Retry-After header present"

[ -z "$FAILED" ] && echo "ALL PASSED" || { echo "SOME FAILED"; exit 1; }
