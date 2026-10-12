CLUSTER := invoiceflow
CONTEXT := k3d-$(CLUSTER)

# Set here as well as in .envrc, so make is safe even without direnv
export DOCKER_HOST := unix://$(HOME)/.colima/invoiceflow/docker.sock
export KUBECONFIG := $(CURDIR)/.kube/config
export INVOICEFLOW_BACKUP_DIR ?= $(HOME)/.local/share/invoiceflow/backups

KUBECTL := kubectl --context $(CONTEXT)
HELM := helm --kube-context $(CONTEXT)

GRAFANA_CHARTS := https://grafana-community.github.io/helm-charts
OTEL_CHARTS := https://open-telemetry.github.io/opentelemetry-helm-charts
LOKI_CHART_VERSION := 18.15.1
GRAFANA_CHART_VERSION := 13.5.0
COLLECTOR_CHART_VERSION := 0.175.1
OBSERVABILITY_NS := invoiceflow-observability
COLLECTOR_NS := invoiceflow-collector

.DEFAULT_GOAL := help
.PHONY: help up down doctor cluster-up cluster-down cluster-status ollama-up ollama-down ollama-check \
	observability-up observability-down grafana-password

help:
	@grep -E '^[a-z-]+:.*## ' $(MAKEFILE_LIST) | awk -F':.*## ' '{printf "  %-20s %s\n", $$1, $$2}'

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

cluster-up: doctor ## Create the k3d cluster (no-op if it exists)
	@mkdir -p "$(INVOICEFLOW_BACKUP_DIR)" "$(dir $(KUBECONFIG))"
	@if k3d cluster list $(CLUSTER) >/dev/null 2>&1; then \
		echo "Cluster $(CLUSTER) already exists"; \
	else \
		k3d cluster create --config deploy/k3d/cluster.yaml; \
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

observability-up: doctor ## Install Loki, Grafana and the OpenTelemetry Collector
	@$(KUBECTL) apply -f deploy/observability/namespaces.yaml
	@$(HELM) upgrade --install loki loki --repo $(GRAFANA_CHARTS) --version $(LOKI_CHART_VERSION) \
		--namespace $(OBSERVABILITY_NS) -f deploy/observability/loki-values.yaml --wait --timeout 5m
	@$(HELM) upgrade --install grafana grafana --repo $(GRAFANA_CHARTS) --version $(GRAFANA_CHART_VERSION) \
		--namespace $(OBSERVABILITY_NS) -f deploy/observability/grafana-values.yaml --wait --timeout 5m
	@$(HELM) upgrade --install collector opentelemetry-collector --repo $(OTEL_CHARTS) --version $(COLLECTOR_CHART_VERSION) \
		--namespace $(COLLECTOR_NS) -f deploy/observability/collector-values.yaml --wait --timeout 5m
	@echo "Grafana: http://grafana.invoiceflow.localhost:8080 (user admin, password from make grafana-password)"

observability-down: doctor ## Remove the observability stack, including stored logs
	@$(HELM) uninstall collector --namespace $(COLLECTOR_NS) --ignore-not-found
	@$(HELM) uninstall grafana loki --namespace $(OBSERVABILITY_NS) --ignore-not-found
	@$(KUBECTL) delete -f deploy/observability/namespaces.yaml --ignore-not-found

grafana-password: ## Print the Grafana admin password
	@$(KUBECTL) get secret grafana --namespace $(OBSERVABILITY_NS) -o jsonpath='{.data.admin-password}' | base64 -d; echo
