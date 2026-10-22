CLUSTER := invoiceflow
CONTEXT := k3d-$(CLUSTER)

# Colima locally; CI uses the runner Docker engine (leave DOCKER_HOST unset).
ifeq ($(CI),true)
else ifndef DOCKER_HOST
export DOCKER_HOST := unix://$(HOME)/.colima/invoiceflow/docker.sock
endif
export KUBECONFIG := $(CURDIR)/.kube/config
export INVOICEFLOW_BACKUP_DIR ?= $(HOME)/.local/share/invoiceflow/backups
K3D_CONFIG ?= deploy/k3d/cluster.yaml
# Empty ServiceUrl → FakeInvoiceExtractor (CI / drills). Local default targets host Ollama via EndpointSlice.
DOCUMENT_EXTRACTION_SERVICE_URL ?= http://ollama.invoiceflow.svc.cluster.local:11434

KUBECTL := kubectl --context $(CONTEXT)
HELM := helm --kube-context $(CONTEXT)

GRAFANA_CHARTS := https://grafana-community.github.io/helm-charts
OTEL_CHARTS := https://open-telemetry.github.io/opentelemetry-helm-charts
LOKI_CHART_VERSION := 18.15.1
GRAFANA_CHART_VERSION := 13.5.0
COLLECTOR_CHART_VERSION := 0.175.1
OBSERVABILITY_NS := invoiceflow-observability
COLLECTOR_NS := invoiceflow-collector
INFRA_NS := invoiceflow-infra
APP_NS := invoiceflow
DB_FORWARD_PORT := 15432
S3_FORWARD_PORT := 18333
RABBITMQ_UI_PORT := 15672
KEYCLOAK_ADMIN_PORT := 18080
# Rebuilt and rolled out only when the Dockerfile changes
KEYCLOAK_TAG := $(shell git hash-object deploy/images/keycloak/Dockerfile | cut -c1-12)
DASHBOARD_IMAGE := $(shell grep -o 'mcr.microsoft.com/dotnet/aspire-dashboard[^ ]*' deploy/observability/aspire-dashboard.yaml)
DASHBOARD_TOKEN_PATTERN := login?t=[0-9a-f]*
# Cluster image host (mirrored by k3d to the registry container). Always localhost so
# nodes do not try to pull via their own 127.0.0.1 loopback.
REGISTRY ?= localhost:5001
# Host push address. In CI, localhost resolves to ::1 but k3d binds IPv4 only.
ifeq ($(CI),true)
PUSH_REGISTRY ?= 127.0.0.1:5001
else
PUSH_REGISTRY ?= $(REGISTRY)
endif
export REGISTRY
export PUSH_REGISTRY
APPS := Api Worker Migrator


# Fixed, so every rebuild of the local environment gets the same tenant; not a secret
DEVELOPMENT_TENANT_ID := 01a130f4-cc70-7419-9b64-feec3b283b96
# Commit-based tags; uncommitted changes get a unique suffix so every build rolls out
ifndef IMAGE_TAG
IMAGE_TAG := $(shell git rev-parse --short HEAD)$(if $(shell git status --porcelain),-dirty-$(shell date +%s))
endif

.DEFAULT_GOAL := help
.PHONY: help up down doctor cluster-up cluster-down cluster-status ollama-up ollama-down ollama-check \
	observability-up observability-down grafana-password dashboard-url telemetry-check dashboard-up dashboard-down \
	postgres-up postgres-down postgres-password db-forward psql \
	onprem-up onprem-down onprem-spike provision-tenant \
	seaweedfs-up seaweedfs-down s3-credentials s3-forward s3-check images web-image \
	app-up app-down app-check rabbitmq-up rabbitmq-down rabbitmq-password rabbitmq-ui \
	keycloak-image keycloak-up keycloak-down keycloak-password keycloak-admin trust-local-ca \
	db-up db-migrate db-down stack-up e2e-install e2e-typecheck e2e-smoke e2e-ci

help:
	@grep -E '^[a-z0-9-]+:.*## ' $(MAKEFILE_LIST) | awk -F':.*## ' '{printf "  %-20s %s\n", $$1, $$2}'

