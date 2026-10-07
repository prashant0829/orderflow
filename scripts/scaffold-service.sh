#!/usr/bin/env bash
# Usage: scripts/scaffold-service.sh Inventory   -> creates the 4 Clean Architecture projects + unit test project
set -euo pipefail
NAME=$1
BASE="src/Services/$NAME"
BB="src/BuildingBlocks"
mkdir -p "$BASE"
for layer in Domain Application Infrastructure; do
  dotnet new classlib -n "OrderFlow.$NAME.$layer" -o "$BASE/OrderFlow.$NAME.$layer" >/dev/null
  rm "$BASE/OrderFlow.$NAME.$layer/Class1.cs"
done
dotnet new web -n "OrderFlow.$NAME.Api" -o "$BASE/OrderFlow.$NAME.Api" >/dev/null
dotnet new xunit -n "OrderFlow.$NAME.UnitTests" -o "tests/OrderFlow.$NAME.UnitTests" >/dev/null
rm "tests/OrderFlow.$NAME.UnitTests/UnitTest1.cs"
dotnet sln add "$BASE"/*/*.csproj "tests/OrderFlow.$NAME.UnitTests/OrderFlow.$NAME.UnitTests.csproj" >/dev/null

D="$BASE/OrderFlow.$NAME.Domain"; A="$BASE/OrderFlow.$NAME.Application"; I="$BASE/OrderFlow.$NAME.Infrastructure"; P="$BASE/OrderFlow.$NAME.Api"; T="tests/OrderFlow.$NAME.UnitTests"
dotnet add "$D" reference "$BB/OrderFlow.SharedKernel" >/dev/null
dotnet add "$A" reference "$D" "$BB/OrderFlow.SharedKernel" "$BB/OrderFlow.Cqrs" "$BB/OrderFlow.Caching" >/dev/null
dotnet add "$I" reference "$A" "$BB/OrderFlow.Contracts" "$BB/OrderFlow.Messaging" "$BB/OrderFlow.Caching" >/dev/null
dotnet add "$P" reference "$A" "$I" "$BB/OrderFlow.Observability" "$BB/OrderFlow.Messaging" "$BB/OrderFlow.Web" "$BB/OrderFlow.Caching" >/dev/null
dotnet add "$T" reference "$D" "$A" "$I" "$BB/OrderFlow.Contracts" "$BB/OrderFlow.Caching" >/dev/null
add() { dotnet add "$1" package "$2" --version "$3" 2>&1 | grep -iE "error|NU1" || true; }
add "$I" Microsoft.EntityFrameworkCore.SqlServer 8.0.11
add "$I" Microsoft.EntityFrameworkCore.Relational 8.0.11
add "$I" MassTransit.EntityFrameworkCore 8.3.6
add "$P" Microsoft.EntityFrameworkCore.Design 8.0.11
add "$P" AspNetCore.HealthChecks.Redis 8.0.1
add "$P" AspNetCore.HealthChecks.SqlServer 8.0.2
add "$T" NSubstitute 5.3.0
add "$T" FluentAssertions 6.12.2
echo "scaffolded $NAME"
