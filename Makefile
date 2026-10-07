# Shortcuts for the common tasks. Everything here is a thin wrapper over the scripts and commands in docs/RUNBOOK.md.
.DEFAULT_GOAL := help
QUIET_PAYMENTS = FakePaymentGateway__DeclineRate=0 FakePaymentGateway__TransientFailureRate=0

.PHONY: help infra dev down reset test integration ui-test e2e load docker-up docker-down monitoring

help: ## List the tasks
	@grep -E '^[a-z0-9-]+:.*##' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*## "}; {printf "  %-14s %s\n", $$1, $$2}'

infra: ## Start SQL Server, RabbitMQ, Redis and Seq in Docker
	cd deploy && docker compose up -d --build

dev: ## Build and start every service and the UI on this machine (UI on :5173)
	dotnet build && $(QUIET_PAYMENTS) scripts/dev-up.sh

down: ## Stop the services and the UI started by `make dev`
	scripts/dev-down.sh

reset: ## Wipe all data (databases, Redis, queues), then `make dev` for a clean demo
	scripts/reset-demo-data.sh

test: ## .NET unit tests
	dotnet test --filter "FullyQualifiedName!~IntegrationTests"

integration: ## Integration tests on real containers (needs Docker, takes a few minutes)
	dotnet test tests/OrderFlow.IntegrationTests

ui-test: ## UI type check and unit tests
	cd web && npm run typecheck && npm test

e2e: ## Browser tests (start the system first with `make dev`, see RUNBOOK for the settings)
	cd web && npx playwright test

load: ## k6 load test at 8 orders/second peak (start the services with RateLimit__Enabled=false)
	scripts/load-test.sh 8

docker-up: ## The whole system in Docker, UI on http://localhost:8081
	cd deploy && docker compose -f docker-compose.yml -f docker-compose.apps.yml up -d --build

docker-down: ## Stop the Docker system (keeps data)
	cd deploy && docker compose -f docker-compose.yml -f docker-compose.apps.yml down

monitoring: ## Add Grafana, Jaeger, Prometheus and the OpenTelemetry collector
	cd deploy && docker compose -f docker-compose.yml -f docker-compose.observability.yml up -d --build