up: ## Start the VM, the cluster and Ollama
	@if colima status --profile $(CLUSTER) >/dev/null 2>&1; then \
		echo "Colima is already running"; \
	else \
		previous=$$(env -u DOCKER_HOST docker context show); \
		colima start --profile $(CLUSTER); \
		env -u DOCKER_HOST docker context use "$$previous" >/dev/null; \
		echo "Docker context restored to $$previous"; \
	fi
	@# Without br_netfilter, a pod gets no reply from a Service whose pod runs on the same node
	@colima ssh --profile $(CLUSTER) -- sudo modprobe br_netfilter
	@$(MAKE) --no-print-directory cluster-up ollama-up

down: ollama-down ## Stop Ollama and the VM (the cluster stops with it)
	@colima stop --profile $(CLUSTER)

doctor: ## Check that Docker and kubectl target the local environment only
	@./scripts/doctor.sh

cluster-up: doctor ## Create the k3d cluster (no-op if it exists; override with K3D_CONFIG=…)
	@mkdir -p "$(INVOICEFLOW_BACKUP_DIR)" "$(dir $(KUBECONFIG))"
	@if k3d cluster list $(CLUSTER) >/dev/null 2>&1; then \
		echo "Cluster $(CLUSTER) already exists"; \
	else \
		k3d cluster create --config $(K3D_CONFIG); \
	fi
	@./scripts/doctor.sh
	@# The API server needs a while after the VM starts; kubectl wait fails at once if it can't connect
	@for i in $$(seq 60); do $(KUBECTL) get --raw /readyz >/dev/null 2>&1 && break; sleep 2; done
	@$(KUBECTL) wait --for=condition=Ready nodes --all --timeout=120s
	@$(KUBECTL) get nodes

cluster-down: ## Delete the k3d cluster and its registry
	@k3d cluster delete $(CLUSTER)
	@k3d registry delete invoiceflow-registry 2>/dev/null || true

cluster-status: doctor ## Show nodes and system pods
	@$(KUBECTL) get nodes -o wide
	@$(KUBECTL) get pods -n kube-system

ollama-up: ## Start Ollama on the host, listening on localhost only
	@if curl -sf http://127.0.0.1:11434/api/version >/dev/null; then \
		echo "Ollama is already running"; \
	else \
		mkdir -p "$(HOME)/Library/Logs"; \
		OLLAMA_HOST=127.0.0.1:11434 nohup ollama serve >"$(HOME)/Library/Logs/ollama.log" 2>&1 & \
		sleep 2; curl -sf http://127.0.0.1:11434/api/version && echo; \
	fi

ollama-down: ## Stop Ollama
	@pkill -f "ollama serve" && echo "Ollama stopped" || echo "Ollama was not running"

ollama-check: doctor ## Call Ollama on the host from a pod in the cluster
	@./scripts/ollama-check.sh

onprem-up: ## Start TLS PostgreSQL on the Docker host (port 5433), simulating a client site
	@./scripts/onprem-up.sh

onprem-down: ## Stop the simulated on-premises PostgreSQL
	@./scripts/onprem-down.sh

onprem-spike: doctor ## Reach host Postgres with TLS verify-full from a cluster pod
	@./scripts/onprem-spike.sh

provision-tenant: doctor images ## Provision or refresh the OnPremises demo tenant (requires onprem-up + spike)
	@./scripts/provision-tenant.sh
	@$(KUBECTL) rollout restart deployment/api deployment/worker --namespace $(APP_NS)
	@./scripts/run-migrator.sh $(IMAGE_TAG)
	@echo "On-prem demo tenant ready (slug=onprem). Point Tenancy__DevelopmentTenantId at ONPREM_TENANT_ID to exercise it."

observability-up: doctor ## Install Loki, Grafana and the OpenTelemetry Collector
	@$(KUBECTL) apply -f deploy/observability/namespaces.yaml
	@$(HELM) upgrade --install loki loki --repo $(GRAFANA_CHARTS) --version $(LOKI_CHART_VERSION) \
		--namespace $(OBSERVABILITY_NS) -f deploy/observability/loki-values.yaml --wait --timeout 5m
	@$(HELM) upgrade --install grafana grafana --repo $(GRAFANA_CHARTS) --version $(GRAFANA_CHART_VERSION) \
		--namespace $(OBSERVABILITY_NS) -f deploy/observability/grafana-values.yaml --wait --timeout 5m
	@$(KUBECTL) apply -f deploy/observability/aspire-dashboard.yaml
	@$(KUBECTL) rollout status deployment/aspire-dashboard --namespace $(OBSERVABILITY_NS) --timeout 5m
	@$(HELM) upgrade --install collector opentelemetry-collector --repo $(OTEL_CHARTS) --version $(COLLECTOR_CHART_VERSION) \
		--namespace $(COLLECTOR_NS) -f deploy/observability/collector-values.yaml --wait --timeout 5m
	@echo "Grafana: http://grafana.invoiceflow.localhost:8080 (user admin, password from make grafana-password)"
	@echo "Aspire Dashboard: make dashboard-url"

observability-down: doctor ## Remove the observability stack, including stored logs
	@$(HELM) uninstall collector --namespace $(COLLECTOR_NS) --ignore-not-found
	@$(HELM) uninstall grafana loki --namespace $(OBSERVABILITY_NS) --ignore-not-found
	@$(KUBECTL) delete -f deploy/observability/namespaces.yaml --ignore-not-found

grafana-password: ## Print the Grafana admin password
	@$(KUBECTL) get secret grafana --namespace $(OBSERVABILITY_NS) -o jsonpath='{.data.admin-password}' | base64 -d; echo

# The dashboard creates a new login token on every start and only prints it to its log
dashboard-url: doctor ## Print the login URL of the Aspire Dashboard in the cluster
	@token=$$($(KUBECTL) logs deployment/aspire-dashboard --namespace $(OBSERVABILITY_NS) | grep -o '$(DASHBOARD_TOKEN_PATTERN)' | tail -1); \
		echo "http://dashboard.invoiceflow.localhost:8080/$$token"

telemetry-check: doctor ## Send test traces through the collector to the Aspire Dashboard
	@./scripts/telemetry-check.sh

dashboard-up: ## Run the Aspire Dashboard in Docker for apps started with dotnet run
	@if [ -z "$$(docker ps -q --filter name=^invoiceflow-dashboard$$)" ]; then \
		docker run -d --rm --name invoiceflow-dashboard \
			-p 127.0.0.1:18888:18888 -p 127.0.0.1:4317:18889 -p 127.0.0.1:4318:18890 \
			-e Dashboard__Otlp__AuthMode=Unsecured $(DASHBOARD_IMAGE) >/dev/null; \
		sleep 3; \
	fi
	@echo "OTLP endpoint: http://localhost:4317 (gRPC), http://localhost:4318 (HTTP)"
	@echo "Dashboard: http://localhost:18888/$$(docker logs invoiceflow-dashboard 2>&1 | grep -o '$(DASHBOARD_TOKEN_PATTERN)' | tail -1)"

dashboard-down: ## Stop the Aspire Dashboard started by dashboard-up
	@docker rm -f invoiceflow-dashboard >/dev/null 2>&1 && echo "Dashboard stopped" || echo "Dashboard was not running"

postgres-up: doctor ## Install PostgreSQL in the infra namespace
	@$(KUBECTL) apply -f deploy/kubernetes/infra-namespace.yaml
	@./scripts/create-local-secrets.sh
	@$(KUBECTL) apply -f deploy/kubernetes/postgres/
	@$(KUBECTL) rollout status statefulset/postgres --namespace $(INFRA_NS) --timeout 5m

postgres-down: doctor ## Remove PostgreSQL, including its data and password
	@$(KUBECTL) delete -f deploy/kubernetes/postgres/ --ignore-not-found
	@$(KUBECTL) delete pvc/data-postgres-0 secret/postgres-superuser --namespace $(INFRA_NS) --ignore-not-found

postgres-password: ## Print the PostgreSQL superuser password
	@$(KUBECTL) get secret postgres-superuser --namespace $(INFRA_NS) -o jsonpath='{.data.password}' | base64 -d; echo

db-forward: doctor ## Forward PostgreSQL to localhost:15432 (Ctrl+C to stop)
	@echo "Connect to localhost:$(DB_FORWARD_PORT) as postgres (password from make postgres-password)"
	@$(KUBECTL) port-forward service/postgres $(DB_FORWARD_PORT):5432 --namespace $(INFRA_NS)

psql: doctor ## Open psql as the superuser inside the PostgreSQL pod
	@$(KUBECTL) exec -it postgres-0 --namespace $(INFRA_NS) -- psql -U postgres

# The tenant can only be registered once the catalog schema exists, hence two migrations
db-up: images ## Create the catalog and the development tenant's database, then migrate both (run postgres-up first)
	@./scripts/create-local-secrets.sh
	@./scripts/app-databases.sh
	@./scripts/run-migrator.sh $(IMAGE_TAG)
	@./scripts/register-development-tenant.sh $(DEVELOPMENT_TENANT_ID)
	@./scripts/run-migrator.sh $(IMAGE_TAG)

db-migrate: images ## Migrate the catalog, then every tenant database
	@./scripts/run-migrator.sh $(IMAGE_TAG)

db-down: doctor ## Remove the catalog and the development tenant's database, including data and credentials
	@$(KUBECTL) delete job/migrator --namespace $(APP_NS) --ignore-not-found
	@printf '%s\n' 'DROP DATABASE IF EXISTS tenant_development WITH (FORCE);' \
		'DROP DATABASE IF EXISTS invoiceflow_catalog WITH (FORCE);' \
		'DROP ROLE IF EXISTS tenant_development_app, tenant_development_migrator, catalog_app, catalog_migrator;' \
		| $(KUBECTL) exec -i postgres-0 --namespace $(INFRA_NS) -- psql -U postgres -v ON_ERROR_STOP=1 -q
	@$(KUBECTL) delete secret/catalog-app secret/catalog-migrator secret/invoiceflow-tenant-databases \
		secret/invoiceflow-tenant-migrations --namespace $(APP_NS) --ignore-not-found

seaweedfs-up: doctor ## Install SeaweedFS (S3 storage) in the infra namespace
	@$(KUBECTL) apply -f deploy/kubernetes/infra-namespace.yaml
	@./scripts/create-local-secrets.sh
	@$(KUBECTL) apply -f deploy/kubernetes/seaweedfs/
	@$(KUBECTL) rollout status statefulset/seaweedfs --namespace $(INFRA_NS) --timeout 5m

seaweedfs-down: doctor ## Remove SeaweedFS, including stored documents and credentials
	@$(KUBECTL) delete -f deploy/kubernetes/seaweedfs/ --ignore-not-found
	@$(KUBECTL) delete pvc/data-seaweedfs-0 secret/seaweedfs-s3 --namespace $(INFRA_NS) --ignore-not-found

s3-credentials: ## Print the S3 access key and secret key
	@echo "access key: $$($(KUBECTL) get secret seaweedfs-s3 --namespace $(INFRA_NS) -o jsonpath='{.data.access-key}' | base64 -d)"
	@echo "secret key: $$($(KUBECTL) get secret seaweedfs-s3 --namespace $(INFRA_NS) -o jsonpath='{.data.secret-key}' | base64 -d)"

s3-forward: doctor ## Forward the S3 API to localhost:18333 (Ctrl+C to stop)
	@echo "S3 endpoint http://localhost:$(S3_FORWARD_PORT), path-style, region us-east-1 (keys from make s3-credentials)"
	@$(KUBECTL) port-forward service/seaweedfs $(S3_FORWARD_PORT):8333 --namespace $(INFRA_NS)

s3-check: doctor ## Upload, read and delete a test object through the S3 API
	@./scripts/s3-check.sh $(S3_FORWARD_PORT)

# CI: docker build+push to PUSH_REGISTRY (IPv4). Deploy manifests still use REGISTRY=localhost:5001.
images: doctor web-image ## Build the API, worker, migrator and web images and push them to the cluster registry
ifeq ($(CI),true)
	@for app in $(APPS); do \
		name=invoiceflow-$$(echo $$app | tr '[:upper:]' '[:lower:]'); \
		push=$(PUSH_REGISTRY)/$$name:$(IMAGE_TAG); \
		cluster=$(REGISTRY)/$$name:$(IMAGE_TAG); \
		docker build --file src/InvoiceFlow.$$app/Dockerfile \
			--build-arg SOURCE_REVISION=$$(git rev-parse HEAD) --tag $$push --tag $$cluster . || exit 1; \
		docker push $$push || exit 1; \
		echo "Pushed $$push (cluster pulls $$cluster)"; \
	done
else
	@for app in $(APPS); do \
		image=$(REGISTRY)/invoiceflow-$$(echo $$app | tr '[:upper:]' '[:lower:]'):$(IMAGE_TAG); \
		docker buildx build --quiet --push --file src/InvoiceFlow.$$app/Dockerfile \
			--build-arg SOURCE_REVISION=$$(git rev-parse HEAD) --tag $$image . >/dev/null || exit 1; \
		echo "Pushed $$image"; \
	done
endif

web-image: doctor ## Build the Angular web image and push it to the cluster registry
ifeq ($(CI),true)
	@push=$(PUSH_REGISTRY)/invoiceflow-web:$(IMAGE_TAG); \
		cluster=$(REGISTRY)/invoiceflow-web:$(IMAGE_TAG); \
		docker build --file web/invoiceflow-web/Dockerfile --tag $$push --tag $$cluster web/invoiceflow-web && \
		docker push $$push && \
		echo "Pushed $$push (cluster pulls $$cluster)"
else
	@image=$(REGISTRY)/invoiceflow-web:$(IMAGE_TAG); \
		docker buildx build --quiet --push --file web/invoiceflow-web/Dockerfile \
			--tag $$image web/invoiceflow-web >/dev/null || exit 1; \
		echo "Pushed $$image"
endif

app-up: images db-migrate ## Build the images, migrate the databases and deploy the API, worker and web
	@./scripts/create-local-secrets.sh
	@$(KUBECTL) apply -f deploy/kubernetes/namespace.yaml
	@$(KUBECTL) -n $(APP_NS) create configmap web-config \
		--from-file=config.json=deploy/kubernetes/app/web-config.json \
		--dry-run=client -o yaml | $(KUBECTL) apply -f -
	@# Prefer StatefulSet pod IPs: ClusterIP/DNS has timed out on GHA k3d even when pods are Ready.
	@postgres_host="$$($(KUBECTL) -n $(INFRA_NS) get pod postgres-0 -o jsonpath='{.status.podIP}')"; \
		seaweedfs_host="$$($(KUBECTL) -n $(INFRA_NS) get pod seaweedfs-0 -o jsonpath='{.status.podIP}')"; \
		rabbitmq_host="$$($(KUBECTL) -n $(INFRA_NS) get pod rabbitmq-0 -o jsonpath='{.status.podIP}')"; \
		if [ -z "$$postgres_host" ] || [ -z "$$seaweedfs_host" ] || [ -z "$$rabbitmq_host" ]; then \
			echo "Missing infra pod IP(s): postgres=$$postgres_host seaweedfs=$$seaweedfs_host rabbitmq=$$rabbitmq_host" >&2; exit 1; \
		fi; \
		echo "App infra hosts: postgres=$$postgres_host seaweedfs=$$seaweedfs_host rabbitmq=$$rabbitmq_host"; \
		for manifest in deploy/kubernetes/app/*.yaml; do \
			sed -e 's/$${IMAGE_TAG}/$(IMAGE_TAG)/g' \
				-e 's|$${REGISTRY}|$(REGISTRY)|g' \
				-e 's/$${DEVELOPMENT_TENANT_ID}/$(DEVELOPMENT_TENANT_ID)/g' \
				-e 's|$${DOCUMENT_EXTRACTION_SERVICE_URL}|$(DOCUMENT_EXTRACTION_SERVICE_URL)|g' \
				-e 's|$${POSTGRES_HOST}|'"$$postgres_host"'|g' \
				-e 's|$${SEAWEEDFS_HOST}|'"$$seaweedfs_host"'|g' \
				-e 's|$${RABBITMQ_HOST}|'"$$rabbitmq_host"'|g' \
				$$manifest; echo ---; \
		done | $(KUBECTL) apply -f -
	@$(KUBECTL) rollout status deployment/api --namespace $(APP_NS) --timeout 5m
	@$(KUBECTL) rollout status deployment/worker --namespace $(APP_NS) --timeout 5m
	@$(KUBECTL) rollout status deployment/web --namespace $(APP_NS) --timeout 5m
	@echo "App: http://invoiceflow.localhost:8080/"
	@echo "API: http://invoiceflow.localhost:8080/api"

stack-up: postgres-up seaweedfs-up keycloak-up rabbitmq-up db-up app-up ## Deploy PostgreSQL, SeaweedFS, Keycloak, RabbitMQ, the databases and the app (run make up first)

app-down: doctor ## Remove the API, worker and web
	@$(KUBECTL) delete -f deploy/kubernetes/app/ --ignore-not-found
	@$(KUBECTL) -n $(APP_NS) delete configmap/web-config --ignore-not-found

app-check: ## Send a request through the ingress and print where to find its log line and trace
	@trace=$$(curl -s -o /dev/null -D - http://invoiceflow.localhost:8080/api/v1/app-check \
		| tr -d '\r' | awk -F': ' 'tolower($$1) == "x-trace-id" { print $$2 }'); \
	if [ -z "$$trace" ]; then echo "No X-Trace-Id in the response; is the API deployed (make app-up)?"; exit 1; fi; \
	echo "Trace id: $$trace"; \
	echo "Log line: Grafana > Explore > Loki: {k8s_namespace_name=\"$(APP_NS)\"} |= \"$$trace\""; \
	echo "Trace:    http://dashboard.invoiceflow.localhost:8080/traces/detail/$$trace"

rabbitmq-up: doctor ## Install RabbitMQ in the infra namespace, with one user per application
	@./scripts/create-local-secrets.sh
	@$(KUBECTL) apply -f deploy/kubernetes/rabbitmq/
	@$(KUBECTL) rollout status statefulset/rabbitmq --namespace $(INFRA_NS) --timeout 5m
	@./scripts/rabbitmq-users.sh
	@./scripts/rabbitmq-topology.sh

rabbitmq-down: doctor ## Remove RabbitMQ, including its data and credentials
	@$(KUBECTL) delete -f deploy/kubernetes/rabbitmq/ --ignore-not-found
	@$(KUBECTL) delete pvc/data-rabbitmq-0 secret/rabbitmq-admin --namespace $(INFRA_NS) --ignore-not-found
	@$(KUBECTL) delete secret/rabbitmq-api secret/rabbitmq-worker --namespace $(APP_NS) --ignore-not-found

rabbitmq-password: ## Print the RabbitMQ admin password
	@$(KUBECTL) get secret rabbitmq-admin --namespace $(INFRA_NS) -o jsonpath='{.data.password}' | base64 -d; echo

rabbitmq-ui: doctor ## Forward the RabbitMQ management UI to localhost:15672 (Ctrl+C to stop)
	@echo "RabbitMQ UI: http://localhost:$(RABBITMQ_UI_PORT) (user admin, password from make rabbitmq-password)"
	@$(KUBECTL) port-forward service/rabbitmq $(RABBITMQ_UI_PORT):15672 --namespace $(INFRA_NS)

keycloak-image: doctor ## Build the Keycloak image and push it to the cluster registry
ifeq ($(CI),true)
	@docker build \
		--tag $(PUSH_REGISTRY)/invoiceflow-keycloak:$(KEYCLOAK_TAG) \
		--tag $(REGISTRY)/invoiceflow-keycloak:$(KEYCLOAK_TAG) \
		deploy/images/keycloak
	@docker push $(PUSH_REGISTRY)/invoiceflow-keycloak:$(KEYCLOAK_TAG)
else
	@docker buildx build --quiet --push --tag $(REGISTRY)/invoiceflow-keycloak:$(KEYCLOAK_TAG) deploy/images/keycloak >/dev/null
endif
	@echo "Pushed $(PUSH_REGISTRY)/invoiceflow-keycloak:$(KEYCLOAK_TAG) (cluster pulls $(REGISTRY)/…)"

keycloak-up: keycloak-image ## Install Keycloak with the invoiceflow realm (run postgres-up first)
	@./scripts/create-local-secrets.sh
	@./scripts/postgres-database.sh keycloak keycloak-db
	@./scripts/local-tls.sh
	@$(KUBECTL) -n $(INFRA_NS) create configmap keycloak-realm \
		--from-file=invoiceflow-realm.json=deploy/keycloak/invoiceflow-realm.json \
		--dry-run=client -o yaml | $(KUBECTL) apply -f -
	@# Prefer the postgres pod IP: ClusterIP/kube-proxy has timed out on GHA k3d even when DNS works.
	@postgres_host="$$($(KUBECTL) -n $(INFRA_NS) get pod postgres-0 -o jsonpath='{.status.podIP}')"; \
		if [ -z "$$postgres_host" ]; then echo "postgres-0 has no pod IP" >&2; exit 1; fi; \
		echo "Keycloak JDBC host (postgres-0 pod IP): $$postgres_host"; \
		sed -e 's/$${KEYCLOAK_TAG}/$(KEYCLOAK_TAG)/g' \
			-e 's|$${REGISTRY}|$(REGISTRY)|g' \
			-e 's|$${POSTGRES_HOST}|'"$$postgres_host"'|g' \
			deploy/kubernetes/keycloak/keycloak.yaml | $(KUBECTL) apply -f -; \
		$(KUBECTL) -n $(INFRA_NS) get deploy keycloak -o jsonpath='{.spec.template.spec.containers[0].env[?(@.name=="KC_DB_URL")].value}{"\n"}'
	@if ! $(KUBECTL) rollout status deployment/keycloak --namespace $(INFRA_NS) --timeout 5m; then \
		echo "Keycloak rollout failed; pod status:"; \
		$(KUBECTL) get pods -n $(INFRA_NS) -o wide; \
		$(KUBECTL) describe pods -n $(INFRA_NS) -l app.kubernetes.io/name=keycloak | tail -n 80; \
		$(KUBECTL) logs -n $(INFRA_NS) -l app.kubernetes.io/name=keycloak --tail=80 || true; \
		exit 1; \
	fi
	@./scripts/keycloak-users.sh
	@echo "Sign-in: https://auth.invoiceflow.localhost:8080/realms/invoiceflow/account"
	@echo "Trust the local CA once: make trust-local-ca"
	@echo "Admin console: make keycloak-admin"
	@echo "Persona passwords: kubectl -n $(INFRA_NS) get secret keycloak-users -o jsonpath='{.data.clerk}' | base64 -d"

keycloak-down: doctor ## Remove Keycloak, including its database and credentials
	@$(KUBECTL) delete -f deploy/kubernetes/keycloak/ --ignore-not-found
	@$(KUBECTL) -n $(INFRA_NS) delete configmap/keycloak-realm secret/auth-invoiceflow-tls \
		secret/keycloak-db secret/keycloak-admin secret/keycloak-users --ignore-not-found
	@printf '%s\n' 'DROP DATABASE IF EXISTS keycloak WITH (FORCE);' 'DROP ROLE IF EXISTS keycloak;' \
		| $(KUBECTL) exec -i postgres-0 --namespace $(INFRA_NS) -- psql -U postgres -v ON_ERROR_STOP=1 -q

keycloak-password: ## Print the password of the temporary Keycloak admin
	@$(KUBECTL) get secret keycloak-admin --namespace $(INFRA_NS) -o jsonpath='{.data.password}' | base64 -d; echo

keycloak-admin: doctor ## Forward the Keycloak admin console to localhost:18080 (Ctrl+C to stop)
	@echo "Admin console: http://localhost:$(KEYCLOAK_ADMIN_PORT)/admin (user admin, password from make keycloak-password)"
	@$(KUBECTL) port-forward service/keycloak $(KEYCLOAK_ADMIN_PORT):8080 --namespace $(INFRA_NS)

trust-local-ca: ## Add the InvoiceFlow local CA to the macOS login keychain (run once after make keycloak-up)
	@security add-trusted-cert -d -r trustRoot -k "$(HOME)/Library/Keychains/login.keychain-db" \
		"$(HOME)/.local/share/invoiceflow/tls/ca.crt"
	@echo "Trusted $(HOME)/.local/share/invoiceflow/tls/ca.crt"

# The tests drive the app in the cluster through the ingress, so run make stack-up (or app-up) first; see tests/e2e/README.md
e2e-install: ## Install the end-to-end test dependencies and the Chromium browser for Playwright
	@npm ci --prefix tests/e2e
	@cd tests/e2e && npx playwright install --with-deps chromium

e2e-typecheck: ## Typecheck the end-to-end tests
	@npm run typecheck --prefix tests/e2e

e2e-smoke: ## Upload an invoice through the app in the cluster and check it is listed (BASE_URL overrides the address)
	@E2E_CLERK_PASSWORD="$$($(KUBECTL) get secret keycloak-users --namespace $(INFRA_NS) -o jsonpath='{.data.clerk}' | base64 -d)" \
		npm run test:smoke --prefix tests/e2e

# GitHub Actions: single-node k3d, fake extractor (no Ollama), Playwright @smoke against the ingress.
e2e-ci: ## Create the CI cluster, deploy the stack with the fake extractor, and run e2e smoke
	@$(MAKE) --no-print-directory cluster-up K3D_CONFIG=deploy/k3d/cluster-ci.yaml
	@$(MAKE) --no-print-directory stack-up DOCUMENT_EXTRACTION_SERVICE_URL=
	@$(MAKE) --no-print-directory e2e-install e2e-smoke
